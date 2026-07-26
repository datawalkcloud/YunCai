import { useCallback, useEffect, useMemo, useState } from "react"
import { ReloadOutlined } from "@ant-design/icons"
import { Alert, Button, Card, DatePicker, Space, Spin, Typography } from "antd"
import dayjs, { type Dayjs } from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type IncomeStatementLineDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"

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

function toDayjs(p: Period | null): Dayjs | null {
    return p ? dayjs(`${p.year}-${String(p.month).padStart(2, "0")}-01`) : null
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

export default function IncomeStatementPage() {
    const { user, token } = useAuth()
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
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
            <div>
                <Typography.Title level={3} style={{ marginBottom: 4 }}>利润表</Typography.Title>
                <Typography.Text type="secondary">
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    。按《企业会计准则》会企02表格式编制，金额由凭证实时汇总。
                </Typography.Text>
            </div>

            <Card>
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    <div className="relative text-center">
                        <div className="text-lg font-bold">利润表</div>
                        <div className="absolute right-0 top-0 text-sm text-muted-foreground">会企02表</div>
                    </div>

                    <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
                        <Space wrap>
                            <span>会计期间：</span>
                            <DatePicker
                                picker="month"
                                value={toDayjs(period)}
                                onChange={(d) => d && setPeriod({ year: d.year(), month: d.month() + 1 })}
                                disabled={!accountSetId}
                                allowClear={false}
                            />
                            <Button
                                icon={<ReloadOutlined spin={loading} />}
                                onClick={() => void load()}
                                disabled={loading || !accountSetId}
                            >
                                刷新
                            </Button>
                        </Space>
                        <span>编制单位：{user?.displayName ?? "——"}</span>
                        <span>{periodText}</span>
                        <span>单位：元</span>
                    </div>

                    {error && <Alert type="error" message={error} showIcon />}

                    {loading && !loaded ? (
                        <div className="flex items-center justify-center py-16">
                            <Spin />
                        </div>
                    ) : !accountSetId ? (
                        <div className="py-16 text-center text-sm text-muted-foreground">请先选择账套。</div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="w-full min-w-[560px] border-collapse text-sm">
                                <colgroup>
                                    <col />
                                    <col style={{ width: 64 }} />
                                    <col style={{ width: 160 }} />
                                    <col style={{ width: 160 }} />
                                </colgroup>
                                <thead>
                                    <tr className="bg-muted/50 text-muted-foreground">
                                        <th className={cn(cellCls, "text-left font-medium")}>项目</th>
                                        <th className={cn(cellCls, "text-center font-medium")}>行次</th>
                                        <th className={cn(cellCls, "text-right font-medium")}>本期金额</th>
                                        <th className={cn(cellCls, "text-right font-medium")}>本年累计金额</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {rows.map((row, i) => {
                                        const isTotal = row.kind === "total"
                                        const indent = (row as StatementLine & { indent?: number }).indent ?? 0
                                        return (
                                            <tr key={i}>
                                                <td
                                                    className={cn(cellCls, isTotal ? "font-medium" : undefined)}
                                                    style={{ paddingLeft: 12 + indent * 16 }}
                                                >
                                                    {row.label}
                                                </td>
                                                <td className={cn(cellCls, "text-center tabular-nums text-muted-foreground")}>
                                                    {row.line}
                                                </td>
                                                <td className={cn(amountCls, isTotal && "font-medium")}>
                                                    {fmt(row.current, isTotal)}
                                                </td>
                                                <td className={cn(amountCls, isTotal && "font-medium")}>
                                                    {fmt(row.yearToDate, isTotal)}
                                                </td>
                                            </tr>
                                        )
                                    })}
                                </tbody>
                            </table>
                        </div>
                    )}
                </Space>
            </Card>
        </Space>
    )
}
