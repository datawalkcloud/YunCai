import { useMemo } from "react"

import { Card, CardContent } from "@/components/ui/card"
import { cn } from "@/lib/utils"

/** 科目余额表一行：期初、本期发生额、期末各含借/贷。 */
interface BalanceRow {
    code: string
    name: string
    openingDebit: number
    openingCredit: number
    periodDebit: number
    periodCredit: number
    endingDebit: number
    endingCredit: number
}

/** 示例数据（后续接入账簿接口后替换）；已构造为借贷试算平衡。 */
const ROWS: BalanceRow[] = [
    { code: "1001", name: "库存现金", openingDebit: 20000, openingCredit: 0, periodDebit: 50000, periodCredit: 40000, endingDebit: 30000, endingCredit: 0 },
    { code: "1002", name: "银行存款", openingDebit: 100000, openingCredit: 0, periodDebit: 200000, periodCredit: 150000, endingDebit: 150000, endingCredit: 0 },
    { code: "1122", name: "应收账款", openingDebit: 54000, openingCredit: 0, periodDebit: 30000, periodCredit: 16000, endingDebit: 68000, endingCredit: 0 },
    { code: "1601", name: "固定资产", openingDebit: 340000, openingCredit: 0, periodDebit: 0, periodCredit: 20000, endingDebit: 320000, endingCredit: 0 },
    { code: "2202", name: "应付账款", openingDebit: 0, openingCredit: 72000, periodDebit: 18000, periodCredit: 30000, endingDebit: 0, endingCredit: 84000 },
    { code: "2221", name: "应交税费", openingDebit: 0, openingCredit: 15000, periodDebit: 5000, periodCredit: 8000, endingDebit: 0, endingCredit: 18000 },
    { code: "4001", name: "实收资本", openingDebit: 0, openingCredit: 400000, periodDebit: 0, periodCredit: 0, endingDebit: 0, endingCredit: 400000 },
    { code: "4104", name: "未分配利润", openingDebit: 0, openingCredit: 27000, periodDebit: 0, periodCredit: 0, endingDebit: 0, endingCredit: 27000 },
    { code: "6001", name: "主营业务收入", openingDebit: 0, openingCredit: 0, periodDebit: 0, periodCredit: 87000, endingDebit: 0, endingCredit: 87000 },
    { code: "6401", name: "主营业务成本", openingDebit: 0, openingCredit: 0, periodDebit: 48000, periodCredit: 0, endingDebit: 48000, endingCredit: 0 },
]

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

const cellCls = "border border-border px-2 py-1 align-middle"
const amountCls = cn(cellCls, "text-right tabular-nums")

export default function TrialBalancePage() {
    const { totals, balanced } = useMemo(() => {
        const t = {
            openingDebit: 0,
            openingCredit: 0,
            periodDebit: 0,
            periodCredit: 0,
            endingDebit: 0,
            endingCredit: 0,
        }
        for (const row of ROWS) {
            for (const key of AMOUNT_KEYS) t[key] += row[key]
        }
        return {
            totals: t,
            balanced:
                t.openingDebit === t.openingCredit &&
                t.periodDebit === t.periodCredit &&
                t.endingDebit === t.endingCredit,
        }
    }, [])

    const period = "2026年07月"

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">科目余额表</h1>
                <p className="text-sm text-muted-foreground">
                    汇总各科目的期初余额、本期发生额与期末余额，用于试算平衡与结账核对。
                </p>
            </div>

            <Card>
                <CardContent className="flex flex-col gap-3 py-6">
                    <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
                        <span>会计期间：{period}</span>
                        <span>单位：元</span>
                    </div>

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
                                {ROWS.map((row) => (
                                    <tr key={row.code} className="hover:bg-muted/40">
                                        <td className={cn(cellCls, "tabular-nums")}>{row.code}</td>
                                        <td className={cellCls}>{row.name}</td>
                                        <td className={amountCls}>{fmt(row.openingDebit)}</td>
                                        <td className={amountCls}>{fmt(row.openingCredit)}</td>
                                        <td className={amountCls}>{fmt(row.periodDebit)}</td>
                                        <td className={amountCls}>{fmt(row.periodCredit)}</td>
                                        <td className={amountCls}>{fmt(row.endingDebit)}</td>
                                        <td className={amountCls}>{fmt(row.endingCredit)}</td>
                                    </tr>
                                ))}
                                <tr className="bg-muted/50 font-medium">
                                    <td className={cellCls} colSpan={2}>合计</td>
                                    <td className={amountCls}>{fmt(totals.openingDebit, true)}</td>
                                    <td className={amountCls}>{fmt(totals.openingCredit, true)}</td>
                                    <td className={amountCls}>{fmt(totals.periodDebit, true)}</td>
                                    <td className={amountCls}>{fmt(totals.periodCredit, true)}</td>
                                    <td className={amountCls}>{fmt(totals.endingDebit, true)}</td>
                                    <td className={amountCls}>{fmt(totals.endingCredit, true)}</td>
                                </tr>
                            </tbody>
                        </table>
                    </div>

                    <div className={cn("text-sm", balanced ? "text-muted-foreground" : "text-destructive")}>
                        {balanced ? "试算平衡：借方合计 = 贷方合计" : "试算不平衡，请核对数据"}
                    </div>
                </CardContent>
            </Card>
        </div>
    )
}
