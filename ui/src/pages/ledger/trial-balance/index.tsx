import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, Spin, Table } from "antd"
import type { TableColumnsType, TableColumnType } from "antd"
import { AuditOutlined } from "@ant-design/icons"
import dayjs from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type TrialBalanceRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { DateRangeField, parseDate } from "@/components/fields/date-fields"
import { AMOUNT_COL, SOFT_PANEL } from "@/components/page/styles"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerTabs } from "@/components/ledger/LedgerTabs"
import { PageHeader, SectionCard } from "@/components/page/section"

/** 选定的日期范围。 */
interface DateRange {
    startDate: string
    endDate: string
}

const AMOUNT_KEYS = [
    "openingDebit",
    "openingCredit",
    "periodDebit",
    "periodCredit",
    "endingDebit",
    "endingCredit",
] as const

type AmountKey = (typeof AMOUNT_KEYS)[number]

function fmt(value: number, force = false): string {
    if (value === 0 && !force) return ""
    return value.toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

/** 金额列（借贷方向）右对齐并使用等宽数字，表头与单元格一致。 */
const amount = (dataIndex: AmountKey, title: string): TableColumnType<TrialBalanceRowDto> => ({
    title,
    dataIndex,
    render: (v) => fmt(v ?? 0),
    ...AMOUNT_COL,
})

/** 多级表头：编码、名称为单层列，三组余额各带借方/贷方两个子列。 */
const columns: TableColumnsType<TrialBalanceRowDto> = [
    { title: "科目编码", dataIndex: "code" },
    { title: "科目名称", dataIndex: "name" },
    {
        key: "opening",
        title: "期初余额",
        children: [amount("openingDebit", "借方"), amount("openingCredit", "贷方")],
    },
    {
        key: "period",
        title: "本期发生额",
        children: [amount("periodDebit", "借方"), amount("periodCredit", "贷方")],
    },
    {
        key: "ending",
        title: "期末余额",
        children: [amount("endingDebit", "借方"), amount("endingCredit", "贷方")],
    },
]

export default function TrialBalancePage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [dateRange, setDateRange] = useState<DateRange | null>(null)
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
                startDate: dateRange?.startDate ? new Date(dateRange.startDate) : undefined,
                endDate: dateRange?.endDate ? new Date(dateRange.endDate) : undefined,
            })
            setRows(res.rows ?? [])
            setPeriodText(res.periodText ?? "")
            // 首次未指定日期范围时，采用后端解析出的日期范围。
            if (dateRange === null && res.startDate && res.endDate) {
                setDateRange({ startDate: res.startDate, endDate: res.endDate })
            }
        } catch (err) {
            setError(await apiErrorMessage(err, "加载科目余额表失败，请重试。"))
            setRows([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, dateRange])

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

    return (
        <div className="flex flex-col gap-4">
            <PageHeader
                icon={AuditOutlined}
                title="科目余额表"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。汇总各科目的期初余额、本期发生额与期末余额，用于试算平衡与结账核对。
                    </>
                }
            />

            <LedgerTabs current="trial-balance" />

            <SectionCard bodyClassName="flex flex-col gap-4">
                <div className={cn(SOFT_PANEL, "flex flex-wrap items-center justify-between gap-2 px-4 py-3")}>
                    <div className="flex flex-wrap items-center gap-1.5">
                        <span className="text-[12.5px] text-slate-500">日期范围：</span>
                        <DateRangeField
                            className="w-56"
                            value={
                                dateRange
                                    ? {
                                          from: parseDate(dateRange.startDate),
                                          to: parseDate(dateRange.endDate),
                                      }
                                    : undefined
                            }
                            onChange={(range) => {
                                if (range?.from && range.to) {
                                    setDateRange({
                                        startDate: dayjs(range.from).format("YYYY-MM-DD"),
                                        endDate: dayjs(range.to).format("YYYY-MM-DD"),
                                    })
                                } else if (!range?.from) {
                                    // 清除后回到后端解析出的默认区间。
                                    setDateRange(null)
                                }
                            }}
                            disabled={!accountSetId}
                            allowClear
                        />
                    </div>
                    <div className="flex items-center gap-2">
                        {loading && <Spin size="small" />}
                        <span className="text-[12.5px] text-slate-400">单位：元</span>
                    </div>
                </div>

                {error && <Alert type="error" showIcon title={error} />}

                <LedgerTable
                    columns={columns}
                    data={rows}
                    loading={loading}
                    emptyText={accountSetId ? "该期间暂无科目余额数据。" : "请先选择账套。"}
                    rowKey={(row) => row.code ?? ""}
                    footer={
                        <Table.Summary.Row>
                            <Table.Summary.Cell index={0} colSpan={2}>
                                合计
                            </Table.Summary.Cell>
                            {AMOUNT_KEYS.map((key, i) => (
                                <Table.Summary.Cell key={key} index={2 + i} {...AMOUNT_COL}>
                                    {fmt(totals[key], true)}
                                </Table.Summary.Cell>
                            ))}
                        </Table.Summary.Row>
                    }
                />

                {rows.length > 0 && (
                    <span className={cn("text-[12.5px] text-slate-400", !balanced && "text-[#ef4444]")}>
                        {balanced
                            ? `试算平衡：借方合计 = 贷方合计（${periodText}）`
                            : "试算不平衡，请核对数据"}
                    </span>
                )}
            </SectionCard>
        </div>
    )
}
