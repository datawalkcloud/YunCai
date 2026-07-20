import { useMemo, useRef, useState } from "react"
import { Plus, Save, Trash2 } from "lucide-react"

import { useAuth } from "@/auth/AuthContext"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"

/** 凭证分录行。借方与贷方以字符串保存，便于输入编辑。 */
interface VoucherEntry {
    id: number
    summary: string
    account: string
    debit: string
    credit: string
}

/** 常用会计科目（示例，后续可接入科目表接口）。 */
const ACCOUNTS = [
    "库存现金",
    "银行存款",
    "应收账款",
    "预付账款",
    "固定资产",
    "应付账款",
    "预收账款",
    "应交税费",
    "实收资本",
    "主营业务收入",
    "主营业务成本",
    "管理费用",
    "销售费用",
    "财务费用",
]

/** 金额分位列，从高位到低位（最高支持到亿位，角、分为小数位）。 */
const AMOUNT_COLUMNS = ["亿", "千万", "百万", "十万", "万", "千", "百", "十", "元", "角", "分"]

function today(): string {
    return new Date().toISOString().slice(0, 10)
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
                    // 聚焦时显示完整数值供录入；失焦后文字透明，由下方分位小格按位呈现。
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

export default function NewVoucherPage() {
    const { user } = useAuth()
    const nextId = useRef(3)
    const [voucherWord, setVoucherWord] = useState("记")
    const [voucherNo, setVoucherNo] = useState("")
    const [date, setDate] = useState(today)
    const [attachments, setAttachments] = useState("0")
    const [entries, setEntries] = useState<VoucherEntry[]>([
        { id: 1, summary: "", account: "", debit: "", credit: "" },
        { id: 2, summary: "", account: "", debit: "", credit: "" },
    ])
    const [saved, setSaved] = useState(false)

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
    const canSave = balanced

    function updateEntry(id: number, patch: Partial<VoucherEntry>) {
        setEntries((prev) => prev.map((e) => (e.id === id ? { ...e, ...patch } : e)))
        setSaved(false)
    }

    function addRow() {
        setEntries((prev) => [
            ...prev,
            { id: nextId.current++, summary: "", account: "", debit: "", credit: "" },
        ])
        setSaved(false)
    }

    function removeRow(id: number) {
        setEntries((prev) => (prev.length > 1 ? prev.filter((e) => e.id !== id) : prev))
        setSaved(false)
    }

    function handleSave() {
        if (!canSave) return
        // 暂无后端接口，先在本地给出保存成功提示（示例）。
        setSaved(true)
    }

    function handleReset() {
        setVoucherNo("")
        setDate(today())
        setAttachments("0")
        setEntries([
            { id: nextId.current++, summary: "", account: "", debit: "", credit: "" },
            { id: nextId.current++, summary: "", account: "", debit: "", credit: "" },
        ])
        setSaved(false)
    }

    const blankCls =
        "h-7 rounded-none border-0 border-b border-input bg-transparent px-1 text-center shadow-none focus-visible:ring-0"

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">新增凭证</h1>
                <p className="text-sm text-muted-foreground">
                    仿纸质记账凭证样式录入，借贷金额需保持平衡后方可保存。
                </p>
            </div>

            <Card>
                <CardContent className="flex flex-col gap-4 py-8">
                    {/* 凭证标题 */}
                    <div className="text-center text-xl font-bold tracking-[0.5em]">记账凭证</div>

                    {/* 凭证字号 / 日期 / 附件 */}
                    <div className="flex flex-wrap items-end justify-between gap-4 text-sm">
                        <div className="flex items-end gap-1">
                            <Input
                                value={voucherWord}
                                onChange={(e) => setVoucherWord(e.target.value)}
                                className={cn(blankCls, "w-12")}
                            />
                            <span className="pb-1">字第</span>
                            <Input
                                value={voucherNo}
                                onChange={(e) => setVoucherNo(e.target.value)}
                                placeholder="001"
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
                                <col style={{ width: 140 }} />
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
                                                value={entry.account}
                                                onChange={(e) => updateEntry(entry.id, { account: e.target.value })}
                                                className="h-9 w-full bg-transparent px-1 text-sm outline-none"
                                            >
                                                <option value="">请选择科目</option>
                                                {ACCOUNTS.map((account) => (
                                                    <option key={account} value={account}>
                                                        {account}
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
                    <div className="flex flex-wrap gap-x-8 gap-y-2 pt-2 text-sm text-muted-foreground">
                        <span>会计主管：______</span>
                        <span>记账：______</span>
                        <span>出纳：______</span>
                        <span>审核：______</span>
                        <span>制单：{user?.displayName ?? "______"}</span>
                    </div>
                </CardContent>
            </Card>

            {/* 操作区 */}
            <div className="flex flex-wrap items-center gap-3">
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
                <Button onClick={handleSave} disabled={!canSave}>
                    <Save />
                    保存凭证
                </Button>
                <Button variant="outline" onClick={handleReset}>
                    重置
                </Button>
                {saved && <span className="text-sm text-muted-foreground">凭证已保存（示例）。</span>}
            </div>
        </div>
    )
}
