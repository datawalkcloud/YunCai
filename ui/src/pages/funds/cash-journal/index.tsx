import { useCallback, useEffect, useMemo, useState } from "react"
import { Card, Col, DatePicker, Row, Space, Statistic, Table, Typography, Alert } from "antd"
import type { ColumnsType } from "antd/es/table"
import dayjs, { type Dayjs } from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type CashJournalRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

const { RangePicker } = DatePicker

/** 选定的日期范围。 */
interface DateRange {
    startDate: string
    endDate: string
}

/** 计算出的日记账行（含序号与累计余额）。 */
interface JournalRow extends CashJournalRowDto {
    seq: number
    balance: number
}

function formatMoney(value: number): string {
    return value.toLocaleString("zh-CN", {
        style: "currency",
        currency: "CNY",
        minimumFractionDigits: 2,
    })
}

/** 金额为 0 时留空，避免表格被 0.00 填满。 */
function fmt(value: number): string {
    if (!value) return ""
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function toDayjsArray(dr: DateRange | null): [Dayjs, Dayjs] | null {
    return dr ? [dayjs(dr.startDate), dayjs(dr.endDate)] : null
}

export default function CashJournalPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [dateRange, setDateRange] = useState<DateRange | null>(null)
    const [entries, setEntries] = useState<CashJournalRowDto[]>([])
    const [openingBalance, setOpeningBalance] = useState(0)
    const [periodText, setPeriodText] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new ReportsApi(apiConfig(token)).accountsetsAccountSetIdReportsCashJournalGet({
                accountSetId,
                startDate: dateRange?.startDate ? new Date(dateRange.startDate) : undefined,
                endDate: dateRange?.endDate ? new Date(dateRange.endDate) : undefined,
            })
            setEntries(res.rows ?? [])
            setOpeningBalance(res.openingBalance ?? 0)
            setPeriodText(res.periodText ?? "")
            // 首次未指定日期范围时，采用后端解析出的日期范围。
            if (dateRange === null && res.startDate && res.endDate) {
                setDateRange({ startDate: res.startDate, endDate: res.endDate })
            }
        } catch (err) {
            setError(await apiErrorMessage(err, "加载现金日记账失败，请重试。"))
            setEntries([])
            setOpeningBalance(0)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, dateRange])

    useEffect(() => {
        void load()
    }, [load])

    // 计算每行的累计余额与本期收支合计。
    const { rows, totalIncome, totalExpense, closingBalance } = useMemo(() => {
        let balance = openingBalance
        let income = 0
        let expense = 0
        const computed = entries.map((entry, index): JournalRow => {
            const inc = entry.income ?? 0
            const exp = entry.expense ?? 0
            balance += inc - exp
            income += inc
            expense += exp
            return { ...entry, seq: index + 1, balance }
        })
        return {
            rows: computed,
            totalIncome: income,
            totalExpense: expense,
            closingBalance: balance,
        }
    }, [entries, openingBalance])

    const summary = [
        { label: "期初余额", value: openingBalance },
        { label: "本期收入", value: totalIncome },
        { label: "本期支出", value: totalExpense },
        { label: "期末余额", value: closingBalance },
    ]

    const columns: ColumnsType<JournalRow> = [
        { title: "日期", dataIndex: "date", width: 120 },
        { title: "凭证号", dataIndex: "voucherNo", width: 110 },
        { title: "摘要", dataIndex: "summary" },
        { title: "对方科目", dataIndex: "contraAccount" },
        { title: "收入（借方）", dataIndex: "income", align: "right", width: 130, render: (v: number) => fmt(v ?? 0) },
        { title: "支出（贷方）", dataIndex: "expense", align: "right", width: 130, render: (v: number) => fmt(v ?? 0) },
        { title: "余额", dataIndex: "balance", align: "right", width: 150, render: (v: number) => <span style={{ fontWeight: 500 }}>{formatMoney(v)}</span> },
        { title: "序号", dataIndex: "seq", align: "center", width: 72 },
    ]

    return (
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
            <div>
                <Typography.Title level={3} style={{ marginBottom: 4 }}>现金日记账</Typography.Title>
                <Typography.Text type="secondary">
                    账套：{currentSet?.name ?? (accountSetId || "未指定")}
                    。由涉及库存现金科目的凭证按日期序时汇总，收入为借方、支出为贷方。
                </Typography.Text>
            </div>

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
                    allowClear={false}
                />
                {periodText && <Typography.Text type="secondary">期间：{periodText}</Typography.Text>}
            </Space>

            {error && <Alert type="error" message={error} showIcon />}

            <Row gutter={[16, 16]}>
                {summary.map((item) => (
                    <Col key={item.label} xs={12} lg={6}>
                        <Card>
                            <Statistic title={item.label} value={item.value} precision={2} prefix="¥" />
                        </Card>
                    </Col>
                ))}
            </Row>

            <Card title="明细流水">
                <Table<JournalRow>
                    rowKey="seq"
                    columns={columns}
                    dataSource={rows}
                    loading={loading}
                    pagination={false}
                    size="small"
                    scroll={{ x: 960 }}
                    locale={{ emptyText: accountSetId ? "该期间暂无现金流水。" : "请先选择账套。" }}
                    summary={() =>
                        rows.length > 0 ? (
                            <Table.Summary fixed>
                                <Table.Summary.Row>
                                    <Table.Summary.Cell index={0} colSpan={4}>本期合计</Table.Summary.Cell>
                                    <Table.Summary.Cell index={4} align="right">{formatMoney(totalIncome)}</Table.Summary.Cell>
                                    <Table.Summary.Cell index={5} align="right">{formatMoney(totalExpense)}</Table.Summary.Cell>
                                    <Table.Summary.Cell index={6} align="right">{formatMoney(closingBalance)}</Table.Summary.Cell>
                                    <Table.Summary.Cell index={7} />
                                </Table.Summary.Row>
                            </Table.Summary>
                        ) : null
                    }
                />
            </Card>
        </Space>
    )
}
