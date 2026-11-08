import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, App, Button, Card, DatePicker, Input, Select, Table, Tooltip } from "antd"
import type { TableColumnsType } from "antd"
import {
    ClockCircleOutlined,
    DownloadOutlined,
    ReloadOutlined,
    SearchOutlined,
    SafetyCertificateOutlined,
} from "@ant-design/icons"
import dayjs, { type Dayjs } from "dayjs"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useCurrentAccountSet } from "@/accountSets/useCurrentAccountSet"
import { useAuth } from "@/auth/AuthContext"
import { OperationLogsApi, type OperationLogDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { StatusPill } from "@/components/page/status-pill"
import type { PillTone } from "@/components/page/styles"

/** 默认查询最近 30 天：操作日志是流水，按月查看即可，避免一次拉全量。 */
const DEFAULT_RANGE_DAYS = 30
const PAGE_SIZE_OPTIONS = [20, 50, 100]

/** 日志类型 → 标签色调；未登记的类型统一灰色。 */
const TYPE_TONES: Record<string, PillTone> = {
    新增: "green",
    修改: "blue",
    删除: "red",
    反过账: "amber",
    红冲: "amber",
    复制: "purple",
    导入: "purple",
    还原: "green",
    整理: "blue",
    结转: "purple",
    结账: "blue",
    反结账: "amber",
    撤销结转: "red",
}

const RANGE_PRESETS = [
    { label: "近 7 天", value: [dayjs().subtract(6, "day"), dayjs()] as [Dayjs, Dayjs] },
    { label: "近 30 天", value: [dayjs().subtract(29, "day"), dayjs()] as [Dayjs, Dayjs] },
    { label: "本月", value: [dayjs().startOf("month"), dayjs()] as [Dayjs, Dayjs] },
    { label: "上月", value: [dayjs().subtract(1, "month").startOf("month"), dayjs().subtract(1, "month").endOf("month")] as [Dayjs, Dayjs] },
]

function formatTime(value?: Date) {
    if (!value) return "—"
    const date = new Date(value)
    return Number.isNaN(date.getTime()) ? "—" : dayjs(date).format("YYYY-MM-DD HH:mm:ss")
}

/** 用户显示名首字作为头像，空名退化为问号。 */
function userInitial(name?: string) {
    const trimmed = (name ?? "").trim()
    return trimmed ? trimmed[0] : "?"
}

function distinctSorted(values: Array<string | undefined>) {
    return Array.from(new Set(values.map((v) => (v ?? "").trim()).filter(Boolean))).sort((a, b) =>
        a.localeCompare(b, "zh-CN"),
    )
}

function csvCell(value: string) {
    return `"${value.replaceAll('"', '""')}"`
}

export default function OperationLogPage() {
    const { message } = App.useApp()
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const accountSet = useCurrentAccountSet()

    const [rows, setRows] = useState<OperationLogDto[]>([])
    const [loading, setLoading] = useState(false)
    const [loadError, setLoadError] = useState<string | null>(null)

    const [range, setRange] = useState<[Dayjs, Dayjs]>(() => [
        dayjs().subtract(DEFAULT_RANGE_DAYS - 1, "day").startOf("day"),
        dayjs().endOf("day"),
    ])
    const [typeFilter, setTypeFilter] = useState<string | undefined>()
    const [moduleFilter, setModuleFilter] = useState<string | undefined>()
    const [userFilter, setUserFilter] = useState<string | undefined>()
    const [keyword, setKeyword] = useState("")

    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(PAGE_SIZE_OPTIONS[0])

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setLoadError(null)
        try {
            setRows(
                await new OperationLogsApi(apiConfig(token)).accountsetsAccountSetIdOperationlogsGet({
                    accountSetId,
                    // 后端按 UTC 时间戳比较：传本地 0 点 / 当日末刻，序列化后即对应同一绝对时刻。
                    start: range[0].startOf("day").toDate(),
                    end: range[1].endOf("day").toDate(),
                }),
            )
        } catch (err) {
            setRows([])
            setLoadError(await apiErrorMessage(err, "加载操作日志失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, range])

    useEffect(() => {
        void load()
    }, [load])

    const typeOptions = useMemo(() => distinctSorted(rows.map((r) => r.type)), [rows])
    const moduleOptions = useMemo(() => distinctSorted(rows.map((r) => r.module)), [rows])
    const userOptions = useMemo(() => distinctSorted(rows.map((r) => r.userName)), [rows])

    const term = keyword.trim()
    const filtered = useMemo(
        () =>
            rows.filter((row) => {
                if (typeFilter && row.type !== typeFilter) return false
                if (moduleFilter && row.module !== moduleFilter) return false
                if (userFilter && row.userName !== userFilter) return false
                if (!term) return true
                return [row.log, row.module, row.type, row.userName, row.userId].some((value) =>
                    (value ?? "").includes(term),
                )
            }),
        [rows, typeFilter, moduleFilter, userFilter, term],
    )

    const hasFilter = Boolean(term || typeFilter || moduleFilter || userFilter)

    /** 筛选条件变化后当前页可能越界，统一回到第一页。 */
    function resetFilters() {
        setTypeFilter(undefined)
        setModuleFilter(undefined)
        setUserFilter(undefined)
        setKeyword("")
        setPage(1)
    }

    function exportCsv() {
        if (filtered.length === 0) {
            message.info("当前筛选条件下没有可导出的记录。")
            return
        }
        const header = ["操作时间", "操作人", "用户ID", "类型", "模块", "操作内容"]
        const lines = filtered.map((row) => [
            formatTime(row.time),
            row.userName || "未知用户",
            row.userId ?? "",
            row.type ?? "",
            row.module ?? "",
            row.log ?? "",
        ])
        const csv = "\uFEFF" + [header, ...lines].map((cells) => cells.map(csvCell).join(",")).join("\r\n")
        const blob = new Blob([csv], { type: "text/csv;charset=utf-8" })
        const url = URL.createObjectURL(blob)
        const name = (accountSet?.name ?? "账套").replace(/[\\/:*?"<>|]/g, "_")
        const link = document.createElement("a")
        link.href = url
        link.download = `操作日志_${name}_${dayjs().format("YYYYMMDD_HHmmss")}.csv`
        link.click()
        URL.revokeObjectURL(url)
        message.success(`已导出 ${filtered.length} 条日志。`)
    }

    const columns: TableColumnsType<OperationLogDto> = [
        {
            title: "操作时间",
            dataIndex: "time",
            width: 176,
            render: (_, row) => (
                <span className="whitespace-nowrap tabular-nums text-(--foreground)">{formatTime(row.time)}</span>
            ),
        },
        {
            title: "操作人",
            dataIndex: "userName",
            width: 160,
            render: (_, row) => {
                const name = (row.userName ?? "").trim() || "未知用户"
                const cell = (
                    <span className="flex min-w-0 items-center gap-2">
                        <span
                            className="flex size-6 shrink-0 items-center justify-center rounded-full text-[12px] font-medium"
                            style={{ background: "color-mix(in srgb, var(--brand) 12%, white)", color: "var(--brand)" }}
                        >
                            {userInitial(name)}
                        </span>
                        <span className="truncate text-(--foreground)">{name}</span>
                    </span>
                )
                return row.userId ? <Tooltip title={`用户ID：${row.userId}`}>{cell}</Tooltip> : cell
            },
        },
        {
            title: "类型",
            dataIndex: "type",
            width: 100,
            render: (_, row) =>
                row.type ? <StatusPill tone={TYPE_TONES[row.type] ?? "gray"}>{row.type}</StatusPill> : <span className="text-(--muted-foreground)">—</span>,
        },
        {
            title: "模块",
            dataIndex: "module",
            width: 130,
            render: (_, row) =>
                row.module ? <span className="text-(--foreground)">{row.module}</span> : <span className="text-(--muted-foreground)">—</span>,
        },
        {
            title: "操作内容",
            dataIndex: "log",
            render: (_, row) =>
                row.log ? (
                    <span className="break-words text-(--foreground)">{row.log}</span>
                ) : (
                    <span className="text-(--muted-foreground)">—</span>
                ),
        },
    ]

    if (!accountSetId) {
        return (
            <Card>
                <span className="text-[13px] text-(--muted-foreground)">请先在左上角选择账套。</span>
            </Card>
        )
    }

    const emptyText = hasFilter ? (
        <div className="flex flex-col items-center gap-2 py-10">
            <SearchOutlined style={{ fontSize: 26, color: "var(--icon-placeholder)" }} />
            <span className="text-[13px] text-(--muted-foreground)">没有匹配当前筛选条件的操作记录</span>
            <Button type="link" size="small" onClick={resetFilters}>
                清除筛选条件
            </Button>
        </div>
    ) : (
        <div className="flex flex-col items-center gap-2 py-10">
            <ClockCircleOutlined style={{ fontSize: 28, color: "var(--icon-placeholder)" }} />
            <div className="text-center">
                <div className="text-[13px] text-(--muted-foreground)">该时间范围内没有操作记录</div>
                <div className="mt-1 text-[12px] text-(--muted-foreground)">调整时间范围，或下拉选择「近 30 天 / 上月」</div>
            </div>
        </div>
    )

    return (
        <Card>
            <div className="flex flex-wrap items-start justify-between gap-3 px-4 pt-4">
                <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                        <h2 className="text-[15px] font-semibold text-(--foreground)">操作日志</h2>
                        <StatusPill tone="gray">只读 · 审计留痕</StatusPill>
                    </div>
                    <p className="mt-1 text-[12.5px] leading-5 text-(--muted-foreground)">
                        记录凭证制单、修改、反过账、导入以及期末结转结账等关键操作。日志只增不改不删，可按时间、类型、模块与操作人筛选，并导出当前结果。
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <Button icon={<ReloadOutlined spin={loading} />} onClick={() => void load()} disabled={loading}>
                        刷新
                    </Button>
                    <Button icon={<DownloadOutlined />} onClick={exportCsv} disabled={filtered.length === 0}>
                        导出
                    </Button>
                </div>
            </div>

            <div className="flex min-h-[56px] flex-wrap items-center gap-2 px-4 py-3">
                <DatePicker.RangePicker
                    allowClear={false}
                    value={range}
                    presets={RANGE_PRESETS}
                    placeholder={["开始日期", "结束日期"]}
                    style={{ width: 260 }}
                    onChange={(value) => {
                        if (value?.[0] && value[1]) {
                            setRange([value[0].startOf("day"), value[1].endOf("day")])
                            setPage(1)
                        }
                    }}
                />
                <Select
                    allowClear
                    placeholder="日志类型"
                    style={{ width: 130 }}
                    value={typeFilter}
                    options={typeOptions.map((value) => ({ value, label: value }))}
                    onChange={(value) => {
                        setTypeFilter(value)
                        setPage(1)
                    }}
                />
                <Select
                    allowClear
                    placeholder="所属模块"
                    style={{ width: 150 }}
                    value={moduleFilter}
                    options={moduleOptions.map((value) => ({ value, label: value }))}
                    onChange={(value) => {
                        setModuleFilter(value)
                        setPage(1)
                    }}
                />
                <Select
                    allowClear
                    showSearch
                    optionFilterProp="label"
                    placeholder="操作人"
                    style={{ width: 150 }}
                    value={userFilter}
                    options={userOptions.map((value) => ({ value, label: value }))}
                    onChange={(value) => {
                        setUserFilter(value)
                        setPage(1)
                    }}
                />
                <Input
                    allowClear
                    style={{ width: 240 }}
                    prefix={<SearchOutlined style={{ color: "var(--icon-muted)" }} />}
                    placeholder="搜索操作内容 / 模块 / 操作人"
                    value={keyword}
                    onChange={(e) => {
                        setKeyword(e.target.value)
                        setPage(1)
                    }}
                />
                {hasFilter && (
                    <Button type="link" size="small" onClick={resetFilters}>
                        重置筛选
                    </Button>
                )}
            </div>

            {loadError && (
                <div className="px-4 pb-3">
                    <Alert type="error" showIcon title={loadError} closable onClose={() => setLoadError(null)} />
                </div>
            )}

            <div className="px-3">
                <Table
                    columns={columns}
                    dataSource={filtered}
                    rowKey={(row) => row.id ?? `${row.time}-${row.log}`}
                    loading={loading}
                    locale={{ emptyText }}
                    scroll={{ x: 860 }}
                    pagination={{
                        current: page,
                        pageSize,
                        pageSizeOptions: PAGE_SIZE_OPTIONS,
                        showSizeChanger: true,
                        showTotal: (total) => `共 ${total} 条`,
                        onChange: (nextPage, nextPageSize) => {
                            setPage(nextPage)
                            setPageSize(nextPageSize)
                        },
                    }}
                />
            </div>

            <div className="flex flex-wrap items-center justify-between gap-2 px-4 py-2.5 text-[12.5px] text-(--muted-foreground)">
                <span>
                    共 <span className="font-medium tabular-nums text-(--foreground)">{rows.length}</span> 条
                    {hasFilter && (
                        <>
                            {" "}· 筛选出 <span className="font-medium tabular-nums text-(--foreground)">{filtered.length}</span> 条
                        </>
                    )}
                    {" "}· 时间范围 {range[0].format("YYYY-MM-DD")} ~ {range[1].format("YYYY-MM-DD")}
                </span>
                <span className="inline-flex items-center gap-1 text-(--muted-foreground)">
                    <SafetyCertificateOutlined />
                    日志只增不改不删，导出为当前筛选结果
                </span>
            </div>
        </Card>
    )
}
