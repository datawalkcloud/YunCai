/**
 * 核对总账（对应「资金 → 核对总账」）。
 *
 * 布局：会计期间（年月）+ 核对日（日）→ 打印 / 导出 / 刷新；
 * 第二行「账户」下拉 + 「只显示不平」勾选框；表格列 项目 / 名称 / 币别 / 期初余额 / 借方(收入) / 贷方(支出) / 余额；
 * 出纳余额与总账科目余额不一致的账户在名称后标「不平」并在余额后附差异，便于逐项核对。
 */
import { useCallback, useEffect, useMemo, useState } from "react"
import { useNavigate } from "react-router"
import { App, Button, Card, Checkbox, DatePicker, Select, Space, Table, Tooltip, Typography } from "antd"
import type { TableColumnsType } from "antd"
import { ReloadOutlined } from "@ant-design/icons"
import dayjs, { type Dayjs } from "dayjs"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    FundReportsApi,
    JournalsApi,
    type FundReconciliationDto,
    type FundReconciliationRowDto,
    type JournalListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { printPdf } from "@/lib/print-pdf"
import { setPath } from "@/accountSets/url-id"

/** 金额：0 显示为 0，其余两位小数。 */
function money(value: number | null | undefined): string {
    if (value === null || value === undefined) return ""
    if (value === 0) return "0"
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 金额：0 留白（期初 / 借贷列无发生额留白，）。 */
function moneyBlankZero(value: number | null | undefined): string {
    return value ? money(value) : ""
}

const AMOUNT_RIGHT = "whitespace-nowrap text-right tabular-nums"

export default function ReconciliationPage() {
    const { message } = App.useApp()
    const { token } = useAuth()
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""

    // 会计期间（年月）+ 核对日（日）。默认当前月当天，打开即能看到核对结果。
    const [period, setPeriod] = useState<Dayjs>(() => dayjs().startOf("month"))
    const [day, setDay] = useState<number>(() => dayjs().date())
    const [journalId, setJournalId] = useState<string>("")
    const [onlyUnbalanced, setOnlyUnbalanced] = useState(false)
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(20)

    const [data, setData] = useState<FundReconciliationDto | null>(null)
    const [journals, setJournals] = useState<JournalListItemDto[]>([])
    const [loading, setLoading] = useState(false)
    const [printing, setPrinting] = useState(false)
    const [error, setError] = useState<string | null>(null)

    /** 核对日 = 会计期间当年当月 + 选中的日。 */
    const asOf = useMemo(() => period.date(Math.min(day, period.daysInMonth())).format("YYYY-MM-DD"), [period, day])
    const dayOptions = useMemo(
        () => Array.from({ length: period.daysInMonth() }, (_, i) => ({ value: i + 1, label: `${i + 1}日` })),
        [period],
    )

    useEffect(() => {
        if (!accountSetId) return
        void (async () => {
            try {
                const list = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId })
                setJournals(list ?? [])
            } catch {
                // 下拉数据失败不阻断主体
            }
        })()
    }, [accountSetId, token])

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new FundReportsApi(apiConfig(token)).accountsetsAccountSetIdFundReportsReconciliationGet({
                accountSetId,
                date: new Date(asOf),
                journalId: journalId || undefined,
            })
            setData(res)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载核对总账数据失败，请重试。"))
            setData(null)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, asOf, journalId])

    useEffect(() => {
        void load()
    }, [load])

    const rows = data?.rows ?? []
    const visibleRows = useMemo(() => (onlyUnbalanced ? rows.filter((r) => !r.balanced) : rows), [rows, onlyUnbalanced])
    const pagedRows = visibleRows.slice((page - 1) * pageSize, page * pageSize)

    const diffCount = rows.filter((r) => !r.balanced).length

    const printReport = async () => {
        if (!accountSetId) return
        setPrinting(true)
        try {
            const qs = new URLSearchParams({ date: asOf })
            if (journalId) qs.set("journalId", journalId)
            if (onlyUnbalanced) qs.set("onlyUnbalanced", "true")
            await printPdf(`/api/accountsets/${accountSetId}/fund-reports/reconciliation/pdf?${qs.toString()}`, token)
        } catch (err) {
            message.error(err instanceof Error ? err.message : "生成打印稿失败，请重试。")
        } finally {
            setPrinting(false)
        }
    }

    const exportReport = async () => {
        if (!accountSetId) return
        const qs = new URLSearchParams({ date: asOf })
        if (journalId) qs.set("journalId", journalId)
        try {
            const res = await fetch(`/api/accountsets/${accountSetId}/fund-reports/reconciliation/export?${qs.toString()}`, {
                headers: { Authorization: `Bearer ${token}` },
            })
            if (!res.ok) throw new Error(await apiErrorMessage(await res.json().catch(() => null), "导出失败，请重试。"))
            const blob = await res.blob()
            const url = URL.createObjectURL(blob)
            const anchor = document.createElement("a")
            anchor.href = url
            const disposition = res.headers.get("Content-Disposition") ?? ""
            const star = /filename\*=UTF-8''([^;\n]+)/i.exec(disposition)
            const plain = /filename="?([^";\n]+)"?/i.exec(disposition)
            anchor.download = star ? decodeURIComponent(star[1]) : plain ? plain[1] : "核对总账.xlsx"
            document.body.appendChild(anchor)
            anchor.click()
            anchor.remove()
            URL.revokeObjectURL(url)
        } catch (err) {
            message.error(err instanceof Error ? err.message : "导出失败，请重试。")
        }
    }

    const columns: TableColumnsType<FundReconciliationRowDto> = [
        {
            title: "项目",
            dataIndex: "projectText",
            width: 140,
            render: (_, r) => <span className="text-(--muted-foreground)">{r.projectText ?? ""}</span>,
        },
        {
            title: "名称",
            dataIndex: "journalName",
            render: (_, r) => (
                <span className="flex items-center gap-2">
                    <span className="font-medium text-(--brand)">{r.journalName ?? ""}</span>
                    {!r.balanced && <span className="rounded-sm bg-(--table-neg-bg) px-1.5 text-[11px] leading-5 text-(--danger)">不平</span>}
                    {!r.accountCode && <span className="text-[11px] text-(--warn-text)">未关联科目</span>}
                </span>
            ),
        },
        { title: "币别", dataIndex: "currency", width: 110, render: (_, r) => r.currency ?? "人民币" },
        { title: "期初余额", dataIndex: "openingBalance", width: 130, align: "right", className: AMOUNT_RIGHT, render: (_, r) => moneyBlankZero(r.openingBalance) },
        { title: "借方(收入)", dataIndex: "debit", width: 130, align: "right", className: AMOUNT_RIGHT, render: (_, r) => moneyBlankZero(r.debit) },
        { title: "贷方(支出)", dataIndex: "credit", width: 130, align: "right", className: AMOUNT_RIGHT, render: (_, r) => moneyBlankZero(r.credit) },
        {
            title: "余额",
            dataIndex: "balance",
            width: 180,
            align: "right",
            className: `${AMOUNT_RIGHT} font-medium text-(--foreground)`,
            render: (_, r) =>
                r.balanced ? (
                    money(r.balance)
                ) : (
                    <Tooltip title={`账面余额 ${money(r.ledgerBalance)}，差异 ${money(r.difference)}`}>
                        <span>
                            {money(r.balance)}
                            <span className="ml-1 text-[11.5px] text-(--danger)">(差异 {money(r.difference)})</span>
                        </span>
                    </Tooltip>
                ),
        },
    ]

    return (
        <div className="flex flex-col gap-3">
            {error && <div className="rounded border border-(--danger-border) bg-(--table-neg-bg) px-3 py-2 text-[13px] text-(--danger)">{error}</div>}

            <Card>
                {/* 工具行一：期间 + 打印/导出/刷新 */}
                <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 px-4 py-2.5">
                    <Space size={10} wrap>
                        <DatePicker
                            picker="month"
                                                       allowClear={false}
                            value={period}
                            style={{ width: 130 }}
                            onChange={(value) => {
                                if (!value) return
                                setPeriod(value)
                                setPage(1)
                            }}
                        />
                        <Select
                                                       style={{ width: 90 }}
                            value={day}
                            showSearch
                            optionFilterProp="label"
                            options={dayOptions}
                            onChange={(value) => {
                                setDay(value)
                                setPage(1)
                            }}
                        />
                        {data?.hasUnlinkedAccounts && (
                            <span className="text-[12.5px] text-(--danger)">
                                现金和银行账户没有关联科目，无法展示核对总账数据
                                <Typography.Link
                                    className="ml-1 text-[12.5px]"
                                    onClick={() => navigate(setPath(accountSetId, "/funds/account"))}
                                >
                                    去关联科目 &gt;&gt;
                                </Typography.Link>
                            </span>
                        )}
                        {!data?.hasUnlinkedAccounts && rows.length > 0 && (
                            <span className={`text-[12.5px] ${diffCount > 0 ? "text-(--warn-text)" : "text-(--muted-foreground)"}`}>
                                {diffCount > 0
                                    ? `共 ${rows.length} 个资金账户，其中 ${diffCount} 个与总账不一致`
                                    : `共 ${rows.length} 个资金账户，出纳账与总账全部一致`}
                            </span>
                        )}
                    </Space>

                    <Space size={8} wrap>
                        <Button loading={printing} disabled={!data} autoInsertSpace={false} onClick={() => void printReport()}>
                            打印
                        </Button>
                        <Button autoInsertSpace={false} onClick={() => void exportReport()}>
                            导出
                        </Button>
                        <Tooltip title="刷新">
                            <Button icon={<ReloadOutlined spin={loading} />} onClick={() => void load()} />
                        </Tooltip>
                    </Space>
                </div>

                {/* 工具行二：账户 + 只显示不平 */}
                <div className="flex min-h-[48px] flex-wrap items-center gap-4 px-4 py-2" style={{ marginBottom: 16 }}>
                    <Space size={8}>
                        <span className="text-[13px] text-(--foreground)">账户：</span>
                        <Select
                                                       style={{ width: 150 }}
                            value={journalId}
                            onChange={(value) => {
                                setJournalId(value)
                                setPage(1)
                            }}
                            options={[{ value: "", label: "所有" }, ...journals.map((j) => ({ value: j.id ?? "", label: j.name ?? "" }))]}
                        />
                    </Space>
                    <Checkbox
                        className="text-[13px] text-(--foreground)"
                        checked={onlyUnbalanced}
                        onChange={(e) => {
                            setOnlyUnbalanced(e.target.checked)
                            setPage(1)
                        }}
                    >
                        只显示不平
                    </Checkbox>
                </div>

                {/* 表格 */}
                <div className="p-3">
                    <Table
                        columns={columns}
                        dataSource={pagedRows}
                        rowKey={(r) => r.journalId ?? ""}
                        pagination={false}
                        loading={loading}
                        locale={{ emptyText: <div className="py-10 text-[13px] text-(--muted-foreground)">暂无数据</div> }}
                        onRow={(r) => (r.balanced ? {} : { style: { background: "var(--table-neg-bg)" } })}
                        summary={() => {
                            // 合计按当前筛选后的全部账户统计，而不是只加当前页。
                            const sum = (pick: (r: FundReconciliationRowDto) => number) =>
                                visibleRows.reduce((acc, r) => acc + pick(r), 0)
                            return (
                                <Table.Summary fixed>
                                    <Table.Summary.Row>
                                        <Table.Summary.Cell index={0} colSpan={3} className="font-medium">
                                            合计（{visibleRows.length} 个账户）
                                        </Table.Summary.Cell>
                                        <Table.Summary.Cell index={3} align="right" className={`${AMOUNT_RIGHT} font-medium`}>
                                            {money(sum((r) => r.openingBalance ?? 0))}
                                        </Table.Summary.Cell>
                                        <Table.Summary.Cell index={4} align="right" className={`${AMOUNT_RIGHT} font-medium`}>
                                            {money(sum((r) => r.debit ?? 0))}
                                        </Table.Summary.Cell>
                                        <Table.Summary.Cell index={5} align="right" className={`${AMOUNT_RIGHT} font-medium`}>
                                            {money(sum((r) => r.credit ?? 0))}
                                        </Table.Summary.Cell>
                                        <Table.Summary.Cell index={6} align="right" className={`${AMOUNT_RIGHT} font-medium`}>
                                            {money(sum((r) => r.balance ?? 0))}
                                        </Table.Summary.Cell>
                                    </Table.Summary.Row>
                                </Table.Summary>
                            )
                        }}
                    />
                    <div className="flex flex-wrap items-center justify-between gap-2 text-[12.5px] text-(--muted-foreground)" style={{ marginTop: 16 }}>
                        <span>
                            显示 {visibleRows.length === 0 ? 0 : (page - 1) * pageSize + 1} 到 {Math.min(page * pageSize, visibleRows.length)}，共{" "}
                            {visibleRows.length} 条
                        </span>
                        <Space size={8}>
                            <Select
                                                               style={{ width: 100 }}
                                value={pageSize}
                                onChange={(value) => {
                                    setPageSize(value)
                                    setPage(1)
                                }}
                                options={[20, 50, 100, 200].map((v) => ({ value: v, label: `${v} 条/页` }))}
                            />
                            <Button disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                                上一页
                            </Button>
                            <span>
                                第 {page} / {Math.max(1, Math.ceil(visibleRows.length / pageSize))} 页
                            </span>
                            <Button
                                                               disabled={page >= Math.ceil(visibleRows.length / pageSize) || visibleRows.length === 0}
                                onClick={() => setPage((p) => p + 1)}
                            >
                                下一页
                            </Button>
                        </Space>
                    </div>
                </div>
            </Card>
        </div>
    )
}
