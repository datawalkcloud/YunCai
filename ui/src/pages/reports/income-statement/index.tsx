import { useCallback, useEffect, useMemo, useState } from "react"
import { ReloadOutlined } from "@ant-design/icons";
import { Button, Table } from "antd"
import type { TableColumnsType } from "antd"
import dayjs from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type IncomeStatementLineDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { PeriodField } from "@/components/fields/date-fields"
import { ReportPageShell } from "@/components/report/ReportPageShell"
import { AMOUNT_COL } from "@/components/page/styles"

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
    /** 缩进层级，来自行次模板定义。 */
    indent?: number
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

// ============ 会企02表标准行次模板（企业会计准则 2007 版式，与柠檬云一致）============
// 营业利润明细项（行次 1-10，其中 10 为投资收益的「其中」行，不参与合计）
const OPERATING_ITEMS: LineDef[] = [
    def("一、营业收入", 1),
    def("减：营业成本", 2, { subtract: true, indent: 1 }),
    def("税金及附加", 3, { subtract: true, indent: 1 }),
    def("销售费用", 4, { subtract: true, indent: 1 }),
    def("管理费用", 5, { subtract: true, indent: 1 }),
    def("财务费用", 6, { subtract: true, indent: 1 }),
    def("资产减值损失", 7, { subtract: true, indent: 1 }),
    def("加：公允价值变动收益（损失以“－”号填列）", 8, { indent: 1 }),
    def("投资收益（损失以“－”号填列）", 9, { indent: 1 }),
    def("其中：对联营企业和合营企业的投资收益", 10, { indent: 2 }),
]
/** 参与营业利润合计的行次（排除「其中」行 10）。 */
const OPERATING_SUM_ITEMS = OPERATING_ITEMS.filter((d) => d.line !== 10)

// 利润总额 = 营业利润 + 营业外收入 − 营业外支出（13/15 为「其中」行，不参与合计）
const PROFIT_ADJUST: LineDef[] = [
    def("加：营业外收入", 12, { indent: 1 }),
    def("其中：非流动资产处置利得", 13, { indent: 2 }),
    def("减：营业外支出", 14, { subtract: true, indent: 1 }),
    def("其中：非流动资产处置损失", 15, { indent: 2 }),
]
const PROFIT_ADJUST_SUM_ITEMS = PROFIT_ADJUST.filter((d) => d.line === 12 || d.line === 14)

const TAX_ITEM: LineDef = def("减：所得税费用", 17, { subtract: true, indent: 1 })

// 其他综合收益（19-28）与每股收益（30-32）：当前账套无对应科目，按 0 列示，保持与柠檬云行数一致
const OTHER_COMPREHENSIVE_ITEMS: LineDef[] = [
    def("五、其他综合收益的税后净额", 19),
    def("(一)以后不能重分类进损益的其他综合收益", 20, { indent: 1 }),
    def("1.重新计量设定受益计划净负债或净资产的变动", 21, { indent: 2 }),
    def("2.权益法下在被投资单位不能重分类进损益的其他综合收益中享有的份额", 22, { indent: 2 }),
    def("(二)以后将重分类进损益的其他综合收益", 23, { indent: 1 }),
    def("1.权益法下在被投资单位以后将重分类进损益的其他综合收益中享有的份额", 24, { indent: 2 }),
    def("2.可供出售金融资产公允价值变动损益", 25, { indent: 2 }),
    def("3.持有至到期投资重分类可供出售金融资产损益", 26, { indent: 2 }),
    def("4.现金流量套期损益的有效部分", 27, { indent: 2 }),
    def("5.外币财务报表折算差额", 28, { indent: 2 }),
]
const EPS_ITEMS: LineDef[] = [
    def("七、每股收益：", 30),
    def("(一) 基本每股收益", 31, { indent: 1 }),
    def("(二) 稀释每股收益", 32, { indent: 1 }),
]

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

/** 依据后端行次金额，构建完整的利润表行序列（行次 1-32，与柠檬云企业会计准则 2007 版式一致）。 */
function buildRows(map: AmountMap): { rows: StatementLine[]; net: { current: number; yearToDate: number } } {
    const operating = toItems(OPERATING_ITEMS, map)
    const operatingProfit = {
        current: netSum(OPERATING_SUM_ITEMS, map, "current"),
        yearToDate: netSum(OPERATING_SUM_ITEMS, map, "yearToDate"),
    }
    const adjust = toItems(PROFIT_ADJUST, map)
    const totalProfit = {
        current: operatingProfit.current + netSum(PROFIT_ADJUST_SUM_ITEMS, map, "current"),
        yearToDate: operatingProfit.yearToDate + netSum(PROFIT_ADJUST_SUM_ITEMS, map, "yearToDate"),
    }
    const tax = { ...TAX_ITEM, ...amountOf(map, TAX_ITEM.line), kind: "item" as const }
    const net = {
        current: totalProfit.current - amountOf(map, TAX_ITEM.line).current,
        yearToDate: totalProfit.yearToDate - amountOf(map, TAX_ITEM.line).yearToDate,
    }
    const otherComprehensive = netSum(OTHER_COMPREHENSIVE_ITEMS.filter((d) => d.line === 19), map, "current")
    const otherComprehensiveYtd = netSum(OTHER_COMPREHENSIVE_ITEMS.filter((d) => d.line === 19), map, "yearToDate")
    const comprehensive = {
        current: net.current + otherComprehensive,
        yearToDate: net.yearToDate + otherComprehensiveYtd,
    }

    const rows: StatementLine[] = [
        ...operating,
        { label: "二、营业利润（亏损以“－”号填列）", line: 11, ...operatingProfit, kind: "total" },
        ...adjust,
        { label: "三、利润总额（亏损总额以“－”号填列）", line: 16, ...totalProfit, kind: "total" },
        tax,
        { label: "四、净利润（净亏损以“－”号填列）", line: 18, ...net, kind: "total" },
        ...toItems(OTHER_COMPREHENSIVE_ITEMS, map),
        { label: "六、综合收益总额", line: 29, ...comprehensive, kind: "total" },
        ...toItems(EPS_ITEMS, map),
    ]
    return { rows, net }
}

/**
 * 明细项缩进：antd 的 .ant-table-cell 内边距压过 Tailwind 的 pl-*，故缩进做在单元格内的 span 上。
 * 每级 16px（会企02表模板目前仅 0/1 两级）。
 */
function indentCls(indent?: number): string {
    if (!indent) return ""
    return indent >= 2 ? "ml-8" : "ml-4"
}

/** 合计行加粗，明细行常规。 */
function cellCls(row: StatementLine): string {
    return row.kind === "total" ? "font-medium" : ""
}

/** 会企02表四列：项目、行次、本年累计金额、本期金额（列序与柠檬云一致）。 */
const COLUMNS: TableColumnsType<StatementLine> = [
    {
        title: "项目",
        onCell: (row) => ({ className: cellCls(row) }),
        render: (_, row) =>
            row.indent ? <span className={indentCls(row.indent)}>{row.label}</span> : row.label,
    },
    {
        title: "行次",
        dataIndex: "line",
        align: "center",
        width: 64,
        onCell: () => ({ className: "tabular-nums text-muted-foreground" }),
    },
    {
        title: "本年累计金额",
        width: 160,
        ...AMOUNT_COL,
        render: (_, row) => fmt(row.yearToDate, row.kind === "total"),
        onCell: (row) => ({ className: cellCls(row) }),
    },
    {
        title: "本期金额",
        width: 160,
        ...AMOUNT_COL,
        render: (_, row) => fmt(row.current, row.kind === "total"),
        onCell: (row) => ({ className: cellCls(row) }),
    },
]

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
            title="利润表"
            template="会企02表"
            period={
                <>
                    <span className="text-[12.5px] text-slate-500">会计期间：</span>
                    <PeriodField
                        value={toPeriodDate(period)}
                        onChange={(d) => d && setPeriod({ year: dayjs(d).year(), month: dayjs(d).month() + 1 })}
                        disabled={!accountSetId}
                        allowClear={false}
                    />
                </>
            }
            actions={
                <Button
                    icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                    onClick={() => void load()}
                    disabled={loading || !accountSetId}
                >
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
            <Table<StatementLine>
                columns={COLUMNS}
                dataSource={rows}
                rowKey={(_, i) => String(i)}
                pagination={false}
                scroll={{ x: 560 }}
            />
        </ReportPageShell>
    )
}
