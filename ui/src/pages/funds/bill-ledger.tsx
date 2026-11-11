/**
 * 票据管理（对应柠檬云「资金 → 票据管理」→ 票据台账）。
 *
 * 布局与交互对齐柠檬云：顶部时间区间 + 显示所有信息 + 新增 / 生成凭证▾ / 导入▾ / 删除 / 刷新 / 全屏，
 * 表格列头带漏斗筛选，行内操作 编辑 / 收款 / 转让 / 删除 / 附件，底部按 20~1000 条分页。
 * 新增 / 编辑弹窗按柠檬云三列布局铺开全部票据要素（含票面与附件上传）。
 */
import { useCallback, useEffect, useMemo, useState } from "react"
import type { ReactNode } from "react"
import {
    App,
    Button,
    Card,
    Checkbox,
    DatePicker,
    Dropdown,
    Empty,
    Flex,
    Input,
    InputNumber,
    Modal,
    Pagination,
    Radio,
    Select,
    Space,
    Spin,
    Table,
    Tooltip,
    Typography,
    Tabs,
    Upload,
} from "antd"
import type { TableColumnsType, UploadFile } from "antd"
import {
    DownOutlined,
    FilterFilled,
    FullscreenOutlined,
    PaperClipOutlined,
    PlusOutlined,
    QuestionCircleOutlined,
    ReloadOutlined,
} from "@ant-design/icons"
import dayjs, { type Dayjs } from "dayjs"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { BillsApi, type BillAttachmentInfo, type BillDto, type SaveBillRequest } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { StatusPill } from "@/components/page/status-pill"

/** 每页条数候选，与柠檬云分页器一致。 */
const PAGE_SIZE_OPTIONS = [20, 50, 100, 200, 500, 1000]

/** 票据种类（与后端 BillKind 一致）。 */
const KIND_OPTIONS = [
    { value: 1, label: "银行汇票" },
    { value: 2, label: "商业汇票" },
    { value: 3, label: "支票" },
    { value: 4, label: "其他" },
]

/** 票据状态（与后端 BillStatus 一致）。 */
const STATUS_OPTIONS = [
    { value: 1, label: "提示收票已签收" },
    { value: 7, label: "背书待签收" },
    { value: 3, label: "背书已签收" },
    { value: 2, label: "买断式贴现已签收" },
    { value: 5, label: "票据已结清" },
]

/** 新增 / 编辑表单模型。 */
interface BillFormModel {
    category: string
    issueDate: string
    billNo: string
    amount: number | null
    kind: number
    dueDate: string
    drawer: string
    payee: string
    acceptor: string
    drawerAccountNo: string
    payeeAccountNo: string
    acceptorAccountNo: string
    drawerBank: string
    payeeBank: string
    acceptorBank: string
    transferable: boolean
    status: number
    remark: string
}

const emptyForm = (status = 1): BillFormModel => ({
    category: "",
    issueDate: "",
    billNo: "",
    amount: null,
    kind: 1,
    dueDate: "",
    drawer: "",
    payee: "",
    acceptor: "",
    drawerAccountNo: "",
    payeeAccountNo: "",
    acceptorAccountNo: "",
    drawerBank: "",
    payeeBank: "",
    acceptorBank: "",
    transferable: true,
    status,
    remark: "",
})

/** 后端日期字段运行时是 Date，统一按 yyyy-MM-dd 展示。 */
const fmtDate = (value: Date | string | null | undefined): string =>
    value ? dayjs(value as string).format("YYYY-MM-DD") : ""

const toDateStr = (value: Dayjs | null): string => (value ? value.format("YYYY-MM-DD") : "")

/** 表格行的显示模型（含派生文案）。 */
type BillRow = BillDto & { transferableText: string }

export function BillLedgerPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { modal, message } = App.useApp()

    const [bills, setBills] = useState<BillDto[]>([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    /** 时间区间（默认本月 1 日至今天）。 */
    const [range, setRange] = useState<[string, string]>(() => [
        dayjs().startOf("month").format("YYYY-MM-DD"),
        dayjs().format("YYYY-MM-DD"),
    ])
    const [showAll, setShowAll] = useState(false)

    const [selectedKeys, setSelectedKeys] = useState<React.Key[]>([])
    const [filters, setFilters] = useState<Record<string, (string | number | boolean)[] | null>>({})
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(20)

    const [formOpen, setFormOpen] = useState(false)
    const [formMode, setFormMode] = useState<"create" | "edit">("create")
    const [formBillId, setFormBillId] = useState<string | null>(null)
    const [form, setForm] = useState<BillFormModel>(emptyForm())
    const [saving, setSaving] = useState(false)
    const [pendingFile, setPendingFile] = useState<UploadFile | null>(null)

    const [attachOpen, setAttachOpen] = useState(false)
    const [attachBill, setAttachBill] = useState<BillDto | null>(null)
    const [attachments, setAttachments] = useState<BillAttachmentInfo[]>([])
    const [attachLoading, setAttachLoading] = useState(false)
    const [attachUploading, setAttachUploading] = useState(false)

    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<File | null>(null)
    const [importing, setImporting] = useState(false)

    const [helpOpen, setHelpOpen] = useState(false)
    const [busy, setBusy] = useState(false)

    const api = useMemo(() => new BillsApi(apiConfig(token)), [token])

    /* ---------- 数据加载 ---------- */

    const load = useCallback(async () => {
        if (!accountSetId) {
            setBills([])
            setLoading(false)
            return
        }
        setLoading(true)
        try {
            const data = await api.accountsetsAccountSetIdBillsGet({
                accountSetId,
                startDate: showAll ? undefined : new Date(range[0]),
                endDate: showAll ? undefined : new Date(range[1]),
            })
            setBills(data)
            setError(null)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载票据台账失败。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, api, range, showAll])

    useEffect(() => {
        void load()
    }, [load])

    useEffect(() => {
        setSelectedKeys([])
        setPage(1)
    }, [accountSetId])

    /* ---------- 列筛选 ---------- */

    const rows: BillRow[] = useMemo(
        () => bills.map((b) => ({ ...b, transferableText: b.transferable ? "可转让" : "不可转让" })),
        [bills],
    )

    const uniq = useCallback((values: string[]) => [...new Set(values.filter(Boolean))].map((v) => ({ text: v, value: v })), [])

    const filtered = useMemo(() => {
        const active = (key: string) => (filters[key] ?? []) as string[]
        const inList = (key: string, value: string) => {
            const list = active(key)
            return list.length === 0 || list.includes(value)
        }
        return rows.filter(
            (r) =>
                inList("category", r.category ?? "") &&
                inList("billNo", r.billNo ?? "") &&
                inList("kindText", r.kindText ?? "") &&
                inList("drawer", r.drawer ?? "") &&
                inList("acceptor", r.acceptor ?? "") &&
                inList("statusText", r.statusText ?? "") &&
                inList("transferableText", r.transferableText),
        )
    }, [rows, filters])

    const paged = useMemo(
        () => filtered.slice((page - 1) * pageSize, page * pageSize),
        [filtered, page, pageSize],
    )

    useEffect(() => {
        const maxPage = Math.max(1, Math.ceil(filtered.length / pageSize))
        if (page > maxPage) {
            setPage(maxPage)
        }
    }, [filtered.length, page, pageSize])

    /* ---------- 新增 / 编辑 ---------- */

    const openCreate = () => {
        setFormMode("create")
        setFormBillId(null)
        setForm(emptyForm())
        setPendingFile(null)
        setFormOpen(true)
    }

    const openEdit = (bill: BillDto) => {
        setFormMode("edit")
        setFormBillId(bill.id ?? null)
        setForm({
            category: bill.category ?? "",
            issueDate: fmtDate(bill.issueDate),
            billNo: bill.billNo ?? "",
            amount: bill.amount ?? null,
            kind: bill.kind ?? 1,
            dueDate: fmtDate(bill.dueDate),
            drawer: bill.drawer ?? "",
            payee: bill.payee ?? "",
            acceptor: bill.acceptor ?? "",
            drawerAccountNo: bill.drawerAccountNo ?? "",
            payeeAccountNo: bill.payeeAccountNo ?? "",
            acceptorAccountNo: bill.acceptorAccountNo ?? "",
            drawerBank: bill.drawerBank ?? "",
            payeeBank: bill.payeeBank ?? "",
            acceptorBank: bill.acceptorBank ?? "",
            transferable: bill.transferable ?? true,
            status: bill.status ?? 1,
            remark: bill.remark ?? "",
        })
        setPendingFile(null)
        setFormOpen(true)
    }

    const buildRequest = (): SaveBillRequest => ({
        category: form.category.trim(),
        issueDate: new Date(form.issueDate),
        billNo: form.billNo.trim(),
        amount: form.amount ?? 0,
        kind: form.kind,
        dueDate: form.dueDate ? new Date(form.dueDate) : null,
        drawer: form.drawer.trim() || null,
        payee: form.payee.trim() || null,
        acceptor: form.acceptor.trim() || null,
        drawerAccountNo: form.drawerAccountNo.trim() || null,
        payeeAccountNo: form.payeeAccountNo.trim() || null,
        acceptorAccountNo: form.acceptorAccountNo.trim() || null,
        drawerBank: form.drawerBank.trim() || null,
        payeeBank: form.payeeBank.trim() || null,
        acceptorBank: form.acceptorBank.trim() || null,
        transferable: form.transferable,
        status: form.status,
        remark: form.remark.trim() || null,
    })

    /** 校验必填项（与柠檬云红点一致：票据号码 / 出票日 / 票据金额 / 承兑人全称 / 承兑人账号行号）。 */
    function validateForm(): string | null {
        if (!form.billNo.trim()) return "请填写票据号码。"
        if (!form.issueDate) return "出票日不能为空。"
        if (!form.amount || form.amount <= 0) return "请填写正确的票据金额。"
        if (!form.acceptor.trim()) return "请填写承兑人全称。"
        if (!form.acceptorAccountNo.trim()) return "请填写承兑人账号/行号。"
        return null
    }

    async function submitForm() {
        if (saving) return
        const invalid = validateForm()
        if (invalid) {
            message.warning(invalid)
            return
        }

        setSaving(true)
        try {
            let billId = formBillId
            if (formMode === "create") {
                const created = await api.accountsetsAccountSetIdBillsPost({
                    accountSetId,
                    saveBillRequest: buildRequest(),
                })
                billId = created.id ?? null
            } else if (billId) {
                await api.accountsetsAccountSetIdBillsIdPut({
                    accountSetId,
                    id: billId,
                    saveBillRequest: buildRequest(),
                })
            }

            // 选了票面 / 附件就顺带传上去。
            if (pendingFile?.originFileObj && billId) {
                await uploadFile(billId, pendingFile.originFileObj as File)
            }

            setFormOpen(false)
            setPendingFile(null)
            message.success(formMode === "create" ? "保存成功" : "修改成功")
            await load()
        } catch (err) {
            message.error(await apiErrorMessage(err, "保存失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    /* ---------- 收款 / 转让 / 删除 ---------- */

    function collect(bill: BillDto) {
        modal.confirm({
            title: "收款",
            content: `确认票据「${bill.billNo}」已收款？收款后票据状态将变为「票据已结清」。`,
            okText: "确定",
            cancelText: "取消",
            onOk: async () => {
                try {
                    await api.accountsetsAccountSetIdBillsIdCollectPost({ accountSetId, id: bill.id ?? "" })
                    message.success("已收款")
                    await load()
                } catch (err) {
                    message.error(await apiErrorMessage(err, "收款失败，请重试。"))
                }
            },
        })
    }

    async function endorse(bill: BillDto) {
        try {
            await api.accountsetsAccountSetIdBillsIdEndorsePost({ accountSetId, id: bill.id ?? "" })
            message.success("已发起转让（背书待签收）")
            await load()
        } catch (err) {
            message.error(await apiErrorMessage(err, "转让失败，请重试。"))
        }
    }

    function removeBills(ids: string[]) {
        if (ids.length === 0) {
            message.warning("请先勾选要删除的票据")
            return
        }
        modal.confirm({
            title: "删除",
            content: "确定删除票据数据吗？",
            okText: "确定",
            cancelText: "取消",
            onOk: async () => {
                try {
                    const removed = await api.accountsetsAccountSetIdBillsBulkDeletePost({
                        accountSetId,
                        bulkBillRequest: { ids },
                    })
                    message.success(`已删除 ${removed} 张票据`)
                    setSelectedKeys((prev) => prev.filter((k) => !ids.includes(String(k))))
                    await load()
                } catch (err) {
                    message.error(await apiErrorMessage(err, "删除失败，请重试。"))
                }
            },
        })
    }

    /* ---------- 生成凭证 ---------- */

    function generateVouchers(kind: "collect" | "discount") {
        if (selectedKeys.length === 0) {
            message.warning("请选择票据后生成凭证")
            return
        }
        modal.confirm({
            title: "生成凭证",
            content: "亲，已生成凭证、票据状态不符、没有设置收支类别的票据将会被跳过，是否继续生成凭证？",
            okText: "确定",
            cancelText: "取消",
            onOk: async () => {
                setBusy(true)
                try {
                    const result = await api.accountsetsAccountSetIdBillsVouchersPost({
                        accountSetId,
                        bulkBillRequest: { ids: selectedKeys.map(String), voucherKind: kind },
                    })
                    message.success(
                        `成功生成 ${result.created ?? 0} 张凭证${(result.skipped ?? 0) > 0 ? `，跳过 ${result.skipped} 张` : ""}。`,
                    )
                    await load()
                } catch (err) {
                    message.error(await apiErrorMessage(err, "生成凭证失败，请重试。"))
                } finally {
                    setBusy(false)
                }
            },
        })
    }

    /* ---------- 导入 / 导出 ---------- */

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

    function exportData() {
        const query = showAll ? "" : `?startDate=${range[0]}&endDate=${range[1]}`
        void downloadFile(`/accountsets/${accountSetId}/bills/export${query}`, "票据台账.xlsx")
    }

    async function submitImport() {
        if (!importFile || importing) {
            message.warning("请选择要导入的文件。")
            return
        }
        setImporting(true)
        try {
            const formData = new FormData()
            formData.append("file", importFile)
            const res = await fetch(`/api/accountsets/${accountSetId}/bills/import`, {
                method: "POST",
                headers: token ? { Authorization: `Bearer ${token}` } : undefined,
                body: formData,
            })
            const payload = await res.json().catch(() => null)
            if (!res.ok) {
                throw new Error(payload?.message ?? "导入失败，请重试。")
            }
            setImportOpen(false)
            setImportFile(null)
            const warnings: string[] = payload?.warnings ?? []
            if (warnings.length > 0) {
                modal.info({
                    title: "导入结果",
                    width: 560,
                    content: (
                        <div className="text-[13px] leading-6">
                            <div>{`共 ${payload.totalRows} 行，成功导入 ${payload.imported} 张，跳过 ${payload.skipped} 行。`}</div>
                            <div className="mt-2 max-h-[220px] overflow-auto text-[12.5px] text-(--muted-foreground)">
                                {warnings.map((w) => (
                                    <div key={w}>{w}</div>
                                ))}
                            </div>
                        </div>
                    ),
                })
            } else {
                message.success(`共 ${payload.totalRows} 行，成功导入 ${payload.imported} 张，跳过 ${payload.skipped} 行。`)
            }
            await load()
        } catch (err) {
            message.error(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    /* ---------- 附件 ---------- */

    async function uploadFile(billId: string, file: File) {
        setAttachUploading(true)
        try {
            const formData = new FormData()
            formData.append("file", file)
            const res = await fetch(`/api/accountsets/${accountSetId}/bills/${billId}/attachments`, {
                method: "POST",
                headers: token ? { Authorization: `Bearer ${token}` } : undefined,
                body: formData,
            })
            const payload = await res.json().catch(() => null)
            if (!res.ok) throw new Error(payload?.message ?? "上传失败，请重试。")
        } catch (err) {
            message.error(await apiErrorMessage(err, "上传失败，请重试。"))
        } finally {
            setAttachUploading(false)
        }
    }

    async function openAttachments(bill: BillDto) {
        setAttachBill(bill)
        setAttachOpen(true)
        setAttachLoading(true)
        try {
            setAttachments(await api.accountsetsAccountSetIdBillsIdAttachmentsGet({ accountSetId, id: bill.id ?? "" }))
        } catch (err) {
            message.error(await apiErrorMessage(err, "加载附件失败。"))
        } finally {
            setAttachLoading(false)
        }
    }

    async function reloadAttachments() {
        if (!attachBill?.id) return
        setAttachLoading(true)
        try {
            setAttachments(await api.accountsetsAccountSetIdBillsIdAttachmentsGet({ accountSetId, id: attachBill.id }))
            await load()
        } catch (err) {
            message.error(await apiErrorMessage(err, "加载附件失败。"))
        } finally {
            setAttachLoading(false)
        }
    }

    async function deleteAttachment(attachmentId: string) {
        if (!attachBill?.id) return
        try {
            await api.accountsetsAccountSetIdBillsIdAttachmentsAttachmentIdDelete({
                accountSetId,
                id: attachBill.id,
                attachmentId,
            })
            message.success("附件已删除")
            await reloadAttachments()
        } catch (err) {
            message.error(await apiErrorMessage(err, "删除附件失败。"))
        }
    }

    async function toggleFullscreen() {
        try {
            if (document.fullscreenElement) {
                await document.exitFullscreen()
            } else {
                await document.documentElement.requestFullscreen()
            }
        } catch {
            message.warning("当前浏览器不允许进入全屏模式。")
        }
    }

    /* ---------- 列定义 ---------- */

    const filterIcon = (active: boolean) => (
        <FilterFilled style={{ fontSize: 11, color: active ? "var(--brand)" : "var(--icon-placeholder)" }} />
    )

    const columns: TableColumnsType<BillRow> = [
        {
            title: "收支类别",
            dataIndex: "category",
            width: 104,
            filters: uniq(rows.map((r) => r.category ?? "")),
            filterSearch: true,
            filterIcon: filterIcon((filters.category ?? []).length > 0),
            render: (value: string) => <span className="text-[13px] text-(--foreground)">{value || ""}</span>,
        },
        {
            title: "出票日",
            dataIndex: "issueDate",
            width: 96,
            render: (value: Date | string) => <span className="text-[13px]">{fmtDate(value)}</span>,
        },
        {
            title: "票据号",
            dataIndex: "billNo",
            width: 136,
            filters: uniq(rows.map((r) => r.billNo ?? "")),
            filterSearch: true,
            filterIcon: filterIcon((filters.billNo ?? []).length > 0),
            render: (value: string, row) => (
                <Flex align="center" gap={6}>
                    <span className="text-[13px]">{value}</span>
                    {row.voucherNo && (
                        <span className="rounded bg-(--brand-soft) px-1.5 py-0.5 text-[11px] text-(--brand)">{row.voucherNo}</span>
                    )}
                </Flex>
            ),
        },
        {
            title: "票据金额",
            dataIndex: "amount",
            width: 104,
            align: "right",
            render: (value: number) => <span className="text-[13px] tabular-nums text-(--foreground)">{fmtAmount(value)}</span>,
        },
        {
            title: "票据种类",
            dataIndex: "kindText",
            width: 96,
            filters: uniq(rows.map((r) => r.kindText ?? "")),
            filterIcon: filterIcon((filters.kindText ?? []).length > 0),
        },
        {
            title: "汇票到期日",
            dataIndex: "dueDate",
            width: 100,
            render: (value: Date | string | null) => <span className="text-[13px]">{fmtDate(value)}</span>,
        },
        {
            title: "出票人",
            dataIndex: "drawer",
            width: 140,
            ellipsis: true,
            filters: uniq(rows.map((r) => r.drawer ?? "")),
            filterSearch: true,
            filterIcon: filterIcon((filters.drawer ?? []).length > 0),
        },
        {
            title: "承兑人",
            dataIndex: "acceptor",
            width: 140,
            ellipsis: true,
            filters: uniq(rows.map((r) => r.acceptor ?? "")),
            filterSearch: true,
            filterIcon: filterIcon((filters.acceptor ?? []).length > 0),
        },
        {
            title: "票据状态",
            dataIndex: "statusText",
            width: 120,
            onCell: () => ({ style: { whiteSpace: "nowrap" } }),
            filters: uniq(rows.map((r) => r.statusText ?? "")),
            filterIcon: filterIcon((filters.statusText ?? []).length > 0),
            render: (value: string | null | undefined) => {
                const text = value ?? ""
                const tone = /逾期|拒付|退票/.test(text)
                    ? "red"
                    : /待|未/.test(text)
                      ? "amber"
                      : /结清|收款|贴现|已/.test(text)
                        ? "green"
                        : "blue"
                return text ? <StatusPill tone={tone}>{text}</StatusPill> : ""
            },
        },
        {
            title: "能否转让",
            dataIndex: "transferableText",
            width: 84,
            filters: [
                { text: "可转让", value: "可转让" },
                { text: "不可转让", value: "不可转让" },
            ],
            filterIcon: filterIcon((filters.transferableText ?? []).length > 0),
        },
        {
            title: "操作",
            key: "actions",
            width: 208,
            render: (_value, row) => (
                <Space size={6}>
                    <Typography.Link style={{ fontSize: 13 }} onClick={() => openEdit(row)}>
                        编辑
                    </Typography.Link>
                    <Typography.Link style={{ fontSize: 13 }} onClick={() => collect(row)}>
                        收款
                    </Typography.Link>
                    <Typography.Link style={{ fontSize: 13 }} onClick={() => void endorse(row)}>
                        转让
                    </Typography.Link>
                    <Typography.Link style={{ fontSize: 13 }} onClick={() => removeBills([row.id ?? ""])}>
                        删除
                    </Typography.Link>
                    <Typography.Link style={{ fontSize: 13 }} onClick={() => void openAttachments(row)}>
                        附件
                        {(row.attachmentCount ?? 0) > 0 ? `(${row.attachmentCount})` : ""}
                    </Typography.Link>
                </Space>
            ),
        },
    ]

    const generateMenu = {
        items: [
            { key: "collect", label: "生成凭证" },
            { key: "receipt", label: "生成收款凭证" },
            { key: "discount", label: "生成贴现凭证" },
        ],
        onClick: ({ key }: { key: string }) => generateVouchers(key === "discount" ? "discount" : "collect"),
    }

    const importMenu = {
        items: [
            { key: "import", label: "导入" },
            { key: "export", label: "导出" },
        ],
        onClick: ({ key }: { key: string }) => {
            if (key === "import") {
                setImportFile(null)
                setImportOpen(true)
            } else {
                exportData()
            }
        },
    }

    return (
        <div className="flex flex-col gap-3">
            {error && (
                <div className="rounded border border-(--danger-border) bg-(--table-neg-bg) px-3 py-2 text-[13px] text-(--danger)">{error}</div>
            )}

            <Card>
                {/* 页签：与柠檬云「票据管理 → 票据台账」一致 */}
                <Tabs
                    activeKey="ledger"
                    items={[{ key: "ledger", label: "票据台账" }]}
                    tabBarStyle={{ marginBottom: 8 }}
                />
                <Flex justify="space-between" align="center" wrap="wrap" gap={8} style={{ marginBottom: 16 }}>
                    <Space size={12} wrap>
                        <DatePicker.RangePicker
                            value={[dayjs(range[0]), dayjs(range[1])]}
                            allowClear={false}
                            disabled={showAll}
                            style={{ width: 250 }}
                            onChange={(values) => {
                                if (!values?.[0] || !values?.[1]) return
                                setRange([toDateStr(values[0]), toDateStr(values[1])])
                                setPage(1)
                            }}
                        />
                        <Checkbox checked={showAll} onChange={(e) => setShowAll(e.target.checked)}>
                            <span className="text-[13px] text-(--foreground)">显示所有信息</span>
                        </Checkbox>
                    </Space>

                    <Space size={8} wrap>
                        <Typography.Link
                            className="text-[13px]"
                            onClick={() => setHelpOpen(true)}
                        >
                            <QuestionCircleOutlined /> 如何管理票据？
                        </Typography.Link>
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                            新增
                        </Button>
                        <Dropdown menu={generateMenu} disabled={busy}>
                            <Button>
                                生成凭证 <DownOutlined style={{ fontSize: 10 }} />
                            </Button>
                        </Dropdown>
                        <Dropdown menu={importMenu}>
                            <Button>
                                导入 <DownOutlined style={{ fontSize: 10 }} />
                            </Button>
                        </Dropdown>
                        <Button disabled={selectedKeys.length === 0} onClick={() => removeBills(selectedKeys.map(String))}>
                            删除
                        </Button>
                        <Tooltip title="刷新">
                            <Button icon={<ReloadOutlined />} onClick={() => void load()} />
                        </Tooltip>
                        <Tooltip title="全屏">
                            <Button icon={<FullscreenOutlined />} onClick={() => void toggleFullscreen()} />
                        </Tooltip>
                    </Space>
                </Flex>

                <Spin spinning={loading}>
                    <div className="mb-3 flex flex-wrap items-center gap-x-8 gap-y-2 rounded-lg bg-(--table-zebra-bg) px-4 py-2.5 text-[13px]">
                        <span className="text-(--muted-foreground)">
                            票据张数 <span className="ml-1 font-medium tabular-nums text-(--foreground)">{filtered.length}</span>
                        </span>
                        <span className="text-(--muted-foreground)">
                            票面金额合计{" "}
                            <span className="ml-1 font-semibold tabular-nums text-(--foreground)">
                                ¥{fmtAmount(filtered.reduce((s, r) => s + (r.amount ?? 0), 0), true)}
                            </span>
                        </span>
                        {[...new Set(filtered.map((r) => r.statusText ?? ""))]
                            .filter(Boolean)
                            .map((text) => (
                                <span key={text} className="text-(--muted-foreground)">
                                    {text}{" "}
                                    <span className="ml-1 font-medium tabular-nums text-(--foreground)">
                                        {filtered.filter((r) => (r.statusText ?? "") === text).length}
                                    </span>
                                </span>
                            ))}
                    </div>
                    <Table<BillRow>
                        rowKey={(row) => row.id ?? row.billNo ?? ""}
                        columns={columns}
                        dataSource={paged}
                        pagination={false}
                        scroll={{ x: 1240 }}
                        locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无数据" /> }}
                        rowSelection={{
                            selectedRowKeys: selectedKeys,
                            onChange: (keys) => setSelectedKeys(keys),
                            columnWidth: 40,
                        }}
                        onChange={(_pagination, tableFilters) => {
                            setFilters(tableFilters as Record<string, (string | number | boolean)[] | null>)
                            setPage(1)
                        }}
                    />
                </Spin>

                <Flex justify="end" align="center" gap={12} style={{ marginTop: 16 }} className="px-1 pb-1 text-[12.5px] text-(--muted-foreground)">
                    <span>{`显示 ${filtered.length === 0 ? 0 : (page - 1) * pageSize + 1} 到 ${Math.min(page * pageSize, filtered.length)}，共 ${filtered.length} 条`}</span>
                    <Pagination
                        current={page}
                        pageSize={pageSize}
                        total={filtered.length}
                        showSizeChanger
                        pageSizeOptions={PAGE_SIZE_OPTIONS.map(String)}
                        onChange={(nextPage, nextSize) => {
                            setPage(nextPage)
                            setPageSize(nextSize)
                        }}
                    />
                </Flex>
            </Card>

            {/* 新增 / 编辑票据 */}
            <Modal
                open={formOpen}
                title={formMode === "create" ? "新增票据" : "编辑票据"}
                width={1000}
                okText="保存"
                cancelText="取消"
                confirmLoading={saving}
                mask={{ closable: false }}
                onOk={() => void submitForm()}
                onCancel={() => setFormOpen(false)}
                destroyOnHidden
            >
                <div className="grid grid-cols-3 gap-x-6 gap-y-3 py-2">
                    <div className="flex items-start gap-2">
                        <span className="w-[104px] shrink-0 pt-1 text-right text-[13px] text-(--foreground)">上传票面：</span>
                        <Upload
                            maxCount={1}
                            beforeUpload={() => false}
                            fileList={pendingFile ? [pendingFile] : []}
                            onChange={({ fileList }) => setPendingFile(fileList[0] ?? null)}
                            onRemove={() => setPendingFile(null)}
                        >
                            <div className="flex h-[70px] w-[104px] cursor-pointer flex-col items-center justify-center rounded border border-dashed border-(--border) text-[12px] text-(--muted-foreground) hover:border-(--brand) hover:text-(--brand)">
                                <PlusOutlined />
                                <span className="mt-1">上传票面</span>
                            </div>
                        </Upload>
                    </div>

                    <Field label="出票日" required>
                        <DatePicker
                            className="w-full"
                            value={form.issueDate ? dayjs(form.issueDate) : null}
                            onChange={(value) => setForm({ ...form, issueDate: toDateStr(value) })}
                        />
                    </Field>
                    <Field label="票据种类" required>
                        <Select
                            className="w-full"
                            value={form.kind}
                            options={KIND_OPTIONS}
                            onChange={(value) => setForm({ ...form, kind: value })}
                        />
                    </Field>

                    <Field label="票据号码" required>
                        <Input value={form.billNo} onChange={(e) => setForm({ ...form, billNo: e.target.value })} />
                    </Field>
                    <Field label="汇票到期日">
                        <DatePicker
                            className="w-full"
                            value={form.dueDate ? dayjs(form.dueDate) : null}
                            onChange={(value) => setForm({ ...form, dueDate: toDateStr(value) })}
                        />
                    </Field>
                    <Field label="票据状态" required>
                        <Select
                            className="w-full"
                            value={form.status}
                            options={STATUS_OPTIONS}
                            onChange={(value) => setForm({ ...form, status: value })}
                        />
                    </Field>

                    <Field label="出票人全称">
                        <Input value={form.drawer} onChange={(e) => setForm({ ...form, drawer: e.target.value })} />
                    </Field>
                    <Field label="票据金额" required>
                        <InputNumber
                            className="w-full"
                            min={0}
                            precision={2}
                            value={form.amount}
                            onChange={(value) => setForm({ ...form, amount: (value as number | null) ?? null })}
                        />
                    </Field>
                    <Field label="能否转让" required>
                        <Radio.Group
                            value={form.transferable}
                            onChange={(e) => setForm({ ...form, transferable: e.target.value as boolean })}
                        >
                            <Radio value={true}>能</Radio>
                            <Radio value={false}>否</Radio>
                        </Radio.Group>
                    </Field>

                    <Field label="收票人全称">
                        <Input value={form.payee} onChange={(e) => setForm({ ...form, payee: e.target.value })} />
                    </Field>
                    <Field label="出票人账号">
                        <Input value={form.drawerAccountNo} onChange={(e) => setForm({ ...form, drawerAccountNo: e.target.value })} />
                    </Field>
                    <Field label="出票人开户银行">
                        <Input value={form.drawerBank} onChange={(e) => setForm({ ...form, drawerBank: e.target.value })} />
                    </Field>

                    <Field label="承兑人全称" required>
                        <Input value={form.acceptor} onChange={(e) => setForm({ ...form, acceptor: e.target.value })} />
                    </Field>
                    <Field label="收票人账号">
                        <Input value={form.payeeAccountNo} onChange={(e) => setForm({ ...form, payeeAccountNo: e.target.value })} />
                    </Field>
                    <Field label="收票人开户银行">
                        <Input value={form.payeeBank} onChange={(e) => setForm({ ...form, payeeBank: e.target.value })} />
                    </Field>

                    <Field label="收支类别">
                        <Input value={form.category} onChange={(e) => setForm({ ...form, category: e.target.value })} placeholder="生成凭证时按此类别匹配" />
                    </Field>
                    <Field label="承兑人账号/行号" required>
                        <Input value={form.acceptorAccountNo} onChange={(e) => setForm({ ...form, acceptorAccountNo: e.target.value })} />
                    </Field>
                    <Field label="承兑人开户银行">
                        <Input value={form.acceptorBank} onChange={(e) => setForm({ ...form, acceptorBank: e.target.value })} />
                    </Field>

                    <div className="col-span-3">
                        <Field label="备注" labelWidth={104}>
                            <Input.TextArea
                                rows={2}
                                value={form.remark}
                                onChange={(e) => setForm({ ...form, remark: e.target.value })}
                            />
                        </Field>
                    </div>
                </div>
            </Modal>

            {/* 附件 */}
            <Modal
                open={attachOpen}
                title={`票据附件${attachBill?.billNo ? ` - ${attachBill.billNo}` : ""}`}
                width={560}
                footer={<Button onClick={() => setAttachOpen(false)}>关闭</Button>}
                onCancel={() => setAttachOpen(false)}
                destroyOnHidden
            >
                <div className="flex flex-col gap-3 py-1">
                    <Upload
                        maxCount={1}
                        showUploadList={false}
                        beforeUpload={(file) => {
                            if (!attachBill?.id) return false
                            void (async () => {
                                await uploadFile(attachBill.id as string, file as File)
                                await reloadAttachments()
                            })()
                            return false
                        }}
                    >
                        <Button icon={<PaperClipOutlined />} loading={attachUploading}>
                            上传附件
                        </Button>
                    </Upload>
                    <Spin spinning={attachLoading}>
                        {attachments.length === 0 ? (
                            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无附件" />
                        ) : (
                            <div className="flex flex-col divide-y divide-(--border) rounded border border-(--border)">
                                {attachments.map((a) => (
                                    <Flex key={a.id} justify="space-between" align="center" className="px-3 py-2">
                                        <span className="truncate text-[13px] text-(--foreground)">{a.fileName}</span>
                                        <Space size={10}>
                                            <Typography.Link
                                                style={{ fontSize: 13 }}
                                                onClick={() =>
                                                    void downloadFile(
                                                        `/accountsets/${accountSetId}/bills/${attachBill?.id}/attachments/${a.id}`,
                                                        a.fileName ?? "附件",
                                                    )
                                                }
                                            >
                                                下载
                                            </Typography.Link>
                                            <Typography.Link
                                                style={{ fontSize: 13, color: "var(--tone-red)" }}
                                                onClick={() => void deleteAttachment(a.id ?? "")}
                                            >
                                                删除
                                            </Typography.Link>
                                        </Space>
                                    </Flex>
                                ))}
                            </div>
                        )}
                    </Spin>
                </div>
            </Modal>

            {/* 导入 */}
            <Modal
                open={importOpen}
                title="导入票据信息"
                width={560}
                okText="导入"
                cancelText="取消"
                confirmLoading={importing}
                onOk={() => void submitImport()}
                onCancel={() => setImportOpen(false)}
                destroyOnHidden
            >
                <div className="flex flex-col gap-3 py-1 text-[13px] text-(--foreground)">
                    <span>1.选择以下任意一种方式导入票据</span>
                    <div className="ps-3 text-[12.5px] leading-6 text-(--muted-foreground)">
                        <div>(1) 在票据列表导出所需数据，确认后直接导入</div>
                        <div>
                            (2) 点击下载模板，按照模板格式进行数据整理再导入
                            <Button
                                type="link"
                                onClick={() =>
                                    void downloadFile(`/accountsets/${accountSetId}/bills/import/template`, "票据导入模板.xlsx")
                                }
                            >
                                下载模板
                            </Button>
                        </div>
                    </div>
                    <span>2.选择文件导入</span>
                    <Upload.Dragger
                        accept=".xls,.xlsx"
                        maxCount={1}
                        beforeUpload={() => false}
                        fileList={importFile ? [{ uid: "import", name: importFile.name }] : []}
                        onChange={({ fileList }) => {
                            const file = fileList[0]?.originFileObj
                            setImportFile((file as File) ?? null)
                        }}
                        onRemove={() => setImportFile(null)}
                    >
                        <p className="text-[13px] text-(--muted-foreground)">“点击”或“按 Ctrl + V 粘贴”或“拖拽至弹窗”上传</p>
                    </Upload.Dragger>
                </div>
            </Modal>

            {/* 如何管理票据 */}
            <Modal open={helpOpen} title="如何管理票据" width={600} footer={null} onCancel={() => setHelpOpen(false)}>
                <div className="flex flex-col gap-2 py-1 text-[13px] leading-6 text-(--foreground)">
                    <span>1.点击「新增」登记票据，必填项为票据号码、出票日、票据金额、承兑人全称与承兑人账号/行号。</span>
                    <span>2.票据到期收到款项后点击行内「收款」，票据状态变为「票据已结清」；需要背书转出时点击「转让」。</span>
                    <span>3.勾选票据后点「生成凭证」，可按收支类别自动生成收款或贴现凭证；已生成凭证、状态不符、未设置收支类别的票据会被跳过。</span>
                    <span>4.「导入」支持先导出再导入，或下载模板按模板格式整理后导入；「附件」可上传票面与回单。</span>
                </div>
            </Modal>
        </div>
    )
}

/** 表单里的「标签 + 控件」一行。 */
function Field({
    label,
    required,
    labelWidth = 104,
    children,
}: {
    label: string
    required?: boolean
    labelWidth?: number
    children: ReactNode
}) {
    return (
        <div className="flex items-center gap-2">
            <span className="shrink-0 text-right text-[13px] text-(--foreground)" style={{ width: labelWidth }}>
                {required && <span className="me-0.5 text-(--danger)">*</span>}
                {label}：
            </span>
            <div className="min-w-0 flex-1">{children}</div>
        </div>
    )
}
