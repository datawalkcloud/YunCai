/**
 * 核对总账（对应柠檬云「资金 → 核对总账」）。
 *
 * 布局对齐柠檬云：会计期间（年月）+ 核对日（日）→ 打印 / 导出 / 刷新；
 * 第二行「账户」下拉 + 「只显示不平」勾选框；表格列 项目 / 名称 / 币别 / 期初余额 / 借方(收入) / 贷方(支出) / 余额；
 * 出纳余额与总账科目余额不一致的账户在名称后标「不平」并在余额后附差异，便于逐项核对。
 */
import { useCallback, useEffect, useMemo, useState } from "react"
import { App, Button, Card, Checkbox, DatePicker, Select, Space, Table, Tooltip, Typography } from "antd"
import type { TableColumnsType } from "antd"
import { ReloadOutlined } from "@ant-design/icons"
import dayjs, { type Dayjs } from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    FundReportsApi,
    JournalsApi,
    type FundReconciliationDto,
    type FundReconciliationRowDto,
    type JournalListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

/** 金额：0 显示为 0，其余两位小数。 */
function money(value: number | null | undefined): string {
    if (value === null || value === undefined) return ""
    if (value === 0) return "0"
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 金额：0 留白（期初 / 借贷列无发生额留白，与柠檬云一致）。 */
function moneyBlankZero(value: number | null | undefined): string {
    return value ? money(value) : ""
}

const AMOUNT_RIGHT = "whitespace-nowrap text-right tabular-nums"

/** 打印用 HTML 转义。 */
function escHtml(text: string): string {
    return text.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c] ?? c)
}

/** 组装核对总账的打印 HTML。 */
function buildPrintHtml(data: FundReconciliationDto, accountSetName: string, periodText: string): string {
    const rows = data.rows ?? []
    const body = rows
        .map(
            (r) => `<tr>
        <td>${escHtml(r.projectText ?? "")}</td>
        <td>${escHtml(r.journalName ?? "")}${r.balanced ? "" : "（不平）"}</td>
        <td>${escHtml(r.currency ?? "人民币")}</td>
        <td class="num">${money(r.openingBalance)}</td>
        <td class="num">${money(r.debit)}</td>
        <td class="num">${money(r.credit)}</td>
        <td class="num">${money(r.balance)}</td>
        <td class="num">${r.balanced ? "" : money(r.difference)}</td>
    </tr>`,
        )
        .join("")
    const total = `<tr class="total"><td>合计</td><td></td><td></td><td class="num">${money(rows.reduce((s, r) => s + (r.openingBalance ?? 0), 0))}</td><td class="num">${money(rows.reduce((s, r) => s + (r.debit ?? 0), 0))}</td><td class="num">${money(rows.reduce((s, r) => s + (r.credit ?? 0), 0))}</td><td class="num">${money(rows.reduce((s, r) => s + (r.balance ?? 0), 0))}</td><td class="num">${money(rows.reduce((s, r) => s + (r.balanced ? 0 : (r.difference ?? 0)), 0))}</td></tr>`

    return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><title>核对总账</title>
<style>
    @page { size: A4 landscape; margin: 12mm; }
    * { box-sizing: border-box; }
    body { margin: 0; font-family: "Microsoft YaHei", SimSun, sans-serif; color: #111; }
    h1 { margin: 0 0 4px; font-size: 16pt; text-align: center; font-weight: 600; }
    .meta { display: flex; justify-content: space-between; font-size: 9pt; color: #444; margin-bottom: 8px; }
    table { width: 100%; border-collapse: collapse; font-size: 9pt; }
    th, td { border: 1px solid #999; padding: 4px 6px; }
    th { background: #f0f7f0; font-weight: 600; text-align: center; white-space: nowrap; }
    .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
    tr.total td { background: #f8fafc; font-weight: 600; }
</style></head><body>
<h1>核对总账</h1>
<div class="meta"><span>账套：${escHtml(accountSetName)}</span><span>会计期间：${escHtml(periodText)}</span></div>
<table><tr><th>项目</th><th>名称</th><th>币别</th><th>期初余额</th><th>借方(收入)</th><th>贷方(支出)</th><th>余额</th><th>差异</th></tr>${body}${total}</table>
</body></html>`
}

function openPrintWindow(html: string): void {
    const win = window.open("", "_blank", "width=1180,height=760")
    if (!win) return
    win.document.open()
    win.document.write(html)
    win.document.close()
    win.focus()
    win.setTimeout(() => win.print(), 400)
}

export default function ReconciliationPage() {
    const { message } = App.useApp()
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const accountSetName = accountSets.find((s) => s.id === accountSetId)?.name ?? ""

    // 会计期间（年月）+ 核对日（日）。默认当前月 1 日。
    const [period, setPeriod] = useState<Dayjs>(() => dayjs().startOf("month"))
    const [day, setDay] = useState<number>(1)
    const [journalId, setJournalId] = useState<string>("")
    const [onlyUnbalanced, setOnlyUnbalanced] = useState(false)
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(20)

    const [data, setData] = useState<FundReconciliationDto | null>(null)
    const [journals, setJournals] = useState<JournalListItemDto[]>([])
    const [loading, setLoading] = useState(false)
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
    const periodText = `${period.format("YYYY年MM月")}${day}日`

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
            render: (_, r) => <span className="text-slate-500">{r.projectText ?? ""}</span>,
        },
        {
            title: "名称",
            dataIndex: "journalName",
            render: (_, r) => (
                <span className="flex items-center gap-2">
                    <span className="font-medium text-[#2f6bff]">{r.journalName ?? ""}</span>
                    {!r.balanced && <span className="bg-[#fef2f2] px-1.5 text-[11px] leading-5 text-[#ef4444]">不平</span>}
                    {!r.accountCode && <span className="text-[11px] text-[#b45309]">未关联科目</span>}
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
            className: `${AMOUNT_RIGHT} font-medium text-slate-700`,
            render: (_, r) =>
                r.balanced ? (
                    money(r.balance)
                ) : (
                    <Tooltip title={`账面余额 ${money(r.ledgerBalance)}，差异 ${money(r.difference)}`}>
                        <span>
                            {money(r.balance)}
                            <span className="ml-1 text-[11.5px] text-[#ef4444]">(差异 {money(r.difference)})</span>
                        </span>
                    </Tooltip>
                ),
        },
    ]

    return (
        <div className="flex flex-col gap-3">
            {error && <div className="rounded border border-[#fecaca] bg-[#fef2f2] px-3 py-2 text-[13px] text-[#ef4444]">{error}</div>}

            <Card styles={{ root: { border: "1px solid #f1f5f9", borderRadius: 8 }, body: { padding: 0 } }}>
                {/* 工具行一：期间 + 打印/导出/刷新 */}
                <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-4 py-2.5">
                    <Space size={10} wrap>
                        <DatePicker
                            picker="month"
                            size="small"
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
                            size="small"
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
                            <span className="text-[12.5px] text-[#ef4444]">
                                现金和银行账户没有关联科目，无法展示核对总账数据
                                <Typography.Link
                                    className="ml-1 text-[12.5px]"
                                    onClick={() => message.info("请到「资金 → 账户设置」中为该账户关联会计科目后重新查询。")}
                                >
                                    去关联科目 &gt;&gt;
                                </Typography.Link>
                            </span>
                        )}
                        {!data?.hasUnlinkedAccounts && rows.length > 0 && (
                            <span className={`text-[12.5px] ${diffCount > 0 ? "text-[#b45309]" : "text-slate-400"}`}>
                                {diffCount > 0
                                    ? `共 ${rows.length} 个资金账户，其中 ${diffCount} 个与总账不一致`
                                    : `共 ${rows.length} 个资金账户，出纳账与总账全部一致`}
                            </span>
                        )}
                    </Space>

                    <Space size={8} wrap>
                        <Button
                            size="small"
                            autoInsertSpace={false}
                            disabled={!data}
                            onClick={() => {
                                if (data) openPrintWindow(buildPrintHtml(data, accountSetName || "当前账套", periodText))
                            }}
                        >
                            打印
                        </Button>
                        <Button size="small" autoInsertSpace={false} onClick={() => void exportReport()}>
                            导出
                        </Button>
                        <Tooltip title="刷新">
                            <Button size="small" icon={<ReloadOutlined spin={loading} />} onClick={() => void load()} />
                        </Tooltip>
                    </Space>
                </div>

                {/* 工具行二：账户 + 只显示不平 */}
                <div className="flex min-h-[48px] flex-wrap items-center gap-4 border-b border-slate-100 px-4 py-2">
                    <Space size={8}>
                        <span className="text-[13px] text-slate-600">账户：</span>
                        <Select
                            size="small"
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
                        className="text-[13px] text-slate-600"
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
                        size="small"
                        bordered
                        loading={loading}
                        locale={{ emptyText: <div className="py-10 text-[13px] text-slate-400">暂无数据</div> }}
                        onRow={(r) => (r.balanced ? {} : { style: { background: "#fff7f7" } })}
                    />
                    <div className="flex flex-wrap items-center justify-between gap-2 pt-3 text-[12.5px] text-slate-500">
                        <span>
                            显示 {visibleRows.length === 0 ? 0 : (page - 1) * pageSize + 1} 到 {Math.min(page * pageSize, visibleRows.length)}，共{" "}
                            {visibleRows.length} 条记录
                        </span>
                        <Space size={8}>
                            <Select
                                size="small"
                                style={{ width: 100 }}
                                value={pageSize}
                                onChange={(value) => {
                                    setPageSize(value)
                                    setPage(1)
                                }}
                                options={[20, 50, 100, 200].map((v) => ({ value: v, label: `${v} 条/页` }))}
                            />
                            <Button size="small" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                                上一页
                            </Button>
                            <span>
                                第 {page} / {Math.max(1, Math.ceil(visibleRows.length / pageSize))} 页
                            </span>
                            <Button
                                size="small"
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
