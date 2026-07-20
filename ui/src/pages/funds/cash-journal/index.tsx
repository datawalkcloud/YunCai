import { useCallback, useEffect, useMemo, useState } from "react"
import { Loader2, RefreshCw } from "lucide-react"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type CashJournalRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"

/** 选定的会计期间。 */
interface Period {
    year: number
    month: number
}

function formatMoney(value: number): string {
    return value.toLocaleString("zh-CN", {
        style: "currency",
        currency: "CNY",
        minimumFractionDigits: 2,
    })
}

/** 金额为 0 时留空，避免表格被 0.00 填满。 */
function fmt(value: number): string {
    if (!value) return ""
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** Period → <input type="month"> 的值（YYYY-MM）。 */
function toMonthInput(p: Period | null): string {
    return p ? `${String(p.year).padStart(4, "0")}-${String(p.month).padStart(2, "0")}` : ""
}

const cellCls = "border border-border px-2 py-2 align-middle"

export default function CashJournalPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [period, setPeriod] = useState<Period | null>(null)
    const [entries, setEntries] = useState<CashJournalRowDto[]>([])
    const [openingBalance, setOpeningBalance] = useState(0)
    const [periodText, setPeriodText] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new ReportsApi(apiConfig(token)).accountsetsAccountSetIdReportsCashJournalGet({
                accountSetId,
                year: period?.year,
                month: period?.month,
            })
            setEntries(res.rows ?? [])
            setOpeningBalance(res.openingBalance ?? 0)
            setPeriodText(res.periodText ?? "")
            // 首次未指定期间时，采用后端解析出的期间（最近有凭证的月份）。
            if (period === null && res.year && res.month) {
                setPeriod({ year: res.year, month: res.month })
            }
        } catch (err) {
            setError(await apiErrorMessage(err, "加载现金日记账失败，请重试。"))
            setEntries([])
            setOpeningBalance(0)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, period])

    useEffect(() => {
        void load()
    }, [load])

    // 计算每行的累计余额与本期收支合计。
    const { rows, totalIncome, totalExpense, closingBalance } = useMemo(() => {
        let balance = openingBalance
        let income = 0
        let expense = 0
        const computed = entries.map((entry, index) => {
            const inc = entry.income ?? 0
            const exp = entry.expense ?? 0
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
    }, [entries, openingBalance])

    function handleMonthChange(value: string) {
        if (!value) return
        const [y, m] = value.split("-").map(Number)
        if (Number.isFinite(y) && Number.isFinite(m)) setPeriod({ year: y, month: m })
    }

    const summary = [
        { label: "期初余额", value: openingBalance },
        { label: "本期收入", value: totalIncome },
        { label: "本期支出", value: totalExpense },
        { label: "期末余额", value: closingBalance },
    ]

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">现金日记账</h1>
                <p className="text-sm text-muted-foreground">
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    。由涉及库存现金科目的凭证按日期序时汇总，收入为借方、支出为贷方。
                </p>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
                <div className="flex items-center gap-2">
                    <span>会计期间：</span>
                    <Input
                        type="month"
                        className="h-8 w-40"
                        value={toMonthInput(period)}
                        onChange={(e) => handleMonthChange(e.target.value)}
                        disabled={!accountSetId}
                    />
                    <button
                        type="button"
                        onClick={() => void load()}
                        disabled={loading || !accountSetId}
                        className="inline-flex items-center gap-1 rounded-md border border-border px-2 py-1 text-xs hover:bg-muted disabled:opacity-50"
                    >
                        <RefreshCw className={cn("size-3.5", loading && "animate-spin")} />
                        刷新
                    </button>
                </div>
                {periodText && <span>期间：{periodText}</span>}
            </div>

            {error && <p className="text-sm text-destructive">{error}</p>}

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
                <CardHeader>
                    <CardTitle>明细流水</CardTitle>
                </CardHeader>
                <CardContent>
                    <div className="overflow-x-auto rounded-md border">
                        <table className="w-full min-w-[960px] border-collapse text-sm">
                            <thead>
                                <tr className="bg-muted/50 text-left text-muted-foreground">
                                    <th className={cn(cellCls, "font-medium")}>日期</th>
                                    <th className={cn(cellCls, "font-medium")}>凭证号</th>
                                    <th className={cn(cellCls, "font-medium")}>摘要</th>
                                    <th className={cn(cellCls, "font-medium")}>对方科目</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>收入（借方）</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>支出（贷方）</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>余额</th>
                                    <th className={cn(cellCls, "text-center font-medium")}>序号</th>
                                </tr>
                            </thead>
                            <tbody>
                                {loading && rows.length === 0 ? (
                                    <tr>
                                        <td className={cn(cellCls, "px-3 py-10 text-center text-muted-foreground")} colSpan={8}>
                                            <Loader2 className="mx-auto size-5 animate-spin" />
                                        </td>
                                    </tr>
                                ) : rows.length === 0 ? (
                                    <tr>
                                        <td className={cn(cellCls, "px-3 py-10 text-center text-muted-foreground")} colSpan={8}>
                                            {accountSetId ? "该期间暂无现金流水。" : "请先选择账套。"}
                                        </td>
                                    </tr>
                                ) : (
                                    rows.map((row) => (
                                        <tr key={row.seq} className="hover:bg-muted/30">
                                            <td className={cn(cellCls, "tabular-nums")}>{row.date}</td>
                                            <td className={cn(cellCls, "tabular-nums")}>{row.voucherNo}</td>
                                            <td className={cellCls}>{row.summary}</td>
                                            <td className={cellCls}>{row.contraAccount}</td>
                                            <td className={cn(cellCls, "text-right tabular-nums")}>{fmt(row.income ?? 0)}</td>
                                            <td className={cn(cellCls, "text-right tabular-nums")}>{fmt(row.expense ?? 0)}</td>
                                            <td className={cn(cellCls, "text-right font-medium tabular-nums")}>
                                                {formatMoney(row.balance)}
                                            </td>
                                            <td className={cn(cellCls, "text-center tabular-nums text-muted-foreground")}>
                                                {row.seq}
                                            </td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                            <tfoot>
                                <tr className="bg-muted/50 font-medium">
                                    <td className={cellCls} colSpan={4}>本期合计</td>
                                    <td className={cn(cellCls, "text-right tabular-nums")}>{formatMoney(totalIncome)}</td>
                                    <td className={cn(cellCls, "text-right tabular-nums")}>{formatMoney(totalExpense)}</td>
                                    <td className={cn(cellCls, "text-right tabular-nums")}>{formatMoney(closingBalance)}</td>
                                    <td className={cellCls} />
                                </tr>
                            </tfoot>
                        </table>
                    </div>
                </CardContent>
            </Card>
        </div>
    )
}
