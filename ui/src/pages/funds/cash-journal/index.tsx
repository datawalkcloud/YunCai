import { useCallback, useEffect, useRef, useState } from "react"
import dayjs from "dayjs"
import {
    Alert,
    Button,
    Checkbox,
    Input,
    Modal,
    Radio,
    Segmented,
    Select,
    Spin,
    Table,
} from "antd"
import type { TableColumnsType } from "antd"
import {
    BankOutlined,
    DeleteOutlined,
    DownloadOutlined,
    EditOutlined,
    FileExcelOutlined,
    FileTextOutlined,
    MinusCircleOutlined,
    PlusCircleOutlined,
    PlusOutlined,
    ReloadOutlined,
    UploadOutlined,
    WalletOutlined,
} from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AccountsApi,
    EntryCategoriesApi,
    JournalsApi,
    type AccountDto,
    type EntryCategoryDto,
    type GenerateVouchersBatchResultDto,
    type JournalDetailDto,
    type JournalEntryDto,
    type JournalImportResult,
    type JournalListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount, fmtMoney, toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { useLocalForm } from "@/hooks/useLocalForm"
import { useConfirm } from "@/hooks/useConfirm"
import { PageHeader, SectionCard } from "@/components/page/section"
import { Tile, type TileTone } from "@/components/page/tile"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"
import { AMOUNT_COL, ICON_STYLE } from "@/components/page/styles"
import { DateField, parseDate } from "@/components/fields/date-fields"
import { NumberField } from "@/components/fields/number-field"

const ACCEPT = ".xls,.xlsx"

/** 新建/编辑账户表单值。 */
interface AccountFormValues {
    name: string
    type: 1 | 2
    openingBalance?: number
    bankName?: string
    bankAccountNo?: string
    isEnabled: boolean
}

/** 新增/编辑流水表单值。 */
interface EntryFormValues {
    date?: Date
    summary: string
    category?: string
    counterparty?: string
    income?: number
    expense?: number
}

/** 生成凭证表单值。 */
interface VoucherFormValues {
    counterAccountId: string
}

const emptyAccountForm: AccountFormValues = { name: "", type: 2, openingBalance: 0, bankName: undefined, bankAccountNo: undefined, isEnabled: true }
const emptyEntryForm: EntryFormValues = {
    date: undefined,
    summary: "",
    category: undefined,
    counterparty: undefined,
    income: undefined,
    expense: undefined,
}
const emptyVoucherForm: VoucherFormValues = { counterAccountId: "" }

/** 账户表单校验，提示文案与原 antd rules 逐条一致。 */
function validateAccount(values: AccountFormValues): Partial<Record<keyof AccountFormValues, string>> {
    const errors: Partial<Record<keyof AccountFormValues, string>> = {}
    // required + whitespace：全是空格也算未填
    if (!values.name.trim()) errors.name = "请填写账户名称，如「工商银行」「现金」。"
    // 账户类型是单选，state 里必有值，等价于原 required 规则
    return errors
}

/** 流水表单校验，提示文案与原 antd rules 逐条一致。 */
function validateEntry(values: EntryFormValues): Partial<Record<keyof EntryFormValues, string>> {
    const errors: Partial<Record<keyof EntryFormValues, string>> = {}
    if (!values.date) errors.date = "请选择日期。"
    if (!values.summary.trim()) errors.summary = "请填写摘要。"
    // 原挂在收入项上的自定义 validator（dependencies: ["expense"]）：两条互斥文案
    const income = values.income ?? 0
    const expense = values.expense ?? 0
    if (income === 0 && expense === 0) errors.income = "收入与支出请填写其一。"
    else if (income > 0 && expense > 0) errors.income = "收入与支出只能填写其一。"
    return errors
}

/** 生成凭证表单校验。 */
function validateVoucher(values: VoucherFormValues): Partial<Record<keyof VoucherFormValues, string>> {
    const errors: Partial<Record<keyof VoucherFormValues, string>> = {}
    if (!values.counterAccountId) errors.counterAccountId = "请选择对方科目。"
    return errors
}

/** 批量生成凭证的合并方式（与后端 MergeBy 对齐）。 */
const MERGE_OPTIONS = [
    { value: 1, label: "合并成一张凭证" },
    { value: 2, label: "按日期分开" },
    { value: 3, label: "按收支类别分开" },
    { value: 4, label: "按收支方向分开（收入 / 支出各一张）" },
]

export default function CashJournalPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { confirm, confirmDialog } = useConfirm()

    const [journals, setJournals] = useState<JournalListItemDto[]>([])
    const [selectedId, setSelectedId] = useState<string | null>(null)
    const [detail, setDetail] = useState<JournalDetailDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [detailLoading, setDetailLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    // ── 视图筛选：全部 / 现金 / 银行 ──
    const [viewTab, setViewTab] = useState<"all" | "1" | "2">("all")

    // ── 账户（账簿）新建/编辑 ──
    const [accountModalOpen, setAccountModalOpen] = useState(false)
    const [editingJournal, setEditingJournal] = useState<JournalListItemDto | null>(null)
    const [accountSaving, setAccountSaving] = useState(false)
    const accountForm = useLocalForm(emptyAccountForm, validateAccount)

    // ── 批量生成凭证 ──
    const [selectedEntryIds, setSelectedEntryIds] = useState<Set<string>>(() => new Set())
    const [batchOpen, setBatchOpen] = useState(false)
    const [batchMergeBy, setBatchMergeBy] = useState(1)
    const [batchCounterId, setBatchCounterId] = useState("")
    const [batchSaving, setBatchSaving] = useState(false)
    const [batchError, setBatchError] = useState<string | null>(null)
    const [batchResult, setBatchResult] = useState<GenerateVouchersBatchResultDto | null>(null)

    // ── 流水新增/编辑 ──
    const [entryModalOpen, setEntryModalOpen] = useState(false)
    const [editingEntry, setEditingEntry] = useState<JournalEntryDto | null>(null)
    const [entrySaving, setEntrySaving] = useState(false)
    const entryForm = useLocalForm(emptyEntryForm, validateEntry)
    // 收支类别字典；记账时从中选择，按已填收入/支出方向联动过滤。
    const [categories, setCategories] = useState<EntryCategoryDto[]>([])

    // ── 由流水生成凭证 ──
    const [voucherEntry, setVoucherEntry] = useState<JournalEntryDto | null>(null)
    const [counterAccounts, setCounterAccounts] = useState<AccountDto[]>([])
    const [voucherSaving, setVoucherSaving] = useState(false)
    const voucherForm = useLocalForm(emptyVoucherForm, validateVoucher)

    // ── 流水导入 ──
    const importInputRef = useRef<HTMLInputElement>(null)
    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<File | null>(null)
    const [importing, setImporting] = useState(false)
    const [importResult, setImportResult] = useState<JournalImportResult | null>(null)
    const [importError, setImportError] = useState<string | null>(null)

    /** 加载账户（账簿）列表；当前选中项已不存在时自动选中第一个。 */
    const loadJournals = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const list = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId })
            setJournals(list)
            setSelectedId((prev) => (prev && list.some((j) => j.id === prev) ? prev : (list[0]?.id ?? null)))
        } catch (err) {
            setError(await apiErrorMessage(err, "加载日记账列表失败，请重试。"))
            setJournals([])
            setSelectedId(null)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    /** 加载当前选中账户的账簿详情（含全部流水）。 */
    const loadDetail = useCallback(async () => {
        if (!accountSetId || !selectedId) {
            setDetail(null)
            return
        }
        setDetailLoading(true)
        try {
            const res = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdGet({
                accountSetId,
                id: selectedId,
            })
            setDetail(res)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载日记账详情失败，请重试。"))
            setDetail(null)
        } finally {
            setDetailLoading(false)
        }
    }, [accountSetId, token, selectedId])

    useEffect(() => {
        void loadJournals()
    }, [loadJournals])

    useEffect(() => {
        void loadDetail()
    }, [loadDetail])

    // 切换账户后清空勾选，避免把其它账户的流水并入批量生成。
    useEffect(() => {
        setSelectedEntryIds(new Set())
    }, [selectedId])

    // 加载收支类别字典（失败不阻断记账，仅无可选项）。
    useEffect(() => {
        if (!accountSetId) return
        void (async () => {
            try {
                const list = await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesGet({ accountSetId })
                setCategories(list)
            } catch {
                setCategories([])
            }
        })()
    }, [accountSetId, token])

    /** 账簿详情更新后同步列表项的合计数据，避免整表重拉。 */
    function applyDetail(updated: JournalDetailDto) {
        setDetail(updated)
        setJournals((prev) =>
            prev.map((j) =>
                j.id === updated.id
                    ? {
                        ...j,
                        name: updated.name,
                        openingBalance: updated.openingBalance,
                        totalIncome: updated.totalIncome,
                        totalExpense: updated.totalExpense,
                        closingBalance: updated.closingBalance,
                        entryCount: updated.entries?.length ?? 0,
                    }
                    : j,
            ),
        )
    }

    // ── 账户（账簿）操作 ──

    function openCreateAccount() {
        setEditingJournal(null)
        accountForm.fill({ name: "", type: 2, openingBalance: 0, bankName: undefined, bankAccountNo: undefined, isEnabled: true })
        setAccountModalOpen(true)
    }

    /** 切换现金 / 银行视图；当前选中账户不在视图内时自动选中第一个可见账户。 */
    function switchViewTab(v: "all" | "1" | "2") {
        setViewTab(v)
        const next = journals.filter((j) => v === "all" || String(j.type) === v)
        if (next.length > 0 && !next.some((j) => j.id === selectedId)) setSelectedId(next[0].id ?? null)
    }

    function openEditAccount(j: JournalListItemDto) {
        setEditingJournal(j)
        accountForm.fill({
            name: j.name ?? "",
            type: (j.type as 1 | 2) ?? 2,
            openingBalance: j.openingBalance ?? 0,
            bankName: j.bankName ?? undefined,
            bankAccountNo: j.bankAccountNo ?? undefined,
            isEnabled: j.isEnabled !== false,
        })
        setAccountModalOpen(true)
    }

    async function handleAccountSubmit() {
        if (!accountForm.check()) return
        const values = accountForm.values
        setAccountSaving(true)
        setError(null)
        try {
            const api = new JournalsApi(apiConfig(token))
            if (editingJournal?.id) {
                // 空串 = 清除开户行信息（后端按三态处理：null 保持、空串清除）。
                const updated = await api.accountsetsAccountSetIdJournalsIdPut({
                    accountSetId,
                    id: editingJournal.id,
                    updateJournalRequest: {
                        name: values.name,
                        openingBalance: values.openingBalance,
                        isEnabled: values.isEnabled,
                        bankName: values.type === 2 ? (values.bankName ?? "") : "",
                        bankAccountNo: values.type === 2 ? (values.bankAccountNo ?? "") : "",
                    },
                })
                applyDetail(updated)
                setJournals((prev) => prev.map((j) => (j.id === editingJournal.id ? { ...j, name: updated.name, openingBalance: updated.openingBalance, closingBalance: updated.closingBalance, bankName: updated.bankName, bankAccountNo: updated.bankAccountNo, isEnabled: updated.isEnabled } : j)))
            } else {
                // 不传 accountId，由后端自动在 1001/1002 下创建同名明细科目并绑定。
                const created = await api.accountsetsAccountSetIdJournalsPost({
                    accountSetId,
                    createJournalRequest: {
                        name: values.name,
                        type: values.type,
                        openingBalance: values.openingBalance,
                        bankName: values.type === 2 ? values.bankName : undefined,
                        bankAccountNo: values.type === 2 ? values.bankAccountNo : undefined,
                    },
                })
                await loadJournals()
                if (created.id) setSelectedId(created.id)
            }
            setAccountModalOpen(false)
        } catch (err) {
            setError(await apiErrorMessage(err, editingJournal ? "保存账户失败，请重试。" : "创建账户失败，请重试。"))
        } finally {
            setAccountSaving(false)
        }
    }

    async function handleDeleteAccount(j: JournalListItemDto) {
        if (!j.id) return
        if (!(await confirm(`确定删除账户「${j.name}」吗？其全部流水将一并删除，此操作不可撤销。`))) return
        setError(null)
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdDelete({ accountSetId, id: j.id })
            await loadJournals()
        } catch (err) {
            setError(await apiErrorMessage(err, "删除账户失败，请重试。"))
        }
    }

    // ── 流水操作 ──

    function openCreateEntry() {
        setEditingEntry(null)
        entryForm.fill({
            date: dayjs().toDate(),
            summary: "",
            category: undefined,
            counterparty: undefined,
            income: undefined,
            expense: undefined,
        })
        setEntryModalOpen(true)
    }

    function openEditEntry(e: JournalEntryDto) {
        setEditingEntry(e)
        entryForm.fill({
            date: parseDate(toISODate(e.date)),
            summary: e.summary ?? "",
            category: e.category ?? undefined,
            counterparty: e.counterparty ?? undefined,
            income: e.income || undefined,
            expense: e.expense || undefined,
        })
        setEntryModalOpen(true)
    }

    async function handleEntrySubmit() {
        if (!selectedId) return
        if (!entryForm.check()) return
        const { date, summary, category, counterparty, income, expense } = entryForm.values
        if (!date) return
        setEntrySaving(true)
        setError(null)
        try {
            const api = new JournalsApi(apiConfig(token))
            const request = {
                // toJSON 按 toISOString 截取日期；先归一到 UTC 零点，避免本地时区回退一天。
                date: new Date(dayjs(date).format("YYYY-MM-DD")),
                summary,
                category: category || null,
                counterparty: counterparty || null,
                income: income ?? 0,
                expense: expense ?? 0,
            }
            const updated = editingEntry?.id
                ? await api.accountsetsAccountSetIdJournalsIdEntriesEntryIdPut({
                    accountSetId,
                    id: selectedId,
                    entryId: editingEntry.id,
                    journalEntryRequest: request,
                })
                : await api.accountsetsAccountSetIdJournalsIdEntriesPost({
                    accountSetId,
                    id: selectedId,
                    journalEntryRequest: request,
                })
            applyDetail(updated)
            setEntryModalOpen(false)
        } catch (err) {
            setError(await apiErrorMessage(err, editingEntry ? "保存流水失败，请重试。" : "登记流水失败，请重试。"))
        } finally {
            setEntrySaving(false)
        }
    }

    async function handleDeleteEntry(e: JournalEntryDto) {
        if (!selectedId || !e.id) return
        if (!(await confirm(`确定删除第 ${e.sequence} 笔流水「${e.summary}」吗？`))) return
        setError(null)
        try {
            const updated = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesEntryIdDelete({
                accountSetId,
                id: selectedId,
                entryId: e.id,
            })
            applyDetail(updated)
        } catch (err) {
            setError(await apiErrorMessage(err, "删除流水失败，请重试。"))
        }
    }

    // ── 由流水生成凭证 ──

    /** 惰性加载末级科目列表（单笔与批量生成凭证的对方科目选择共用）。 */
    async function ensureCounterAccounts() {
        if (counterAccounts.length > 0) return
        try {
            const accs = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({
                accountSetId,
                leafOnly: true,
            })
            setCounterAccounts(accs)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载科目列表失败，请重试。"))
        }
    }

    async function openVoucherModal(e: JournalEntryDto) {
        setVoucherEntry(e)
        voucherForm.fill(emptyVoucherForm)
        await ensureCounterAccounts()
    }

    /** 打开批量生成凭证对话框：重置参数并加载对方科目选项。 */
    function openBatchDialog() {
        setBatchMergeBy(1)
        setBatchCounterId("auto")
        setBatchError(null)
        setBatchResult(null)
        setBatchOpen(true)
        void ensureCounterAccounts()
    }

    /** 批量生成凭证：按合并方式出票，对方科目取统一科目或类别默认科目。 */
    async function handleBatchGenerate() {
        if (!selectedId || selectedEntryIds.size === 0) return
        setBatchSaving(true)
        setBatchError(null)
        try {
            const res = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesVouchersPost({
                accountSetId,
                id: selectedId,
                generateVouchersBatchRequest: {
                    entryIds: [...selectedEntryIds],
                    mergeBy: batchMergeBy,
                    counterAccountId: batchCounterId && batchCounterId !== "auto" ? batchCounterId : null,
                },
            })
            setBatchResult(res)
            setSelectedEntryIds(new Set())
            await loadDetail()
        } catch (err) {
            setBatchError(await apiErrorMessage(err, "批量生成凭证失败，请重试。"))
        } finally {
            setBatchSaving(false)
        }
    }

    async function handleGenerateVoucher() {
        if (!selectedId || !voucherEntry?.id) return
        if (!voucherForm.check()) return
        setVoucherSaving(true)
        setError(null)
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesEntryIdVoucherPost({
                accountSetId,
                id: selectedId,
                entryId: voucherEntry.id,
                generateVoucherRequest: { counterAccountId: voucherForm.values.counterAccountId },
            })
            setVoucherEntry(null)
            await loadDetail()
        } catch (err) {
            setError(await apiErrorMessage(err, "生成凭证失败，请重试。"))
        } finally {
            setVoucherSaving(false)
        }
    }

    // ── 导入 / 导出 ──

    /** 携带认证头下载后端文件（模板/导出），从 Content-Disposition 解析文件名。 */
    async function downloadFile(path: string, fallbackName: string) {
        setError(null)
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
            const a = document.createElement("a")
            a.href = url
            a.download = fileName
            a.click()
            URL.revokeObjectURL(url)
        } catch (err) {
            setError(await apiErrorMessage(err, "下载失败，请重试。"))
        }
    }

    function pickImportFile(next: File | null) {
        setImportFile(next)
        setImportResult(null)
        setImportError(null)
    }

    async function handleImport() {
        if (!importFile || importing || !selectedId) return
        setImporting(true)
        setImportError(null)
        setImportResult(null)
        try {
            const res = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdImportPost({
                accountSetId,
                id: selectedId,
                file: importFile,
            })
            setImportResult(res)
            await loadDetail()
            await loadJournals()
        } catch (err) {
            setImportError(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    // ── 渲染 ──

    const selected = journals.find((j) => j.id === selectedId)

    // 视图筛选后的账户列表（全部 / 现金 / 银行）。
    const visibleJournals = journals.filter((j) => viewTab === "all" || String(j.type) === viewTab)

    const entries = detail?.entries ?? []

    // 批量勾选：仅未生成凭证的流水可勾；全选针对这些行。
    const selectableIds = entries.filter((e) => !e.voucherId).map((e) => e.id ?? "")
    const allSelected = selectableIds.length > 0 && selectableIds.every((id) => selectedEntryIds.has(id))
    const someSelected = !allSelected && selectableIds.some((id) => selectedEntryIds.has(id))

    /** 勾选/取消一笔流水。 */
    function toggleEntrySelected(id: string, checked: boolean) {
        setSelectedEntryIds((prev) => {
            const next = new Set(prev)
            if (checked) next.add(id)
            else next.delete(id)
            return next
        })
    }

    /** 表头全选 / 取消全选（仅作用于可勾选行）。 */
    function toggleSelectAll(checked: boolean) {
        setSelectedEntryIds(checked ? new Set(selectableIds) : new Set())
    }

    // 记账可选类别：填了收入只列收入类别，填了支出只列支出类别，否则两组全列。
    const toCategoryItems = (type: number) =>
        categories.filter((c) => c.type === type).map((c) => c.name ?? "").filter((name) => name !== "")
    const categoryOptions = (
        (entryForm.values.income ?? 0) > 0
            ? [{ label: "收入类别", items: toCategoryItems(1) }]
            : (entryForm.values.expense ?? 0) > 0
                ? [{ label: "支出类别", items: toCategoryItems(2) }]
                : [
                    { label: "收入类别", items: toCategoryItems(1) },
                    { label: "支出类别", items: toCategoryItems(2) },
                ]
    )
        .filter((g) => g.items.length > 0)
        .map((g) => ({ label: g.label, options: g.items.map((name) => ({ value: name, label: name })) }))

    // 对方科目候选：排除当前账户绑定的科目（单笔与批量生成共用）。
    const counterAccountOptions = counterAccounts
        .filter((a) => a.id !== detail?.accountId)
        .map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` }))

    const tiles: {
        label: string
        value: string
        tone: TileTone
        icon: React.ComponentType<{ className?: string }>
        hint?: string
    }[] = [
        { label: "期初余额", value: fmtMoney(detail?.openingBalance ?? 0), tone: "gray", icon: WalletOutlined },
        { label: "本期收入", value: fmtMoney(detail?.totalIncome ?? 0), tone: "green", icon: PlusCircleOutlined },
        { label: "本期支出", value: fmtMoney(detail?.totalExpense ?? 0), tone: "red", icon: MinusCircleOutlined },
        { label: "期末余额", value: fmtMoney(detail?.closingBalance ?? 0), tone: "blue", icon: BankOutlined, hint: "期初 + 收入 − 支出" },
    ]

    const columns: TableColumnsType<JournalEntryDto> = [
        {
            title: (
                <Checkbox
                    checked={allSelected}
                    indeterminate={someSelected}
                    onChange={(e) => toggleSelectAll(e.target.checked)}
                    disabled={selectableIds.length === 0}
                    aria-label="全选未生成凭证的流水"
                />
            ),
            width: 48,
            render: (_, e) =>
                // 已生成凭证的流水不可再勾选。
                e.voucherId
                    ? <span className="text-slate-300">—</span>
                    : (
                        <Checkbox
                            checked={selectedEntryIds.has(e.id ?? "")}
                            onChange={(ev) => toggleEntrySelected(e.id ?? "", ev.target.checked)}
                            aria-label={`选择第 ${e.sequence} 笔流水`}
                        />
                    ),
        },
        { title: "序号", align: "center", width: 64, render: (_, e) => e.sequence },
        { title: "日期", className: "whitespace-nowrap", render: (_, e) => toISODate(e.date) },
        { title: "摘要", render: (_, e) => <div className="truncate">{e.summary}</div> },
        { title: "类别", render: (_, e) => e.category ?? "" },
        { title: "往来", render: (_, e) => e.counterparty ?? "" },
        { title: "收入", ...AMOUNT_COL, render: (_, e) => fmtAmount(e.income ?? 0) },
        { title: "支出", ...AMOUNT_COL, render: (_, e) => fmtAmount(e.expense ?? 0) },
        {
            title: "余额",
            ...AMOUNT_COL,
            className: cn(AMOUNT_COL.className, "font-medium text-slate-700"),
            render: (_, e) => fmtMoney(e.balance ?? 0),
        },
        {
            title: "凭证",
            align: "center",
            render: (_, e) =>
                e.voucherNo
                    ? <StatusPill tone="blue">{e.voucherNo}</StatusPill>
                    : <span className="text-slate-300">—</span>,
        },
        {
            title: "操作",
            align: "right",
            render: (_, e) => (
                <div className="flex items-center justify-end gap-0">
                    <Button type="text" size="small" title="编辑流水" icon={<EditOutlined />} onClick={() => openEditEntry(e)} />
                    {!e.voucherId && (
                        <Button
                            type="text"
                            size="small"
                            title="由此流水生成凭证"
                            icon={<FileTextOutlined />}
                            onClick={() => void openVoucherModal(e)}
                        />
                    )}
                    <Button
                        type="text"
                        size="small"
                        danger
                        title="删除流水"
                        icon={<DeleteOutlined />}
                        onClick={() => void handleDeleteEntry(e)}
                    />
                </div>
            ),
        },
    ]

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={WalletOutlined}
                title="资金流水"
                subtitle={
                    <>
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。资金账户（现金、银行）分别登记序时流水；勾选多笔流水可批量生成记账凭证。
                    </>
                }
                actions={
                    <>
                        <Button
                            icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                            disabled={!accountSetId || loading}
                            onClick={() => { void loadJournals(); void loadDetail() }}
                        >
                            刷新
                        </Button>
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreateAccount} disabled={!accountSetId}>
                            新建账户
                        </Button>
                    </>
                }
            />

            {error && <Alert type="error" showIcon closable title={error} onClose={() => setError(null)} />}

            {journals.length === 0 && !loading ? (
                <SectionCard bodyClassName="p-0">
                    <div className="flex flex-col items-center gap-3 py-12">
                        <FileExcelOutlined className="text-[34px]" style={ICON_STYLE.placeholder} />
                        <p className="text-[13px] text-slate-400">
                            {accountSetId ? "还没有资金账户。点击「新建账户」创建现金或银行账户，开始登记流水。" : "请先选择账套。"}
                        </p>
                        {accountSetId && (
                            <Button type="primary" icon={<PlusOutlined />} onClick={openCreateAccount}>
                                新建账户
                            </Button>
                        )}
                    </div>
                </SectionCard>
            ) : (
                <>
                    <div className="flex flex-wrap items-center gap-3">
                        <Segmented
                            options={[
                                { value: "all", label: "全部" },
                                { value: "1", label: "现金账户" },
                                { value: "2", label: "银行账户" },
                            ]}
                            value={viewTab}
                            onChange={(v) => switchViewTab(v as "all" | "1" | "2")}
                        />
                        <nav className="flex min-w-0 flex-1 flex-wrap items-center gap-1.5 rounded-2xl border border-slate-100 bg-white p-1.5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                            {visibleJournals.map((j) => {
                                const active = j.id === selectedId
                                return (
                                    <button
                                        key={j.id}
                                        type="button"
                                        aria-current={active ? "page" : undefined}
                                        onClick={() => setSelectedId(j.id ?? null)}
                                        className={cn(
                                            "flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[12.5px] whitespace-nowrap transition-colors",
                                            active
                                                ? "bg-gradient-to-r from-[#5b9bff] to-[#2f6bff] font-medium text-white shadow-[0_4px_12px_rgba(47,107,255,0.25)]"
                                                : "text-slate-500 hover:bg-[#eef4ff] hover:text-[#2f6bff]",
                                        )}
                                    >
                                        <span className="truncate">{j.name}</span>
                                        {j.isEnabled === false ? (
                                            <span className={cn("text-[11px]", active ? "text-white/75" : "text-amber-500")}>已停用</span>
                                        ) : (
                                            <span className={cn("text-[11px]", active ? "text-white/75" : "text-slate-400")}>
                                                {j.type === 1 ? "现金" : "银行"}
                                            </span>
                                        )}
                                    </button>
                                )
                            })}
                            {visibleJournals.length === 0 && (
                                <span className="px-3 py-1.5 text-[12.5px] text-slate-400">
                                    {viewTab === "1" ? "暂无现金账户" : "暂无银行账户"}
                                </span>
                            )}
                        </nav>
                    </div>

                    {visibleJournals.length > 0 && (
                        <>
                            <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
                                {tiles.map((t) => (
                                    <Tile key={t.label} {...t} />
                                ))}
                            </section>

                            <SectionCard
                                title={
                                    <>
                                        <h3 className="truncate text-[14px] font-semibold text-slate-800">
                                            {selected?.name ?? "明细流水"}
                                        </h3>
                                        {selected?.accountCode && (
                                            <span className="truncate text-[12px] text-slate-400">
                                                对应科目：{selected.accountCode} {selected.accountName}
                                            </span>
                                        )}
                                        {selected?.type === 2 && selected?.bankName && (
                                            <span className="truncate text-[12px] text-slate-400">
                                                开户行：{selected.bankName}
                                                {selected.bankAccountNo ? ` · ${selected.bankAccountNo}` : ""}
                                            </span>
                                        )}
                                        {selected && selected.isEnabled === false && <StatusPill tone="amber">已停用</StatusPill>}
                                        {detailLoading && <Spin size="small" />}
                                    </>
                                }
                                bodyClassName="p-0"
                                action={
                                    <div className="flex flex-wrap items-center justify-end gap-2">
                                        <Button icon={<DownloadOutlined />} onClick={() => void downloadFile(`/accountsets/${accountSetId}/Journals/import/template`, "日记账导入模板.xlsx")}>
                                            模板
                                        </Button>
                                        <Button icon={<UploadOutlined />} onClick={() => { setImportOpen(true); pickImportFile(null) }} disabled={!selectedId}>
                                            导入
                                        </Button>
                                        <Button
                                            icon={<DownloadOutlined />}
                                            disabled={!selectedId}
                                            onClick={() => selectedId && void downloadFile(`/accountsets/${accountSetId}/Journals/${selectedId}/export`, `${selected?.name ?? "日记账"}.xlsx`)}
                                        >
                                            导出
                                        </Button>
                                        <Button icon={<EditOutlined />} onClick={() => selected && openEditAccount(selected)} disabled={!selected}>
                                            账户设置
                                        </Button>
                                        <Button danger icon={<DeleteOutlined />} onClick={() => selected && void handleDeleteAccount(selected)} disabled={!selected}>
                                            删除账户
                                        </Button>
                                        <Button
                                            icon={<FileTextOutlined />}
                                            onClick={openBatchDialog}
                                            disabled={!selectedId || selectedEntryIds.size === 0}
                                            title="勾选左侧流水后批量生成凭证"
                                        >
                                            批量生成凭证{selectedEntryIds.size > 0 ? `（${selectedEntryIds.size}）` : ""}
                                        </Button>
                                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreateEntry} disabled={!selectedId}>
                                            登记流水
                                        </Button>
                                    </div>
                                }
                            >
                                <Table<JournalEntryDto>
                                    columns={columns}
                                    dataSource={entries}
                                    rowKey={(row) => row.id ?? String(row.sequence)}
                                    pagination={false}
                                    size="small"
                                    loading={detailLoading && entries.length === 0}
                                    locale={{ emptyText: "该账户暂无流水，点击「登记流水」或「导入」添加。" }}
                                    summary={
                                        entries.length > 0
                                            ? () => (
                                                <Table.Summary>
                                                    <Table.Summary.Row>
                                                        <Table.Summary.Cell index={0} colSpan={6}>本期合计</Table.Summary.Cell>
                                                        <Table.Summary.Cell index={6} {...AMOUNT_COL}>
                                                            {fmtMoney(detail?.totalIncome ?? 0)}
                                                        </Table.Summary.Cell>
                                                        <Table.Summary.Cell index={7} {...AMOUNT_COL}>
                                                            {fmtMoney(detail?.totalExpense ?? 0)}
                                                        </Table.Summary.Cell>
                                                        <Table.Summary.Cell index={8} {...AMOUNT_COL}>
                                                            {fmtMoney(detail?.closingBalance ?? 0)}
                                                        </Table.Summary.Cell>
                                                        <Table.Summary.Cell index={9} colSpan={2} />
                                                    </Table.Summary.Row>
                                                </Table.Summary>
                                            )
                                            : undefined
                                    }
                                />
                            </SectionCard>
                        </>
                    )}
                    {visibleJournals.length === 0 && (
                        <SectionCard bodyClassName="p-0">
                            <div className="flex flex-col items-center gap-3 py-12">
                                <BankOutlined className="text-[34px]" style={ICON_STYLE.placeholder} />
                                <p className="text-[13px] text-slate-400">
                                    该视图下暂无账户，可切换到「全部」或点击「新建账户」创建。
                                </p>
                            </div>
                        </SectionCard>
                    )}
                </>
            )}

            {/* 新建/编辑账户 */}
            <Modal
                open={accountModalOpen}
                title={editingJournal ? "账户设置" : "新建账户"}
                onCancel={() => setAccountModalOpen(false)}
                onOk={() => void handleAccountSubmit()}
                okText={editingJournal ? "保存" : "创建"}
                cancelText="取消"
                confirmLoading={accountSaving}
            >
                <div className="flex flex-col gap-4 py-2">
                    <FormField label="账户名称" htmlFor="account-name" required error={accountForm.errors.name}>
                        <Input
                            id="account-name"
                            value={accountForm.values.name}
                            onChange={(e) => accountForm.set("name", e.target.value)}
                            placeholder="如：工商银行、农业银行、现金"
                            maxLength={100}
                            status={accountForm.errors.name ? "error" : undefined}
                        />
                    </FormField>
                    <FormField label="账户类型" required>
                        <Radio.Group
                            disabled={!!editingJournal}
                            value={accountForm.values.type}
                            onChange={(e) =>
                                accountForm.patch(
                                    e.target.value === 1
                                        ? { type: 1, bankName: undefined, bankAccountNo: undefined }
                                        : { type: 2 },
                                )
                            }
                            options={[
                                { value: 2, label: "银行存款" },
                                { value: 1, label: "现金" },
                            ]}
                        />
                    </FormField>
                    {accountForm.values.type === 2 && (
                        <div className="grid grid-cols-2 gap-3">
                            <FormField label="开户行（选填）" htmlFor="account-bank-name">
                                <Input
                                    id="account-bank-name"
                                    value={accountForm.values.bankName ?? ""}
                                    onChange={(e) => accountForm.set("bankName", e.target.value)}
                                    placeholder="如：中国工商银行深圳分行"
                                    maxLength={100}
                                />
                            </FormField>
                            <FormField label="银行账号（选填）" htmlFor="account-bank-no">
                                <Input
                                    id="account-bank-no"
                                    value={accountForm.values.bankAccountNo ?? ""}
                                    onChange={(e) => accountForm.set("bankAccountNo", e.target.value)}
                                    placeholder="如：6222020200112233445"
                                    maxLength={50}
                                />
                            </FormField>
                        </div>
                    )}
                    <FormField label="期初余额" htmlFor="account-opening-balance">
                        <NumberField
                            id="account-opening-balance"
                            value={accountForm.values.openingBalance}
                            onChange={(v) => accountForm.set("openingBalance", v)}
                            precision={2}
                            min={0}
                            placeholder="0.00"
                        />
                    </FormField>
                    {editingJournal ? (
                        <div className="flex flex-col gap-2">
                            <Checkbox
                                checked={accountForm.values.isEnabled}
                                onChange={(e) => accountForm.set("isEnabled", e.target.checked)}
                            >
                                启用此账户
                            </Checkbox>
                            <p className="text-sm leading-normal font-normal text-slate-400">
                                停用后不能再登记新流水（含导入、内部转账），历史数据与报表照常保留。
                            </p>
                        </div>
                    ) : (
                        <p className="text-sm leading-normal font-normal text-slate-400">
                            创建后系统会自动在「库存现金 1001 / 银行存款 1002」下建立同名明细科目并与账户绑定。
                        </p>
                    )}
                </div>
            </Modal>

            {/* 登记/编辑流水 */}
            <Modal
                open={entryModalOpen}
                title={editingEntry ? "编辑流水" : "登记流水"}
                onCancel={() => setEntryModalOpen(false)}
                onOk={() => void handleEntrySubmit()}
                okText="保存"
                cancelText="取消"
                confirmLoading={entrySaving}
            >
                <div className="flex flex-col gap-4 py-2">
                    <FormField label="日期" required error={entryForm.errors.date}>
                        <DateField
                            value={entryForm.values.date}
                            onChange={(v) => entryForm.set("date", v)}
                            allowClear={false}
                            className="w-full"
                        />
                    </FormField>
                    <FormField label="摘要" htmlFor="entry-summary" required error={entryForm.errors.summary}>
                        <Input
                            id="entry-summary"
                            value={entryForm.values.summary}
                            onChange={(e) => entryForm.set("summary", e.target.value)}
                            placeholder="如：收到货款、支付办公费"
                            maxLength={200}
                            status={entryForm.errors.summary ? "error" : undefined}
                        />
                    </FormField>
                    <div className="grid grid-cols-2 gap-3">
                        <FormField label="收支类别（选填）" htmlFor="entry-category">
                            <Select
                                id="entry-category"
                                className="w-full"
                                allowClear
                                placeholder={categories.length > 0 ? "选择收支类别" : "先在「收支类别」页新建类别"}
                                value={entryForm.values.category ?? undefined}
                                onChange={(v) => entryForm.set("category", v || undefined)}
                                options={categoryOptions}
                            />
                        </FormField>
                        <FormField label="往来（选填）" htmlFor="entry-counterparty">
                            <Input
                                id="entry-counterparty"
                                value={entryForm.values.counterparty ?? ""}
                                onChange={(e) => entryForm.set("counterparty", e.target.value)}
                                placeholder="往来单位或个人"
                                maxLength={100}
                            />
                        </FormField>
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                        <FormField label="收入" htmlFor="entry-income" error={entryForm.errors.income}>
                            <NumberField
                                id="entry-income"
                                value={entryForm.values.income}
                                onChange={(v) => entryForm.set("income", v)}
                                precision={2}
                                min={0}
                                placeholder="0.00"
                            />
                        </FormField>
                        <FormField label="支出" htmlFor="entry-expense">
                            <NumberField
                                id="entry-expense"
                                value={entryForm.values.expense}
                                onChange={(v) => entryForm.set("expense", v)}
                                precision={2}
                                min={0}
                                placeholder="0.00"
                            />
                        </FormField>
                    </div>
                </div>
            </Modal>

            {/* 由流水生成凭证 */}
            <Modal
                open={voucherEntry !== null}
                title="由流水生成凭证"
                onCancel={() => setVoucherEntry(null)}
                onOk={() => void handleGenerateVoucher()}
                okText="生成凭证"
                cancelText="取消"
                confirmLoading={voucherSaving}
            >
                {voucherEntry && (
                    <div className="flex flex-col gap-4 py-2">
                        <Alert
                            type="info"
                            showIcon
                            title={
                                voucherEntry.income
                                    ? `收入 ${fmtMoney(voucherEntry.income)}：借 ${selected?.accountName ?? "货币资金"} / 贷 对方科目`
                                    : `支出 ${fmtMoney(voucherEntry.expense ?? 0)}：借 对方科目 / 贷 ${selected?.accountName ?? "货币资金"}`
                            }
                            description={`${toISODate(voucherEntry.date)} ${voucherEntry.summary}`}
                        />
                        <FormField label="对方科目" htmlFor="voucher-counter-account" required error={voucherForm.errors.counterAccountId}>
                            <Select
                                id="voucher-counter-account"
                                className="w-full"
                                placeholder="选择对方科目（末级科目）"
                                value={voucherForm.values.counterAccountId || undefined}
                                onChange={(v) => voucherForm.set("counterAccountId", v)}
                                options={counterAccountOptions}
                                status={voucherForm.errors.counterAccountId ? "error" : undefined}
                            />
                        </FormField>
                    </div>
                )}
            </Modal>

            {/* 批量生成凭证 */}
            <Modal
                open={batchOpen}
                title="批量生成凭证"
                onCancel={() => setBatchOpen(false)}
                width={512}
                footer={
                    batchResult
                        ? (
                            <Button type="primary" onClick={() => setBatchOpen(false)}>
                                完成
                            </Button>
                        )
                        : (
                            <>
                                <Button onClick={() => setBatchOpen(false)}>取消</Button>
                                <Button
                                    type="primary"
                                    loading={batchSaving}
                                    disabled={selectedEntryIds.size === 0}
                                    onClick={() => void handleBatchGenerate()}
                                >
                                    生成凭证
                                </Button>
                            </>
                        )
                }
            >
                <p className="mb-4 text-[12.5px] text-slate-400">
                    {batchResult
                        ? "凭证已生成并回填到对应流水。"
                        : `已选 ${selectedEntryIds.size} 笔流水；同一张凭证内相同科目、相同方向的行将自动汇总为一行。`}
                </p>
                {batchResult ? (
                    <Alert
                        type="success"
                        showIcon
                        title={`成功生成 ${batchResult.vouchers?.length ?? 0} 张凭证，共涉及 ${batchResult.entryCount ?? 0} 笔流水。`}
                        description={
                            <div className="flex flex-col gap-2">
                                <div className="flex flex-wrap items-center gap-1.5">
                                    {batchResult.vouchers?.map((v) => (
                                        <StatusPill key={v.id} tone="blue">{v.label}</StatusPill>
                                    ))}
                                </div>
                                <p>生成的凭证为草稿状态，请到「凭证管理」审核并过账后计入账簿。</p>
                            </div>
                        }
                    />
                ) : (
                    <div className="flex flex-col gap-4">
                        {batchError && <Alert type="error" showIcon title={batchError} />}
                        <FormField label="合并方式">
                            <Radio.Group
                                className="flex flex-col gap-2"
                                value={batchMergeBy}
                                onChange={(e) => setBatchMergeBy(e.target.value)}
                                options={MERGE_OPTIONS}
                            />
                        </FormField>
                        <FormField
                            label="对方科目"
                            htmlFor="batch-counter-account"
                            description="未配置默认对方科目的流水将拒绝出票；可在此指定统一对方科目一次出票。"
                        >
                            <Select
                                id="batch-counter-account"
                                className="w-full"
                                value={batchCounterId}
                                onChange={setBatchCounterId}
                                options={[
                                    { value: "auto", label: "自动：取「收支类别」的默认对方科目" },
                                    ...counterAccountOptions,
                                ]}
                            />
                        </FormField>
                    </div>
                )}
            </Modal>

            {/* 导入流水 */}
            <Modal
                open={importOpen}
                title={`导入流水${selected ? `：${selected.name}` : ""}`}
                onCancel={() => setImportOpen(false)}
                footer={null}
            >
                <p className="mb-4 text-[12.5px] text-slate-400">
                    支持 .xls / .xlsx 文件，列格式与导入模板一致（日期、摘要必填）。非法行会跳过并提示，不影响其余行导入。
                </p>
                <div className="flex flex-col gap-4">
                    <div className="flex flex-wrap items-center gap-2">
                        <Button icon={<FileExcelOutlined />} onClick={() => importInputRef.current?.click()}>
                            {importFile ? importFile.name : "选择文件"}
                        </Button>
                        <input
                            ref={importInputRef}
                            type="file"
                            accept={ACCEPT}
                            className="hidden"
                            onChange={(e) => pickImportFile(e.target.files?.[0] ?? null)}
                        />
                        <Button
                            type="primary"
                            icon={importing ? undefined : <UploadOutlined />}
                            loading={importing}
                            disabled={!importFile}
                            onClick={() => void handleImport()}
                        >
                            {importing ? "导入中…" : "开始导入"}
                        </Button>
                        {importFile && !importing && <Button onClick={() => pickImportFile(null)}>清除</Button>}
                    </div>
                    {importError && <Alert type="error" showIcon title={importError} />}
                    {importResult && (
                        <Alert
                            type={(importResult.skippedRows ?? 0) > 0 ? "warning" : "success"}
                            showIcon
                            title={`共 ${importResult.totalRows ?? 0} 行，成功导入 ${importResult.importedEntries ?? 0} 条，跳过 ${importResult.skippedRows ?? 0} 行。`}
                            description={
                                (importResult.warnings?.length ?? 0) > 0 ? (
                                    <ul className="flex list-disc flex-col gap-1 ps-4">
                                        {importResult.warnings?.map((w, i) => <li key={i}>{w}</li>)}
                                    </ul>
                                ) : undefined
                            }
                        />
                    )}
                </div>
            </Modal>

            {confirmDialog}
        </div>
    )
}
