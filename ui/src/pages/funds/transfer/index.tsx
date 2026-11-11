/**
 * 内部转账账页，布局对齐柠檬云「内部转账」：
 * 顶部时间区间 + 打入/打出动作组（打印列表 / 导出 / 导入 / 批量删除 / 生成凭证 / 刷新 / 全屏），
 * 下方表格首行为行内登记（摘要 / 转出账户 / 转入账户 / 金额 / 备注），流水行可编辑、删除、生成凭证。
 */
import { useCallback, useEffect, useMemo, useState } from "react"
import dayjs from "dayjs"
import { Alert, Button, Card, Checkbox, DatePicker, Dropdown, Input, InputNumber, Modal, Pagination, Select, Space, Table, Tooltip } from "antd"
import type { TableColumnsType } from "antd"
import { DownOutlined, FilterFilled, FullscreenOutlined, ReloadOutlined, SwapOutlined } from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    JournalsApi,
    TransfersApi,
    type JournalListItemDto,
    type SaveTransferRequest,
    type TransferDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { printPdf } from "@/lib/print-pdf"
import { fmtMoney, toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useConfirm } from "@/hooks/useConfirm"

/** 每页条数候选，与账页页脚的分页器一致。 */
const PAGE_SIZE_OPTIONS = [20, 50, 100]

/** 可被列头漏斗筛选的列。 */
type FilterKey = "date" | "summary" | "from" | "to" | "amount" | "voucher"

const EMPTY_FILTERS: Record<FilterKey, string> = {
    date: "",
    summary: "",
    from: "",
    to: "",
    amount: "",
    voucher: "",
}

/** 行内登记 / 编辑用的表单值（金额保留字符串，避免半角输入被吃掉）。 */
interface TransferDraft {
    date: string
    summary: string
    fromJournalId: string
    toJournalId: string
    amount: string
    remark: string
}

export default function TransferPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { confirm, confirmDialog } = useConfirm()

    const [transfers, setTransfers] = useState<TransferDto[]>([])
    const [journals, setJournals] = useState<JournalListItemDto[]>([])
    const [loading, setLoading] = useState(false)
    const [printing, setPrinting] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [notice, setNotice] = useState<string | null>(null)

    const [period, setPeriod] = useState<[string, string] | null>(() => [
        dayjs().startOf("month").format("YYYY-MM-DD"),
        dayjs().endOf("month").format("YYYY-MM-DD"),
    ])
    const [showAll, setShowAll] = useState(false)
    const [filters, setFilters] = useState<Record<FilterKey, string>>(EMPTY_FILTERS)
    const [selected, setSelected] = useState<string[]>([])
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(PAGE_SIZE_OPTIONS[0])

    // 行内登记（柠檬云首行即为一张录入表单）
    const [draft, setDraft] = useState<TransferDraft>(() => ({
        date: dayjs().format("YYYY-MM-DD"),
        summary: "资金内部调拨",
        fromJournalId: "",
        toJournalId: "",
        amount: "",
        remark: "",
    }))
    const [savingDraft, setSavingDraft] = useState(false)

    // 编辑已有转账（行内「编辑」走弹窗，避免与登记行冲突）
    const [editing, setEditing] = useState<TransferDto | null>(null)
    const [editForm, setEditForm] = useState<TransferDraft | null>(null)
    const [savingEdit, setSavingEdit] = useState(false)
    const [busyId, setBusyId] = useState<string | null>(null)

    // 导入
    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<File | null>(null)
    const [importing, setImporting] = useState(false)

    const loadTransfers = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const [list, journalList] = await Promise.all([
                new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersGet({ accountSetId }),
                new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId }),
            ])
            setTransfers(list)
            setJournals(journalList)
            // 默认把行内登记的两个账户填上，减少重复选择。
            const enabled = journalList.filter((j) => j.isEnabled !== false)
            setDraft((prev) => ({
                ...prev,
                fromJournalId: prev.fromJournalId || enabled[0]?.id || "",
                toJournalId: prev.toJournalId || enabled[1]?.id || "",
            }))
        } catch (err) {
            setError(await apiErrorMessage(err, "加载内部转账失败，请重试。"))
            setTransfers([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void loadTransfers()
    }, [loadTransfers])

    const enabledJournals = useMemo(() => journals.filter((j) => j.isEnabled !== false), [journals])
    const journalOptions = useMemo(
        () =>
            enabledJournals.map((j) => ({
                value: j.id ?? "",
                label: `${j.name ?? ""}${j.type === 1 ? "（现金）" : "（银行）"}`,
            })),
        [enabledJournals],
    )

    /** 期间 + 列头漏斗一起生效。 */
    const filtered = useMemo(() => {
        const hit = (text: string, key: FilterKey) => {
            const keyword = filters[key].trim()
            return keyword === "" || text.includes(keyword)
        }
        return transfers.filter((t) => {
            const day = toISODate(t.date)
            if (!showAll && period && (day < period[0] || day > period[1])) return false
            if (!hit(day, "date")) return false
            if (!hit(t.summary ?? "", "summary")) return false
            if (!hit(t.fromJournalName ?? "", "from")) return false
            if (!hit(t.toJournalName ?? "", "to")) return false
            if (filters.amount.trim() && !String(t.amount ?? "").includes(filters.amount.trim())) return false
            if (!hit(t.voucherNo ?? "", "voucher")) return false
            return true
        })
    }, [transfers, period, showAll, filters])

    const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize))
    const current = Math.min(page, pageCount)
    const pageRows = filtered.slice((current - 1) * pageSize, current * pageSize)
    const enabledIds = filtered.filter((t) => !t.voucherId && t.id).map((t) => t.id as string)

    /** 当前筛选结果的笔数与金额合计，表头上方直接展示。 */
    const summary = useMemo(() => {
        const amount = filtered.reduce((sum, t) => sum + (t.amount ?? 0), 0)
        return { count: filtered.length, amount, pending: filtered.filter((t) => !t.voucherId).length }
    }, [filtered])

    /* ---------- 行内登记 ---------- */

    async function saveDraft() {
        if (!accountSetId || savingDraft) return
        if (!draft.date) {
            setError("请选择转账日期。")
            return
        }
        if (!draft.summary.trim()) {
            setError("请填写摘要。")
            return
        }
        if (!draft.fromJournalId || !draft.toJournalId) {
            setError("请选择转出与转入账户。")
            return
        }
        if (draft.fromJournalId === draft.toJournalId) {
            setError("转出与转入账户不能相同。")
            return
        }
        const amount = Number(draft.amount)
        if (!amount || amount <= 0) {
            setError("请填写大于 0 的转账金额。")
            return
        }

        setSavingDraft(true)
        setError(null)
        try {
            await new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersPost({
                accountSetId,
                saveTransferRequest: {
                    date: new Date(draft.date),
                    summary: draft.summary.trim(),
                    fromJournalId: draft.fromJournalId,
                    toJournalId: draft.toJournalId,
                    amount,
                    remark: draft.remark.trim() || null,
                },
            })
            setDraft((prev) => ({ ...prev, amount: "", remark: "" }))
            setNotice("转账已保存，两端账户已登记流水。")
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "保存转账失败，请重试。"))
        } finally {
            setSavingDraft(false)
        }
    }

    function startEdit(t: TransferDto) {
        setEditing(t)
        setEditForm({
            date: toISODate(t.date),
            summary: t.summary ?? "",
            fromJournalId: t.fromJournalId ?? "",
            toJournalId: t.toJournalId ?? "",
            amount: String(t.amount ?? ""),
            remark: t.remark ?? "",
        })
    }

    async function saveEdit() {
        if (!editing?.id || !editForm || savingEdit) return
        const amount = Number(editForm.amount)
        if (!editForm.summary.trim() || !editForm.fromJournalId || !editForm.toJournalId || amount <= 0) {
            setError("请补全摘要、转出 / 转入账户与金额。")
            return
        }
        setSavingEdit(true)
        try {
            const request: SaveTransferRequest = {
                date: new Date(editForm.date),
                summary: editForm.summary.trim(),
                fromJournalId: editForm.fromJournalId,
                toJournalId: editForm.toJournalId,
                amount,
                remark: editForm.remark.trim() || null,
            }
            await new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersIdPut({
                accountSetId,
                id: editing.id,
                saveTransferRequest: request,
            })
            setEditing(null)
            setEditForm(null)
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "保存转账失败，请重试。"))
        } finally {
            setSavingEdit(false)
        }
    }

    async function handleGenerateVoucher(t: TransferDto) {
        if (!t.id) return
        setBusyId(t.id)
        setError(null)
        try {
            await new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersIdVoucherPost({ accountSetId, id: t.id })
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "生成转账凭证失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    async function handleDelete(t: TransferDto) {
        if (!t.id) return
        if (!(await confirm(`确定删除转账「${t.summary}」吗？两端账户中对应的转出 / 转入流水将一并删除。`))) return
        setError(null)
        try {
            await new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersIdDelete({ accountSetId, id: t.id })
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "删除转账失败，请重试。"))
        }
    }

    /* ---------- 批量 / 导入 / 导出 / 打印 ---------- */

    async function batchDelete() {
        if (selected.length === 0) {
            setError("请先勾选要删除的转账。")
            return
        }
        if (!(await confirm(`确定删除选中的 ${selected.length} 笔转账吗？已生成凭证的将被跳过。`))) return
        try {
            const removed = await new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersBulkDeletePost({
                accountSetId,
                bulkTransferRequest: { ids: selected },
            })
            setSelected([])
            setNotice(`已删除 ${removed} 笔转账。`)
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "批量删除失败，请重试。"))
        }
    }

    async function batchVoucher() {
        const ids = selected.length > 0 ? selected.filter((id) => enabledIds.includes(id)) : enabledIds
        if (ids.length === 0) {
            setError(selected.length > 0 ? "勾选的转账都已生成凭证。" : "当前没有可生成凭证的转账。")
            return
        }
        // 未勾选时会对「全部未制证转账」出票，必须先说清影响范围再执行。
        if (
            !(await confirm(
                selected.length > 0
                    ? `确定为选中的 ${ids.length} 笔转账生成凭证吗？`
                    : `未勾选转账，将为当前筛选结果中全部 ${ids.length} 笔未制证转账生成凭证，是否继续？`,
            ))
        )
            return
        try {
            const created = await new TransfersApi(apiConfig(token)).accountsetsAccountSetIdTransfersVouchersPost({
                accountSetId,
                bulkTransferRequest: { ids },
            })
            setSelected([])
            setNotice(`已生成 ${created} 张转账凭证。`)
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "生成凭证失败，请重试。"))
        }
    }

    async function downloadFile(path: string, fallbackName: string) {
        try {
            const res = await fetch(`/api${path}`, {
                headers: token ? { Authorization: `Bearer ${token}` } : undefined,
            })
            if (!res.ok) throw new Error("下载失败，请重试。")
            const blob = await res.blob()
            const disposition = res.headers.get("Content-Disposition") ?? ""
            const match = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
            const fileName = match ? decodeURIComponent(match[1]) : fallbackName
            const url = URL.createObjectURL(blob)
            const anchor = document.createElement("a")
            anchor.href = url
            anchor.download = fileName
            anchor.click()
            URL.revokeObjectURL(url)
        } catch (err) {
            setError(await apiErrorMessage(err, "下载失败，请重试。"))
        }
    }

    async function submitImport() {
        if (!importFile || importing) {
            setError("请先选择要导入的文件。")
            return
        }
        setImporting(true)
        try {
            const form = new FormData()
            form.append("file", importFile)
            const res = await fetch(`/api/accountsets/${accountSetId}/transfers/import`, {
                method: "POST",
                headers: token ? { Authorization: `Bearer ${token}` } : undefined,
                body: form,
            })
            const payload = await res.json().catch(() => null)
            if (!res.ok) {
                throw new Error(payload?.message ?? "导入失败，请重试。")
            }
            setImportOpen(false)
            setImportFile(null)
            setNotice(`共 ${payload.totalRows} 行，成功导入 ${payload.imported} 笔，跳过 ${payload.skipped} 行。`)
            await loadTransfers()
        } catch (err) {
            setError(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    /** 打印列表：把当前期间与列头漏斗条件原样交给后端，由后端渲染 PDF。 */
    async function printList() {
        if (!accountSetId || printing) return
        setPrinting(true)
        setError(null)
        try {
            const qs = new URLSearchParams()
            if (!showAll) {
                if (period) {
                    qs.set("startDate", period[0])
                    qs.set("endDate", period[1])
                }
            } else {
                qs.set("showAll", "true")
            }
            for (const [key, value] of Object.entries(filters)) {
                if (value.trim()) qs.set(key, value.trim())
            }
            await printPdf(`/api/accountsets/${accountSetId}/transfers/pdf?${qs.toString()}`, token)
        } catch (err) {
            setError(await apiErrorMessage(err, "生成打印稿失败，请重试。"))
        } finally {
            setPrinting(false)
        }
    }

    async function toggleFullscreen() {
        try {
            if (document.fullscreenElement) await document.exitFullscreen()
            else await document.documentElement.requestFullscreen()
        } catch {
            setError("当前浏览器不允许进入全屏模式。")
        }
    }

    /* ---------- 列 ---------- */

    const textFilter = (key: FilterKey) => ({
        filterIcon: <FilterFilled style={{ color: filters[key] ? "var(--brand)" : "var(--muted-foreground)" }} />,
        filteredValue: filters[key] ? [filters[key]] : null,
        filterDropdown: ({ confirm: apply }: { confirm: () => void }) => (
            <div className="w-[200px] p-2" onKeyDown={(e) => e.stopPropagation()}>
                <Input
                    autoFocus
                    allowClear
                                       placeholder="输入关键字筛选"
                    value={filters[key]}
                    onChange={(e) => setFilters((prev) => ({ ...prev, [key]: e.target.value }))}
                    onPressEnter={() => apply()}
                />
                <div className="mt-2 flex justify-end gap-2">
                    <Button
                                               onClick={() => {
                            setFilters((prev) => ({ ...prev, [key]: "" }))
                            apply()
                        }}
                    >
                        重置
                    </Button>
                    <Button type="primary" onClick={() => apply()}>
                        筛选
                    </Button>
                </div>
            </div>
        ),
    })

    /** 账页行：首行为行内登记，其余为已有转账。 */
    type TransferRow = { key: string; kind: "draft" } | { key: string; kind: "data"; t: TransferDto }

    const columns: TableColumnsType<TransferRow> = [
        {
            title: "日期",
            width: 132,
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("date"),
            render: (_, row) =>
                row.kind === "draft" ? (
                    <DatePicker
                                               className="w-full"
                        allowClear={false}
                        value={draft.date ? dayjs(draft.date) : null}
                        format="YYYY-MM-DD"
                        onChange={(d) => setDraft((prev) => ({ ...prev, date: d ? d.format("YYYY-MM-DD") : "" }))}
                    />
                ) : (
                    <span className="whitespace-nowrap">{toISODate(row.t.date)}</span>
                ),
        },
        {
            title: "摘要",
            ...textFilter("summary"),
            render: (_, row) =>
                row.kind === "draft" ? (
                    <Input
                                               value={draft.summary}
                        placeholder="资金内部调拨"
                        onChange={(e) => setDraft((prev) => ({ ...prev, summary: e.target.value }))}
                    />
                ) : (
                    <span className="cursor-pointer text-(--brand) hover:underline" onClick={() => startEdit(row.t)}>
                        {row.t.summary}
                    </span>
                ),
        },
        {
            title: "转出账户",
            width: 160,
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("from"),
            render: (_, row) =>
                row.kind === "draft" ? (
                    <Select
                                               className="w-full"
                        value={draft.fromJournalId || undefined}
                        placeholder="选择账户"
                        options={journalOptions}
                        onChange={(v) => setDraft((prev) => ({ ...prev, fromJournalId: v }))}
                    />
                ) : (
                    <span className="text-(--foreground)">{row.t.fromJournalName}</span>
                ),
        },
        {
            title: "转入账户",
            width: 160,
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("to"),
            render: (_, row) =>
                row.kind === "draft" ? (
                    <Select
                                               className="w-full"
                        value={draft.toJournalId || undefined}
                        placeholder="选择账户"
                        options={journalOptions}
                        onChange={(v) => setDraft((prev) => ({ ...prev, toJournalId: v }))}
                    />
                ) : (
                    <span className="text-(--foreground)">{row.t.toJournalName}</span>
                ),
        },
        {
            title: "金额",
            width: 140,
            align: "right",
            ...textFilter("amount"),
            render: (_, row) =>
                row.kind === "draft" ? (
                    <InputNumber
                                               className="w-full"
                        min={0}
                        precision={2}
                        value={draft.amount ? Number(draft.amount) : null}
                        placeholder="0.00"
                        onChange={(v: number | null) => setDraft((prev) => ({ ...prev, amount: v == null ? "" : String(v) }))}
                    />
                ) : (
                    <span className="font-medium text-(--foreground)">{fmtMoney(row.t.amount ?? 0)}</span>
                ),
        },
        {
            title: "备注",
            width: 180,
            render: (_, row) =>
                row.kind === "draft" ? (
                    <Input
                                               value={draft.remark}
                        placeholder="备注"
                        onChange={(e) => setDraft((prev) => ({ ...prev, remark: e.target.value }))}
                    />
                ) : (
                    <div className="truncate text-(--muted-foreground)">{row.t.remark ?? ""}</div>
                ),
        },
        {
            title: "关联凭证",
            width: 120,
            align: "center",
            ...textFilter("voucher"),
            render: (_, row) =>
                row.kind === "draft" ? (
                    <span className="text-(--muted-foreground)">—</span>
                ) : row.t.voucherNo ? (
                    <span className="text-(--brand)">{row.t.voucherNo}</span>
                ) : (
                    <span className="text-(--muted-foreground)">—</span>
                ),
        },
        {
            title: "操作",
            width: 170,
            align: "center",
            fixed: "right",
            render: (_, row) => {
                if (row.kind === "draft") {
                    return (
                        <div className="flex items-center justify-center gap-1 text-[12.5px]">
                            <Button type="link" loading={savingDraft} onClick={() => void saveDraft()}>
                                保存
                            </Button>
                            <Button
                                type="link"
                                                               onClick={() =>
                                    setDraft({
                                        date: dayjs().format("YYYY-MM-DD"),
                                        summary: "资金内部调拨",
                                        fromJournalId: enabledJournals[0]?.id ?? "",
                                        toJournalId: enabledJournals[1]?.id ?? "",
                                        amount: "",
                                        remark: "",
                                    })
                                }
                            >
                                重置
                            </Button>
                        </div>
                    )
                }

                const hasVoucher = Boolean(row.t.voucherId)
                return (
                    <div className="flex items-center justify-center gap-1 text-[12.5px]">
                        <Button type="link" disabled={hasVoucher} onClick={() => startEdit(row.t)}>
                            编辑
                        </Button>
                        <Button
                            type="link"
                                                       disabled={hasVoucher || busyId === row.t.id}
                            loading={busyId === row.t.id}
                            onClick={() => void handleGenerateVoucher(row.t)}
                        >
                            生成凭证
                        </Button>
                        <Button type="link" danger disabled={hasVoucher} onClick={() => void handleDelete(row.t)}>
                            删除
                        </Button>
                    </div>
                )
            },
        },
    ]

    const toolbar = (
        <Space size={8} wrap style={{ marginBottom: 16 }}>
            <DatePicker.RangePicker
                allowClear={false}
                value={period ? [dayjs(period[0]), dayjs(period[1])] : null}
                placeholder={["开始日期", "结束日期"]}
                onChange={(dates) => {
                    if (dates && dates[0] && dates[1]) {
                        setPeriod([dates[0].format("YYYY-MM-DD"), dates[1].format("YYYY-MM-DD")])
                        setShowAll(false)
                    }
                }}
            />
            <Checkbox
                checked={showAll}
                onChange={(e) => {
                    const all = e.target.checked
                    setShowAll(all)
                    if (!all) {
                        setPeriod([dayjs().startOf("month").format("YYYY-MM-DD"), dayjs().endOf("month").format("YYYY-MM-DD")])
                    }
                }}
            >
                显示全部
            </Checkbox>
            <Button disabled={!transfers.length} loading={printing} onClick={() => void printList()}>
                打印列表
            </Button>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: [
                        { key: "query", label: "当前查询数据" },
                        { key: "all", label: "所有数据" },
                    ],
                    onClick: ({ key }) =>
                        void downloadFile(
                            `/accountsets/${accountSetId}/transfers/export`,
                            `内部转账${key === "all" ? "_全部" : ""}_${dayjs().format("YYYYMMDDHHmmss")}.xlsx`,
                        ),
                }}
            >
                <Button>
                    导出 <DownOutlined className="text-[10px]" />
                </Button>
            </Dropdown>
            <Button onClick={() => setImportOpen(true)}>导入</Button>
            <Button onClick={() => void batchDelete()}>批量删除</Button>
            <Button type="primary" onClick={() => void batchVoucher()}>
                生成凭证
            </Button>
            <Tooltip title="刷新">
                <Button icon={<ReloadOutlined spin={loading} />} onClick={() => void loadTransfers()} />
            </Tooltip>
            <Tooltip title="全屏">
                <Button icon={<FullscreenOutlined />} onClick={() => void toggleFullscreen()} />
            </Tooltip>
        </Space>
    )

    const tableRows: TransferRow[] = [
        { key: "__draft__", kind: "draft" },
        ...pageRows.map((t) => ({ key: t.id ?? t.summary ?? "", kind: "data" as const, t })),
    ]


    return (
        <div className="flex flex-col gap-3">
            {error && <Alert type="error" showIcon closable title={error} onClose={() => setError(null)} />}
            {notice && <Alert type="success" showIcon closable title={notice} onClose={() => setNotice(null)} />}

            <Card>
                <div className="mb-3 flex items-center justify-between gap-3">
                    <span className="flex items-center gap-2 text-[14px] font-semibold text-(--foreground)">
                        <SwapOutlined className="text-(--brand)" />
                        内部转账
                        <span className="text-[12px] font-normal text-(--muted-foreground)">
                            {currentSet?.name ?? ""}
                        </span>
                    </span>
                </div>
                {toolbar}

                <div className="mb-3 flex flex-wrap items-center gap-x-8 gap-y-2 rounded-lg bg-(--table-zebra-bg) px-4 py-2.5 text-[13px]">
                    <span className="text-(--muted-foreground)">
                        转账笔数 <span className="ml-1 font-medium tabular-nums text-(--foreground)">{summary.count}</span>
                    </span>
                    <span className="text-(--muted-foreground)">
                        金额合计{" "}
                        <span className="ml-1 font-semibold tabular-nums text-(--foreground)">
                            {fmtMoney(summary.amount)}
                        </span>
                    </span>
                    <span className="text-(--muted-foreground)">
                        未生成凭证{" "}
                        <span className={cn("ml-1 font-medium tabular-nums", summary.pending > 0 ? "text-(--warn-text)" : "text-(--foreground)")}>
                            {summary.pending}
                        </span>
                    </span>
                </div>

                <div className="overflow-hidden rounded-lg">
                    <Table<TransferRow>
                                               rowKey="key"
                        columns={columns}
                        dataSource={tableRows}
                        loading={loading && transfers.length === 0}
                        pagination={false}
                        scroll={{ x: 1180 }}
                        rowSelection={{
                            selectedRowKeys: selected,
                            onChange: (keys) => setSelected(keys.map(String)),
                            getCheckboxProps: (row) => ({ disabled: row.kind === "draft" }),
                        }}
                        locale={{ emptyText: enabledJournals.length < 2 ? "至少需要两个启用的资金账户才能登记内部转账。" : "暂无数据" }}
                    />
                </div>

                <div className="flex items-center justify-between text-[12.5px] text-(--muted-foreground)" style={{ marginTop: 16 }}>
                    <span>
                        显示 {filtered.length === 0 ? 0 : (current - 1) * pageSize + 1} 到{" "}
                        {Math.min(current * pageSize, filtered.length)}，共 {filtered.length} 条
                    </span>
                    <Pagination
                                               current={current}
                        pageSize={pageSize}
                        total={filtered.length}
                        showSizeChanger
                        pageSizeOptions={PAGE_SIZE_OPTIONS}
                        showLessItems
                        onChange={(p, size) => {
                            setPage(p)
                            setPageSize(size)
                        }}
                    />
                </div>
            </Card>

            {/* 导入内部转账 */}
            <Modal
                open={importOpen}
                title="导入内部转账"
                width={560}
                onCancel={() => {
                    setImportOpen(false)
                    setImportFile(null)
                }}
                onOk={() => void submitImport()}
                okText="导入"
                cancelText="取消"
                confirmLoading={importing}
            >
                <div className="flex flex-col gap-3 py-1 text-[13px] text-(--foreground)">
                    <span>1.选择以下任意一种方式导入内部转账</span>
                    <div className="ps-3 text-[12.5px] leading-6 text-(--muted-foreground)">
                        <div>(1) 在内部转账列表导出所需数据，确认后直接导入</div>
                        <div>
                            (2) 下载模板，按照模板格式进行数据整理再导入
                            <Button
                                type="link"
                                                               onClick={() =>
                                    void downloadFile(
                                        `/accountsets/${accountSetId}/transfers/import/template`,
                                        "内部转账导入模板.xlsx",
                                    )
                                }
                            >
                                下载模板
                            </Button>
                        </div>
                    </div>
                    <span>2.选择文件导入</span>
                    <label className="flex h-[120px] cursor-pointer flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-(--border) bg-(--muted) text-[12.5px] text-(--muted-foreground) hover:border-(--brand)">
                        <input
                            type="file"
                            accept=".xls,.xlsx"
                            className="hidden"
                            onChange={(e) => setImportFile(e.target.files?.[0] ?? null)}
                        />
                        <span className="text-[13px] text-(--brand)">{importFile ? importFile.name : "点击选择文件"}</span>
                        <span>支持 .xls / .xlsx，表头需含 日期 / 转出账户 / 转入账户 / 金额</span>
                    </label>
                </div>
            </Modal>

            {/* 编辑转账 */}
            <Modal
                open={editing !== null}
                title="编辑转账"
                width={520}
                onCancel={() => {
                    setEditing(null)
                    setEditForm(null)
                }}
                onOk={() => void saveEdit()}
                okText="保存"
                cancelText="取消"
                confirmLoading={savingEdit}
            >
                {editForm && (
                    <div className="flex flex-col gap-3 py-1 text-[13px]">
                        <label className="flex items-center gap-3">
                            <span className="w-[76px] shrink-0 text-right text-(--foreground)">日期：</span>
                            <DatePicker
                                className="flex-1"
                                allowClear={false}
                                value={editForm.date ? dayjs(editForm.date) : null}
                                onChange={(d) => setEditForm({ ...editForm, date: d ? d.format("YYYY-MM-DD") : "" })}
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[76px] shrink-0 text-right text-(--foreground)">摘要：</span>
                            <Input
                                className="flex-1"
                                value={editForm.summary}
                                onChange={(e) => setEditForm({ ...editForm, summary: e.target.value })}
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[76px] shrink-0 text-right text-(--foreground)">转出账户：</span>
                            <Select
                                className="flex-1"
                                value={editForm.fromJournalId || undefined}
                                options={journalOptions}
                                onChange={(v) => setEditForm({ ...editForm, fromJournalId: v })}
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[76px] shrink-0 text-right text-(--foreground)">转入账户：</span>
                            <Select
                                className="flex-1"
                                value={editForm.toJournalId || undefined}
                                options={journalOptions}
                                onChange={(v) => setEditForm({ ...editForm, toJournalId: v })}
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[76px] shrink-0 text-right text-(--foreground)">金额：</span>
                            <InputNumber
                                className="flex-1"
                                min={0}
                                precision={2}
                                value={editForm.amount ? Number(editForm.amount) : null}
                                onChange={(v: number | null) => setEditForm({ ...editForm, amount: v == null ? "" : String(v) })}
                            />
                        </label>
                        <label className="flex items-center gap-3">
                            <span className="w-[76px] shrink-0 text-right text-(--foreground)">备注：</span>
                            <Input
                                className="flex-1"
                                value={editForm.remark}
                                onChange={(e) => setEditForm({ ...editForm, remark: e.target.value })}
                            />
                        </label>
                        <p className="ps-[88px] text-[12px] leading-5 text-(--muted-foreground)">
                            保存后会重建两端账户的转出 / 转入流水；已生成凭证的转账不允许修改。
                        </p>
                    </div>
                )}
            </Modal>

            {confirmDialog}
        </div>
    )
}
