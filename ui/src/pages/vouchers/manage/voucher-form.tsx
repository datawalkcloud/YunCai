import { useMemo, useRef, useState } from "react"
import { Button, DatePicker, Input, InputNumber, Modal, Select, Table } from "antd"
import { DeleteOutlined, PlusOutlined, SaveOutlined } from "@ant-design/icons"
import dayjs from "dayjs"

import { useAccountSets } from "@/accountSets/AccountSetsContext"
import { VouchersApi, type AccountDto, type CreateVoucherRequest, type UpdateVoucherRequest, type VoucherDetailDto, type VoucherEntryDto } from "@/apis-gen"
import { amountToChineseUppercase } from "@/lib/amount-uppercase"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtMoney, toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { AssistItemSelect } from "@/components/assist/assist-item-select"
import { assistLabel } from "@/components/assist/constants"
import { useAccountAssistConfigs } from "@/components/assist/assist-store"
import { AmountCells, AmountHeaderCell } from "@/components/voucher/amount-grid"

/**
 * 凭证分录行。借方与贷方以字符串保存，便于输入编辑；account 存科目 id；
 * assists 为「辅助核算类别 → 已选档案 id」，只在所选科目挂了辅助核算时使用。
 */
interface EntryRow {
    id: number
    summary: string
    accountId: string
    debit: string
    credit: string
    assists: Record<number, string>
}

/** 制单默认日期：本地今天的 yyyy-MM-dd（不能用 UTC 口径，否则凌晨会落到昨天）。 */
function today(): string {
    return toISODate(new Date())
}

/** 分录已挂的辅助核算（类别 → 档案 id）。录入界面每个类别只选一个档案，同类型多档案时取首个。 */
function assistsOf(entry: VoucherEntryDto): Record<number, string> {
    const assists: Record<number, string> = {}
    for (const a of entry.assists ?? []) {
        if (a.type != null && a.assistItemId) assists[a.type] = a.assistItemId
    }
    return assists
}

/** 编辑模式下将凭证详情的分录映射为可编辑行；无分录时给两条空行。 */
function toEntryRows(detail?: VoucherDetailDto): EntryRow[] {
    const src = detail?.entries
    if (src && src.length > 0) {
        return src.map((e, i) => ({
            id: i + 1,
            summary: e.summary ?? "",
            accountId: e.accountId ?? "",
            debit: e.debit != null && e.debit !== 0 ? String(e.debit) : "",
            credit: e.credit != null && e.credit !== 0 ? String(e.credit) : "",
            assists: assistsOf(e),
        }))
    }
    return [
        { id: 1, summary: "", accountId: "", debit: "", credit: "", assists: {} },
        { id: 2, summary: "", accountId: "", debit: "", credit: "", assists: {} },
    ]
}

function toAmount(value: string): number {
    const n = Number(value)
    return Number.isFinite(n) && n !== 0 ? n : 0
}

/** 损益类科目中，收入类（科目名称含"收入"）正常应记贷方；费用类正常应记借方。 */
function isRevenueAccount(account: AccountDto | undefined): boolean {
    if (!account?.code || !account?.name) return false
    return (account.code.startsWith("5") || account.code.startsWith("6")) && account.name.includes("收入")
}

function isExpenseAccount(account: AccountDto | undefined): boolean {
    if (!account?.code || !account?.name) return false
    return (account.code.startsWith("5") || account.code.startsWith("6")) && !account.name.includes("收入")
}

/** 检测凭证分录中损益类科目方向不规范的条目，返回行号（从 1 开始）数组。 */
function detectDirectionIssues(entries: EntryRow[], accountMap: Map<string | undefined, AccountDto>): number[] {
    const issues: number[] = []
    let line = 0
    for (const entry of entries) {
        line++
        const account = accountMap.get(entry.accountId)
        const debit = toAmount(entry.debit)
        const credit = toAmount(entry.credit)
        if (isRevenueAccount(account) && debit > 0) {
            issues.push(line)
        } else if (isExpenseAccount(account) && credit > 0) {
            issues.push(line)
        }
    }
    return issues
}

/**
 * 凭证录入表单（仿纸质记账凭证）。校验借贷平衡后调用后端保存，成功后回调 onSaved。
 * 科目来自账套下启用中的末级科目；凭证号留空时由后端按凭证字自动续号。
 */
export default function VoucherForm({
    accountSetId,
    accounts,
    preparer,
    token,
    initial,
    initialDate,
    initialWord,
    onSaved,
    onCancel,
}: {
    accountSetId: string
    accounts: AccountDto[]
    preparer?: string
    token?: string
    /** 传入则为编辑模式：回填字段并调用更新端点；凭证字与号不可更改。 */
    initial?: VoucherDetailDto
    /** 新增模式下预设凭证日期（列表「插入」用）。 */
    initialDate?: string
    /** 新增模式下预设凭证字。 */
    initialWord?: string
    onSaved: () => void
    onCancel: () => void
}) {
    const editing = Boolean(initial?.id)
    const nextId = useRef((initial?.entries?.length ?? 2) + 1)
    // 凭证字候选来自账套配置（逗号分隔），制单时只能从中选择。
    const { accountSets } = useAccountSets()
    const voucherWordOptions = useMemo(() => {
        const raw = accountSets.find((s) => s.id === accountSetId)?.voucherWords ?? "记,收,付,转"
        return raw.split(/[,，、\s]+/).filter(Boolean)
    }, [accountSets, accountSetId])
    const [voucherWord, setVoucherWord] = useState(initial?.word ?? initialWord ?? "记")
    const [voucherNo, setVoucherNo] = useState(initial?.number ? String(initial.number) : "")
    const [date, setDate] = useState(() => (initial?.date ? toISODate(initial.date) : initialDate ?? today()))
    const [attachments, setAttachments] = useState(initial?.attachmentCount != null ? String(initial.attachmentCount) : "0")
    const [entries, setEntries] = useState<EntryRow[]>(() => toEntryRows(initial))
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [directionModal, setDirectionModal] = useState<{ issues: number[] } | null>(null)

    const accountMap = useMemo(() => new Map(accounts.map((a) => [a.id, a] as const)), [accounts])
    // 分录科目下拉候选：编码 + 名称，可按名称/编码检索。
    // 编辑历史凭证时，分录科目可能已停用或变成非末级而不在候选里，用详情带的编码名称补一项保住回显。
    const accountOptions = useMemo(() => {
        const opts = accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` }))
        const known = new Set(opts.map((o) => o.value))
        const stale: { value: string; label: string; disabled: boolean }[] = []
        for (const e of initial?.entries ?? []) {
            const id = e.accountId
            if (!id || known.has(id) || stale.some((o) => o.value === id)) continue
            stale.push({
                value: id,
                label: `${e.accountCode ?? ""} ${e.accountName ?? ""}`.trim() + "（当前不可选）",
                disabled: true,
            })
        }
        return [...opts, ...stale]
    }, [accounts, initial])

    // 分录所选科目的辅助核算维度：只为实际用到的科目发请求（模块级缓存去重）。
    const entryAccountIds = useMemo(() => entries.map((e) => e.accountId), [entries])
    const { configs: assistConfigs } = useAccountAssistConfigs(accountSetId, entryAccountIds, token)
    const assistTypesOf = (accountId: string) => assistConfigs[accountId] ?? []

    const { totalDebit, totalCredit } = useMemo(() => {
        let debit = 0
        let credit = 0
        for (const entry of entries) {
            debit += toAmount(entry.debit)
            credit += toAmount(entry.credit)
        }
        return { totalDebit: debit, totalCredit: credit }
    }, [entries])

    // 按分比较，避免 0.1 + 0.2 这类浮点误差把平衡凭证判成不平。
    const balanced = Math.round(totalDebit * 100) === Math.round(totalCredit * 100)
    const diffCents = Math.abs(Math.round(totalDebit * 100) - Math.round(totalCredit * 100))
    const canSave = balanced && !saving

    function updateEntry(id: number, patch: Partial<EntryRow>) {
        setEntries((prev) => prev.map((e) => (e.id === id ? { ...e, ...patch } : e)))
        setError(null)
    }

    /** 有任一字段填了内容即视为有效分录行（空行不提交）。 */
    const isActiveEntry = (e: EntryRow) =>
        Boolean(e.summary.trim()) || Boolean(e.accountId) || toAmount(e.debit) !== 0 || toAmount(e.credit) !== 0

    function addRow() {
        setEntries((prev) => [
            ...prev,
            { id: nextId.current++, summary: "", accountId: "", debit: "", credit: "", assists: {} },
        ])
    }

    /** 选择某类别的核算项目档案；itemId 为空串表示取消选择。 */
    function updateAssist(entryId: number, type: number, itemId: string) {
        setEntries((prev) =>
            prev.map((e) => (e.id === entryId ? { ...e, assists: { ...e.assists, [type]: itemId } } : e)),
        )
        setError(null)
    }

    function removeRow(id: number) {
        setEntries((prev) => (prev.length > 1 ? prev.filter((e) => e.id !== id) : prev))
    }

    // 实际执行保存的 API 调用。
    async function doSave(entriesToSave: EntryRow[]) {
        const mappedEntries = entriesToSave.map((e) => {
            const assists = Object.entries(e.assists)
                .filter(([, itemId]) => Boolean(itemId))
                .map(([type, itemId]) => ({ type: Number(type), itemId }))
            return {
                summary: e.summary.trim(),
                accountId: e.accountId,
                debit: toAmount(e.debit),
                credit: toAmount(e.credit),
                // 未挂辅助核算时不带该字段，保持与改动前完全一致的请求体。
                assists: assists.length > 0 ? assists : undefined,
            }
        })
        const dateValue = new Date(date)
        const attachmentCount = Number.parseInt(attachments, 10) > 0 ? Number.parseInt(attachments, 10) : 0

        setSaving(true)
        setError(null)
        try {
            const api = new VouchersApi(apiConfig(token))
            if (editing && initial?.id) {
                // 已过账凭证先反过账，更新后再重新入账（列表里直接点「修改」无需用户手动反过账）。
                if (initial.status === 3) {
                    await api.accountsetsAccountSetIdVouchersIdUnpostPost({ accountSetId, id: initial.id })
                }
                const request: UpdateVoucherRequest = {
                    date: dateValue,
                    attachmentCount,
                    entries: mappedEntries,
                }
                await api.accountsetsAccountSetIdVouchersIdPut({
                    accountSetId,
                    id: initial.id,
                    updateVoucherRequest: request,
                })
            } else {
                const request: CreateVoucherRequest = {
                    word: voucherWord.trim() || "记",
                    number: Number.parseInt(voucherNo, 10) > 0 ? Number.parseInt(voucherNo, 10) : 0,
                    date: dateValue,
                    attachmentCount,
                    entries: mappedEntries,
                }
                await api.accountsetsAccountSetIdVouchersPost({
                    accountSetId,
                    createVoucherRequest: request,
                })
            }
            onSaved()
        } catch (err) {
            setError(await apiErrorMessage(err, "保存失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function handleSave() {
        if (!canSave) return

        // 仅提交有内容的分录行；一次性汇总所有缺项，避免逐条改完再报下一条。
        const active = entries.filter(isActiveEntry)
        if (active.length === 0) {
            setError("请至少录入一条分录。")
            return
        }
        const noSummary: number[] = []
        const noAccount: number[] = []
        const badAmount: number[] = []
        const noAssist: string[] = []
        entries.forEach((e, i) => {
            if (!isActiveEntry(e)) return
            const line = i + 1
            if (!e.summary.trim()) noSummary.push(line)
            if (!e.accountId) noAccount.push(line)
            if ((toAmount(e.debit) !== 0) === (toAmount(e.credit) !== 0)) badAmount.push(line)
            const missing = assistTypesOf(e.accountId)
                .filter((c) => c.isRequired !== false && !e.assists[c.type as number])
                .map((c) => assistLabel(c.type))
            if (missing.length > 0) noAssist.push(`第 ${line} 行缺 ${missing.join("、")}`)
        })
        const problems = [
            noSummary.length > 0 ? `第 ${noSummary.join("、")} 行未填摘要` : "",
            noAccount.length > 0 ? `第 ${noAccount.join("、")} 行未选科目` : "",
            badAmount.length > 0 ? `第 ${badAmount.join("、")} 行需且只能填借方或贷方之一` : "",
            ...noAssist,
        ].filter(Boolean)
        if (problems.length > 0) {
            setError(`${problems.join("；")}。`)
            return
        }

        // 损益类科目方向检查
        const issues = detectDirectionIssues(active, accountMap)
        if (issues.length > 0) {
            setDirectionModal({ issues })
            return
        }

        await doSave(active)
    }

    /** 批量调整：收入类借方正数 → 贷方正数，费用类贷方正数 → 借方正数。 */
    function autoAdjustEntries() {
        setEntries((prev) =>
            prev.map((entry) => {
                const account = accountMap.get(entry.accountId)
                const debit = toAmount(entry.debit)
                const credit = toAmount(entry.credit)
                if (isRevenueAccount(account) && debit > 0) {
                    return { ...entry, debit: "", credit: String(debit) }
                }
                if (isExpenseAccount(account) && credit > 0) {
                    return { ...entry, debit: String(credit), credit: "" }
                }
                return entry
            }),
        )
        setDirectionModal(null)
    }

    return (
        <div className="flex flex-col gap-4">
            {/* 凭证标题 */}
            <div className="text-center text-xl font-bold tracking-[0.5em]">记账凭证</div>

            {/* 凭证字号 / 日期 / 附件 */}
            <div className="flex items-end gap-4 text-sm whitespace-nowrap">
                <div className="flex items-end gap-1 shrink-0">
                    <Select
                        className="w-16"
                        disabled={editing}
                        value={voucherWord}
                        options={voucherWordOptions.map((w) => ({ value: w, label: w }))}
                        onChange={setVoucherWord}
                    />
                    <span className="pb-1">字第</span>
                    <Input
                        value={voucherNo}
                        onChange={(e) => setVoucherNo(e.target.value.replace(/\D/g, ""))}
                        placeholder="自动"
                        disabled={editing}
                        className="w-16 text-center"
                    />
                    <span className="pb-1">号</span>
                </div>
                <div className="flex items-end gap-1 shrink-0">
                    <span className="pb-1">日期：</span>
                    <DatePicker
                        className="w-32"
                        format="YYYY-MM-DD"
                        allowClear={false}
                        value={dayjs(date)}
                        onChange={(d) => {
                            if (d) setDate(d.format("YYYY-MM-DD"))
                        }}
                    />
                </div>
                <div className="flex items-end gap-1 shrink-0">
                    <span className="pb-1">附单据</span>
                    <InputNumber
                        className="w-12"
                        controls={false}
                        min="0"
                        value={attachments}
                        onChange={(v) => setAttachments(v == null ? "" : String(v))}
                    />
                    <span className="pb-1">张</span>
                </div>
                <div className="flex-1" />
            </div>

            {/* 分录表格 */}
            <div className="overflow-x-auto">
                <Table<EntryRow>
                    size="small"
                    pagination={false}
                    rowKey={(e) => e.id}
                    dataSource={entries}
                    columns={[
                        {
                            title: "摘要",
                            width: 240,
                            onCell: () => ({ style: { padding: 0 } }),
                            render: (_, entry) => (
                                <div className="flex items-center">
                                    <Input
                                        variant="borderless"
                                        value={entry.summary}
                                        onChange={(e) => updateEntry(entry.id, { summary: e.target.value })}
                                        placeholder="摘要"
                                        className="flex-1"
                                    />
                                    <Button
                                        type="text"
                                        danger
                                        title="删除该行"
                                        disabled={entries.length <= 1}
                                        icon={<DeleteOutlined />}
                                        className="mr-1 shrink-0"
                                        onClick={() => removeRow(entry.id)}
                                    />
                                </div>
                            ),
                        },
                        {
                            title: "会计科目",
                            width: 200,
                            onCell: () => ({ style: { padding: 0 } }),
                            render: (_, entry) => (
                                <Select
                                    variant="borderless"
                                    className="w-full"
                                    placeholder="请选择科目"
                                    allowClear
                                    showSearch
                                    optionFilterProp="label"
                                    value={entry.accountId || undefined}
                                    options={accountOptions}
                                    onChange={(v) =>
                                        // 换科目后原辅助核算取值不再适用，清空重选。
                                        updateEntry(entry.id, { accountId: v ?? "", assists: {} })
                                    }
                                />
                            ),
                        },
                        {
                            title: <AmountHeaderCell title="借方金额" idPrefix="dh" />,
                            onCell: () => ({ style: { padding: 0 } }),
                            render: (_, entry) => (
                                <AmountCells
                                    value={toAmount(entry.debit)}
                                    editable
                                    raw={entry.debit}
                                    onChange={(v) => updateEntry(entry.id, { debit: v })}
                                />
                            ),
                        },
                        {
                            title: <AmountHeaderCell title="贷方金额" idPrefix="ch" />,
                            onCell: () => ({ style: { padding: 0 } }),
                            render: (_, entry) => (
                                <AmountCells
                                    value={toAmount(entry.credit)}
                                    editable
                                    raw={entry.credit}
                                    onChange={(v) => updateEntry(entry.id, { credit: v })}
                                />
                            ),
                        },
                        Table.EXPAND_COLUMN,
                    ]}
                    expandable={{
                        // 辅助核算录入行：每维度一个档案下拉，必录项标红星。
                        expandedRowRender: (entry) => (
                            <div className="flex flex-wrap items-center gap-x-4 gap-y-1 bg-muted/30 px-2 py-1">
                                {assistTypesOf(entry.accountId).map((cfg) => (
                                    <div key={cfg.type} className="flex items-center gap-1">
                                        <span className="text-xs text-muted-foreground">
                                            {assistLabel(cfg.type)}
                                            {cfg.isRequired !== false && <span className="text-destructive">*</span>}
                                        </span>
                                        <AssistItemSelect
                                            accountSetId={accountSetId}
                                            token={token}
                                            type={cfg.type as number}
                                            value={entry.assists[cfg.type as number] ?? ""}
                                            onChange={(itemId) => updateAssist(entry.id, cfg.type as number, itemId)}
                                        />
                                    </div>
                                ))}
                            </div>
                        ),
                        expandedRowKeys: entries
                            .filter((e) => assistTypesOf(e.accountId).length > 0)
                            .map((e) => e.id),
                        rowExpandable: (entry) => assistTypesOf(entry.accountId).length > 0,
                        expandIcon: () => null,
                    }}
                    summary={() => (
                        <Table.Summary.Row>
                            <Table.Summary.Cell index={0} colSpan={2} className="px-2 py-2 font-medium">
                                合计：{amountToChineseUppercase(totalDebit)}
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={2} className="pk-cell-flush">
                                <AmountCells value={totalDebit} />
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={3} className="pk-cell-flush">
                                <AmountCells value={totalCredit} />
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={4} />
                        </Table.Summary.Row>
                    )}
                />
            </div>

            {/* 签字栏 */}
            <div className="flex flex-wrap gap-x-8 gap-y-2 pt-1 text-sm text-muted-foreground">
                <span>制单：{initial?.preparer ?? preparer ?? "______"}</span>
            </div>

            {accounts.length === 0 && (
                <p className="text-sm text-destructive">当前账套暂无可用的末级科目，请先在「科目」中维护或导入科目。</p>
            )}
            {error && <p className="text-sm text-destructive">{error}</p>}

            {/* 操作区 */}
            <div className="flex flex-wrap items-center gap-3 border-t border-border pt-4">
                <Button icon={<PlusOutlined />} onClick={addRow}>
                    添加分录
                </Button>
                <span className={cn("text-sm", balanced ? "text-muted-foreground" : "text-destructive")}>
                    {balanced
                        ? "借贷平衡"
                        : totalDebit === 0 && totalCredit === 0
                            ? "请输入借贷金额"
                            : `借贷不平衡，差额 ${fmtMoney(diffCents / 100)}`}
                </span>
                <div className="flex-1" />
                <Button onClick={onCancel} disabled={saving}>
                    取消
                </Button>
                <Button type="primary" disabled={!balanced} loading={saving} icon={saving ? undefined : <SaveOutlined />} onClick={() => void handleSave()}>
                    {saving ? "保存中…" : "保存凭证"}
                </Button>
            </div>

            {/* 损益类科目方向警告弹窗 */}
            <Modal
                open={directionModal !== null}
                width={512}
                title="凭证录入提示"
                onCancel={() => setDirectionModal(null)}
                footer={
                    <div className="flex flex-col gap-2">
                        <Button type="primary" block onClick={autoAdjustEntries}>
                            批量调整，系统帮您自动调整所有金额方向
                        </Button>
                        <Button
                            block
                            onClick={() => {
                                setDirectionModal(null)
                                // 重新获取 active 并保存
                                const active = entries.filter(isActiveEntry)
                                void doSave(active)
                            }}
                        >
                            继续保存，直接保存凭证
                        </Button>
                        <Button block onClick={() => setDirectionModal(null)}>
                            关闭弹窗，自行修改凭证后重新保存
                        </Button>
                    </div>
                }
            >
                <div className="flex flex-col gap-4">
                    <div>
                        <p className="font-medium text-foreground">系统检测到当前凭证存在损益科目录入不规范的凭证行哦~</p>
                        <p className="mt-2 text-sm text-muted-foreground">
                            收入类 要做在 贷方 ， 费用类 要做在 借方 。 如果要冲回，应该录入相同方向的红字，即负数。否则科目余额表和利润表数据会对不上
                        </p>
                    </div>
                    <p className="text-sm">
                        需要调整金额方向的凭证行：<span className="font-semibold">{directionModal?.issues.join("、")}</span>
                    </p>
                </div>
            </Modal>
        </div>
    )
}
