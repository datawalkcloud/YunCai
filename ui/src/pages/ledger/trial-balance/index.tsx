import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, Card, DatePicker, Space, Table, Typography } from "antd"
import type { ColumnsType } from "antd/es/table"
import dayjs, { type Dayjs } from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type TrialBalanceRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

const { RangePicker } = DatePicker

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

function fmt(value: number, force = false): string {
    if (value === 0 && !force) return ""
    return value.toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

function toDayjsArray(dr: DateRange | null): [Dayjs, Dayjs] | null {
    return dr ? [dayjs(dr.startDate), dayjs(dr.endDate)] : null
}

const amount = (dataIndex: string, title: string) => ({
    title,
    dataIndex,
    align: "right" as const,
    width: 120,
    render: (v: number) => fmt(v ?? 0),
})

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

    const columns: ColumnsType<TrialBalanceRowDto> = [
        { title: "科目编码", dataIndex: "code", width: 120 },
        { title: "科目名称", dataIndex: "name" },
        { title: "期初余额", children: [amount("openingDebit", "借方"), amount("openingCredit", "贷方")] },
        { title: "本期发生额", children: [amount("periodDebit", "借方"), amount("periodCredit", "贷方")] },
        { title: "期末余额", children: [amount("endingDebit", "借方"), amount("endingCredit", "贷方")] },
    ]

    return (
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
            <div>
                <Typography.Title level={3} style={{ marginBottom: 4 }}>科目余额表</Typography.Title>
                <Typography.Text type="secondary">
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    。汇总各科目的期初余额、本期发生额与期末余额，用于试算平衡与结账核对。
                </Typography.Text>
            </div>

            <Card>
                <Space direction="vertical" size="middle" style={{ width: "100%" }}>
                    <div className="flex flex-wrap items-center justify-between gap-2">
                        <Space wrap>
                            <span>日期范围：</span>
                            <RangePicker
                                value={toDayjsArray(dateRange)}
                                onChange={(dates) => {
                                    if (dates && dates[0] && dates[1]) {
                                        setDateRange({
                                            startDate: dates[0].format("YYYY-MM-DD"),
                                            endDate: dates[1].format("YYYY-MM-DD"),
                                        })
                                    }
                                }}
                                disabled={!accountSetId}
                                allowClear
                            />
                        </Space>
                        <Typography.Text type="secondary">单位：元</Typography.Text>
                    </div>

                    {error && <Alert type="error" message={error} showIcon />}

                    <Table<TrialBalanceRowDto>
                        rowKey="code"
                        columns={columns}
                        dataSource={rows}
                        loading={loading}
                        pagination={false}
                        size="small"
                        bordered
                        scroll={{ x: 900 }}
                        locale={{ emptyText: accountSetId ? "该期间暂无科目余额数据。" : "请先选择账套。" }}
                        summary={() =>
                            rows.length > 0 ? (
                                <Table.Summary fixed>
                                    <Table.Summary.Row>
                                        <Table.Summary.Cell index={0} colSpan={2}>合计</Table.Summary.Cell>
                                        <Table.Summary.Cell index={2} align="right">{fmt(totals.openingDebit, true)}</Table.Summary.Cell>
                                        <Table.Summary.Cell index={3} align="right">{fmt(totals.openingCredit, true)}</Table.Summary.Cell>
                                        <Table.Summary.Cell index={4} align="right">{fmt(totals.periodDebit, true)}</Table.Summary.Cell>
                                        <Table.Summary.Cell index={5} align="right">{fmt(totals.periodCredit, true)}</Table.Summary.Cell>
                                        <Table.Summary.Cell index={6} align="right">{fmt(totals.endingDebit, true)}</Table.Summary.Cell>
                                        <Table.Summary.Cell index={7} align="right">{fmt(totals.endingCredit, true)}</Table.Summary.Cell>
                                    </Table.Summary.Row>
                                </Table.Summary>
                            ) : null
                        }
                    />

                    {rows.length > 0 && (
                        <Typography.Text type={balanced ? "secondary" : "danger"}>
                            {balanced
                                ? `试算平衡：借方合计 = 贷方合计（${periodText}）`
                                : "试算不平衡，请核对数据"}
                        </Typography.Text>
                    )}
                </Space>
            </Card>
        </Space>
    )
}
