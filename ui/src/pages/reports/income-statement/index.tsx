import { useCallback, useEffect, useMemo, useState } from "react"
import { RefreshCw, TrendingUp } from "lucide-react"
import dayjs from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type IncomeStatementLineDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { PeriodField } from "@/components/fields/date-fields"
import { ReportPageShell } from "@/components/report/ReportPageShell"
import { PAGE_BTN_GHOST, PERIOD_FIELD } from "@/components/page/styles"
import { Button } from "@/components/ui/button"
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table"
import { Spinner } from "@/components/ui/spinner"

type RowKind = "item" | "total"

/** 选定的会计期间。 */
interface Period {
    year: number
    month: number
}

/** 报表一行。total 为合计行（加粗）。 */
interface StatementLine {
    label: string
    line?: number
    current: number
    yearToDate: number
    kind: RowKind
}

/** 会企02表明细行定义：名称与报表行次；金额由后端按行次填入。 */
interface LineDef {
    label: string
    line: number
    /** 减项：参与合计时取负（如营业成本、各项费用、营业外支出、所得税费用）。 */
    subtract?: boolean
    /** 缩进层级，用于展示明细项的层次。 */
    indent?: number
}

const def = (label: string, line: number, opts?: { subtract?: boolean; indent?: number }): LineDef => ({
    label,
    line,
    ...opts,
})

// ============ 会企02表标准行次模板（金额来自后端，按行次归集） ============
// 营业利润明细项（营业收入 − 营业成本 − 税金及附加 − 各项费用 + 各类收益 − 各类减值损失）
const OPERATING_ITEMS: LineDef[] = [
    def("一、营业收入", 1),
    def("减：营业成本", 2, { subtract: true, indent: 1 }),
    def("税金及附加", 3, { subtract: true, indent: 1 }),
    def("销售费用", 4, { subtract: true, indent: 1 }),
    def("管理费用", 5, { subtract: true, indent: 1 }),
    def("研发费用", 6, { subtract: true, indent: 1 }),
    def("财务费用", 7, { subtract: true, indent: 1 }),
    def("加：其他收益", 8, { indent: 1 }),
    def("投资收益", 9, { indent: 1 }),
    def("公允价值变动收益", 11, { indent: 1 }),
    def("减：信用减值损失", 12, { subtract: true, indent: 1 }),
    def("资产减值损失", 13, { subtract: true, indent: 1 }),
    def("加：资产处置收益", 14, { indent: 1 }),
]
// 利润总额 = 营业利润 + 营业外收入 − 营业外支出
const PROFIT_ADJUST: LineDef[] = [
    def("加：营业外收入", 16, { indent: 1 }),
    def("减：营业外支出", 17, { subtract: true, indent: 1 }),
]
// 净利润 = 利润总额 − 所得税费用
const TAX_ITEM: LineDef = def("减：所得税费用", 19, { subtract: true, indent: 1 })

/** 行次 → 金额（本期/本年累计）。缺失的行次视为 0。 */
type AmountMap = Map<number, { current: number; yearToDate: number }>

function amountOf(map: AmountMap, line: number) {
    return map.get(line) ?? { current: 0, yearToDate: 0 }
}

function toItems(defs: LineDef[], map: AmountMap): StatementLine[] {
    return defs.map((d): StatementLine => ({ ...d, ...amountOf(map, d.line), kind: "item" }))
}

/** 按减项标记求净合计（减项取负）。 */
function netSum(defs: LineDef[], map: AmountMap, key: "current" | "yearToDate") {
    return defs.reduce((s, d) => {
        const v = amountOf(map, d.line)[key]
        return s + (d.subtract ? -v : v)
    }, 0)
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

/** 依据后端行次金额，构建完整的利润表行序列（含营业利润、利润总额、净利润三条合计行）。 */
function buildRows(map: AmountMap): { rows: StatementLine[]; net: { current: number; yearToDate: number } } {
    const operating = toItems(OPERATING_ITEMS, map)
    const operatingProfit = {
        current: netSum(OPERATING_ITEMS, map, "current"),
        yearToDate: netSum(OPERATING_ITEMS, map, "yearToDate"),
    }
    const adjust = toItems(PROFIT_ADJUST, map)
    const totalProfit = {
        current: operatingProfit.current + netSum(PROFIT_ADJUST, map, "current"),
        yearToDate: operatingProfit.yearToDate + netSum(PROFIT_ADJUST, map, "yearToDate"),
    }
    const tax = { ...TAX_ITEM, ...amountOf(map, TAX_ITEM.line), kind: "item" as const }
    const net = {
        current: totalProfit.current - amountOf(map, TAX_ITEM.line).current,
        yearToDate: totalProfit.yearToDate - amountOf(map, TAX_ITEM.line).yearToDate,
    }

    const rows: StatementLine[] = [
        ...operating,
        { label: "二、营业利润", line: 20, ...operatingProfit, kind: "total" },
        ...adjust,
        { label: "三、利润总额", line: 23, ...totalProfit, kind: "total" },
        tax,
        { label: "四、净利润", line: 31, ...net, kind: "total" },
    ]
    return { rows, net }
}

const cellCls = "border border-border px-3 py-1.5 align-middle"
const amountCls = cn(cellCls, "text-right tabular-nums")

/** 明细项缩进：单元格基础左内边距 12px，每级再加 16px（会企02表模板目前仅 0/1 两级）。 */
function indentCls(indent: number): string | undefined {
    if (indent <= 0) return undefined
    return indent >= 2 ? "pl-11" : "pl-7"
}

export default function IncomeStatementPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [period, setPeriod] = useState<Period | null>(null)
    const [lines, setLines] = useState<IncomeStatementLineDto[]>([])
    const [periodText, setPeriodText] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [loaded, setLoaded] = useState(false)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new ReportsApi(apiConfig(token)).accountsetsAccountSetIdReportsIncomeStatementGet({
                accountSetId,
                year: period?.year,
                month: period?.month,
            })
            setLines(res.lines ?? [])
            setPeriodText(res.periodText ?? "")
            setLoaded(true)
            // 首次未指定期间时，采用后端解析出的期间（最近有凭证的月份）。
            if (period === null && res.year && res.month) {
                setPeriod({ year: res.year, month: res.month })
            }
        } catch (err) {
            setError(await apiErrorMessage(err, "加载利润表失败，请重试。"))
            setLines([])
            setLoaded(false)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, period])

    useEffect(() => {
        void load()
    }, [load])

    const rows = useMemo(() => {
        const map: AmountMap = new Map()
        for (const l of lines) {
            if (l.line != null) {
                map.set(l.line, { current: l.current ?? 0, yearToDate: l.yearToDate ?? 0 })
            }
        }
        return buildRows(map).rows
    }, [lines])

    return (
        <ReportPageShell
            icon={TrendingUp}
            title="利润表"
            subtitle={
                <>
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    。按《企业会计准则》会企02表格式编制，金额由凭证实时汇总。
                </>
            }
            template="会企02表"
            period={
                <>
                    <span className="text-[12.5px] text-slate-500">会计期间：</span>
                    <PeriodField
                        className={PERIOD_FIELD}
                        value={toPeriodDate(period)}
                        onChange={(d) => d && setPeriod({ year: dayjs(d).year(), month: dayjs(d).month() + 1 })}
                        disabled={!accountSetId}
                        allowClear={false}
                    />
                </>
            }
            actions={
                <Button
                    variant="outline"
                    onClick={() => void load()}
                    disabled={loading || !accountSetId}
                    className={cn(PAGE_BTN_GHOST, "bg-white")}
                >
                    {loading ? (
                        <Spinner data-icon="inline-start" />
                    ) : (
                        <RefreshCw data-icon="inline-start" />
                    )}
                    刷新
                </Button>
            }
            meta={
                <>
                    <span>编制单位：{currentSet?.companyName ?? "——"}</span>
                    <span>{periodText}</span>
                    <span>单位：元</span>
                </>
            }
            loading={loading && !loaded}
            error={error}
            placeholder={!accountSetId ? "请先选择账套。" : undefined}
        >
            <Table className="min-w-[560px] border-collapse">
                <colgroup>
                    <col />
                    <col className="w-16" />
                    <col className="w-40" />
                    <col className="w-40" />
                </colgroup>
                <TableHeader>
                    <TableRow className="bg-muted hover:bg-muted">
                        <TableHead className={cn(cellCls, "text-left font-medium text-slate-500")}>项目</TableHead>
                        <TableHead className={cn(cellCls, "text-center font-medium text-slate-500")}>行次</TableHead>
                        <TableHead className={cn(cellCls, "text-right font-medium text-slate-500")}>本期金额</TableHead>
                        <TableHead className={cn(cellCls, "text-right font-medium text-slate-500")}>本年累计金额</TableHead>
                    </TableRow>
                </TableHeader>
                <TableBody>
                    {rows.map((row, i) => {
                        const isTotal = row.kind === "total"
                        const indent = (row as StatementLine & { indent?: number }).indent ?? 0
                        return (
                            <TableRow key={i} className="hover:bg-transparent">
                                <TableCell className={cn(cellCls, "whitespace-normal", indentCls(indent), isTotal && "font-medium")}>
                                    {row.label}
                                </TableCell>
                                <TableCell className={cn(cellCls, "text-center tabular-nums text-muted-foreground")}>
                                    {row.line}
                                </TableCell>
                                <TableCell className={cn(amountCls, isTotal && "font-medium")}>
                                    {fmt(row.current, isTotal)}
                                </TableCell>
                                <TableCell className={cn(amountCls, isTotal && "font-medium")}>
                                    {fmt(row.yearToDate, isTotal)}
                                </TableCell>
                            </TableRow>
                        )
                    })}
                </TableBody>
            </Table>
        </ReportPageShell>
    )
}
