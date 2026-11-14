import { useCallback, useEffect, useMemo, useState } from "react"
import { useNavigate, useParams } from "react-router"
import { DownloadOutlined, PrinterOutlined, ReloadOutlined } from "@ant-design/icons";
import { Button, Table, Tooltip } from "antd"
import type { TableColumnsType } from "antd"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAccountingPeriodBounds } from "@/accountSets/useCurrentAccountSet"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type IncomeStatementLineDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { printPdf } from "@/lib/print-pdf"
import { ReportPageShell } from "@/components/report/ReportPageShell"
import { ReportPeriodToolbar } from "@/components/report/ReportPeriodToolbar"
import { ReportDrilldownDrawer } from "@/components/report/ReportDrilldownDrawer"
import { downloadReportExport } from "@/components/report/report-export"
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
    def("其中：利息费用（收入以“-”号填列）", 33, { indent: 2 }),
    def("资产减值损失", 7, { subtract: true, indent: 1 }),
    def("加：公允价值变动收益（损失以“－”号填列）", 8, { indent: 1 }),
    def("投资收益（损失以“－”号填列）", 9, { indent: 1 }),
    def("其中：对联营企业和合营企业的投资收益", 10, { indent: 2 }),
]
/** 参与营业利润合计的行次（排除「其中」行 10、33）。 */
const OPERATING_SUM_ITEMS = OPERATING_ITEMS.filter((d) => d.line !== 10 && d.line !== 33)

// 利润总额 = 营业利润 + 营业外收入 − 营业外支出（13/15 为「其中」行，不参与合计）
const PROFIT_ADJUST: LineDef[] = [
    def("加：营业外收入", 12, { indent: 1 }),
    def("其中：政府补助", 34, { indent: 2 }),
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

/**
 * 小企业会计准则（会小企02表）利润表行序列：仅营业收入、营业成本、税金及附加、三项费用、
 * 投资收益与营业外收支、所得税、净利润；不含「其中」子项、资产减值损失、公允价值变动收益、
 * 其他综合收益与每股收益。营业利润 = 营业收入 − 成本 − 税金及附加 − 销售费用 − 管理费用 − 财务费用 + 投资收益；
 * 行次沿用小企业会计准则编号（1-13），与柠檬云小企业准则版式一致。
 */
function buildSmallBusinessRows(map: AmountMap): StatementLine[] {
    const get = (line: number) => amountOf(map, line)
    const rev = get(1)
    const cost = get(2)
    const tax = get(3)
    const sell = get(4)
    const admin = get(5)
    const fin = get(6)
    const invest = get(9)
    const opCur = rev.current - cost.current - tax.current - sell.current - admin.current - fin.current + invest.current
    const opYtd = rev.yearToDate - cost.yearToDate - tax.yearToDate - sell.yearToDate - admin.yearToDate - fin.yearToDate + invest.yearToDate

    const nonOpInc = get(12)
    const nonOpExp = get(14)
    const profitCur = opCur + nonOpInc.current - nonOpExp.current
    const profitYtd = opYtd + nonOpInc.yearToDate - nonOpExp.yearToDate

    const taxExpense = get(17)
    const netCur = profitCur - taxExpense.current
    const netYtd = profitYtd - taxExpense.yearToDate

    return [
        { label: "一、营业收入", line: 1, ...rev, kind: "item" },
        { label: "减：营业成本", line: 2, ...cost, kind: "item", indent: 1 },
        { label: "税金及附加", line: 3, ...tax, kind: "item", indent: 1 },
        { label: "销售费用", line: 4, ...sell, kind: "item", indent: 1 },
        { label: "管理费用", line: 5, ...admin, kind: "item", indent: 1 },
        { label: "财务费用", line: 6, ...fin, kind: "item", indent: 1 },
        { label: "加：投资收益（损失以“-”号填列）", line: 7, ...invest, kind: "item", indent: 1 },
        { label: "二、营业利润（亏损以“－”号填列）", line: 8, current: opCur, yearToDate: opYtd, kind: "total" },
        { label: "加：营业外收入", line: 9, ...nonOpInc, kind: "item", indent: 1 },
        { label: "减：营业外支出", line: 10, ...nonOpExp, kind: "item", indent: 1 },
        { label: "三、利润总额（亏损总额以“－”号填列）", line: 11, current: profitCur, yearToDate: profitYtd, kind: "total" },
        { label: "减：所得税费用", line: 12, ...taxExpense, kind: "item", indent: 1 },
        { label: "四、净利润（净亏损以“－”号填列）", line: 13, current: netCur, yearToDate: netYtd, kind: "total" },
    ]
}

/** 企业会计准则（会企02表）利润表行序列：1-32 行，含「其中」子项、其他综合收益与每股收益。 */
function buildEnterpriseRows(map: AmountMap): StatementLine[] {
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

    return [
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
}

/**
 * 依据账套会计标准选择利润表版式：小企业会计准则（枚举值 2）→ 简洁版（会小企02表）；其余 → 完整会企02表。
 * 账套列表异步加载，标准未就绪时返回空表，避免小准则账套首屏先闪现 32 行会企02表版式。
 */
function buildRows(map: AmountMap, standard?: number): StatementLine[] {
    if (standard == null) return []
    return standard === 2 ? buildSmallBusinessRows(map) : buildEnterpriseRows(map)
}

/**
 * 明细项缩进：antd 的 .ant-table-cell 内边距压过 Tailwind 的 pl-*，故缩进做在单元格内的 span 上。
 * 每级 16px（会企02表模板目前仅 0/1 两级）。
 */
function indentCls(indent?: number): string {
    if (!indent) return ""
    return indent >= 2 ? "ml-8" : "ml-4"
}

/** 合计行加粗并浅底，与明细行区分；此前两者同为常规字重，金额一眼扫不出来。 */
function cellCls(row: StatementLine): string {
    return row.kind === "total" ? "bg-(--table-zebra-bg) font-semibold text-(--foreground)" : ""
}

/** 会企02表四列：项目、行次、本年累计金额、本期金额（列序与柠檬云一致）。 */
function buildColumns(isDrillable: (row: StatementLine) => boolean): TableColumnsType<StatementLine> {
    return [
        {
            title: "项目",
            onCell: (row) => ({ className: cellCls(row) }),
            render: (_, row) =>
                row.indent ? (
                    <span
                        className={[
                            indentCls(row.indent),
                            isDrillable(row) ? "cursor-pointer hover:text-(--brand) hover:underline" : "",
                        ].join(" ")}
                    >
                        {row.label}
                    </span>
                ) : isDrillable(row) ? (
                    <span className="cursor-pointer hover:text-(--brand) hover:underline">{row.label}</span>
                ) : (
                    row.label
                ),
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
}

export default function IncomeStatementPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const routeAccountSetId = useParams<{ accountSetId: string }>().accountSetId
    const navigate = useNavigate()
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const periodBounds = useAccountingPeriodBounds()

    const [period, setPeriod] = useState<Period | null>(null)
    const [lines, setLines] = useState<IncomeStatementLineDto[]>([])
    const [periodText, setPeriodText] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [loaded, setLoaded] = useState(false)
    const [printing, setPrinting] = useState(false)
    const [drill, setDrill] = useState<StatementLine | null>(null)

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
        return buildRows(map, currentSet?.standard)
    }, [lines, currentSet?.standard])

    // 只有后端真的给出发生额的行次才能下钻（合计行由前端汇总，「其中」子项无数据时不可点）。
    const drillableLines = useMemo(
        () => new Set(lines.filter((l) => l.line != null).map((l) => l.line!)),
        [lines],
    )
    const isDrillable = useCallback(
        (row: StatementLine) => row.kind === "item" && row.line != null && drillableLines.has(row.line),
        [drillableLines],
    )
    const columns = useMemo(() => buildColumns(isDrillable), [isDrillable])

    /** 从下钻浮层进入科目明细账（明细账行再点开即凭证详情），沿用报表所选月份。 */
    const openSubsidiary = useCallback(
        (code: string) => {
            if (!routeAccountSetId || !period) return
            const mm = String(period.month).padStart(2, "0")
            const lastDay = new Date(period.year, period.month, 0).getDate()
            const params = new URLSearchParams({
                account: code,
                startDate: `${period.year}-${mm}-01`,
                endDate: `${period.year}-${mm}-${String(lastDay).padStart(2, "0")}`,
            })
            setDrill(null)
            navigate(`/${routeAccountSetId}/ledger/subsidiary?${params.toString()}`)
        },
        [routeAccountSetId, period, navigate],
    )

    /** 导出当前期间的利润表（后端生成 .xlsx）。 */
    const exportSheet = useCallback(async () => {
        if (!token || !accountSetId || !period) return
        await downloadReportExport(
            `/api/accountsets/${accountSetId}/reports/export?kind=income-statement&year=${period.year}&month=${period.month}`,
            token,
            `利润表_${period.year}年${String(period.month).padStart(2, "0")}月.xlsx`,
        )
    }, [token, accountSetId, period])

    /** 打印：后端按会企02表版式渲染 PDF，在新窗口预览（浏览器打印的页眉页脚不再出现）。 */
    const printSheet = useCallback(async () => {
        if (!token || !accountSetId || !period || printing) return
        setPrinting(true)
        try {
            await printPdf(
                `/api/accountsets/${accountSetId}/reports/pdf?kind=income-statement&year=${period.year}&month=${period.month}`,
                token,
            )
        } catch (err) {
            setError(await apiErrorMessage(err, "生成打印稿失败，请重试。"))
        } finally {
            setPrinting(false)
        }
    }, [token, accountSetId, period, printing])

    return (
        <ReportPageShell
            toolbar={
                <ReportPeriodToolbar
                    value={period}
                    min={periodBounds.min}
                    max={periodBounds.max}
                    onChange={setPeriod}
                    loading={loading && !loaded}
                    disabled={!accountSetId}
                    actions={
                        <>
                            <Button
                                icon={<DownloadOutlined />}
                                onClick={() => void exportSheet()}
                                disabled={!accountSetId || loading}
                            >
                                导出
                            </Button>
                            <Button
                                icon={<PrinterOutlined />}
                                loading={printing}
                                onClick={() => void printSheet()}
                                disabled={!accountSetId || !period}
                            >
                                打印
                            </Button>
                            <Tooltip title="刷新">
                                <Button
                                    type="text"
                                    shape="circle"
                                    aria-label="刷新"
                                    icon={<ReloadOutlined spin={loading} />}
                                    onClick={() => void load()}
                                    disabled={loading || !accountSetId}
                                />
                            </Tooltip>
                        </>
                    }
                    meta={
                        <>
                            <span>编制单位：{currentSet?.companyName ?? "——"}</span>
                            <span>{currentSet?.standard == null ? "——" : currentSet.standard === 2 ? "会小企02表" : "会企02表"}</span>
                            <span>{periodText}</span>
                            <span>单位：元</span>
                        </>
                    }
                />
            }
            loading={loading && !loaded}
            error={error}
            placeholder={!accountSetId ? "请先选择账套。" : undefined}
        >
            <Table<StatementLine>
                columns={columns}
                dataSource={rows}
                rowKey={(_, i) => String(i)}
                pagination={false}
                scroll={{ x: 560 }}
                onRow={(row) =>
                    isDrillable(row)
                        ? { onClick: () => setDrill(row), className: "cursor-pointer" }
                        : {}
                }
            />

            {period && (
                <ReportDrilldownDrawer
                    open={drill != null}
                    onClose={() => setDrill(null)}
                    accountSetId={accountSetId}
                    token={token}
                    kind="income-statement"
                    year={period.year}
                    month={period.month}
                    line={drill?.line ?? null}
                    title={drill ? `科目明细 · ${drill.label}` : "科目明细"}
                    currentLabel="本期金额"
                    yearToDateLabel="本年累计"
                    onOpenLedger={openSubsidiary}
                />
            )}
        </ReportPageShell>
    )
}
