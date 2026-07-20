import { useMemo } from "react"

import { useAuth } from "@/auth/AuthContext"
import { Card, CardContent } from "@/components/ui/card"
import { cn } from "@/lib/utils"

type RowKind = "section" | "item" | "total"

/** 报表一行。section 为分类小标题（如“流动资产”），total 为合计行（加粗）。 */
interface SheetLine {
    label: string
    line?: number
    ending: number
    opening: number
    kind: RowKind
}

/** 数值型明细：仅需名称、行次与金额。 */
interface Leaf {
    label: string
    line: number
    ending: number
    opening: number
}

const num = (label: string, line: number, ending = 0, opening = 0): Leaf => ({
    label,
    line,
    ending,
    opening,
})

const sumEnding = (items: Leaf[]) => items.reduce((s, i) => s + i.ending, 0)
const sumOpening = (items: Leaf[]) => items.reduce((s, i) => s + i.opening, 0)

// ============ 资产方（示例数据，后续接入报表接口后替换） ============
const CURRENT_ASSETS: Leaf[] = [
    num("货币资金", 1, 152000, 120000),
    num("交易性金融资产", 2),
    num("应收票据", 3),
    num("应收账款", 4, 68000, 54000),
    num("预付款项", 5, 12000, 8000),
    num("其他应收款", 6),
    num("存货", 7, 96000, 88000),
    num("一年内到期的非流动资产", 8),
    num("其他流动资产", 9),
]
const NON_CURRENT_ASSETS: Leaf[] = [
    num("长期股权投资", 11),
    num("固定资产", 12, 320000, 340000),
    num("在建工程", 13),
    num("无形资产", 14),
    num("长期待摊费用", 15),
    num("递延所得税资产", 16),
    num("其他非流动资产", 17),
]

// ============ 负债与所有者权益方 ============
const CURRENT_LIABILITIES: Leaf[] = [
    num("短期借款", 31),
    num("应付票据", 32),
    num("应付账款", 33, 84000, 72000),
    num("预收款项", 34, 26000, 18000),
    num("应付职工薪酬", 35),
    num("应交税费", 36, 18000, 15000),
    num("其他应付款", 37),
    num("一年内到期的非流动负债", 38),
    num("其他流动负债", 39),
]
const NON_CURRENT_LIABILITIES: Leaf[] = [
    num("长期借款", 41),
    num("应付债券", 42),
    num("长期应付款", 43),
    num("递延所得税负债", 44),
    num("其他非流动负债", 45),
]
const EQUITY: Leaf[] = [
    num("实收资本（或股本）", 51, 400000, 400000),
    num("资本公积", 52),
    num("盈余公积", 53),
    num("未分配利润", 54, 120000, 105000),
]

function fmt(value: number, force = false): string {
    if (value === 0 && !force) return ""
    return value.toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

function buildAssetRows(): SheetLine[] {
    const currentTotal = { ending: sumEnding(CURRENT_ASSETS), opening: sumOpening(CURRENT_ASSETS) }
    const nonCurrentTotal = {
        ending: sumEnding(NON_CURRENT_ASSETS),
        opening: sumOpening(NON_CURRENT_ASSETS),
    }
    return [
        { label: "流动资产：", ending: 0, opening: 0, kind: "section" },
        ...CURRENT_ASSETS.map((i): SheetLine => ({ ...i, kind: "item" })),
        { label: "流动资产合计", line: 10, ...currentTotal, kind: "total" },
        { label: "非流动资产：", ending: 0, opening: 0, kind: "section" },
        ...NON_CURRENT_ASSETS.map((i): SheetLine => ({ ...i, kind: "item" })),
        { label: "非流动资产合计", line: 18, ...nonCurrentTotal, kind: "total" },
        {
            label: "资产总计",
            line: 19,
            ending: currentTotal.ending + nonCurrentTotal.ending,
            opening: currentTotal.opening + nonCurrentTotal.opening,
            kind: "total",
        },
    ]
}

function buildLiabilityRows(): SheetLine[] {
    const curLiab = {
        ending: sumEnding(CURRENT_LIABILITIES),
        opening: sumOpening(CURRENT_LIABILITIES),
    }
    const nonCurLiab = {
        ending: sumEnding(NON_CURRENT_LIABILITIES),
        opening: sumOpening(NON_CURRENT_LIABILITIES),
    }
    const liabTotal = {
        ending: curLiab.ending + nonCurLiab.ending,
        opening: curLiab.opening + nonCurLiab.opening,
    }
    const equityTotal = { ending: sumEnding(EQUITY), opening: sumOpening(EQUITY) }
    return [
        { label: "流动负债：", ending: 0, opening: 0, kind: "section" },
        ...CURRENT_LIABILITIES.map((i): SheetLine => ({ ...i, kind: "item" })),
        { label: "流动负债合计", line: 40, ...curLiab, kind: "total" },
        { label: "非流动负债：", ending: 0, opening: 0, kind: "section" },
        ...NON_CURRENT_LIABILITIES.map((i): SheetLine => ({ ...i, kind: "item" })),
        { label: "非流动负债合计", line: 46, ...nonCurLiab, kind: "total" },
        { label: "负债合计", line: 47, ...liabTotal, kind: "total" },
        { label: "所有者权益（或股东权益）：", ending: 0, opening: 0, kind: "section" },
        ...EQUITY.map((i): SheetLine => ({ ...i, kind: "item" })),
        { label: "所有者权益（或股东权益）合计", line: 55, ...equityTotal, kind: "total" },
        {
            label: "负债和所有者权益（或股东权益）总计",
            line: 56,
            ending: liabTotal.ending + equityTotal.ending,
            opening: liabTotal.opening + equityTotal.opening,
            kind: "total",
        },
    ]
}

const cellCls = "border border-border px-2 py-1 align-middle"
const amountCls = cn(cellCls, "text-right tabular-nums")

/** 渲染半张表（资产 / 负债及权益）的一行的 4 个单元格，空行时输出占位单元格。 */
function HalfRow({ row }: { row?: SheetLine }) {
    if (!row) {
        return (
            <>
                <td className={cellCls} />
                <td className={cellCls} />
                <td className={amountCls} />
                <td className={amountCls} />
            </>
        )
    }
    if (row.kind === "section") {
        return (
            <>
                <td className={cn(cellCls, "font-medium")}>{row.label}</td>
                <td className={cellCls} />
                <td className={amountCls} />
                <td className={amountCls} />
            </>
        )
    }
    const isTotal = row.kind === "total"
    return (
        <>
            <td className={cn(cellCls, isTotal ? "font-medium" : "pl-6")}>{row.label}</td>
            <td className={cn(cellCls, "text-center tabular-nums text-muted-foreground")}>{row.line}</td>
            <td className={cn(amountCls, isTotal && "font-medium")}>{fmt(row.ending, isTotal)}</td>
            <td className={cn(amountCls, isTotal && "font-medium")}>{fmt(row.opening, isTotal)}</td>
        </>
    )
}

export default function BalanceSheetPage() {
    const { user } = useAuth()
    const { assetRows, liabilityRows, balanced } = useMemo(() => {
        const assets = buildAssetRows()
        const liabilities = buildLiabilityRows()
        const assetTotal = assets[assets.length - 1]
        const liabTotal = liabilities[liabilities.length - 1]
        return {
            assetRows: assets,
            liabilityRows: liabilities,
            balanced:
                assetTotal.ending === liabTotal.ending && assetTotal.opening === liabTotal.opening,
        }
    }, [])

    const rowCount = Math.max(assetRows.length, liabilityRows.length)
    const period = "2026年07月31日"

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-2xl font-bold">资产负债表</h1>
                <p className="text-sm text-muted-foreground">
                    按《企业会计准则》会企01表格式编制。
                </p>
            </div>

            <Card>
                <CardContent className="flex flex-col gap-3 py-6">
                    <div className="relative text-center">
                        <div className="text-lg font-bold">资产负债表</div>
                        <div className="absolute right-0 top-0 text-sm text-muted-foreground">
                            会企01表
                        </div>
                    </div>
                    <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
                        <span>编制单位：{user?.displayName ?? "——"}</span>
                        <span>{period}</span>
                        <span>单位：元</span>
                    </div>

                    <div className="overflow-x-auto">
                        <table className="w-full min-w-[900px] border-collapse text-sm">
                            <colgroup>
                                <col />
                                <col style={{ width: 48 }} />
                                <col style={{ width: 130 }} />
                                <col style={{ width: 130 }} />
                                <col />
                                <col style={{ width: 48 }} />
                                <col style={{ width: 130 }} />
                                <col style={{ width: 130 }} />
                            </colgroup>
                            <thead>
                                <tr className="bg-muted/50 text-muted-foreground">
                                    <th className={cn(cellCls, "text-left font-medium")}>资产</th>
                                    <th className={cn(cellCls, "text-center font-medium")}>行次</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>期末余额</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>年初余额</th>
                                    <th className={cn(cellCls, "text-left font-medium")}>
                                        负债和所有者权益（或股东权益）
                                    </th>
                                    <th className={cn(cellCls, "text-center font-medium")}>行次</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>期末余额</th>
                                    <th className={cn(cellCls, "text-right font-medium")}>年初余额</th>
                                </tr>
                            </thead>
                            <tbody>
                                {Array.from({ length: rowCount }).map((_, i) => (
                                    <tr key={i}>
                                        <HalfRow row={assetRows[i]} />
                                        <HalfRow row={liabilityRows[i]} />
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    <div className={cn("text-sm", balanced ? "text-muted-foreground" : "text-destructive")}>
                        {balanced ? "资产总计 = 负债和所有者权益总计" : "试算不平衡，请核对数据"}
                    </div>
                </CardContent>
            </Card>
        </div>
    )
}
