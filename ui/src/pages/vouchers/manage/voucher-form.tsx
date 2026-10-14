import { Fragment, useMemo, useRef, useState } from "react"
import { Plus, Save, Trash2 } from "lucide-react"

import { useAccountSets } from "@/accountSets/AccountSetsContext"
import { VouchersApi, type AccountDto, type CreateVoucherRequest, type UpdateVoucherRequest, type VoucherDetailDto, type VoucherEntryDto } from "@/apis-gen"
import { amountToChineseUppercase } from "@/lib/amount-uppercase"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtMoney, toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { AssistItemSelect } from "@/components/assist/assist-item-select"
import { assistLabel } from "@/components/assist/constants"
import { useAccountAssistConfigs } from "@/components/assist/assist-store"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Spinner } from "@/components/ui/spinner"

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

/** 金额分位列，从高位到低位（最高支持到亿位，角、分为小数位）。 */
const AMOUNT_COLUMNS = ["亿", "千万", "百万", "十万", "万", "千", "百", "十", "元", "角", "分"]

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

/** 将金额按分位拆解为与 AMOUNT_COLUMNS 对齐的字符数组，0 时全部留空。 */
function digitCells(value: number): string[] {
    const cells = Array<string>(AMOUNT_COLUMNS.length).fill("")
    if (value === 0) return cells
    const absValue = Math.abs(value)
    const cents = Math.round(absValue * 100)
    if (cents <= 0) return cells
    const str = String(cents)
    for (let i = 0; i < str.length && i < cells.length; i++) {
        cells[cells.length - 1 - i] = str[str.length - 1 - i]
    }
    return cells
}

const thCls = "border border-border px-1 py-1 text-center font-medium text-muted-foreground"
const tdCls = "border border-border p-0 align-middle"

/** 规整金额输入：仅数字与一个小数点，且小数点后最多两位。 */
function sanitizeAmount(v: string): string {
    let s = v.replace(/[^\d.]/g, "")
    const dot = s.indexOf(".")
    if (dot !== -1) {
        const intPart = s.slice(0, dot).slice(0, 9)
        const decPart = s.slice(dot + 1).replace(/\./g, "").slice(0, 2)
        s = intPart + "." + decPart
    } else {
        s = s.slice(0, 9)
    }
    return s
}

/** 分位竖线：元→角、千→百、百万→十万处用强调色分隔，其余常规边框色；末列（分）不画线。 */
function digitCellCls(i: number): string {
    if (i >= 10) return ""
    if (i === 8) return "border-r border-destructive/40"
    if (i === 5 || i === 2) return "border-r border-primary/40"
    return "border-r border-border"
}

/** 借方 / 贷方金额的 11 个分位表头；竖排中文按字换行纵向排布。 */
function AmountColumnHeader({ idPrefix }: { idPrefix: string }) {
    return (
        <div className="grid grid-cols-11">
            {AMOUNT_COLUMNS.map((c, i) => (
                <div
                    key={`${idPrefix}-${i}`}
                    className={cn(
                        "flex h-12 flex-col items-center justify-center gap-0.5 text-[10px] font-medium text-muted-foreground",
                        digitCellCls(i),
                    )}
                >
                    {c.split("").map((ch, j) => (
                        <span key={j}>{ch}</span>
                    ))}
                </div>
            ))}
        </div>
    )
}

/** 借方 / 贷方金额分位小格；editable 时聚焦可直接输入完整数值，失焦后按位对齐到分位小格。 */
function AmountCells({
    value,
    editable,
    raw,
    onChange,
}: {
    value: number
    editable?: boolean
    raw?: string
    onChange?: (v: string) => void
}) {
    const [focused, setFocused] = useState(false)
    const cells = digitCells(value)
    const isNegative = value < 0
    return (
        <div className="relative">
            <div className={cn("grid grid-cols-11", isNegative && "text-destructive")}>
                {cells.map((d, i) => (
                    <div key={i} className={cn("flex h-[54px] items-center justify-center tabular-nums", digitCellCls(i))}>
                        {focused ? "" : d}
                    </div>
                ))}
            </div>
            {editable && (
                <input
                    inputMode="decimal"
                    value={raw ?? ""}
                    onChange={(e) => onChange?.(sanitizeAmount(e.target.value))}
                    onFocus={(e) => {
                        setFocused(true)
                        e.target.select()
                    }}
                    onBlur={() => setFocused(false)}
                    className={cn(
                        "absolute inset-0 h-full w-full px-2 text-right outline-none",
                        focused
                            ? "bg-accent/40 text-foreground caret-primary"
                            : "bg-transparent text-transparent caret-transparent",
                    )}
                />
            )}
        </div>
    )
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
    onSaved,
    onCancel,
}: {
    accountSetId: string
    accounts: AccountDto[]
    preparer?: string
    token?: string
    /** 传入则为编辑模式：回填字段并调用更新端点；凭证字与号不可更改。 */
    initial?: VoucherDetailDto
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
    const [voucherWord, setVoucherWord] = useState(initial?.word ?? "记")
    const [voucherNo, setVoucherNo] = useState(initial?.number ? String(initial.number) : "")
    const [date, setDate] = useState(() => (initial?.date ? toISODate(initial.date) : today()))
    const [attachments, setAttachments] = useState(initial?.attachmentCount != null ? String(initial.attachmentCount) : "0")
    const [entries, setEntries] = useState<EntryRow[]>(() => toEntryRows(initial))
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [directionModal, setDirectionModal] = useState<{ issues: number[] } | null>(null)

    const accountMap = useMemo(() => new Map(accounts.map((a) => [a.id, a] as const)), [accounts])

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

    const balanced = totalDebit === totalCredit
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

        // 仅提交有内容的分录行；逐行校验摘要、科目与借贷单边非零。
        const active = entries.filter(isActiveEntry)
        if (active.length === 0) {
            setError("请至少录入一条分录。")
            return
        }
        for (const e of active) {
            if (!e.summary.trim()) return setError("每条分录都需要填写摘要。")
            if (!e.accountId) return setError("每条分录都需要选择会计科目。")
            const debit = toAmount(e.debit)
            const credit = toAmount(e.credit)
            const hasDebit = debit !== 0
            const hasCredit = credit !== 0
            if (hasDebit === hasCredit) {
                return setError("每条分录只能填写借方或贷方其一，且金额不能为零。")
            }
        }

        // 辅助核算必录校验：行号按表格中的可见行序（与用户所见一致）。
        for (let i = 0; i < entries.length; i++) {
            const e = entries[i]
            if (!isActiveEntry(e)) continue
            const missing = assistTypesOf(e.accountId)
                .filter((c) => c.isRequired !== false && !e.assists[c.type as number])
                .map((c) => assistLabel(c.type))
            if (missing.length > 0) {
                return setError(`第 ${i + 1} 行 ${missing.join("、")} 未选择核算项目。`)
            }
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
                    <Select value={voucherWord} onValueChange={setVoucherWord} disabled={editing}>
                        <SelectTrigger size="sm" className="w-16">
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            {voucherWordOptions.map((w) => (
                                <SelectItem key={w} value={w}>{w}</SelectItem>
                            ))}
                        </SelectContent>
                    </Select>
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
                    <Input
                        type="date"
                        value={date}
                        onChange={(e) => setDate(e.target.value)}
                        className="w-32"
                    />
                </div>
                <div className="flex items-end gap-1 shrink-0">
                    <span className="pb-1">附单据</span>
                    <Input
                        type="number"
                        min={0}
                        value={attachments}
                        onChange={(e) => setAttachments(e.target.value)}
                        className="w-12 text-center"
                    />
                    <span className="pb-1">张</span>
                </div>
                <div className="flex-1" />
            </div>

            {/* 分录表格 */}
            <div className="overflow-x-auto">
                <table className="w-full min-w-[1040px] border-collapse text-sm">
                    <colgroup>
                        <col className="w-60" />
                        <col className="w-50" />
                        {AMOUNT_COLUMNS.map((_, i) => (
                            <col key={`d-${i}`} className="w-6" />
                        ))}
                        {AMOUNT_COLUMNS.map((_, i) => (
                            <col key={`c-${i}`} className="w-6" />
                        ))}
                    </colgroup>
                    <thead>
                        <tr>
                            <th rowSpan={2} className={thCls}>
                                摘要
                            </th>
                            <th rowSpan={2} className={thCls}>
                                会计科目
                            </th>
                            <th colSpan={11} className={thCls}>
                                借方金额
                            </th>
                            <th colSpan={11} className={thCls}>
                                贷方金额
                            </th>
                        </tr>
                        <tr>
                            <th colSpan={11} className={cn(thCls, "p-0 align-top")}>
                                <AmountColumnHeader idPrefix="dh" />
                            </th>
                            <th colSpan={11} className={cn(thCls, "p-0 align-top")}>
                                <AmountColumnHeader idPrefix="ch" />
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {entries.map((entry) => {
                            // 本行科目挂了的辅助核算维度；为空则不展开辅助核算行。
                            const assistTypes = assistTypesOf(entry.accountId)
                            return (
                            <Fragment key={entry.id}>
                            <tr>
                                <td className={tdCls}>
                                    <div className="flex items-center">
                                        <input
                                            value={entry.summary}
                                            onChange={(e) => updateEntry(entry.id, { summary: e.target.value })}
                                            placeholder="摘要"
                                            className="h-9 w-full bg-transparent px-2 outline-none placeholder:text-muted-foreground"
                                        />
                                        <Button
                                            variant="destructive"
                                            size="icon-xs"
                                            onClick={() => removeRow(entry.id)}
                                            disabled={entries.length <= 1}
                                            title="删除该行"
                                            className="mr-1 shrink-0"
                                        >
                                            <Trash2 />
                                        </Button>
                                    </div>
                                </td>
                                <td className={tdCls}>
                                    <select
                                        value={entry.accountId}
                                        onChange={(e) =>
                                            // 换科目后原辅助核算取值不再适用，清空重选。
                                            updateEntry(entry.id, { accountId: e.target.value, assists: {} })
                                        }
                                        className="h-9 w-full bg-transparent px-1 text-sm outline-none"
                                    >
                                        <option value="">请选择科目</option>
                                        {accounts.map((account) => (
                                            <option key={account.id} value={account.id}>
                                                {account.code} {account.name}
                                            </option>
                                        ))}
                                    </select>
                                </td>
                                <td className={tdCls} colSpan={11}>
                                    <AmountCells
                                        value={toAmount(entry.debit)}
                                        editable
                                        raw={entry.debit}
                                        onChange={(v) => updateEntry(entry.id, { debit: v })}
                                    />
                                </td>
                                <td className={tdCls} colSpan={11}>
                                    <AmountCells
                                        value={toAmount(entry.credit)}
                                        editable
                                        raw={entry.credit}
                                        onChange={(v) => updateEntry(entry.id, { credit: v })}
                                    />
                                </td>
                            </tr>
                            {assistTypes.length > 0 && (
                                // 辅助核算录入行：每维度一个档案下拉，必录项标红星。
                                <tr>
                                    <td className={cn(tdCls, "bg-muted/30 px-2 py-1")} colSpan={24}>
                                        <div className="flex flex-wrap items-center gap-x-4 gap-y-1">
                                            {assistTypes.map((cfg) => (
                                                <div key={cfg.type} className="flex items-center gap-1">
                                                    <span className="text-xs text-muted-foreground">
                                                        {assistLabel(cfg.type)}
                                                        {cfg.isRequired !== false && (
                                                            <span className="text-destructive">*</span>
                                                        )}
                                                    </span>
                                                    <AssistItemSelect
                                                        accountSetId={accountSetId}
                                                        token={token}
                                                        type={cfg.type as number}
                                                        value={entry.assists[cfg.type as number] ?? ""}
                                                        onChange={(itemId) =>
                                                            updateAssist(entry.id, cfg.type as number, itemId)
                                                        }
                                                    />
                                                </div>
                                            ))}
                                        </div>
                                    </td>
                                </tr>
                            )}
                            </Fragment>
                            )
                        })}
                        <tr className="font-medium">
                            <td className={cn(tdCls, "px-2 py-2")} colSpan={2}>
                                合计：{amountToChineseUppercase(totalDebit)}
                            </td>
                            <td className={tdCls} colSpan={11}>
                                <AmountCells value={totalDebit} />
                            </td>
                            <td className={tdCls} colSpan={11}>
                                <AmountCells value={totalCredit} />
                            </td>
                        </tr>
                    </tbody>
                </table>
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
                <Button variant="outline" size="sm" onClick={addRow}>
                    <Plus data-icon="inline-start" />
                    添加分录
                </Button>
                <span className={cn("text-sm", balanced ? "text-muted-foreground" : "text-destructive")}>
                    {balanced
                        ? "借贷平衡"
                        : totalDebit === 0 && totalCredit === 0
                            ? "请输入借贷金额"
                            : `借贷不平衡，差额 ${fmtMoney(Math.abs(totalDebit - totalCredit))}`}
                </span>
                <div className="flex-1" />
                <Button variant="outline" onClick={onCancel} disabled={saving}>
                    取消
                </Button>
                <Button onClick={handleSave} disabled={!canSave}>
                    {saving ? <Spinner data-icon="inline-start" /> : <Save data-icon="inline-start" />}
                    {saving ? "保存中…" : "保存凭证"}
                </Button>
            </div>

            {/* 损益类科目方向警告弹窗 */}
            <Dialog
                open={directionModal !== null}
                onOpenChange={(next) => {
                    if (next) return
                    setDirectionModal(null)
                }}
            >
                <DialogContent className="sm:max-w-lg" aria-describedby={undefined}>
                    <DialogHeader>
                        <DialogTitle>凭证录入提示</DialogTitle>
                    </DialogHeader>
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
                    <DialogFooter className="sm:flex-col">
                        <Button
                            className="w-full"
                            onClick={() => {
                                autoAdjustEntries()
                            }}
                        >
                            批量调整，系统帮您自动调整所有金额方向
                        </Button>
                        <Button
                            variant="outline"
                            className="w-full"
                            onClick={() => {
                                setDirectionModal(null)
                                // 重新获取 active 并保存
                                const active = entries.filter(isActiveEntry)
                                doSave(active)
                            }}
                        >
                            继续保存，直接保存凭证
                        </Button>
                        <Button variant="outline" className="w-full" onClick={() => setDirectionModal(null)}>
                            关闭弹窗，自行修改凭证后重新保存
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    )
}
