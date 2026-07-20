import { useCallback, useEffect, useMemo, useState } from "react"
import { Loader2, RefreshCw } from "lucide-react"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type TrialBalanceRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"

/** 选定的会计期间。 */
interface Period {
    year: number
    month: number
}

const AMOUNT_KEYS = [
    "openingDebit",
    "openingCredit",
    "periodDebit",
    "periodCredit",
    "endingDebit",
    "endingCredit",
] as const

function fmt(value: number, force = false): string {
    if (value === 0 && !force) return ""
    return value.toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

/** Period → <input type="month"> 的值（YYYY-MM）。 */
function toMonthInput(p: Period | null): string {
    return p ? `${String(p.year).padStart(4, "0")}-${String(p.month).padStart(2, "0")}` : ""
}

const cellCls = "border border-border px-2 py-1 align-middle"
const amountCls = cn(cellCls, "text-right tabular-nums")

export default function TrialBalancePage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [period, setPeriod] = useState<Period | null>(null)
    const [rows, setRows] = useState<TrialBalanceRowDto[]>([])
    const [periodText, setPeriodText] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new ReportsApi(apiConfig(token)).accountsetsAccountSetIdReportsTrialBalanceGet({
                accountSetId,
                year: period?.year,
                month: period?.month,
            })
            setRows(res.rows ?? [])
            setPeriodText(res.periodText ?? "")
            // 首次未指定期间时，采用后端解析出的期间（最近有凭证的月份）。
            if (period === null && res.year && res.month) {
                setPeriod({ year: res.year, month: res.month })
            }
        } catch (err) {
            setError(await apiErrorMessage(err, "加载科目余额表失败，请重试。"))
            setRows([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, period])

    useEffect(() => {
        void load()
    }, [load])

    const { totals, balanced } = useMemo(() => {
        const t = {
            openingDebit: 0,
            openingCredit: 0,
            periodDebit: 0,
            periodCredit: 0,
            endingDebit: 0,
            endingCredit: 0,
        }
        for (const row of rows) {
            for (const key of AMOUNT_KEYS) t[key] += row[key] ?? 0
        }
        // 金额以分为单位比较，规避浮点误差。
        const eq = (a: number, b: number) => Math.round(a * 100) === Math.round(b * 100)
        return {
            totals: t,
            balanced:
                eq(t.openingDebit, t.openingCredit) &&
                eq(t.periodDebit, t.periodCredit) &&
                eq(t.endingDebit, t.endingCredit),
        }
    }, [rows])

    function handleMonthChange(value: string) {
        if (!value) return
        const [y, m] = value.split("-").map(Number)
        if (Number.isFinite(y) && Number.isFinite(m)) setPeriod({ year: y, month: m })
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">科目余额表</h1>
                <p className="text-sm text-muted-foreground">
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    。汇总各科目的期初余额、本期发生额与期末余额，用于试算平衡与结账核对。
                </p>
            </div>

            <Card>
                <CardContent className="flex flex-col gap-3 py-6">
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
                        <span>单位：元</span>
                    </div>

                    {error && <p className="text-sm text-destructive">{error}</p>}

                    <div className="overflow-x-auto">
                        <table className="w-full min-w-[900px] border-collapse text-sm">
                            <thead className="bg-muted/50 text-muted-foreground">
                                <tr>
                                    <th rowSpan={2} className={cn(cellCls, "text-center font-medium")}>科目编码</th>
                                    <th rowSpan={2} className={cn(cellCls, "text-center font-medium")}>科目名称</th>
                                    <th colSpan={2} className={cn(cellCls, "text-center font-medium")}>期初余额</th>
                                    <th colSpan={2} className={cn(cellCls, "text-center font-medium")}>本期发生额</th>
                                    <th colSpan={2} className={cn(cellCls, "text-center font-medium")}>期末余额</th>
                                </tr>
                                <tr>
                                    <th className={cn(cellCls, "text-right font-medium")}>借方</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>贷方</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>借方</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>贷方</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>借方</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>贷方</th>
                                </tr>
                            </thead>
                            <tbody>
                                {loading && rows.length === 0 ? (
                                    <tr>
                                        <td colSpan={8} className="px-2 py-10 text-center text-muted-foreground">
                                            <Loader2 className="mx-auto size-5 animate-spin" />
                                        </td>
                                    </tr>
                                ) : rows.length === 0 ? (
                                    <tr>
                                        <td colSpan={8} className="px-2 py-10 text-center text-muted-foreground">
                                            {accountSetId ? "该期间暂无科目余额数据。" : "请先选择账套。"}
                                        </td>
                                    </tr>
                                ) : (
                                    rows.map((row) => (
                                        <tr key={row.code} className="hover:bg-muted/40">
                                            <td className={cn(cellCls, "tabular-nums")}>{row.code}</td>
                                            <td className={cellCls}>{row.name}</td>
                                            <td className={amountCls}>{fmt(row.openingDebit ?? 0)}</td>
                                            <td className={amountCls}>{fmt(row.openingCredit ?? 0)}</td>
                                            <td className={amountCls}>{fmt(row.periodDebit ?? 0)}</td>
                                            <td className={amountCls}>{fmt(row.periodCredit ?? 0)}</td>
                                            <td className={amountCls}>{fmt(row.endingDebit ?? 0)}</td>
                                            <td className={amountCls}>{fmt(row.endingCredit ?? 0)}</td>
                                        </tr>
                                    ))
                                )}
                                {rows.length > 0 && (
                                    <tr className="bg-muted/50 font-medium">
                                        <td className={cellCls} colSpan={2}>合计</td>
                                        <td className={amountCls}>{fmt(totals.openingDebit, true)}</td>
                                        <td className={amountCls}>{fmt(totals.openingCredit, true)}</td>
                                        <td className={amountCls}>{fmt(totals.periodDebit, true)}</td>
                                        <td className={amountCls}>{fmt(totals.periodCredit, true)}</td>
                                        <td className={amountCls}>{fmt(totals.endingDebit, true)}</td>
                                        <td className={amountCls}>{fmt(totals.endingCredit, true)}</td>
                                    </tr>
                                )}
                            </tbody>
                        </table>
                    </div>

                    {rows.length > 0 && (
                        <div className={cn("text-sm", balanced ? "text-muted-foreground" : "text-destructive")}>
                            {balanced
                                ? `试算平衡：借方合计 = 贷方合计（${periodText}）`
                                : "试算不平衡，请核对数据"}
                        </div>
                    )}
                </CardContent>
            </Card>
        </div>
    )
}
