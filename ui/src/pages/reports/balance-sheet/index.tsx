import { useCallback, useEffect, useMemo, useState } from "react"
import { RefreshCw } from "lucide-react"
import dayjs from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type BalanceSheetLineDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { PeriodField } from "@/components/fields/date-fields"
import { Alert, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Spinner } from "@/components/ui/spinner"

type RowKind = "section" | "item" | "total"

/** 选定的会计期间。 */
interface Period {
    year: number
    month: number
}

/** 报表一行。section 为分类小标题（如“流动资产”），total 为合计行（加粗）。 */
interface SheetLine {
    label: string
    line?: number
    ending: number
    opening: number
    kind: RowKind
}

/** 会企01表明细行定义：名称与报表行次；金额由后端按行次填入。 */
interface LineDef {
    label: string
    line: number
}

const def = (label: string, line: number): LineDef => ({ label, line })

// ============ 会企01表标准行次模板（金额来自后端，按行次归集） ============
const CURRENT_ASSETS: LineDef[] = [
    def("货币资金", 1),
    def("交易性金融资产", 2),
    def("应收票据", 3),
    def("应收账款", 4),
    def("预付款项", 5),
    def("其他应收款", 6),
    def("存货", 7),
    def("一年内到期的非流动资产", 8),
    def("其他流动资产", 9),
]
const NON_CURRENT_ASSETS: LineDef[] = [
    def("长期股权投资", 11),
    def("固定资产", 12),
    def("在建工程", 13),
    def("无形资产", 14),
    def("长期待摊费用", 15),
    def("递延所得税资产", 16),
    def("其他非流动资产", 17),
]
const CURRENT_LIABILITIES: LineDef[] = [
    def("短期借款", 31),
    def("应付票据", 32),
    def("应付账款", 33),
    def("预收款项", 34),
    def("应付职工薪酬", 35),
    def("应交税费", 36),
    def("其他应付款", 37),
    def("一年内到期的非流动负债", 38),
    def("其他流动负债", 39),
]
const NON_CURRENT_LIABILITIES: LineDef[] = [
    def("长期借款", 41),
    def("应付债券", 42),
    def("长期应付款", 43),
    def("递延所得税负债", 44),
    def("其他非流动负债", 45),
]
const EQUITY: LineDef[] = [
    def("实收资本（或股本）", 51),
    def("资本公积", 52),
    def("盈余公积", 53),
    def("未分配利润", 54),
]

/** 行次 → 金额（期末/年初）。缺失的行次视为 0。 */
type AmountMap = Map<number, { ending: number; opening: number }>

function amountOf(map: AmountMap, line: number) {
    return map.get(line) ?? { ending: 0, opening: 0 }
}

function toItems(defs: LineDef[], map: AmountMap): SheetLine[] {
    return defs.map((d): SheetLine => ({ ...d, ...amountOf(map, d.line), kind: "item" }))
}

function sumEnding(items: SheetLine[]) {
    return items.reduce((s, i) => s + i.ending, 0)
}
function sumOpening(items: SheetLine[]) {
    return items.reduce((s, i) => s + i.opening, 0)
}

function fmt(value: number, force = false): string {
    if (value === 0 && !force) return ""
    return value.toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

/** 期间 → 当月 1 日的 Date，与 PeriodField 的落值形状一致。 */
function toPeriodDate(p: Period | null): Date | undefined {
    return p ? dayjs(`${p.year}-${String(p.month).padStart(2, "0")}-01`).toDate() : undefined
}

function buildAssetRows(map: AmountMap): SheetLine[] {
    const current = toItems(CURRENT_ASSETS, map)
    const nonCurrent = toItems(NON_CURRENT_ASSETS, map)
    const currentTotal = { ending: sumEnding(current), opening: sumOpening(current) }
    const nonCurrentTotal = { ending: sumEnding(nonCurrent), opening: sumOpening(nonCurrent) }
    return [
        { label: "流动资产：", ending: 0, opening: 0, kind: "section" },
        ...current,
        { label: "流动资产合计", line: 10, ...currentTotal, kind: "total" },
        { label: "非流动资产：", ending: 0, opening: 0, kind: "section" },
        ...nonCurrent,
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

function buildLiabilityRows(map: AmountMap): SheetLine[] {
    const curLiabItems = toItems(CURRENT_LIABILITIES, map)
    const nonCurLiabItems = toItems(NON_CURRENT_LIABILITIES, map)
    const equityItems = toItems(EQUITY, map)
    const curLiab = { ending: sumEnding(curLiabItems), opening: sumOpening(curLiabItems) }
    const nonCurLiab = { ending: sumEnding(nonCurLiabItems), opening: sumOpening(nonCurLiabItems) }
    const liabTotal = {
        ending: curLiab.ending + nonCurLiab.ending,
        opening: curLiab.opening + nonCurLiab.opening,
    }
    const equityTotal = { ending: sumEnding(equityItems), opening: sumOpening(equityItems) }
    return [
        { label: "流动负债：", ending: 0, opening: 0, kind: "section" },
        ...curLiabItems,
        { label: "流动负债合计", line: 40, ...curLiab, kind: "total" },
        { label: "非流动负债：", ending: 0, opening: 0, kind: "section" },
        ...nonCurLiabItems,
        { label: "非流动负债合计", line: 46, ...nonCurLiab, kind: "total" },
        { label: "负债合计", line: 47, ...liabTotal, kind: "total" },
        { label: "所有者权益（或股东权益）：", ending: 0, opening: 0, kind: "section" },
        ...equityItems,
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
    const { user, token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [period, setPeriod] = useState<Period | null>(null)
    const [lines, setLines] = useState<BalanceSheetLineDto[]>([])
    const [endingDate, setEndingDate] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [loaded, setLoaded] = useState(false)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new ReportsApi(apiConfig(token)).accountsetsAccountSetIdReportsBalanceSheetGet({
                accountSetId,
                year: period?.year,
                month: period?.month,
            })
            setLines(res.lines ?? [])
            setEndingDate(res.endingDate ?? "")
            setLoaded(true)
            // 首次未指定期间时，采用后端解析出的期间（最近有凭证的月份）。
            if (period === null && res.year && res.month) {
                setPeriod({ year: res.year, month: res.month })
            }
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资产负债表失败，请重试。"))
            setLines([])
            setLoaded(false)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, period])

    useEffect(() => {
        void load()
    }, [load])

    const { assetRows, liabilityRows, balanced } = useMemo(() => {
        const map: AmountMap = new Map()
        for (const l of lines) {
            if (l.line != null) {
                map.set(l.line, { ending: l.ending ?? 0, opening: l.opening ?? 0 })
            }
        }
        const assets = buildAssetRows(map)
        const liabilities = buildLiabilityRows(map)
        const assetTotal = assets[assets.length - 1]
        const liabTotal = liabilities[liabilities.length - 1]
        // 金额以分为单位比较，规避浮点误差。
        const eq = (a: number, b: number) => Math.round(a * 100) === Math.round(b * 100)
        return {
            assetRows: assets,
            liabilityRows: liabilities,
            balanced: eq(assetTotal.ending, liabTotal.ending) && eq(assetTotal.opening, liabTotal.opening),
        }
    }, [lines])

    const rowCount = Math.max(assetRows.length, liabilityRows.length)

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-lg font-semibold">资产负债表</h1>
                <span className="text-muted-foreground">
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    。按《企业会计准则》会企01表格式编制，金额由凭证实时汇总。
                </span>
            </div>

            <Card>
                <CardContent className="flex flex-col gap-4">
                    <div className="relative text-center">
                        <div className="text-lg font-bold">资产负债表</div>
                        <div className="absolute right-0 top-0 text-sm text-muted-foreground">会企01表</div>
                    </div>

                    <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
                        <div className="flex flex-wrap items-center gap-2">
                            <span>会计期间：</span>
                            <PeriodField
                                value={toPeriodDate(period)}
                                onChange={(d) => d && setPeriod({ year: dayjs(d).year(), month: dayjs(d).month() + 1 })}
                                disabled={!accountSetId}
                                allowClear={false}
                            />
                            <Button
                                variant="outline"
                                onClick={() => void load()}
                                disabled={loading || !accountSetId}
                            >
                                {loading ? (
                                    <Spinner data-icon="inline-start" />
                                ) : (
                                    <RefreshCw data-icon="inline-start" />
                                )}
                                刷新
                            </Button>
                        </div>
                        <span>编制单位：{user?.displayName ?? "——"}</span>
                        <span>{endingDate}</span>
                        <span>单位：元</span>
                    </div>

                    {error && (
                        <Alert variant="destructive">
                            <AlertTitle>{error}</AlertTitle>
                        </Alert>
                    )}

                    {loading && !loaded ? (
                        <div className="flex items-center justify-center py-16">
                            <Spinner />
                        </div>
                    ) : !accountSetId ? (
                        <div className="py-16 text-center text-sm text-muted-foreground">请先选择账套。</div>
                    ) : (
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
                    )}

                    {loaded && !loading && (
                        <span className={cn(balanced ? "text-muted-foreground" : "text-destructive")}>
                            {balanced ? "资产总计 = 负债和所有者权益总计" : "试算不平衡，请核对数据"}
                        </span>
                    )}
                </CardContent>
            </Card>
        </div>
    )
}
