import type { ReactNode } from "react"
import { useCallback, useEffect, useState } from "react"
import dayjs from "dayjs"
import {
    Alert,
    Button,
    Segmented,
    Table,
} from "antd"
import type { TableColumnsType } from "antd"
import {
    BarChartOutlined,
    BankOutlined,
    FundOutlined,
    MinusCircleOutlined,
    PlusCircleOutlined,
    ReloadOutlined,
    SwapOutlined,
    WalletOutlined,
} from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { FundReportsApi, type FundReportDto, type FundReportRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount, fmtMoney } from "@/lib/format"
import { cn } from "@/lib/utils"
import { PageHeader, SectionCard } from "@/components/page/section"
import { Tile, type TileTone } from "@/components/page/tile"
import { AMOUNT_COL } from "@/components/page/styles"
import { DateRangeField, parseDate } from "@/components/fields/date-fields"

/** 报表维度（与后端 FundReportDimensions 对齐）。 */
type Dimension = "account" | "category" | "counterparty"

/** 日期区间（YYYY-MM-DD 字符串，直接透传后端 DateOnly）。 */
interface Range {
    start: string
    end: string
}

const DIMENSION_LABEL: Record<Dimension, string> = {
    account: "账户",
    category: "收支类别",
    counterparty: "往来单位",
}

const DIMENSION_OPTIONS = (Object.keys(DIMENSION_LABEL) as Dimension[]).map((d) => ({
    value: d,
    label: DIMENSION_LABEL[d],
}))

export default function FundReportPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [dimension, setDimension] = useState<Dimension>("account")
    // 默认本月（月初 ~ 今天）。
    const [range, setRange] = useState<Range>(() => ({
        start: dayjs().startOf("month").format("YYYY-MM-DD"),
        end: dayjs().format("YYYY-MM-DD"),
    }))
    const [data, setData] = useState<FundReportDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new FundReportsApi(apiConfig(token)).accountsetsAccountSetIdFundReportsGet({
                accountSetId,
                dimension,
                startDate: new Date(range.start),
                endDate: new Date(range.end),
            })
            setData(res)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资金报表失败，请重试。"))
            setData(null)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, dimension, range])

    useEffect(() => {
        void load()
    }, [load])

    const rows: FundReportRowDto[] = data?.rows ?? []
    const total = data?.total
    const isAccount = dimension === "account"

    const tiles: {
        label: string
        value: string
        tone: TileTone
        icon: React.ComponentType<{ className?: string }>
        hint?: string
    }[] = isAccount
        ? [
            { label: "期初合计", value: fmtMoney(total?.openingBalance ?? 0), tone: "gray", icon: WalletOutlined },
            { label: "区间收入", value: fmtMoney(total?.totalIncome ?? 0), tone: "green", icon: PlusCircleOutlined },
            { label: "区间支出", value: fmtMoney(total?.totalExpense ?? 0), tone: "red", icon: MinusCircleOutlined },
            { label: "期末合计", value: fmtMoney(total?.balance ?? 0), tone: "blue", icon: BankOutlined },
        ]
        : [
            { label: "区间收入", value: fmtMoney(total?.totalIncome ?? 0), tone: "green", icon: PlusCircleOutlined },
            { label: "区间支出", value: fmtMoney(total?.totalExpense ?? 0), tone: "red", icon: MinusCircleOutlined },
            { label: "收支净额", value: fmtMoney(total?.balance ?? 0), tone: "blue", icon: SwapOutlined },
            {
                label: "变动笔数",
                value: `${(total?.incomeCount ?? 0) + (total?.expenseCount ?? 0)}`,
                tone: "gray",
                icon: BarChartOutlined,
                hint: `收 ${total?.incomeCount ?? 0} · 支 ${total?.expenseCount ?? 0}`,
            },
        ]

    const columns: TableColumnsType<FundReportRowDto> = [
        {
            title: DIMENSION_LABEL[dimension],
            render: (_, r) => <span className="truncate font-medium text-slate-700">{r.name}</span>,
        },
        ...(isAccount
            ? [
                  {
                      title: "对应科目",
                      className: "whitespace-nowrap text-slate-500",
                      render: (_: unknown, r: FundReportRowDto) => r.code ?? "",
                  },
                  {
                      title: "期初余额",
                      ...AMOUNT_COL,
                      className: cn(AMOUNT_COL.className, "text-slate-500"),
                      render: (_: unknown, r: FundReportRowDto) => fmtMoney(r.openingBalance ?? 0),
                  },
              ]
            : []),
        { title: "收入", render: (_, r) => fmtAmount(r.totalIncome), ...AMOUNT_COL },
        { title: "支出", render: (_, r) => fmtAmount(r.totalExpense), ...AMOUNT_COL },
        {
            title: isAccount ? "期末余额" : "净额",
            ...AMOUNT_COL,
            className: cn(AMOUNT_COL.className, "font-medium text-slate-700"),
            render: (_, r) => fmtMoney(r.balance ?? 0),
        },
        {
            title: "笔数（收 / 支）",
            ...AMOUNT_COL,
            className: cn(AMOUNT_COL.className, "text-slate-500"),
            render: (_, r) => `${r.incomeCount ?? 0} / ${r.expenseCount ?? 0}`,
        },
    ]

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={FundOutlined}
                title="资金报表"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。按 账户 / 收支类别 / 往来单位 汇总区间内资金收付；出纳台账口径，含尚未生成凭证的流水。
                    </>
                }
                actions={
                    <Button
                        icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                        disabled={!accountSetId || loading}
                        onClick={() => void load()}
                    >
                        刷新
                    </Button>
                }
            />

            {error && <Alert type="error" showIcon closable title={error} onClose={() => setError(null)} />}

            <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
                {tiles.map((t) => (
                    <Tile key={t.label} {...t} />
                ))}
            </section>

            <SectionCard
                title={<h3 className="text-[14px] font-semibold text-slate-800">{`按${DIMENSION_LABEL[dimension]}汇总`}</h3>}
                action={
                    <div className="flex flex-wrap items-center gap-2">
                        <Segmented
                            options={DIMENSION_OPTIONS}
                            value={dimension}
                            onChange={(v) => setDimension(v as Dimension)}
                        />
                        <span className="text-[12px] text-slate-400">区间</span>
                        <DateRangeField
                            value={{ from: parseDate(range.start), to: parseDate(range.end) }}
                            onChange={(r) => {
                                if (r?.from && r.to) {
                                    setRange({
                                        start: dayjs(r.from).format("YYYY-MM-DD"),
                                        end: dayjs(r.to).format("YYYY-MM-DD"),
                                    })
                                }
                            }}
                            disabled={!accountSetId}
                            allowClear={false}
                        />
                    </div>
                }
                bodyClassName="p-0"
            >
                <Table
                    columns={columns}
                    dataSource={rows}
                    rowKey={(r) => `${dimension}-${r.name}`}
                    pagination={false}
                    loading={loading}
                    size="small"
                    locale={{ emptyText: "所选区间内暂无资金收付数据。" }}
                    summary={
                        rows.length > 0 && total
                            ? () => {
                                  // 合计单元格随维度增减，用递增下标保证 index 与列序一致。
                                  const cells: ReactNode[] = []
                                  const cell = (content: ReactNode, props?: Record<string, unknown>) => {
                                      cells.push(
                                          <Table.Summary.Cell key={cells.length} index={cells.length} {...props}>
                                              {content}
                                          </Table.Summary.Cell>,
                                      )
                                  }
                                  cell("总计")
                                  if (isAccount) cell(null)
                                  if (isAccount) cell(fmtMoney(total.openingBalance ?? 0), AMOUNT_COL)
                                  cell(fmtMoney(total.totalIncome ?? 0), AMOUNT_COL)
                                  cell(fmtMoney(total.totalExpense ?? 0), AMOUNT_COL)
                                  cell(fmtMoney(total.balance ?? 0), AMOUNT_COL)
                                  cell(`${total.incomeCount ?? 0} / ${total.expenseCount ?? 0}`, {
                                      ...AMOUNT_COL,
                                      className: cn(AMOUNT_COL.className, "text-slate-500"),
                                  })
                                  return (
                                      <Table.Summary>
                                          <Table.Summary.Row>{cells}</Table.Summary.Row>
                                      </Table.Summary>
                                  )
                              }
                            : undefined
                    }
                />
            </SectionCard>
        </div>
    )
}
