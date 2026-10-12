import { useCallback, useEffect, useState } from "react"
import dayjs from "dayjs"
import { ArrowDownLeft, ArrowUpRight, BarChart3, CircleAlert, PiggyBank, RefreshCw, Scale, Wallet, X } from "lucide-react"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { FundReportsApi, type FundReportDto, type FundReportRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { PageHeader, SectionCard } from "@/components/page/section"
import { Tile, type TileTone } from "@/components/page/tile"
import { DATE_FIELD, PAGE_BTN_GHOST } from "@/components/page/styles"
import { Alert, AlertAction, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { DateRangeField, parseDate } from "@/components/fields/date-fields"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group"

/** 报表维度（与后端 FundReportDimensions 对齐）。 */
type Dimension = "account" | "category" | "counterparty"

/** 日期区间（YYYY-MM-DD 字符串，直接透传后端 DateOnly）。 */
interface Range {
    start: string
    end: string
}

function formatMoney(value: number): string {
    return value.toLocaleString("zh-CN", { style: "currency", currency: "CNY", minimumFractionDigits: 2 })
}

/** 金额为 0 时留空，避免表格被 0.00 填满（总计行除外）。 */
function fmt(value?: number | null): string {
    if (!value) return ""
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

const DIMENSION_LABEL: Record<Dimension, string> = {
    account: "账户",
    category: "收支类别",
    counterparty: "往来单位",
}

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
            { label: "期初合计", value: formatMoney(total?.openingBalance ?? 0), tone: "gray", icon: Wallet },
            { label: "区间收入", value: formatMoney(total?.totalIncome ?? 0), tone: "green", icon: ArrowDownLeft },
            { label: "区间支出", value: formatMoney(total?.totalExpense ?? 0), tone: "red", icon: ArrowUpRight },
            { label: "期末合计", value: formatMoney(total?.balance ?? 0), tone: "blue", icon: PiggyBank },
        ]
        : [
            { label: "区间收入", value: formatMoney(total?.totalIncome ?? 0), tone: "green", icon: ArrowDownLeft },
            { label: "区间支出", value: formatMoney(total?.totalExpense ?? 0), tone: "red", icon: ArrowUpRight },
            { label: "收支净额", value: formatMoney(total?.balance ?? 0), tone: "blue", icon: Scale },
            {
                label: "变动笔数",
                value: `${(total?.incomeCount ?? 0) + (total?.expenseCount ?? 0)}`,
                tone: "gray",
                icon: BarChart3,
                hint: `收 ${total?.incomeCount ?? 0} · 支 ${total?.expenseCount ?? 0}`,
            },
        ]

    const cellAlign = { income: "text-right tabular-nums", expense: "text-right tabular-nums", balance: "text-right tabular-nums", opening: "text-right tabular-nums" }

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={BarChart3}
                title="资金报表"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。按 账户 / 收支类别 / 往来单位 汇总区间内资金收付；出纳台账口径，含尚未生成凭证的流水。
                    </>
                }
                actions={
                    <Button variant="outline" className={PAGE_BTN_GHOST} onClick={() => void load()} disabled={!accountSetId || loading}>
                        {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                        刷新
                    </Button>
                }
            />

            {error && (
                <Alert variant="destructive" className="rounded-2xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                    <CircleAlert />
                    <AlertTitle>{error}</AlertTitle>
                    <AlertAction>
                        <Button variant="ghost" size="icon-xs" title="关闭" onClick={() => setError(null)}>
                            <X />
                        </Button>
                    </AlertAction>
                </Alert>
            )}

            <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
                {tiles.map((t) => (
                    <Tile key={t.label} {...t} />
                ))}
            </section>

            <SectionCard
                title={<h3 className="text-[14px] font-semibold text-slate-800">{`按${DIMENSION_LABEL[dimension]}汇总`}</h3>}
                action={
                    <div className="flex flex-wrap items-center gap-2">
                        <ToggleGroup
                            type="single"
                            variant="outline"
                            size="sm"
                            spacing={0}
                            value={dimension}
                            onValueChange={(v) => v && setDimension(v as Dimension)}
                        >
                            <ToggleGroupItem value="account">账户</ToggleGroupItem>
                            <ToggleGroupItem value="category">收支类别</ToggleGroupItem>
                            <ToggleGroupItem value="counterparty">往来单位</ToggleGroupItem>
                        </ToggleGroup>
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
                            className={DATE_FIELD}
                        />
                    </div>
                }
                bodyClassName="p-0"
            >
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead className="text-left">{DIMENSION_LABEL[dimension]}</TableHead>
                            {isAccount && <TableHead className="text-left">对应科目</TableHead>}
                            {isAccount && <TableHead className="text-right">期初余额</TableHead>}
                            <TableHead className="text-right">收入</TableHead>
                            <TableHead className="text-right">支出</TableHead>
                            <TableHead className="text-right">{isAccount ? "期末余额" : "净额"}</TableHead>
                            <TableHead className="text-right">笔数（收 / 支）</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {rows.map((r) => (
                            <TableRow key={`${dimension}-${r.name}`}>
                                <TableCell className="w-full">
                                    <div className="truncate font-medium text-slate-700">{r.name}</div>
                                </TableCell>
                                {isAccount && (
                                    <TableCell className="whitespace-nowrap text-slate-500">{r.code ?? ""}</TableCell>
                                )}
                                {isAccount && (
                                    <TableCell className={cellAlign.opening}>
                                        <span className="text-slate-500">{formatMoney(r.openingBalance ?? 0)}</span>
                                    </TableCell>
                                )}
                                <TableCell className={cellAlign.income}>{fmt(r.totalIncome)}</TableCell>
                                <TableCell className={cellAlign.expense}>{fmt(r.totalExpense)}</TableCell>
                                <TableCell className={cellAlign.balance}>
                                    <span className="font-medium text-slate-700">{formatMoney(r.balance ?? 0)}</span>
                                </TableCell>
                                <TableCell className="text-right text-slate-500 tabular-nums">
                                    {`${r.incomeCount ?? 0} / ${r.expenseCount ?? 0}`}
                                </TableCell>
                            </TableRow>
                        ))}
                        {rows.length === 0 && !loading && (
                            <TableRow>
                                <TableCell colSpan={isAccount ? 7 : 5} className="py-8 text-center text-slate-400">
                                    所选区间内暂无资金收付数据。
                                </TableCell>
                            </TableRow>
                        )}
                    </TableBody>
                    {rows.length > 0 && total && (
                        <TableFooter>
                            <TableRow>
                                <TableCell>总计</TableCell>
                                {isAccount && <TableCell />}
                                {isAccount && (
                                    <TableCell className="text-right tabular-nums">
                                        {formatMoney(total.openingBalance ?? 0)}
                                    </TableCell>
                                )}
                                <TableCell className="text-right tabular-nums">{formatMoney(total.totalIncome ?? 0)}</TableCell>
                                <TableCell className="text-right tabular-nums">{formatMoney(total.totalExpense ?? 0)}</TableCell>
                                <TableCell className="text-right tabular-nums">{formatMoney(total.balance ?? 0)}</TableCell>
                                <TableCell className="text-right tabular-nums text-slate-500">
                                    {`${total.incomeCount ?? 0} / ${total.expenseCount ?? 0}`}
                                </TableCell>
                            </TableRow>
                        </TableFooter>
                    )}
                </Table>
            </SectionCard>
        </div>
    )
}
