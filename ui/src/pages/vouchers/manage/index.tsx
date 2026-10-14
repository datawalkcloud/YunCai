import { Fragment, useCallback, useEffect, useRef, useState } from "react"
import {
    CheckCircle2,
    ChevronDown,
    ChevronRight,
    CircleAlert,
    ClipboardCheck,
    FileSpreadsheet,
    FileText,
    Pencil,
    Plus,
    RefreshCw,
    Trash2,
    Undo2,
    Upload,
} from "lucide-react"
import { createColumnHelper, tableFeatures, useTable } from "@tanstack/react-table"
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
import {
    DATE_FIELD,
    ICON_BTN,
    ICON_BTN_DANGER,
    ICON_BTN_SUCCESS,
    PAGE_BTN_GHOST,
    PAGE_BTN_PRIMARY,
    SOFT_PANEL,
    voucherStatusTone,
} from "@/components/page/styles"
import { DateRangeField, parseDate } from "@/components/fields/date-fields"
import { Alert, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Pagination, PaginationContent, PaginationEllipsis, PaginationItem, PaginationLink, PaginationNext, PaginationPrevious } from "@/components/ui/pagination"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group"
import VoucherForm from "./voucher-form"

/** 日期范围。 */
interface DateRange {
    startDate: string
    endDate: string
}

/** 每页条数候选，等价原 antd 分页的 showSizeChanger。 */
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100]

/** 原 antd 分页默认每页 20 条。 */
const DEFAULT_PAGE_SIZE = 20

const ACCEPT = ".xls,.xlsx"
const features = tableFeatures({})
const voucherColumn = createColumnHelper<typeof features, VoucherListItemDto>()

/** 行以凭证 id 为键，等价原先的 rowKey="id"。 */
const getRowId = (row: VoucherListItemDto) => row.id ?? ""

/** 各列的对齐方式按列 id 取用，避免写行内样式。 */
const COLUMN_CLASS: Record<string, { head: string, cell: string }> = {
    voucherNo: { head: "text-left", cell: "text-left" },
    date: { head: "text-left", cell: "text-left whitespace-nowrap" },
    totalDebit: { head: "text-right", cell: "text-right tabular-nums" },
    totalCredit: { head: "text-right", cell: "text-right tabular-nums" },
    preparer: { head: "text-left", cell: "text-left" },
    actions: { head: "text-right", cell: "text-right" },
}

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
}) {
    return [
        voucherColumn.display({
            id: "voucherNo",
            header: "凭证号",
            cell: ({ row }) => {
                const r = row.original
                const id = r.id ?? ""
                const expanded = ops.expandedIds.has(id)
                return (
                    <div className="flex items-center gap-1.5">
                        <Button
                            variant="ghost"
                            size="icon-xs"
                            title={expanded ? "收起分录" : "展开分录"}
                            aria-expanded={expanded}
                            onClick={() => ops.onToggle(id)}
                            className={ICON_BTN}
                        >
                            {expanded ? <ChevronDown /> : <ChevronRight />}
                        </Button>
                        <span className="font-medium text-slate-700">{r.voucherNo}</span>
                        <StatusPill record={r} />
                    </div>
                )
            },
        }),
        voucherColumn.display({
            id: "date",
            header: "日期",
            cell: ({ row }) => <span className="text-slate-500">{toISODate(row.original.date)}</span>,
        }),
        voucherColumn.display({
            id: "totalDebit",
            header: "借方合计",
            cell: ({ row }) => <AmountText value={row.original.totalDebit} />,
        }),
        voucherColumn.display({
            id: "totalCredit",
            header: "贷方合计",
            cell: ({ row }) => <AmountText value={row.original.totalCredit} />,
        }),
        voucherColumn.display({
            id: "preparer",
            header: "制单人",
            cell: ({ row }) => (
                <span className="text-slate-500">{row.original.preparer ?? "—"}</span>
            ),
        }),
        voucherColumn.display({
            id: "actions",
            header: "操作",
            cell: ({ row }) => {
                const r = row.original
                return (
                    <div className="flex items-center justify-end gap-0.5">
                        {r.status === 1 && (
                            <Button
                                variant="ghost"
                                size="icon-xs"
                                disabled={ops.openingId === r.id}
                                onClick={() => ops.onEdit(r)}
                                title="编辑凭证"
                                className={ICON_BTN}
                            >
                                {ops.openingId === r.id ? <Spinner /> : <Pencil />}
                            </Button>
                        )}
                        {r.status === 1 && (
                            <Button
                                variant="ghost"
                                size="icon-xs"
                                disabled={ops.workflowId === r.id}
                                onClick={() => ops.onReview(r)}
                                title="审核凭证"
                                className={ICON_BTN_SUCCESS}
                            >
                                {ops.workflowId === r.id ? <Spinner /> : <CheckCircle2 />}
                            </Button>
                        )}
                        {r.status === 2 && (
                            <Button
                                variant="ghost"
                                size="icon-xs"
                                disabled={ops.workflowId === r.id}
                                onClick={() => ops.onUnreview(r)}
                                title="反审核（退回草稿）"
                                className={ICON_BTN}
                            >
                                {ops.workflowId === r.id ? <Spinner /> : <Undo2 />}
                            </Button>
                        )}
                        {r.status === 2 && (
                            <Button
                                variant="ghost"
                                size="icon-xs"
                                disabled={ops.workflowId === r.id}
                                onClick={() => ops.onPost(r)}
                                title="过账（登记入账锁定）"
                                className={ICON_BTN_SUCCESS}
                            >
                                {ops.workflowId === r.id ? <Spinner /> : <ClipboardCheck />}
                            </Button>
                        )}
                        {r.status !== 3 && (
                            <Button
                                variant="ghost"
                                size="icon-xs"
                                disabled={ops.deletingId === r.id}
                                onClick={() => ops.onDelete(r)}
                                title="删除凭证"
                                className={ICON_BTN_DANGER}
                            >
                                {ops.deletingId === r.id ? <Spinner /> : <Trash2 />}
                            </Button>
                        )}
                    </div>
                )
            },
        }),
    ]
}

/** 生成分页页码序列：页数过多时首尾保留、中间折叠为省略号。 */
function pageItems(current: number, total: number): (number | "gap")[] {
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
    const items: (number | "gap")[] = [1]
    const start = Math.max(2, current - 1)
    const end = Math.min(total - 1, current + 1)
    if (start > 2) items.push("gap")
    for (let p = start; p <= end; p++) items.push(p)
    if (end < total - 1) items.push("gap")
    items.push(total)
    return items
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
            icon: FileText,
            hint: `借方合计 ${fmtMoney(debitSum)}`,
        },
        {
            label: "草稿待审",
            value: draft,
            tone: "amber",
            icon: Pencil,
            hint: `${Math.round(pct(draft) * 100)}%`,
            ratio: pct(draft),
        },
        {
            label: "已审核待过账",
            value: reviewed,
            tone: "purple",
            icon: CheckCircle2,
            hint: `${Math.round(pct(reviewed) * 100)}%`,
            ratio: pct(reviewed),
        },
        {
            label: "已过账入册",
            value: posted,
            tone: "green",
            icon: ClipboardCheck,
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

    const table = useTable({ features, columns, data: pageRows, getRowId })
    const headerGroups = table.getHeaderGroups()
    const rows = table.getRowModel().rows
    const leafCount = table.getLeafHeaders().length

    return (
        <div className="flex flex-col gap-5">
            <PageHeader
                icon={FileText}
                title="凭证管理"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        {currentSet?.isCurrent ? "（当前）" : ""} · 共 {vouchers.length} 张凭证
                    </>
                }
                actions={
                    <>
                        <Button variant="outline" onClick={() => void load()} disabled={!accountSetId || loading} className={PAGE_BTN_GHOST}>
                            {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                            刷新
                        </Button>
                        <Button variant="outline" onClick={() => setImportOpen(true)} disabled={!accountSetId} className={PAGE_BTN_GHOST}>
                            <Upload data-icon="inline-start" />
                            导入凭证
                        </Button>
                        <Button onClick={handleCreate} disabled={!accountSetId} className={PAGE_BTN_PRIMARY}>
                            <Plus data-icon="inline-start" />
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

            {error && (
                <Alert variant="destructive" className="rounded-2xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                    <CircleAlert />
                    <AlertTitle>{error}</AlertTitle>
                </Alert>
            )}

            {/* 凭证列表 */}
            <SectionCard
                title={
                    <>
                        <h3 className="text-[14px] font-semibold text-slate-800">凭证列表</h3>
                        {loading && <Spinner className="size-3.5 text-slate-300" />}
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
                            className={DATE_FIELD}
                        />
                    </div>
                }
            >
                <div className="px-2">
                    <Table>
                        <TableHeader>
                            {headerGroups.map((headerGroup) => (
                                <TableRow key={headerGroup.id} className="border-slate-100 hover:bg-transparent">
                                    {headerGroup.headers.map((header) => (
                                        <TableHead
                                            key={header.id}
                                            className={COLUMN_CLASS[header.column.id]?.head}
                                        >
                                            <table.FlexRender header={header} />
                                        </TableHead>
                                    ))}
                                </TableRow>
                            ))}
                        </TableHeader>
                        <TableBody>
                            {loading && rows.length === 0 ? (
                                <TableRow className="hover:bg-transparent">
                                    <TableCell colSpan={leafCount} className="py-10 text-center">
                                        <Spinner className="mx-auto size-5 text-slate-300" />
                                    </TableCell>
                                </TableRow>
                            ) : rows.length === 0 ? (
                                <TableRow className="hover:bg-transparent">
                                    <TableCell colSpan={leafCount} className="py-10 text-center text-[13px] text-slate-400">
                                        暂无凭证，点击右上角「新增凭证」开始记账。
                                    </TableCell>
                                </TableRow>
                            ) : (
                                rows.map((row) => {
                                    const record = row.original
                                    const id = record.id ?? ""
                                    const expanded = expandedIds.has(id)
                                    return (
                                        <Fragment key={row.id}>
                                            <TableRow>
                                                {row.getAllCells().map((cell) => (
                                                    <TableCell key={cell.id} className={COLUMN_CLASS[cell.column.id]?.cell}>
                                                        <table.FlexRender cell={cell} />
                                                    </TableCell>
                                                ))}
                                            </TableRow>
                                            {/* 展开行：分录明细 + 合计条，等价原 expandedRowRender */}
                                            {expanded && (
                                                <TableRow className="border-0 hover:bg-transparent">
                                                    <TableCell colSpan={leafCount} className="p-0">
                                                        <div className={cn(SOFT_PANEL, "mx-3 mb-3 overflow-hidden")}>
                                                            {(record.entries?.length ?? 0) > 0 && (
                                                                <EntryTable entries={record.entries ?? []} />
                                                            )}
                                                            <EntryTotals record={record} />
                                                        </div>
                                                    </TableCell>
                                                </TableRow>
                                            )}
                                        </Fragment>
                                    )
                                })
                            )}
                        </TableBody>
                    </Table>
                </div>

                {/* 客户端分页 */}
                <div className="flex flex-wrap items-center gap-3 border-t border-slate-100 px-5 py-3">
                    <span className="whitespace-nowrap text-[12.5px] text-slate-400">共 {vouchers.length} 张凭证</span>
                    <Select
                        value={String(pageSize)}
                        onValueChange={(v) => {
                            setPageSize(Number(v))
                            setPageIndex(0)
                        }}
                    >
                        <SelectTrigger
                            size="sm"
                            className="w-auto border-slate-200 text-[12px] text-slate-500 shadow-none [&>svg]:text-slate-400"
                        >
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            {PAGE_SIZE_OPTIONS.map((n) => (
                                <SelectItem key={n} value={String(n)}>{`${n} 条/页`}</SelectItem>
                            ))}
                        </SelectContent>
                    </Select>
                    <Pagination className="mx-0 w-auto justify-end">
                        <PaginationContent>
                            <PaginationItem>
                                <PaginationPrevious
                                    text="上一页"
                                    className="text-[12.5px] text-slate-500 hover:bg-slate-100"
                                    onClick={() => setPageIndex(Math.max(0, currentPage - 1))}
                                />
                            </PaginationItem>
                            {pageItems(currentPage + 1, pageCount).map((item, i) =>
                                item === "gap" ? (
                                    <PaginationItem key={`gap-${i}`}>
                                        <PaginationEllipsis className="text-slate-300" />
                                    </PaginationItem>
                                ) : (
                                    <PaginationItem key={item}>
                                        <PaginationLink
                                            isActive={item === currentPage + 1}
                                            className={
                                                item === currentPage + 1
                                                    ? "bg-[#2f6bff] text-white shadow-[0_1px_2px_rgba(47,107,255,0.25)] hover:bg-[#255ce0]"
                                                    : "text-[12.5px] text-slate-500 hover:bg-slate-100"
                                            }
                                            onClick={() => setPageIndex(item - 1)}
                                        >
                                            {item}
                                        </PaginationLink>
                                    </PaginationItem>
                                ),
                            )}
                            <PaginationItem>
                                <PaginationNext
                                    text="下一页"
                                    className="text-[12.5px] text-slate-500 hover:bg-slate-100"
                                    onClick={() => setPageIndex(Math.min(pageCount - 1, currentPage + 1))}
                                />
                            </PaginationItem>
                        </PaginationContent>
                    </Pagination>
                </div>
            </SectionCard>

            {/* 新增 / 编辑凭证抽屉 */}
            <Sheet
                open={open}
                onOpenChange={(next) => {
                    if (next) return
                    setOpen(false)
                    setEditing(null)
                }}
            >
                <SheetContent className="sm:max-w-6xl" aria-describedby={undefined}>
                    <SheetHeader>
                        <SheetTitle className="text-[16px] font-bold text-slate-800">
                            {editing ? `编辑凭证 ${editing.voucherNo}` : "新增凭证"}
                        </SheetTitle>
                    </SheetHeader>
                    <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-auto px-5 pb-5">
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
                    </div>
                </SheetContent>
            </Sheet>

            {/* 导入凭证抽屉 */}
            <Sheet
                open={importOpen}
                onOpenChange={(next) => {
                    if (next) return
                    setImportOpen(false)
                    pickImportFile(null)
                }}
            >
                <SheetContent className="sm:max-w-2xl">
                    <SheetHeader>
                        <SheetTitle className="text-[16px] font-bold text-slate-800">导入凭证</SheetTitle>
                        <SheetDescription className="text-[12.5px] text-slate-500">
                            支持导入其他平台导出的记账凭证文件（.xls / .xlsx）。系统按凭证号聚合分录、自动补建缺失科目，
                            借贷金额原样保留。
                        </SheetDescription>
                    </SheetHeader>
                    <div className="flex min-h-0 flex-1 flex-col gap-5 overflow-auto px-5 pb-5">
                        <div className="flex flex-col gap-1">
                            <span className="text-[13px] font-semibold text-slate-700">导入到账套</span>
                            <span className="text-[12.5px] text-slate-500">
                                {currentSet?.name ?? accountSetId}
                                {currentSet?.isCurrent ? "（当前）" : ""}
                            </span>
                        </div>

                        {/* 冲突策略 */}
                        <div className="flex flex-col gap-2">
                            <span className="text-[13px] font-semibold text-slate-700">重复凭证处理</span>
                            <ToggleGroup
                                type="single"
                                variant="outline"
                                size="sm"
                                spacing={0}
                                value={String(importConflict)}
                                // 单选组取消选择时回传空串，保持原有两项必选其一的语义
                                onValueChange={(v) => {
                                    if (v) setImportConflict(v === "1" ? 1 : 0)
                                }}
                            >
                                <ToggleGroupItem value="0">跳过</ToggleGroupItem>
                                <ToggleGroupItem value="1">覆盖</ToggleGroupItem>
                            </ToggleGroup>
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
                                "flex cursor-pointer flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed p-10 text-center transition-colors",
                                importDragging ? "border-[#2f6bff] bg-[#dbeafe]" : "border-slate-200 bg-muted",
                            )}
                        >
                            <span className="flex size-12 items-center justify-center rounded-2xl bg-[#e8f1ff] text-[#2f6bff]">
                                <FileSpreadsheet className="size-6" />
                            </span>
                            {importFile ? (
                                <div className="flex flex-col gap-1">
                                    <span className="truncate text-[13px] font-medium text-slate-700">{importFile.name}</span>
                                    <span className="text-[12px] text-slate-400">
                                        {(importFile.size / 1024).toFixed(1)} KB · 点击可重新选择
                                    </span>
                                </div>
                            ) : (
                                <div className="flex flex-col gap-1">
                                    <span className="text-[13px] font-medium text-slate-700">点击选择，或拖拽文件到此处</span>
                                    <span className="text-[12px] text-slate-400">支持 .xls / .xlsx，最大 10MB</span>
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
                            <Button onClick={() => void handleImport()} disabled={!importFile || !accountSetId || importing} className={PAGE_BTN_PRIMARY}>
                                {importing ? <Spinner data-icon="inline-start" /> : <Upload data-icon="inline-start" />}
                                {importing ? "导入中…" : "开始导入"}
                            </Button>
                            {importFile && !importing && (
                                <Button variant="outline" onClick={() => pickImportFile(null)} className={PAGE_BTN_GHOST}>
                                    清除
                                </Button>
                            )}
                        </div>

                        {importError && (
                            <Alert variant="destructive" className="rounded-2xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{importError}</AlertTitle>
                            </Alert>
                        )}

                        {/* 导入结果 */}
                        {importResult && (
                            <div className="flex flex-col gap-4 rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                                <h3 className="text-[14px] font-semibold text-slate-800">导入结果</h3>
                                <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
                                    <Tile label="解析行数" value={importResult.totalRows ?? 0} tone="blue" icon={FileSpreadsheet} />
                                    <Tile label="导入凭证" value={importResult.importedVouchers ?? 0} tone="green" icon={CheckCircle2} />
                                    <Tile label="导入分录" value={importResult.importedEntries ?? 0} tone="purple" icon={FileText} />
                                    <Tile label="新建科目" value={importResult.createdAccounts ?? 0} tone="amber" icon={Plus} />
                                </div>
                                <div className="grid grid-cols-2 gap-3">
                                    <Tile label="覆盖凭证" value={importResult.overwrittenVouchers ?? 0} tone="gray" icon={Undo2} />
                                    <Tile label="跳过凭证" value={importResult.skippedVouchers ?? 0} tone="gray" icon={ChevronRight} />
                                </div>

                                {importResult.warnings && importResult.warnings.length > 0 && (
                                    <div className="flex flex-col gap-2">
                                        <span className="text-[12.5px] text-slate-400">
                                            告警（{importResult.warnings.length}）
                                        </span>
                                        <ul className={cn(SOFT_PANEL, "max-h-64 overflow-auto p-3 text-[12.5px] text-slate-500")}>
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
                    </div>
                </SheetContent>
            </Sheet>

            {confirmDialog}
        </div>
    )
}
