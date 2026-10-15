import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { Alert, Button, Drawer, Pagination, Segmented, Spin, Table } from "antd"
import type { TableColumnsType } from "antd"
import {
    AccountBookOutlined,
    CheckCircleOutlined,
    DeleteOutlined,
    DownOutlined,
    EditOutlined,
    FileExcelOutlined,
    FileTextOutlined,
    PlusOutlined,
    ReloadOutlined,
    RightOutlined,
    RollbackOutlined,
    UploadOutlined,
} from "@ant-design/icons"
import dayjs from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AccountsApi,
    VouchersApi,
    type AccountDto,
    type VoucherDetailDto,
    type VoucherEntryDto,
    type VoucherImportResult,
    type VoucherListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount, fmtMoney, toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useConfirm } from "@/hooks/useConfirm"
import { PageHeader, SectionCard } from "@/components/page/section"
import { Tile, type TileTone } from "@/components/page/tile"
import { StatusPill as StatusPillBase } from "@/components/page/status-pill"
import { ICON_STYLE, SOFT_PANEL, voucherStatusTone } from "@/components/page/styles"
import { DateRangeField, parseDate } from "@/components/fields/date-fields"
import VoucherForm from "./voucher-form"

/** 日期范围。 */
interface DateRange {
    startDate: string
    endDate: string
}

/** 每页条数候选，等价原 antd 分页的 showSizeChanger。 */
const PAGE_SIZE_OPTIONS = ["10", "20", "50", "100"]

/** 原 antd 分页默认每页 20 条。 */
const DEFAULT_PAGE_SIZE = 20

const ACCEPT = ".xls,.xlsx"

/** 借贷金额文本：负数用红色标注，正值回归常规前景色。 */
function AmountText({ value }: { value: number | undefined }) {
    const v = value ?? 0
    return <span className={cn("font-medium", v < 0 ? "text-[#ef4444]" : "text-slate-700")}>{fmtAmount(v)}</span>
}

/** 状态胶囊：配色口径见共享的 voucherStatusTone。 */
function StatusPill({ record }: { record: VoucherListItemDto }) {
    return (
        <StatusPillBase tone={voucherStatusTone(record.status, record.isBalanced)}>
            {record.statusText}
        </StatusPillBase>
    )
}

/** 展开行的分录明细（内嵌子表、无表头），等价原 ENTRY_COLUMNS + showHeader={false}。 */
function EntryTable({ entries }: { entries: VoucherEntryDto[] }) {
    return (
        <table className="w-full text-[12.5px]">
            <tbody>
                {entries.map((e) => (
                    <tr key={e.id} className="border-b border-slate-100 last:border-0">
                        <td className="max-w-[320px] py-2 pr-3 pl-4 text-slate-600">
                            <div className="truncate">{e.summary}</div>
                        </td>
                        <td className="py-2 pr-3 whitespace-nowrap text-slate-600">
                            <span className="text-slate-400">{e.accountCode} </span>
                            {e.accountName}
                        </td>
                        {(["debit", "credit"] as const).map((key) => {
                            const v = e[key]
                            return (
                                <td key={key} className="w-[130px] py-2 pr-4 text-right tabular-nums">
                                    {v != null && v !== 0 ? (
                                        <span className={cn(v < 0 ? "text-[#ef4444]" : "text-slate-700")}>
                                            {fmtAmount(v)}
                                        </span>
                                    ) : null}
                                </td>
                            )
                        })}
                    </tr>
                ))}
            </tbody>
        </table>
    )
}

/** 展开行底部的制单与借贷合计条。 */
function EntryTotals({ record }: { record: VoucherListItemDto }) {
    return (
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 bg-white/70 px-4 py-2 text-[12.5px]">
            <span className="text-slate-400">
                制单：{record.preparer ?? "—"}
                {record.reviewer ? ` · 审核：${record.reviewer}` : ""}
            </span>
            <div className="flex items-center gap-2">
                <span className="rounded-lg bg-[#dbeafe] px-2.5 py-1 text-slate-600">
                    <span className="text-slate-400">借方 </span>
                    <AmountText value={record.totalDebit} />
                </span>
                <span className="rounded-lg bg-[#d1fae5] px-2.5 py-1 text-slate-600">
                    <span className="text-slate-400">贷方 </span>
                    <AmountText value={record.totalCredit} />
                </span>
            </div>
        </div>
    )
}

/** 凭证列表主表格列（接收操作回调）。 */
function getVoucherColumns(ops: {
    onEdit: (v: VoucherListItemDto) => void
    onDelete: (v: VoucherListItemDto) => void
    onReview: (v: VoucherListItemDto) => void
    onUnreview: (v: VoucherListItemDto) => void
    onPost: (v: VoucherListItemDto) => void
    onToggle: (id: string) => void
    expandedIds: Set<string>
    openingId: string | null
    deletingId: string | null
    workflowId: string | null
}): TableColumnsType<VoucherListItemDto> {
    return [
        {
            title: "凭证号",
            render: (_, r) => {
                const id = r.id ?? ""
                const expanded = ops.expandedIds.has(id)
                return (
                    <div className="flex items-center gap-1.5">
                        <Button
                            type="text"
                            size="small"
                            title={expanded ? "收起分录" : "展开分录"}
                            aria-expanded={expanded}
                            icon={expanded ? <DownOutlined /> : <RightOutlined />}
                            onClick={() => ops.onToggle(id)}
                        />
                        <span className="font-medium text-slate-700">{r.voucherNo}</span>
                        <StatusPill record={r} />
                    </div>
                )
            },
        },
        {
            title: "日期",
            className: "whitespace-nowrap",
            render: (_, r) => <span className="text-slate-500">{toISODate(r.date)}</span>,
        },
        { title: "借方合计", align: "right", className: "tabular-nums", render: (_, r) => <AmountText value={r.totalDebit} /> },
        { title: "贷方合计", align: "right", className: "tabular-nums", render: (_, r) => <AmountText value={r.totalCredit} /> },
        { title: "制单人", render: (_, r) => <span className="text-slate-500">{r.preparer ?? "—"}</span> },
        {
            title: "操作",
            align: "right",
            render: (_, r) => (
                <div className="flex items-center justify-end gap-0">
                    {r.status === 1 && (
                        <Button
                            type="text"
                            size="small"
                            title="编辑凭证"
                            loading={ops.openingId === r.id}
                            disabled={ops.openingId === r.id}
                            icon={<EditOutlined />}
                            onClick={() => ops.onEdit(r)}
                        />
                    )}
                    {r.status === 1 && (
                        <Button
                            type="text"
                            size="small"
                            title="审核凭证"
                            loading={ops.workflowId === r.id}
                            disabled={ops.workflowId === r.id}
                            icon={<CheckCircleOutlined style={{ color: "#10b981" }} />}
                            onClick={() => ops.onReview(r)}
                        />
                    )}
                    {r.status === 2 && (
                        <Button
                            type="text"
                            size="small"
                            title="反审核（退回草稿）"
                            loading={ops.workflowId === r.id}
                            disabled={ops.workflowId === r.id}
                            icon={<RollbackOutlined />}
                            onClick={() => ops.onUnreview(r)}
                        />
                    )}
                    {r.status === 2 && (
                        <Button
                            type="text"
                            size="small"
                            title="过账（登记入账锁定）"
                            loading={ops.workflowId === r.id}
                            disabled={ops.workflowId === r.id}
                            icon={<AccountBookOutlined style={{ color: "#10b981" }} />}
                            onClick={() => ops.onPost(r)}
                        />
                    )}
                    {r.status !== 3 && (
                        <Button
                            type="text"
                            size="small"
                            danger
                            title="删除凭证"
                            loading={ops.deletingId === r.id}
                            disabled={ops.deletingId === r.id}
                            icon={<DeleteOutlined />}
                            onClick={() => ops.onDelete(r)}
                        />
                    )}
                </div>
            ),
        },
    ]
}

/** 凭证状态瓦片的取数口径。 */
function buildTiles(vouchers: VoucherListItemDto[]) {
    const total = vouchers.length
    const draft = vouchers.filter((v) => v.status === 1).length
    const reviewed = vouchers.filter((v) => v.status === 2).length
    const posted = vouchers.filter((v) => v.status === 3).length
    const debitSum = vouchers.reduce((sum, v) => sum + (v.totalDebit ?? 0), 0)
    const pct = (n: number) => (total === 0 ? 0 : n / total)
    const tiles: {
        label: string
        value: number
        tone: TileTone
        icon: React.ComponentType<{ className?: string }>
        hint: string
        ratio?: number
    }[] = [
        {
            label: "凭证总数",
            value: total,
            tone: "blue",
            icon: FileTextOutlined,
            hint: `借方合计 ${fmtMoney(debitSum)}`,
        },
        {
            label: "草稿待审",
            value: draft,
            tone: "amber",
            icon: EditOutlined,
            hint: `${Math.round(pct(draft) * 100)}%`,
            ratio: pct(draft),
        },
        {
            label: "已审核待过账",
            value: reviewed,
            tone: "purple",
            icon: CheckCircleOutlined,
            hint: `${Math.round(pct(reviewed) * 100)}%`,
            ratio: pct(reviewed),
        },
        {
            label: "已过账入册",
            value: posted,
            tone: "green",
            icon: AccountBookOutlined,
            hint: `${Math.round(pct(posted) * 100)}%`,
            ratio: pct(posted),
        },
    ]
    return tiles
}

export default function VoucherManagePage() {
    const { token, user } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { confirm, confirmDialog } = useConfirm()

    const [vouchers, setVouchers] = useState<VoucherListItemDto[]>([])
    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [open, setOpen] = useState(false)
    const [editing, setEditing] = useState<VoucherDetailDto | null>(null)
    const [openingId, setOpeningId] = useState<string | null>(null)
    const [deletingId, setDeletingId] = useState<string | null>(null)
    const [workflowId, setWorkflowId] = useState<string | null>(null)
    const [dateRange, setDateRange] = useState<DateRange | null>(null)
    // 展开的分录行集合；原先 defaultExpandAllRows 等价于加载后全部纳入该集合。
    const [expandedIds, setExpandedIds] = useState<Set<string>>(() => new Set())

    // 客户端分页：直接对数组切页，不启用表格分页特性。
    const [pageIndex, setPageIndex] = useState(0)
    const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE)

    // ── 凭证导入相关状态 ──
    const importInputRef = useRef<HTMLInputElement>(null)
    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<File | null>(null)
    const [importDragging, setImportDragging] = useState(false)
    const [importing, setImporting] = useState(false)
    const [importConflict, setImportConflict] = useState<0 | 1>(0)
    const [importResult, setImportResult] = useState<VoucherImportResult | null>(null)
    const [importError, setImportError] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const config = apiConfig(token)
            const [list, accs] = await Promise.all([
                new VouchersApi(config).accountsetsAccountSetIdVouchersGet({
                    accountSetId,
                    startDate: dateRange?.startDate ? new Date(dateRange.startDate) : undefined,
                    endDate: dateRange?.endDate ? new Date(dateRange.endDate) : undefined,
                }),
                new AccountsApi(config).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true }),
            ])
            setVouchers(list)
            setAccounts(accs)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, dateRange])

    useEffect(() => {
        void load()
    }, [load])

    // 当 URL hash 为 #import 时自动打开导入抽屉并清除 hash
    useEffect(() => {
        if (window.location.hash === "#import") {
            setImportOpen(true)
            window.history.replaceState(null, "", window.location.pathname + window.location.search)
        }
    }, [])

    // 列表变化后默认展开全部分录，等价原 defaultExpandAllRows。
    useEffect(() => {
        setExpandedIds(new Set(vouchers.map((v) => v.id ?? "")))
    }, [vouchers])

    function toggleExpand(id: string) {
        setExpandedIds((prev) => {
            const next = new Set(prev)
            if (next.has(id)) next.delete(id)
            else next.add(id)
            return next
        })
    }

    async function handleDelete(v: VoucherListItemDto) {
        if (deletingId || !v.id) return
        if (!(await confirm(`确定删除凭证「${v.voucherNo}」（${toISODate(v.date)}）吗？此操作不可撤销。`))) return
        setDeletingId(v.id)
        setError(null)
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdDelete({
                accountSetId,
                id: v.id,
            })
            setVouchers((prev) => prev.filter((x) => x.id !== v.id))
        } catch (err) {
            setError(await apiErrorMessage(err, "删除失败，请重试。"))
        } finally {
            setDeletingId(null)
        }
    }

    async function handleEdit(v: VoucherListItemDto) {
        if (openingId || !v.id) return
        setOpeningId(v.id)
        setError(null)
        try {
            const detail = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdGet({
                accountSetId,
                id: v.id,
            })
            setEditing(detail)
            setOpen(true)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载凭证详情失败，请重试。"))
        } finally {
            setOpeningId(null)
        }
    }

    function handleCreate() {
        setEditing(null)
        setOpen(true)
    }

    /** 凭证状态流转（审核/反审核/过账）：调用后刷新列表。 */
    async function handleWorkflow(v: VoucherListItemDto, action: "review" | "unreview" | "post") {
        if (workflowId || !v.id) return
        if (action === "post" && !(await confirm(`确定过账凭证「${v.voucherNo}」吗？过账后凭证将登记入账并锁定，不可再修改或删除。`))) return
        setWorkflowId(v.id)
        setError(null)
        try {
            const api = new VouchersApi(apiConfig(token))
            const params = { accountSetId, id: v.id }
            const updated =
                action === "review"
                    ? await api.accountsetsAccountSetIdVouchersIdReviewPost(params)
                    : action === "unreview"
                        ? await api.accountsetsAccountSetIdVouchersIdUnreviewPost(params)
                        : await api.accountsetsAccountSetIdVouchersIdPostPost(params)
            // 就地更新该张凭证的状态与审核人，避免整表重拉。
            setVouchers((prev) =>
                prev.map((x) =>
                    x.id === v.id
                        ? { ...x, status: updated.status, statusText: updated.statusText, reviewer: updated.reviewer }
                        : x,
                ),
            )
        } catch (err) {
            const fallback = action === "review" ? "审核失败，请重试。" : action === "unreview" ? "反审核失败，请重试。" : "过账失败，请重试。"
            setError(await apiErrorMessage(err, fallback))
        } finally {
            setWorkflowId(null)
        }
    }

    function handleSaved() {
        setOpen(false)
        setEditing(null)
        void load()
    }

    // ── 凭证导入处理 ──
    function pickImportFile(next: File | null) {
        setImportFile(next)
        setImportResult(null)
        setImportError(null)
    }

    function onImportDrop(e: React.DragEvent<HTMLDivElement>) {
        e.preventDefault()
        setImportDragging(false)
        const dropped = e.dataTransfer.files?.[0]
        if (dropped) pickImportFile(dropped)
    }

    async function handleImport() {
        if (!importFile || importing) return
        if (!accountSetId) {
            setImportError("请先选择账套。")
            return
        }
        setImporting(true)
        setImportError(null)
        setImportResult(null)
        try {
            const res = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersImportPost({
                accountSetId,
                file: importFile,
                conflict: importConflict,
            })
            setImportResult(res)
            void load()
        } catch (err) {
            setImportError(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    const columns = getVoucherColumns({
        onEdit: handleEdit,
        onDelete: handleDelete,
        onReview: (v) => void handleWorkflow(v, "review"),
        onUnreview: (v) => void handleWorkflow(v, "unreview"),
        onPost: (v) => void handleWorkflow(v, "post"),
        onToggle: toggleExpand,
        expandedIds,
        openingId,
        deletingId,
        workflowId,
    })

    // 数据变少时把页码夹回有效范围，避免停留在空页。
    const pageCount = Math.max(1, Math.ceil(vouchers.length / pageSize))
    const currentPage = Math.min(pageIndex, pageCount - 1)
    const pageRows = vouchers.slice(currentPage * pageSize, currentPage * pageSize + pageSize)

    const expandedRowKeys = useMemo(() => [...expandedIds], [expandedIds])

    return (
        <div className="flex flex-col gap-5">
            <PageHeader
                icon={FileTextOutlined}
                title="凭证管理"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        {currentSet?.isCurrent ? "（当前）" : ""} · 共 {vouchers.length} 张凭证
                    </>
                }
                actions={
                    <>
                        <Button
                            icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                            disabled={!accountSetId || loading}
                            onClick={() => void load()}
                        >
                            刷新
                        </Button>
                        <Button icon={<UploadOutlined />} disabled={!accountSetId} onClick={() => setImportOpen(true)}>
                            导入凭证
                        </Button>
                        <Button type="primary" icon={<PlusOutlined />} disabled={!accountSetId} onClick={handleCreate}>
                            新增凭证
                        </Button>
                    </>
                }
            />

            {/* 状态瓦片 */}
            <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
                {buildTiles(vouchers).map((t) => (
                    <Tile key={t.label} {...t} />
                ))}
            </section>

            {error && <Alert type="error" showIcon title={error} />}

            {/* 凭证列表 */}
            <SectionCard
                title={
                    <>
                        <h3 className="text-[14px] font-semibold text-slate-800">凭证列表</h3>
                        {loading && <Spin size="small" />}
                    </>
                }
                bodyClassName="p-0"
                action={
                    <div className="flex flex-wrap items-center gap-2">
                        <span className="text-[12px] text-slate-400">日期范围</span>
                        <DateRangeField
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
                                    setDateRange(null)
                                }
                            }}
                            disabled={!accountSetId}
                            allowClear
                        />
                    </div>
                }
            >
                <Table<VoucherListItemDto>
                    rowKey={(row) => row.id ?? ""}
                    columns={columns}
                    dataSource={pageRows}
                    pagination={false}
                    size="small"
                    loading={loading && vouchers.length === 0}
                    locale={{ emptyText: "暂无凭证，点击右上角「新增凭证」开始记账。" }}
                    // 展开行 = 分录明细 + 合计条；展开按钮自绘在凭证号列，故隐藏默认列。
                    expandable={{
                        showExpandColumn: false,
                        expandedRowKeys,
                        expandedRowRender: (record) => (
                            <>
                                {(record.entries?.length ?? 0) > 0 && <EntryTable entries={record.entries ?? []} />}
                                <EntryTotals record={record} />
                            </>
                        ),
                    }}
                />

                {/* 客户端分页 */}
                <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-100 px-5 py-3">
                    <span className="whitespace-nowrap text-[12.5px] text-slate-400">共 {vouchers.length} 张凭证</span>
                    <Pagination
                        size="small"
                        current={currentPage + 1}
                        pageSize={pageSize}
                        total={vouchers.length}
                        pageSizeOptions={PAGE_SIZE_OPTIONS}
                        showSizeChanger
                        onChange={(page, size) => {
                            if (size !== pageSize) {
                                setPageSize(size)
                                setPageIndex(0)
                            } else {
                                setPageIndex(page - 1)
                            }
                        }}
                    />
                </div>
            </SectionCard>

            {/* 新增 / 编辑凭证抽屉 */}
            <Drawer
                open={open}
                size={1152}
                title={editing ? `编辑凭证 ${editing.voucherNo}` : "新增凭证"}
                onClose={() => {
                    setOpen(false)
                    setEditing(null)
                }}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
            >
                {open && (
                    <VoucherForm
                        key={editing?.id ?? "new"}
                        accountSetId={accountSetId}
                        accounts={accounts}
                        preparer={user?.displayName}
                        token={token ?? undefined}
                        initial={editing ?? undefined}
                        onSaved={handleSaved}
                        onCancel={() => {
                            setOpen(false)
                            setEditing(null)
                        }}
                    />
                )}
            </Drawer>

            {/* 导入凭证抽屉 */}
            <Drawer
                open={importOpen}
                size={672}
                title="导入凭证"
                onClose={() => {
                    setImportOpen(false)
                    pickImportFile(null)
                }}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
            >
                <p className="text-[12.5px] text-slate-400">
                    支持导入其他平台导出的记账凭证文件（.xls / .xlsx）。系统按凭证号聚合分录、自动补建缺失科目，
                    借贷金额原样保留。
                </p>

                <div className="flex flex-col gap-1">
                    <span className="font-medium">导入到账套</span>
                    <span className="text-[12.5px] text-slate-400">
                        {currentSet?.name ?? accountSetId}
                        {currentSet?.isCurrent ? "（当前）" : ""}
                    </span>
                </div>

                {/* 冲突策略 */}
                <div className="flex flex-col gap-2">
                    <span className="text-[13px] font-semibold text-slate-700">重复凭证处理</span>
                    <Segmented
                        value={String(importConflict)}
                        options={[
                            { value: "0", label: "跳过" },
                            { value: "1", label: "覆盖" },
                        ]}
                        onChange={(v) => setImportConflict(v === "1" ? 1 : 0)}
                    />
                </div>

                {/* 文件拖放 / 选择区 */}
                <div
                    role="button"
                    tabIndex={0}
                    onClick={() => importInputRef.current?.click()}
                    onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") importInputRef.current?.click()
                    }}
                    onDragOver={(e) => {
                        e.preventDefault()
                        setImportDragging(true)
                    }}
                    onDragLeave={() => setImportDragging(false)}
                    onDrop={onImportDrop}
                    className={cn(
                        "flex cursor-pointer flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-10 text-center transition-colors",
                        importDragging ? "border-[#bfdbfe] bg-[#eef4ff]" : "border-slate-200",
                    )}
                >
                    <FileExcelOutlined className="text-[40px]" style={ICON_STYLE.placeholder} />
                    {importFile ? (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">{importFile.name}</span>
                            <span className="text-sm leading-normal font-normal text-slate-400">
                                {(importFile.size / 1024).toFixed(1)} KB · 点击可重新选择
                            </span>
                        </div>
                    ) : (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">点击选择，或拖拽文件到此处</span>
                            <span className="text-sm leading-normal font-normal text-slate-400">支持 .xls / .xlsx，最大 10MB</span>
                        </div>
                    )}
                    <input
                        ref={importInputRef}
                        type="file"
                        accept={ACCEPT}
                        className="hidden"
                        onChange={(e) => pickImportFile(e.target.files?.[0] ?? null)}
                    />
                </div>

                <div className="flex items-center gap-3">
                    <Button
                        type="primary"
                        loading={importing}
                        icon={importing ? undefined : <UploadOutlined />}
                        disabled={!importFile || !accountSetId}
                        onClick={() => void handleImport()}
                    >
                        {importing ? "导入中…" : "开始导入"}
                    </Button>
                    {importFile && !importing && <Button onClick={() => pickImportFile(null)}>清除</Button>}
                </div>

                {importError && <Alert type="error" showIcon title={importError} />}

                {/* 导入结果 */}
                {importResult && (
                    <div className={cn(SOFT_PANEL, "flex flex-col gap-4 p-4")}>
                        <h3 className="text-[13px] font-semibold text-slate-800">导入结果</h3>
                        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
                            <Tile label="解析行数" value={importResult.totalRows ?? 0} tone="blue" icon={FileExcelOutlined} />
                            <Tile label="导入凭证" value={importResult.importedVouchers ?? 0} tone="green" icon={CheckCircleOutlined} />
                            <Tile label="导入分录" value={importResult.importedEntries ?? 0} tone="purple" icon={FileTextOutlined} />
                            <Tile label="新建科目" value={importResult.createdAccounts ?? 0} tone="amber" icon={PlusOutlined} />
                        </div>
                        <div className="grid grid-cols-2 gap-3">
                            <Tile label="覆盖凭证" value={importResult.overwrittenVouchers ?? 0} tone="gray" icon={RollbackOutlined} />
                            <Tile label="跳过凭证" value={importResult.skippedVouchers ?? 0} tone="gray" icon={RightOutlined} />
                        </div>

                        {importResult.warnings && importResult.warnings.length > 0 && (
                            <div className="flex flex-col gap-2">
                                <span className="text-[12.5px] text-slate-400">
                                    告警（{importResult.warnings.length}）
                                </span>
                                <ul className="max-h-64 overflow-auto rounded-lg border border-slate-100 bg-white p-3 text-[12.5px] text-slate-500">
                                    {importResult.warnings.map((w, i) => (
                                        <li key={i} className="py-0.5">
                                            · {w}
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        )}
                    </div>
                )}
            </Drawer>

            {confirmDialog}
        </div>
    )
}
