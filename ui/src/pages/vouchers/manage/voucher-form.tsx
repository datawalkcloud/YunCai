import { useMemo, useRef, useState } from "react"
import { Plus, Save, Trash2 } from "lucide-react"
import { Button, Input, Modal, Select } from "antd"

import { useAccountSets } from "@/accountSets/AccountSetsContext"
import { VouchersApi, type AccountDto, type CreateVoucherRequest, type UpdateVoucherRequest, type VoucherDetailDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"

/** 凭证分录行。借方与贷方以字符串保存，便于输入编辑；account 存科目 id。 */
interface EntryRow {
    id: number
    summary: string
    accountId: string
    debit: string
    credit: string
}

/** 金额分位列，从高位到低位（最高支持到亿位，角、分为小数位）。 */
const AMOUNT_COLUMNS = ["亿", "千万", "百万", "十万", "万", "千", "百", "十", "元", "角", "分"]

function today(): string {
    return new Date().toISOString().slice(0, 10)
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
        }))
    }
    return [
        { id: 1, summary: "", accountId: "", debit: "", credit: "" },
        { id: 2, summary: "", accountId: "", debit: "", credit: "" },
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

/** 人民币金额大写。 */
function digitUppercase(n: number): string {
    if (n === 0) return "零元整"
    const fraction = ["角", "分"]
    const digit = ["零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖"]
    const unit = [
        ["元", "万", "亿"],
        ["", "拾", "佰", "仟"],
    ]
    const head = n < 0 ? "负" : ""
    let num = Math.abs(n)
    let s = ""
    for (let i = 0; i < fraction.length; i++) {
        s += (digit[Math.floor(num * 10 * Math.pow(10, i)) % 10] + fraction[i]).replace(/零./, "")
    }
    s = s || "整"
    num = Math.floor(num)
    for (let i = 0; i < unit[0].length && num > 0; i++) {
        let p = ""
        for (let j = 0; j < unit[1].length && num > 0; j++) {
            p = digit[num % 10] + unit[1][j] + p
            num = Math.floor(num / 10)
        }
        s = p.replace(/(零.)*零$/, "").replace(/^$/, "零") + unit[0][i] + s
    }
    return head + s.replace(/(零.)*零元/, "元").replace(/(零.)*零$/, "").replace(/^整$/, "零元整")
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
    const redStyle = isNegative ? { color: "#ff4d4f" } : undefined
    return (
        <div className="relative">
            <div className="grid grid-cols-11" style={redStyle}>
                {cells.map((d, i) => {
                    // 分位竖线：元→角粉红，千→百/百万→十万蓝色，其余浅灰
                    const isYuanSep = i === 8
                    const isQianSep = i === 5
                    const isBaiwanSep = i === 2
                    const colored = isYuanSep || isQianSep || isBaiwanSep
                    return (
                        <div
                            key={i}
                            className="flex h-[54px] items-center justify-center tabular-nums"
                            style={i < 10 ? {
                                borderRight: "1px solid",
                                borderRightColor: colored ? (isYuanSep ? "#ffd6e7" : "#bae7ff") : undefined,
                            } : undefined}
                        >
                            {focused ? "" : d}
                        </div>
                    )
                })}
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
    const [date, setDate] = useState(() => (initial?.date ? initial.date.toISOString().slice(0, 10) : today()))
    const [attachments, setAttachments] = useState(initial?.attachmentCount != null ? String(initial.attachmentCount) : "0")
    const [entries, setEntries] = useState<EntryRow[]>(() => toEntryRows(initial))
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [directionModal, setDirectionModal] = useState<{ issues: number[] } | null>(null)

    const accountMap = useMemo(() => new Map(accounts.map((a) => [a.id, a] as const)), [accounts])

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

    function addRow() {
        setEntries((prev) => [
            ...prev,
            { id: nextId.current++, summary: "", accountId: "", debit: "", credit: "" },
        ])
    }

    function removeRow(id: number) {
        setEntries((prev) => (prev.length > 1 ? prev.filter((e) => e.id !== id) : prev))
    }

    // 实际执行保存的 API 调用。
    async function doSave(entriesToSave: EntryRow[]) {
        const mappedEntries = entriesToSave.map((e) => ({
            summary: e.summary.trim(),
            accountId: e.accountId,
            debit: toAmount(e.debit),
            credit: toAmount(e.credit),
        }))
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
        const active = entries.filter(
            (e) => e.summary.trim() || e.accountId || toAmount(e.debit) !== 0 || toAmount(e.credit) !== 0,
        )
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

    const blankCls =
        "h-7 rounded-none border-0 border-b border-input bg-transparent px-1 text-center shadow-none focus-visible:ring-0"

    return (
        <div className="flex flex-col gap-4">
            {/* 凭证标题 */}
            <div className="text-center text-xl font-bold tracking-[0.5em]">记账凭证</div>

            {/* 凭证字号 / 日期 / 附件 */}
            <div className="flex items-end gap-4 text-sm whitespace-nowrap">
                <div className="flex items-end gap-1 shrink-0">
                    <Select
                        variant="borderless"
                        size="small"
                        value={voucherWord}
                        onChange={setVoucherWord}
                        disabled={editing}
                        options={voucherWordOptions.map((w) => ({ value: w, label: w }))}
                        className="w-16 border-b border-input"
                    />
                    <span className="pb-1">字第</span>
                    <Input
                        variant="borderless"
                        value={voucherNo}
                        onChange={(e) => setVoucherNo(e.target.value.replace(/\D/g, ""))}
                        placeholder="自动"
                        disabled={editing}
                        className={cn(blankCls, "w-16")}
                    />
                    <span className="pb-1">号</span>
                </div>
                <div className="flex items-end gap-1 shrink-0">
                    <span className="pb-1">日期：</span>
                    <Input
                        variant="borderless"
                        type="date"
                        value={date}
                        onChange={(e) => setDate(e.target.value)}
                        className={cn(blankCls, "w-32")}
                    />
                </div>
                <div className="flex items-end gap-1 shrink-0">
                    <span className="pb-1">附单据</span>
                    <Input
                        variant="borderless"
                        type="number"
                        min={0}
                        value={attachments}
                        onChange={(e) => setAttachments(e.target.value)}
                        className={cn(blankCls, "w-12")}
                    />
                    <span className="pb-1">张</span>
                </div>
                <div className="flex-1" />
            </div>

            {/* 分录表格 */}
            <div className="overflow-x-auto">
                <table className="w-full min-w-[1040px] border-collapse text-sm">
                    <colgroup>
                        <col style={{ width: 240 }} />
                        <col style={{ width: 200 }} />
                        {AMOUNT_COLUMNS.map((_, i) => (
                            <col key={`d-${i}`} style={{ width: 24 }} />
                        ))}
                        {AMOUNT_COLUMNS.map((_, i) => (
                            <col key={`c-${i}`} style={{ width: 24 }} />
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
                                <div className="grid grid-cols-11">
                                    {AMOUNT_COLUMNS.map((c, i) => {
                                        const isYuanSep = i === 8
                                        const isQianSep = i === 5
                                        const isBaiwanSep = i === 2
                                        const colored = isYuanSep || isQianSep || isBaiwanSep
                                        return (
                                            <div
                                                key={`dh-${i}`}
                                                className="flex h-12 items-center justify-center text-[10px] font-medium text-muted-foreground"
                                                style={{
                                                    ...(i < 10 ? { borderRight: "1px solid", borderRightColor: colored ? (isYuanSep ? "#ffd6e7" : "#bae7ff") : undefined } : {}),
                                                    writingMode: "vertical-rl",
                                                }}
                                            >
                                                {c}
                                            </div>
                                        )
                                    })}
                                </div>
                            </th>
                            <th colSpan={11} className={cn(thCls, "p-0 align-top")}>
                                <div className="grid grid-cols-11">
                                    {AMOUNT_COLUMNS.map((c, i) => {
                                        const isYuanSep = i === 8
                                        const isQianSep = i === 5
                                        const isBaiwanSep = i === 2
                                        const colored = isYuanSep || isQianSep || isBaiwanSep
                                        return (
                                            <div
                                                key={`ch-${i}`}
                                                className="flex h-12 items-center justify-center text-[10px] font-medium text-muted-foreground"
                                                style={{
                                                    ...(i < 10 ? { borderRight: "1px solid", borderRightColor: colored ? (isYuanSep ? "#ffd6e7" : "#bae7ff") : undefined } : {}),
                                                    writingMode: "vertical-rl",
                                                }}
                                            >
                                                {c}
                                            </div>
                                        )
                                    })}
                                </div>
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {entries.map((entry) => (
                            <tr key={entry.id}>
                                <td className={tdCls}>
                                    <div className="flex items-center">
                                        <input
                                            value={entry.summary}
                                            onChange={(e) => updateEntry(entry.id, { summary: e.target.value })}
                                            placeholder="摘要"
                                            className="h-9 w-full bg-transparent px-2 outline-none placeholder:text-muted-foreground"
                                        />
                                        <Button
                                            type="text"
                                            size="small"
                                            danger
                                            onClick={() => removeRow(entry.id)}
                                            disabled={entries.length <= 1}
                                            title="删除该行"
                                            className="mr-1 shrink-0"
                                            icon={<Trash2 size={16} />}
                                        />
                                    </div>
                                </td>
                                <td className={tdCls}>
                                    <select
                                        value={entry.accountId}
                                        onChange={(e) => updateEntry(entry.id, { accountId: e.target.value })}
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
                        ))}
                        <tr className="font-medium">
                            <td className={cn(tdCls, "px-2 py-2")} colSpan={2}>
                                合计：{digitUppercase(totalDebit)}
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
                <Button size="small" onClick={addRow} icon={<Plus size={16} />}>
                    添加分录
                </Button>
                <span className={cn("text-sm", balanced ? "text-muted-foreground" : "text-destructive")}>
                    {balanced
                        ? "借贷平衡"
                        : totalDebit === 0 && totalCredit === 0
                            ? "请输入借贷金额"
                            : `借贷不平衡，差额 ¥${Math.abs(totalDebit - totalCredit).toFixed(2)}`}
                </span>
                <div className="flex-1" />
                <Button onClick={onCancel} disabled={saving}>
                    取消
                </Button>
                <Button type="primary" onClick={handleSave} disabled={!canSave} loading={saving} icon={saving ? undefined : <Save size={16} />}>
                    {saving ? "保存中…" : "保存凭证"}
                </Button>
            </div>

            {/* 损益类科目方向警告弹窗 */}
            <Modal
                title="凭证录入提示"
                open={directionModal !== null}
                onCancel={() => setDirectionModal(null)}
                footer={null}
                width={520}
            >
                <div className="space-y-4">
                    <div>
                        <p className="font-medium text-amber-600">系统检测到当前凭证存在损益科目录入不规范的凭证行哦~</p>
                        <p className="mt-2 text-sm text-muted-foreground">
                            收入类 要做在 贷方 ， 费用类 要做在 借方 。 如果要冲回，应该录入相同方向的红字，即负数。否则科目余额表和利润表数据会对不上
                        </p>
                    </div>
                    <p className="text-sm">
                        需要调整金额方向的凭证行：<span className="font-semibold">{directionModal?.issues.join("、")}</span>
                    </p>
                    <div className="flex flex-col gap-2">
                        <Button
                            type="primary"
                            block
                            onClick={() => {
                                autoAdjustEntries()
                            }}
                        >
                            批量调整，系统帮您自动调整所有金额方向
                        </Button>
                        <Button
                            block
                            onClick={() => {
                                setDirectionModal(null)
                                // 重新获取 active 并保存
                                const active = entries.filter(
                                    (e) => e.summary.trim() || e.accountId || toAmount(e.debit) !== 0 || toAmount(e.credit) !== 0,
                                )
                                doSave(active)
                            }}
                        >
                            继续保存，直接保存凭证
                        </Button>
                        <Button block onClick={() => setDirectionModal(null)}>
                            关闭弹窗，自行修改凭证后重新保存
                        </Button>
                    </div>
                </div>
            </Modal>
        </div>
    )
}
