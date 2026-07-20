import { useMemo, useRef, useState } from "react"
import { Plus, Trash2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from "@/components/ui/card"
import { cn } from "@/lib/utils"

/** 现金日记账的一条流水（收入/支出金额以字符串保存，便于表格内编辑）。 */
interface JournalEntry {
    id: string
    date: string
    summary: string
    /** 收支类别。 */
    category: string
    /** 往来单位。 */
    counterparty: string
    /** 收入金额（借方）。 */
    income: string
    /** 支出金额（贷方）。 */
    expense: string
    /** 关联凭证号。 */
    voucher: string
}

/** 期初余额（示例数据，后续接入后端后替换）。 */
const OPENING_BALANCE = 12000

const INITIAL_ENTRIES: JournalEntry[] = [
    { id: "1", date: "2026-07-01", summary: "期初结转", category: "期初", counterparty: "", income: "", expense: "", voucher: "记-001" },
    { id: "2", date: "2026-07-03", summary: "销售收款", category: "销售收入", counterparty: "华信科技", income: "8600", expense: "", voucher: "记-002" },
    { id: "3", date: "2026-07-05", summary: "采购办公用品", category: "办公费", counterparty: "晨光文具", income: "", expense: "1250", voucher: "记-003" },
    { id: "4", date: "2026-07-09", summary: "报销差旅费", category: "差旅费", counterparty: "员工-张三", income: "", expense: "3200", voucher: "记-004" },
    { id: "5", date: "2026-07-12", summary: "客户预付款", category: "预收款", counterparty: "远大商贸", income: "15000", expense: "", voucher: "记-005" },
    { id: "6", date: "2026-07-18", summary: "支付水电费", category: "水电费", counterparty: "国网电力", income: "", expense: "860", voucher: "记-006" },
]

function toNum(value: string): number {
    const n = Number(value)
    return Number.isFinite(n) ? n : 0
}

/** 规整金额输入：仅数字与一个小数点，且小数点后最多两位。 */
function sanitizeAmount(v: string): string {
    let s = v.replace(/[^\d.]/g, "")
    const dot = s.indexOf(".")
    if (dot !== -1) {
        s = s.slice(0, dot + 1) + s.slice(dot + 1).replace(/\./g, "").slice(0, 2)
    }
    return s
}

function formatMoney(value: number): string {
    return value.toLocaleString("zh-CN", {
        style: "currency",
        currency: "CNY",
        minimumFractionDigits: 2,
    })
}

function today(): string {
    return new Date().toISOString().slice(0, 10)
}

const cellCls = "border border-border p-0"
const cellInput =
    "h-9 w-full bg-transparent px-2 outline-none focus:bg-accent/50 placeholder:text-muted-foreground/60"
const amountInput = cn(cellInput, "text-right tabular-nums")

export default function CashJournalPage() {
    const nextId = useRef(INITIAL_ENTRIES.length + 1)
    const [entries, setEntries] = useState<JournalEntry[]>(INITIAL_ENTRIES)

    // 计算每行的累计余额与本期收支合计。
    const { rows, totalIncome, totalExpense, closingBalance } = useMemo(() => {
        let balance = OPENING_BALANCE
        let income = 0
        let expense = 0
        const computed = entries.map((entry, index) => {
            const inc = toNum(entry.income)
            const exp = toNum(entry.expense)
            balance += inc - exp
            income += inc
            expense += exp
            return { ...entry, seq: index + 1, balance }
        })
        return {
            rows: computed,
            totalIncome: income,
            totalExpense: expense,
            closingBalance: balance,
        }
    }, [entries])

    function updateEntry(id: string, patch: Partial<JournalEntry>) {
        setEntries((prev) => prev.map((e) => (e.id === id ? { ...e, ...patch } : e)))
    }

    function removeEntry(id: string) {
        setEntries((prev) => prev.filter((e) => e.id !== id))
    }

    function addRow() {
        setEntries((prev) => [
            ...prev,
            {
                id: String(nextId.current++),
                date: today(),
                summary: "",
                category: "",
                counterparty: "",
                income: "",
                expense: "",
                voucher: "",
            },
        ])
    }

    const summary = [
        { label: "期初余额", value: OPENING_BALANCE },
        { label: "本期收入", value: totalIncome },
        { label: "本期支出", value: totalExpense },
        { label: "期末余额", value: closingBalance },
    ]

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">现金日记账</h1>
                <p className="text-sm text-muted-foreground">
                    按日期顺序记录现金的收入、支出及结余，可像表格一样直接编辑任意单元格。
                </p>
            </div>

            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                {summary.map((item) => (
                    <Card key={item.label}>
                        <CardHeader>
                            <CardDescription>{item.label}</CardDescription>
                            <CardTitle className="text-2xl tabular-nums">
                                {formatMoney(item.value)}
                            </CardTitle>
                        </CardHeader>
                    </Card>
                ))}
            </div>

            <Card>
                <CardHeader className="flex-row items-center justify-between gap-2">
                    <CardTitle>明细流水</CardTitle>
                    <Button size="sm" onClick={addRow}>
                        <Plus />
                        新增一行
                    </Button>
                </CardHeader>
                <CardContent>
                    <div className="overflow-x-auto rounded-md border">
                        <table className="w-full min-w-[1080px] border-collapse text-sm">
                            <thead>
                                <tr className="bg-muted/50 text-left text-muted-foreground">
                                    <th className={cn(cellCls, "px-2 py-2 font-medium")}>日期</th>
                                    <th className={cn(cellCls, "px-2 py-2 font-medium")}>摘要</th>
                                    <th className={cn(cellCls, "px-2 py-2 font-medium")}>收支类别</th>
                                    <th className={cn(cellCls, "px-2 py-2 font-medium")}>往来单位</th>
                                    <th className={cn(cellCls, "px-2 py-2 text-right font-medium")}>收入（借方）</th>
                                    <th className={cn(cellCls, "px-2 py-2 text-right font-medium")}>支出（贷方）</th>
                                    <th className={cn(cellCls, "px-2 py-2 text-right font-medium")}>余额</th>
                                    <th className={cn(cellCls, "px-2 py-2 font-medium")}>关联凭证</th>
                                    <th className={cn(cellCls, "px-2 py-2 text-center font-medium")}>日记账序号</th>
                                    <th className={cn(cellCls, "w-14 px-2 py-2 text-center font-medium")}>操作</th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows.map((row) => (
                                    <tr key={row.id} className="hover:bg-muted/30">
                                        <td className={cn(cellCls, "w-40")}>
                                            <input
                                                type="date"
                                                value={row.date}
                                                onChange={(e) => updateEntry(row.id, { date: e.target.value })}
                                                className={cn(cellInput, "tabular-nums")}
                                            />
                                        </td>
                                        <td className={cellCls}>
                                            <input
                                                value={row.summary}
                                                placeholder="摘要"
                                                onChange={(e) => updateEntry(row.id, { summary: e.target.value })}
                                                className={cellInput}
                                            />
                                        </td>
                                        <td className={cellCls}>
                                            <input
                                                value={row.category}
                                                placeholder="收支类别"
                                                onChange={(e) => updateEntry(row.id, { category: e.target.value })}
                                                className={cellInput}
                                            />
                                        </td>
                                        <td className={cellCls}>
                                            <input
                                                value={row.counterparty}
                                                placeholder="往来单位"
                                                onChange={(e) => updateEntry(row.id, { counterparty: e.target.value })}
                                                className={cellInput}
                                            />
                                        </td>
                                        <td className={cellCls}>
                                            <input
                                                inputMode="decimal"
                                                value={row.income}
                                                placeholder="0.00"
                                                onChange={(e) => updateEntry(row.id, { income: sanitizeAmount(e.target.value) })}
                                                onFocus={(e) => e.target.select()}
                                                className={amountInput}
                                            />
                                        </td>
                                        <td className={cellCls}>
                                            <input
                                                inputMode="decimal"
                                                value={row.expense}
                                                placeholder="0.00"
                                                onChange={(e) => updateEntry(row.id, { expense: sanitizeAmount(e.target.value) })}
                                                onFocus={(e) => e.target.select()}
                                                className={amountInput}
                                            />
                                        </td>
                                        <td className={cn(cellCls, "px-2 text-right font-medium tabular-nums")}>
                                            {formatMoney(row.balance)}
                                        </td>
                                        <td className={cellCls}>
                                            <input
                                                value={row.voucher}
                                                placeholder="凭证号"
                                                onChange={(e) => updateEntry(row.id, { voucher: e.target.value })}
                                                className={cellInput}
                                            />
                                        </td>
                                        <td className={cn(cellCls, "px-2 text-center tabular-nums text-muted-foreground")}>
                                            {row.seq}
                                        </td>
                                        <td className={cn(cellCls, "text-center")}>
                                            <Button
                                                variant="ghost"
                                                size="icon-sm"
                                                onClick={() => removeEntry(row.id)}
                                                title="删除该行"
                                            >
                                                <Trash2 />
                                            </Button>
                                        </td>
                                    </tr>
                                ))}
                                {rows.length === 0 && (
                                    <tr>
                                        <td className={cn(cellCls, "px-3 py-6 text-center text-muted-foreground")} colSpan={10}>
                                            暂无流水，点击“新增一行”开始记账
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                            <tfoot>
                                <tr className="bg-muted/50 font-medium">
                                    <td className={cn(cellCls, "px-2 py-2")} colSpan={4}>
                                        本期合计
                                    </td>
                                    <td className={cn(cellCls, "px-2 py-2 text-right tabular-nums")}>{formatMoney(totalIncome)}</td>
                                    <td className={cn(cellCls, "px-2 py-2 text-right tabular-nums")}>{formatMoney(totalExpense)}</td>
                                    <td className={cn(cellCls, "px-2 py-2 text-right tabular-nums")}>{formatMoney(closingBalance)}</td>
                                    <td className={cellCls} colSpan={3} />
                                </tr>
                            </tfoot>
                        </table>
                    </div>
                </CardContent>
            </Card>
        </div>
    )
}
