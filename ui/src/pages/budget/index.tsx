import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, App, Button, Card, Drawer, Empty, Input, InputNumber, Select, Table, Tabs, Tag, Tooltip } from "antd"
import type { TableColumnsType } from "antd"
import {
    BarChartOutlined,
    DeleteOutlined,
    EditOutlined,
    PlusOutlined,
    ReloadOutlined,
} from "@ant-design/icons"

import { useAuth } from "@/auth/AuthContext"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { usePermissions } from "@/accountSets/PermissionsContext"
import { useAccounts } from "@/components/ledger/useLedgerOptions"
import { useConfirm } from "@/hooks/useConfirm"
import { formatAmount } from "@/lib/tax-format"
import { apiErrorMessage } from "@/lib/api-client"
import { StatusPill } from "@/components/page/status-pill"
import { AMOUNT_COL } from "@/components/page/styles"
import {
    createBudget,
    deleteBudget,
    getBudget,
    getBudgetExecution,
    listBudgets,
    updateBudget,
    type BudgetExecutionDto,
    type BudgetExecutionRowDto,
    type BudgetSummaryDto,
    type SaveBudgetLinePayload,
} from "@/api/budget"

const MONTHS = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]
const MONTH_LABELS = MONTHS.map((m) => `${m}月`)
const EMPTY_MONTHS = () => Array<number>(12).fill(0)

let lineKeySeq = 0

interface EditorLine {
    key: number
    accountId: string
    months: number[]
}

/** 金额展示：0 显示为「—」，避免月度表格被 0 填满。 */
function amountText(value?: number | null) {
    if (value == null || value === 0) return "—"
    return formatAmount(value)
}

/** 执行差异配色：借方科目（费用/资产）超预算提示红色，贷方科目（收入）超额完成提示绿色。 */
function varianceColor(variance: number, direction: number): string | undefined {
    if (variance === 0) return undefined
    const favorable = direction === 2 ? variance > 0 : variance < 0
    return favorable ? "var(--tone-green)" : "var(--tone-amber)"
}

export default function BudgetPage() {
    const { message } = App.useApp()
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { has } = usePermissions()
    const canEdit = has("budget.edit")
    const accounts = useAccounts(accountSetId, token)
    const { confirm, confirmDialog } = useConfirm()

    const [year, setYear] = useState(() => new Date().getFullYear())
    const [tab, setTab] = useState("edit")

    const [summaries, setSummaries] = useState<BudgetSummaryDto[]>([])
    const [loading, setLoading] = useState(false)
    const [loadError, setLoadError] = useState<string | null>(null)

    const [editorOpen, setEditorOpen] = useState(false)
    const [editingId, setEditingId] = useState<string | null>(null)
    const [editorLoading, setEditorLoading] = useState(false)
    const [saving, setSaving] = useState(false)
    const [editorError, setEditorError] = useState<string | null>(null)
    const [name, setName] = useState("")
    const [note, setNote] = useState("")
    const [lines, setLines] = useState<EditorLine[]>([])

    const [executionBudgetId, setExecutionBudgetId] = useState<string | undefined>()
    const [throughMonth, setThroughMonth] = useState(() => new Date().getMonth() + 1)
    const [execution, setExecution] = useState<BudgetExecutionDto | null>(null)
    const [executionLoading, setExecutionLoading] = useState(false)
    const [executionError, setExecutionError] = useState<string | null>(null)

    const yearOptions = useMemo(() => {
        const current = new Date().getFullYear()
        return Array.from({ length: 6 }, (_, i) => current + 1 - i)
    }, [])

    const accountOptions = useMemo(
        () =>
            accounts
                .filter((a) => a.isLeaf !== false && a.isEnabled !== false)
                .map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
        [accounts],
    )

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setLoadError(null)
        try {
            setSummaries(await listBudgets(accountSetId, year, token))
        } catch (err) {
            setSummaries([])
            setLoadError(await apiErrorMessage(err, "加载预算列表失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, year])

    useEffect(() => {
        void load()
    }, [load])

    // 年度切换后执行对比里的预算单可能不在新年度，清空选择。
    useEffect(() => {
        setExecutionBudgetId(undefined)
        setExecution(null)
    }, [year])

    const loadExecution = useCallback(async () => {
        if (!accountSetId || !executionBudgetId) {
            setExecution(null)
            return
        }
        setExecutionLoading(true)
        setExecutionError(null)
        try {
            setExecution(await getBudgetExecution(accountSetId, executionBudgetId, throughMonth, token))
        } catch (err) {
            setExecution(null)
            setExecutionError(await apiErrorMessage(err, "加载预算执行对比失败，请重试。"))
        } finally {
            setExecutionLoading(false)
        }
    }, [accountSetId, executionBudgetId, throughMonth, token])

    useEffect(() => {
        void loadExecution()
    }, [loadExecution])

    function openCreate() {
        setEditingId(null)
        setName("")
        setNote("")
        setLines([{ key: ++lineKeySeq, accountId: "", months: EMPTY_MONTHS() }])
        setEditorError(null)
        setEditorOpen(true)
    }

    async function openEdit(budget: BudgetSummaryDto) {
        if (!accountSetId || !budget.id) return
        setEditingId(budget.id)
        setName(budget.name)
        setNote(budget.note ?? "")
        setLines([])
        setEditorError(null)
        setEditorOpen(true)
        setEditorLoading(true)
        try {
            const detail = await getBudget(accountSetId, budget.id, token)
            setName(detail.name)
            setNote(detail.note ?? "")
            setLines(
                detail.lines.map((line) => ({
                    key: ++lineKeySeq,
                    accountId: line.accountId,
                    months: MONTHS.map((_, i) => Number(line.months[i] ?? 0)),
                })),
            )
        } catch (err) {
            setEditorError(await apiErrorMessage(err, "加载预算明细失败，请重试。"))
        } finally {
            setEditorLoading(false)
        }
    }

    function updateLine(key: number, patch: Partial<EditorLine>) {
        setLines((prev) => prev.map((line) => (line.key === key ? { ...line, ...patch } : line)))
    }

    function updateMonth(key: number, index: number, value?: number) {
        setLines((prev) =>
            prev.map((line) => {
                if (line.key !== key) return line
                const months = [...line.months]
                months[index] = value ?? 0
                return { ...line, months }
            }),
        )
    }

    function removeLine(key: number) {
        setLines((prev) => prev.filter((line) => line.key !== key))
    }

    async function save() {
        if (!accountSetId) return
        const trimmedName = name.trim()
        if (!trimmedName) {
            setEditorError("请填写预算名称。")
            return
        }
        const filled = lines.filter((line) => line.accountId)
        if (filled.length === 0) {
            setEditorError("请至少为一个科目填写预算。")
            return
        }
        const duplicated = filled.find((line, i) => filled.findIndex((x) => x.accountId === line.accountId) !== i)
        if (duplicated) {
            setEditorError("同一科目只能填写一行预算。")
            return
        }

        const payloadLines: SaveBudgetLinePayload[] = filled.map((line) => ({
            accountId: line.accountId,
            months: line.months,
        }))

        setSaving(true)
        setEditorError(null)
        try {
            const body = { year, name: trimmedName, note: note.trim() || null, lines: payloadLines }
            if (editingId) {
                await updateBudget(accountSetId, editingId, body, token)
                message.success("预算已保存。")
            } else {
                await createBudget(accountSetId, body, token)
                message.success("预算已创建。")
            }
            setEditorOpen(false)
            await load()
        } catch (err) {
            setEditorError(await apiErrorMessage(err, "保存预算失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function remove(budget: BudgetSummaryDto) {
        if (!accountSetId || !budget.id) return
        const ok = await confirm(`确定删除预算「${budget.name}」吗？删除后执行对比数据一并移除。`, {
            title: "删除预算",
            confirmText: "删除",
        })
        if (!ok) return
        try {
            await deleteBudget(accountSetId, budget.id, token)
            message.success("预算已删除。")
            if (editingId === budget.id) setEditorOpen(false)
            await load()
        } catch (err) {
            message.error(await apiErrorMessage(err, "删除预算失败，请重试。"))
        }
    }

    const editingLineTotals = useMemo(
        () => MONTHS.map((_, i) => lines.reduce((sum, line) => sum + (line.months[i] ?? 0), 0)),
        [lines],
    )
    const editingGrandTotal = editingLineTotals.reduce((a, b) => a + b, 0)

    const listColumns: TableColumnsType<BudgetSummaryDto> = [
        {
            title: "预算名称",
            dataIndex: "name",
            render: (_, row) => (
                <div className="flex min-w-0 flex-col">
                    <span className="truncate font-medium text-(--foreground)">{row.name}</span>
                    {row.note && (
                        <span className="truncate text-[12px] text-(--muted-foreground)">{row.note}</span>
                    )}
                </div>
            ),
        },
        { title: "年度", dataIndex: "year", width: 90 },
        { title: "科目数", dataIndex: "lineCount", width: 90, ...AMOUNT_COL },
        {
            title: "全年预算",
            dataIndex: "total",
            width: 150,
            ...AMOUNT_COL,
            render: (_, row) => <span className="font-medium">{formatAmount(row.total)}</span>,
        },
        { title: "制单人", dataIndex: "createdBy", width: 120 },
        {
            title: "更新时间",
            dataIndex: "updatedAt",
            width: 170,
            render: (_, row) => <span className="tabular-nums">{formatDateTime(row.updatedAt)}</span>,
        },
        {
            title: "操作",
            key: "actions",
            width: 220,
            render: (_, row) => (
                <div className="flex items-center gap-1">
                    <Button
                        type="link"
                        size="small"
                        icon={<BarChartOutlined />}
                        onClick={() => {
                            setExecutionBudgetId(row.id)
                            setTab("execution")
                        }}
                    >
                        执行对比
                    </Button>
                    {canEdit && (
                        <>
                            <Button
                                type="link"
                                size="small"
                                icon={<EditOutlined />}
                                onClick={() => void openEdit(row)}
                            >
                                编辑
                            </Button>
                            <Button
                                type="link"
                                size="small"
                                danger
                                icon={<DeleteOutlined />}
                                onClick={() => void remove(row)}
                            >
                                删除
                            </Button>
                        </>
                    )}
                </div>
            ),
        },
    ]

    const editorColumns: TableColumnsType<EditorLine> = [
        {
            title: "科目",
            key: "account",
            width: 220,
            fixed: "left",
            render: (_, line) => (
                <Select
                    showSearch
                    optionFilterProp="label"
                    placeholder="选择科目"
                    style={{ width: "100%" }}
                    value={line.accountId || undefined}
                    options={accountOptions}
                    onChange={(value) => updateLine(line.key, { accountId: value })}
                />
            ),
        },
        ...MONTHS.map((month, index) => ({
            title: MONTH_LABELS[index],
            key: `m${month}`,
            width: 96,
            align: "right" as const,
            render: (_: unknown, line: EditorLine) => (
                <InputNumber
                    className="w-full [&>input]:text-right [&>input]:tabular-nums"
                    controls={false}
                    precision={2}
                    min={0}
                    placeholder="0"
                    value={line.months[index] || null}
                    onChange={(value) => updateMonth(line.key, index, value == null ? 0 : Number(value))}
                />
            ),
        })),
        {
            title: "合计",
            key: "total",
            width: 130,
            fixed: "right",
            ...AMOUNT_COL,
            render: (_, line) => (
                <span className="font-medium">{formatAmount(line.months.reduce((a, b) => a + (b || 0), 0))}</span>
            ),
        },
        {
            title: "",
            key: "remove",
            width: 56,
            fixed: "right",
            render: (_, line) => (
                <Tooltip title="删除该行">
                    <Button
                        type="text"
                        danger
                        size="small"
                        icon={<DeleteOutlined />}
                        onClick={() => removeLine(line.key)}
                    />
                </Tooltip>
            ),
        },
    ]

    const executionColumns: TableColumnsType<BudgetExecutionRowDto> = [
        {
            title: "科目",
            key: "account",
            fixed: "left",
            width: 220,
            render: (_, row) => (
                <span className="flex min-w-0 flex-col">
                    <span className="truncate text-(--foreground)">
                        {row.accountCode} {row.accountName}
                    </span>
                    <span className="text-[11.5px] text-(--muted-foreground)">{row.direction === 1 ? "借方" : "贷方"}</span>
                </span>
            ),
        },
        {
            title: "全年预算",
            key: "budgetYear",
            width: 150,
            ...AMOUNT_COL,
            render: (_, row) => amountText(row.budgetMonths.reduce((a, b) => a + b, 0)),
        },
        {
            title: "截至预算",
            key: "budgetToDate",
            width: 150,
            ...AMOUNT_COL,
            render: (_, row) => amountText(row.budgetToDate),
        },
        {
            title: "截至实际",
            key: "actualToDate",
            width: 150,
            ...AMOUNT_COL,
            render: (_, row) => amountText(row.actualToDate),
        },
        {
            title: "差异",
            key: "variance",
            width: 140,
            ...AMOUNT_COL,
            render: (_, row) => (
                <span style={{ color: varianceColor(row.variance, row.direction) }}>
                    {row.variance === 0 ? "—" : formatAmount(row.variance)}
                </span>
            ),
        },
        {
            title: "执行率",
            key: "rate",
            width: 110,
            ...AMOUNT_COL,
            render: (_, row) => (row.rate == null ? "—" : `${formatAmount(row.rate)}%`),
        },
    ]

    function executionMonthDetail(row: BudgetExecutionRowDto) {
        const rows = MONTHS.map((month, index) => ({
            key: month,
            month,
            budget: row.budgetMonths[index] ?? 0,
            actual: row.actualMonths[index] ?? 0,
        }))
        return (
            <Table
                size="small"
                pagination={false}
                rowKey="key"
                dataSource={rows}
                columns={[
                    { title: "月份", dataIndex: "month", width: 80, render: (_, r) => `${r.month} 月` },
                    {
                        title: "预算",
                        dataIndex: "budget",
                        ...AMOUNT_COL,
                        render: (_, r) => amountText(r.budget),
                    },
                    {
                        title: "实际",
                        dataIndex: "actual",
                        ...AMOUNT_COL,
                        render: (_, r) => amountText(r.actual),
                    },
                    {
                        title: "差异",
                        key: "variance",
                        ...AMOUNT_COL,
                        render: (_, r) => (
                            <span style={{ color: varianceColor(r.actual - r.budget, row.direction) }}>
                                {r.actual - r.budget === 0 ? "—" : formatAmount(r.actual - r.budget)}
                            </span>
                        ),
                    },
                ]}
            />
        )
    }

    if (!accountSetId) {
        return (
            <Card>
                <span className="text-[13px] text-(--muted-foreground)">请先在左上角选择账套。</span>
            </Card>
        )
    }

    const executionEmpty = executionBudgetId ? (
        <Empty description="该预算暂无可展示的执行数据" />
    ) : (
        <Empty description="请选择要对比的预算单" />
    )

    return (
        <>
            <Card>
                <div className="flex flex-wrap items-start justify-between gap-3 px-4 pt-4">
                    <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                            <h2 className="text-[15px] font-semibold text-(--foreground)">预算管理</h2>
                            <StatusPill tone="blue">按科目编制 · 执行对比</StatusPill>
                        </div>
                        <p className="mt-1 text-[12.5px] leading-5 text-(--muted-foreground)">
                            按年度为科目编制 12 个月预算，执行数取已过账凭证发生额；末级科目的发生额自动归集到上级预算科目，父子科目同时编制预算时不重复统计。
                        </p>
                    </div>
                    <div className="flex items-center gap-2">
                        <Select
                            style={{ width: 110 }}
                            value={year}
                            options={yearOptions.map((value) => ({ value, label: `${value} 年` }))}
                            onChange={setYear}
                        />
                        <Button icon={<ReloadOutlined spin={loading} />} onClick={() => void load()} disabled={loading}>
                            刷新
                        </Button>
                        {canEdit && (
                            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                                新建预算
                            </Button>
                        )}
                    </div>
                </div>

                <div className="px-4 pt-3">
                    {loadError && (
                        <Alert type="error" showIcon title={loadError} closable onClose={() => setLoadError(null)} />
                    )}
                </div>

                <Tabs
                    activeKey={tab}
                    onChange={setTab}
                    className="px-4"
                    items={[
                        {
                            key: "edit",
                            label: "预算编制",
                            children: (
                                <Table
                                    columns={listColumns}
                                    dataSource={summaries}
                                    rowKey={(row) => row.id}
                                    loading={loading}
                                    scroll={{ x: 1000 }}
                                    pagination={{ pageSize: 20, showTotal: (total) => `共 ${total} 个预算` }}
                                    locale={{
                                        emptyText: (
                                            <div className="py-10 text-[13px] text-(--muted-foreground)">
                                                {`${year} 年度还没有预算，${canEdit ? "点击右上角「新建预算」开始编制。" : "请联系账套管理员编制。"}`}
                                            </div>
                                        ),
                                    }}
                                />
                            ),
                        },
                        {
                            key: "execution",
                            label: "执行对比",
                            children: (
                                <div className="flex flex-col gap-3 pb-4">
                                    <div className="flex flex-wrap items-center gap-2">
                                        <Select
                                            placeholder="选择预算单"
                                            style={{ width: 260 }}
                                            value={executionBudgetId}
                                            options={summaries.map((row) => ({ value: row.id, label: row.name }))}
                                            onChange={setExecutionBudgetId}
                                            notFoundContent="当前年度没有预算"
                                        />
                                        <Select
                                            style={{ width: 130 }}
                                            value={throughMonth}
                                            options={MONTHS.map((month) => ({
                                                value: month,
                                                label: `截至 ${month} 月`,
                                            }))}
                                            onChange={setThroughMonth}
                                        />
                                        <Button
                                            icon={<ReloadOutlined spin={executionLoading} />}
                                            onClick={() => void loadExecution()}
                                            disabled={!executionBudgetId || executionLoading}
                                        >
                                            刷新
                                        </Button>
                                    </div>

                                    {executionError && (
                                        <Alert
                                            type="error"
                                            showIcon
                                            title={executionError}
                                            closable
                                            onClose={() => setExecutionError(null)}
                                        />
                                    )}

                                    {execution ? (
                                        <>
                                            <div className="flex flex-wrap items-center gap-x-6 gap-y-2 rounded-xl border border-(--input) bg-muted px-4 py-3 text-[12.5px] text-(--muted-foreground)">
                                                <span>
                                                    {execution.year} 年度 ·{" "}
                                                    <span className="font-medium text-(--foreground)">{execution.name}</span>
                                                </span>
                                                <span>
                                                    预算累计{" "}
                                                    <span className="font-medium tabular-nums text-(--foreground)">
                                                        {formatAmount(execution.budgetToDate)}
                                                    </span>
                                                </span>
                                                <span>
                                                    实际累计{" "}
                                                    <span className="font-medium tabular-nums text-(--foreground)">
                                                        {formatAmount(execution.actualToDate)}
                                                    </span>
                                                </span>
                                                <span>
                                                    差异{" "}
                                                    <span
                                                        className="font-medium tabular-nums"
                                                        style={{ color: varianceColor(execution.variance, 1) }}
                                                    >
                                                        {execution.variance === 0 ? "—" : formatAmount(execution.variance)}
                                                    </span>
                                                </span>
                                                <span>
                                                    执行率{" "}
                                                    <span className="font-medium tabular-nums text-(--foreground)">
                                                        {execution.rate == null ? "—" : `${formatAmount(execution.rate)}%`}
                                                    </span>
                                                </span>
                                                <Tag color="default">截至 {execution.throughMonth} 月</Tag>
                                            </div>

                                            <Table
                                                columns={executionColumns}
                                                dataSource={execution.rows}
                                                rowKey={(row) => row.accountId}
                                                loading={executionLoading}
                                                scroll={{ x: 940 }}
                                                pagination={false}
                                                expandable={{ expandedRowRender: executionMonthDetail }}
                                                summary={() =>
                                                    execution.rows.length > 0 ? (
                                                        <Table.Summary.Row>
                                                            <Table.Summary.Cell index={0}>
                                                                <span className="font-medium">合计</span>
                                                            </Table.Summary.Cell>
                                                            <Table.Summary.Cell index={1} align="right">
                                                                <span className="font-medium tabular-nums">
                                                                    {formatAmount(
                                                                        execution.budgetMonths.reduce((a, b) => a + b, 0),
                                                                    )}
                                                                </span>
                                                            </Table.Summary.Cell>
                                                            <Table.Summary.Cell index={2} align="right">
                                                                <span className="font-medium tabular-nums">
                                                                    {formatAmount(execution.budgetToDate)}
                                                                </span>
                                                            </Table.Summary.Cell>
                                                            <Table.Summary.Cell index={3} align="right">
                                                                <span className="font-medium tabular-nums">
                                                                    {formatAmount(execution.actualToDate)}
                                                                </span>
                                                            </Table.Summary.Cell>
                                                            <Table.Summary.Cell index={4} align="right">
                                                                <span
                                                                    className="font-medium tabular-nums"
                                                                    style={{ color: varianceColor(execution.variance, 1) }}
                                                                >
                                                                    {execution.variance === 0
                                                                        ? "—"
                                                                        : formatAmount(execution.variance)}
                                                                </span>
                                                            </Table.Summary.Cell>
                                                            <Table.Summary.Cell index={5} align="right">
                                                                <span className="font-medium tabular-nums">
                                                                    {execution.rate == null
                                                                        ? "—"
                                                                        : `${formatAmount(execution.rate)}%`}
                                                                </span>
                                                            </Table.Summary.Cell>
                                                        </Table.Summary.Row>
                                                    ) : null
                                                }
                                            />
                                        </>
                                    ) : (
                                        <div className="py-10">{executionLoading ? null : executionEmpty}</div>
                                    )}
                                </div>
                            ),
                        },
                    ]}
                />
            </Card>

            <Drawer
                open={editorOpen}
                size={1180}
                title={editingId ? "编辑预算" : "新建预算"}
                onClose={() => setEditorOpen(false)}
                extra={
                    <div className="flex items-center gap-2">
                        <Button onClick={() => setEditorOpen(false)} disabled={saving}>
                            取消
                        </Button>
                        <Button type="primary" loading={saving} onClick={() => void save()} disabled={editorLoading}>
                            保存
                        </Button>
                    </div>
                }
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16, overflow: "auto" } }}
            >
                <div className="flex flex-wrap items-end gap-3">
                    <label className="flex flex-col gap-1 text-[12.5px] text-(--muted-foreground)">
                        预算名称
                        <Input
                            style={{ width: 300 }}
                            maxLength={100}
                            placeholder="如：2026 年费用预算"
                            value={name}
                            onChange={(e) => setName(e.target.value)}
                        />
                    </label>
                    <label className="flex flex-col gap-1 text-[12.5px] text-(--muted-foreground)">
                        备注
                        <Input
                            style={{ width: 360 }}
                            maxLength={200}
                            placeholder="选填"
                            value={note}
                            onChange={(e) => setNote(e.target.value)}
                        />
                    </label>
                    <Tag color="blue">{year} 年度</Tag>
                    {editingId && <Tag color="default">年度创建后不可修改</Tag>}
                </div>

                {editorError && (
                    <Alert type="error" showIcon title={editorError} closable onClose={() => setEditorError(null)} />
                )}

                <Table
                    columns={editorColumns}
                    dataSource={lines}
                    rowKey={(line) => line.key}
                    loading={editorLoading}
                    pagination={false}
                    size="small"
                    scroll={{ x: 1540 }}
                    locale={{ emptyText: <Empty description="还没有预算行，点击下方「添加科目」" /> }}
                    summary={() => (
                        <Table.Summary>
                            <Table.Summary.Row>
                                <Table.Summary.Cell index={0}>
                                    <span className="font-medium">合计</span>
                                </Table.Summary.Cell>
                                {editingLineTotals.map((total, index) => (
                                    <Table.Summary.Cell key={index} index={index + 1} align="right">
                                        <span className="tabular-nums">{total === 0 ? "—" : formatAmount(total)}</span>
                                    </Table.Summary.Cell>
                                ))}
                                <Table.Summary.Cell index={13} align="right">
                                    <span className="font-medium tabular-nums">{formatAmount(editingGrandTotal)}</span>
                                </Table.Summary.Cell>
                                <Table.Summary.Cell index={14} />
                            </Table.Summary.Row>
                        </Table.Summary>
                    )}
                />

                <div>
                    <Button
                        icon={<PlusOutlined />}
                        onClick={() =>
                            setLines((prev) => [...prev, { key: ++lineKeySeq, accountId: "", months: EMPTY_MONTHS() }])
                        }
                    >
                        添加科目
                    </Button>
                </div>
            </Drawer>

            {confirmDialog}
        </>
    )
}

function formatDateTime(value?: string | null) {
    if (!value) return "—"
    const date = new Date(value)
    if (Number.isNaN(date.getTime())) return "—"
    const pad = (n: number) => String(n).padStart(2, "0")
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}
