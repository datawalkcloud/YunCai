import { useMemo, useRef, useState } from "react"
import { Plus, Save, Trash2, Loader2 } from "lucide-react"

import { VouchersApi, type AccountDto, type CreateVoucherRequest, type UpdateVoucherRequest, type VoucherDetailDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
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
            debit: e.debit && e.debit > 0 ? String(e.debit) : "",
            credit: e.credit && e.credit > 0 ? String(e.credit) : "",
        }))
    }
    return [
        { id: 1, summary: "", accountId: "", debit: "", credit: "" },
        { id: 2, summary: "", accountId: "", debit: "", credit: "" },
    ]
}

function toAmount(value: string): number {
    const n = Number(value)
    return Number.isFinite(n) && n > 0 ? n : 0
}

/** 将金额按分位拆解为与 AMOUNT_COLUMNS 对齐的字符数组，0 时全部留空。 */
function digitCells(value: number): string[] {
    const cells = Array<string>(AMOUNT_COLUMNS.length).fill("")
    const cents = Math.round(value * 100)
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
    return (
        <div className="relative">
            <div className="grid grid-cols-11 divide-x divide-border">
                {cells.map((d, i) => (
                    <div key={i} className="flex h-9 items-center justify-center tabular-nums">
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
    const [voucherWord, setVoucherWord] = useState(initial?.word ?? "记")
    const [voucherNo, setVoucherNo] = useState(initial?.number ? String(initial.number) : "")
    const [date, setDate] = useState(() => (initial?.date ? initial.date.toISOString().slice(0, 10) : today()))
    const [attachments, setAttachments] = useState(initial?.attachmentCount != null ? String(initial.attachmentCount) : "0")
    const [entries, setEntries] = useState<EntryRow[]>(() => toEntryRows(initial))
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const { totalDebit, totalCredit } = useMemo(() => {
        let debit = 0
        let credit = 0
        for (const entry of entries) {
            debit += toAmount(entry.debit)
            credit += toAmount(entry.credit)
        }
        return { totalDebit: debit, totalCredit: credit }
    }, [entries])

    const balanced = totalDebit > 0 && totalDebit === totalCredit
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

    async function handleSave() {
        if (!canSave) return

        // 仅提交有内容的分录行；逐行校验摘要、科目与借贷单边非零。
        const active = entries.filter(
            (e) => e.summary.trim() || e.accountId || toAmount(e.debit) > 0 || toAmount(e.credit) > 0,
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
            if (debit > 0 === credit > 0) {
                return setError("每条分录只能填写借方或贷方其一，且金额不能为零。")
            }
        }

        const mappedEntries = active.map((e) => ({
            summary: e.summary.trim(),
            accountId: e.accountId,
            debit: toAmount(e.debit),
            credit: toAmount(e.credit),
        }))
        // date 为 yyyy-MM-dd 字符串，转为 Date；生成客户端会序列化为 date-only 传给后端。
        const dateValue = new Date(date)
        const attachmentCount = Number.parseInt(attachments, 10) > 0 ? Number.parseInt(attachments, 10) : 0

        setSaving(true)
        setError(null)
        try {
            const api = new VouchersApi(apiConfig(token))
            if (editing && initial?.id) {
                // 编辑：凭证字与号不变，仅更新日期、附单据数与分录。
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

    const blankCls =
        "h-7 rounded-none border-0 border-b border-input bg-transparent px-1 text-center shadow-none focus-visible:ring-0"

    return (
        <div className="flex flex-col gap-4">
            {/* 凭证标题 */}
            <div className="text-center text-xl font-bold tracking-[0.5em]">记账凭证</div>

            {/* 凭证字号 / 日期 / 附件 */}
            <div className="flex flex-wrap items-end justify-between gap-4 text-sm">
                <div className="flex items-end gap-1">
                    <Input
                        value={voucherWord}
                        onChange={(e) => setVoucherWord(e.target.value)}
                        disabled={editing}
                        className={cn(blankCls, "w-12")}
                    />
                    <span className="pb-1">字第</span>
                    <Input
                        value={voucherNo}
                        onChange={(e) => setVoucherNo(e.target.value.replace(/\D/g, ""))}
                        placeholder="自动"
                        disabled={editing}
                        className={cn(blankCls, "w-20")}
                    />
                    <span className="pb-1">号</span>
                </div>
                <div className="flex items-end gap-1">
                    <span className="pb-1">日期：</span>
                    <Input
                        type="date"
                        value={date}
                        onChange={(e) => setDate(e.target.value)}
                        className={cn(blankCls, "w-40")}
                    />
                </div>
                <div className="flex items-end gap-1">
                    <span className="pb-1">附单据</span>
                    <Input
                        type="number"
                        min={0}
                        value={attachments}
                        onChange={(e) => setAttachments(e.target.value)}
                        className={cn(blankCls, "w-16")}
                    />
                    <span className="pb-1">张</span>
                </div>
            </div>

            {/* 分录表格 */}
            <div className="overflow-x-auto">
                <table className="w-full min-w-[1040px] border-collapse text-sm">
                    <colgroup>
                        <col style={{ width: 240 }} />
                        <col style={{ width: 200 }} />
                        {AMOUNT_COLUMNS.map((_, i) => (
                            <col key={`d-${i}`} style={{ width: 28 }} />
                        ))}
                        {AMOUNT_COLUMNS.map((_, i) => (
                            <col key={`c-${i}`} style={{ width: 28 }} />
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
                            {AMOUNT_COLUMNS.map((c, i) => (
                                <th key={`dh-${i}`} className={cn(thCls, "text-[10px]")}>
                                    {c}
                                </th>
                            ))}
                            {AMOUNT_COLUMNS.map((c, i) => (
                                <th key={`ch-${i}`} className={cn(thCls, "text-[10px]")}>
                                    {c}
                                </th>
                            ))}
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
                                            variant="ghost"
                                            size="icon-xs"
                                            onClick={() => removeRow(entry.id)}
                                            disabled={entries.length <= 1}
                                            title="删除该行"
                                            className="mr-1 shrink-0 text-muted-foreground"
                                        >
                                            <Trash2 />
                                        </Button>
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
                <Button variant="outline" size="sm" onClick={addRow}>
                    <Plus />
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
                <Button variant="outline" onClick={onCancel} disabled={saving}>
                    取消
                </Button>
                <Button onClick={handleSave} disabled={!canSave}>
                    {saving ? <Loader2 className="animate-spin" /> : <Save />}
                    {saving ? "保存中…" : "保存凭证"}
                </Button>
            </div>
        </div>
    )
}
