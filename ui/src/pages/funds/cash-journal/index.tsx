import { useCallback, useEffect, useRef, useState } from "react"
import {
    ArrowDownLeft,
    ArrowUpRight,
    CircleAlert,
    CircleCheck,
    Download,
    FileSpreadsheet,
    FileText,
    Info,
    Landmark,
    Pencil,
    PiggyBank,
    Plus,
    RefreshCw,
    Trash2,
    TriangleAlert,
    Upload,
    Wallet,
    X,
} from "lucide-react"
import { createColumnHelper, tableFeatures, useTable } from "@tanstack/react-table"
import dayjs from "dayjs"

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
import { StatusPill } from "@/components/page/status-pill"
import {
    CARD_BTN,
    CARD_BTN_DANGER,
    CARD_BTN_PRIMARY,
    ICON_BTN,
    ICON_BTN_DANGER,
    PAGE_BTN_GHOST,
    PAGE_BTN_PRIMARY,
} from "@/components/page/styles"
import { Alert, AlertAction, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { DateField, parseDate } from "@/components/fields/date-fields"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { NumberField } from "@/components/fields/number-field"
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group"
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group"

const ACCEPT = ".xls,.xlsx"

/** Radix Select 不允许空串作为选项值，用这个哨兵表示「不选收支类别」。 */
const NO_CATEGORY = "__none__"

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

const features = tableFeatures({})
const entryColumn = createColumnHelper<typeof features, JournalEntryDto>()

/** 各列的对齐方式按列 id 取用，避免写行内样式。 */
const COLUMN_CLASS: Record<string, { head: string, cell: string }> = {
    select: { head: "w-[40px] pl-4", cell: "pl-4" },
    sequence: { head: "text-center", cell: "text-center" },
    summary: { head: "text-left", cell: "w-full" },
    income: { head: "text-right", cell: "text-right tabular-nums" },
    expense: { head: "text-right", cell: "text-right tabular-nums" },
    balance: { head: "text-right", cell: "text-right tabular-nums" },
    voucherNo: { head: "text-center", cell: "text-center" },
    actions: { head: "text-right", cell: "text-right" },
}

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

    /** 加载账户（账簿）列表；keepSelection 为 false 或当前选中项已不存在时自动选中第一个。 */
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

    // 批量勾选：仅未生成凭证的流水可勾；全选针对这些行。
    const selectableIds = (detail?.entries ?? []).filter((e) => !e.voucherId).map((e) => e.id ?? "")
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

    // 记账可选类别：填了收入只列收入类别，填了支出只列支出类别，否则分组全列。
    const toCategoryItems = (type: number) =>
        categories.filter((c) => c.type === type).map((c) => c.name ?? "").filter((name) => name !== "")
    const categoryGroups =
        (entryForm.values.income ?? 0) > 0
            ? [{ label: "收入类别", items: toCategoryItems(1) }]
            : (entryForm.values.expense ?? 0) > 0
                ? [{ label: "支出类别", items: toCategoryItems(2) }]
                : [
                    { label: "收入类别", items: toCategoryItems(1) },
                    { label: "支出类别", items: toCategoryItems(2) },
                ]

    const tiles: {
        label: string
        value: string
        tone: TileTone
        icon: React.ComponentType<{ className?: string }>
        hint?: string
    }[] = [
        { label: "期初余额", value: fmtMoney(detail?.openingBalance ?? 0), tone: "gray", icon: Wallet },
        { label: "本期收入", value: fmtMoney(detail?.totalIncome ?? 0), tone: "green", icon: ArrowDownLeft },
        { label: "本期支出", value: fmtMoney(detail?.totalExpense ?? 0), tone: "red", icon: ArrowUpRight },
        { label: "期末余额", value: fmtMoney(detail?.closingBalance ?? 0), tone: "blue", icon: PiggyBank, hint: "期初 + 收入 − 支出" },
    ]

    const columns = [
        entryColumn.display({
            id: "select",
            header: () => (
                <Checkbox
                    checked={allSelected ? true : someSelected ? "indeterminate" : false}
                    onCheckedChange={(v) => toggleSelectAll(v === true)}
                    aria-label="全选未生成凭证的流水"
                    disabled={selectableIds.length === 0}
                />
            ),
            cell: ({ row }) => {
                const e = row.original
                // 已生成凭证的流水不可再勾选。
                if (e.voucherId) return <span className="text-slate-300">—</span>
                return (
                    <Checkbox
                        checked={selectedEntryIds.has(e.id ?? "")}
                        onCheckedChange={(v) => toggleEntrySelected(e.id ?? "", v === true)}
                        aria-label={`选择第 ${e.sequence} 笔流水`}
                    />
                )
            },
        }),
        entryColumn.display({
            id: "sequence",
            header: "序号",
            cell: ({ row }) => <span>{row.original.sequence}</span>,
        }),
        entryColumn.display({
            id: "date",
            header: "日期",
            cell: ({ row }) => <span>{toISODate(row.original.date)}</span>,
        }),
        entryColumn.display({
            id: "summary",
            header: "摘要",
            cell: ({ row }) => <div className="truncate">{row.original.summary}</div>,
        }),
        entryColumn.display({
            id: "category",
            header: "类别",
            cell: ({ row }) => <span>{row.original.category ?? ""}</span>,
        }),
        entryColumn.display({
            id: "counterparty",
            header: "往来",
            cell: ({ row }) => <span>{row.original.counterparty ?? ""}</span>,
        }),
        entryColumn.display({
            id: "income",
            header: "收入",
            cell: ({ row }) => <span>{fmtAmount(row.original.income ?? 0)}</span>,
        }),
        entryColumn.display({
            id: "expense",
            header: "支出",
            cell: ({ row }) => <span>{fmtAmount(row.original.expense ?? 0)}</span>,
        }),
        entryColumn.display({
            id: "balance",
            header: "余额",
            cell: ({ row }) => <span className="font-medium text-slate-700">{fmtMoney(row.original.balance ?? 0)}</span>,
        }),
        entryColumn.display({
            id: "voucherNo",
            header: "凭证",
            cell: ({ row }) =>
                row.original.voucherNo
                    ? <StatusPill tone="blue">{row.original.voucherNo}</StatusPill>
                    : <span className="text-slate-300">—</span>,
        }),
        entryColumn.display({
            id: "actions",
            header: "操作",
            cell: ({ row }) => {
                const e = row.original
                return (
                    <div className="flex items-center justify-end gap-0">
                        <Button variant="ghost" size="icon-xs" title="编辑流水" onClick={() => openEditEntry(e)} className={ICON_BTN}>
                            <Pencil />
                        </Button>
                        {!e.voucherId && (
                            <Button variant="ghost" size="icon-xs" title="由此流水生成凭证" onClick={() => void openVoucherModal(e)} className={ICON_BTN}>
                                <FileText />
                            </Button>
                        )}
                        <Button variant="ghost" size="icon-xs" title="删除流水" onClick={() => void handleDeleteEntry(e)} className={ICON_BTN_DANGER}>
                            <Trash2 />
                        </Button>
                    </div>
                )
            },
        }),
    ]

    const table = useTable({
        features,
        data: detail?.entries ?? [],
        columns,
        getRowId: (row) => row.id ?? String(row.sequence),
    })
    const headerGroups = table.getHeaderGroups()
    const rows = table.getRowModel().rows

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={Wallet}
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
                            variant="outline"
                            className={PAGE_BTN_GHOST}
                            onClick={() => { void loadJournals(); void loadDetail() }}
                            disabled={!accountSetId || loading}
                        >
                            {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                            刷新
                        </Button>
                        <Button className={PAGE_BTN_PRIMARY} onClick={openCreateAccount} disabled={!accountSetId}>
                            <Plus data-icon="inline-start" />
                            新建账户
                        </Button>
                    </>
                }
            />

            {error && (
                <Alert variant="destructive" className="rounded-2xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                    <CircleAlert />
                    <AlertTitle>{error}</AlertTitle>
                    <AlertAction>
                        <Button variant="ghost" size="icon-xs" title="关闭" onClick={() => setError(null)}>
                            <X />
                        </Button>
                    </AlertAction>
                </Alert>
            )}

            {journals.length === 0 && !loading ? (
                <SectionCard bodyClassName="p-0">
                    <div className="flex flex-col items-center gap-3 py-12">
                        <FileSpreadsheet size={40} strokeWidth={1.2} className="text-slate-300" />
                        <p className="text-[13px] text-slate-400">
                            {accountSetId ? "还没有资金账户。点击「新建账户」创建现金或银行账户，开始登记流水。" : "请先选择账套。"}
                        </p>
                        {accountSetId && (
                            <Button className={PAGE_BTN_PRIMARY} onClick={openCreateAccount}>
                                <Plus data-icon="inline-start" />
                                新建账户
                            </Button>
                        )}
                    </div>
                </SectionCard>
            ) : (
                <>
                    <div className="flex flex-wrap items-center gap-3">
                        <ToggleGroup
                            type="single"
                            variant="outline"
                            size="sm"
                            spacing={0}
                            value={viewTab}
                            onValueChange={(v) => v && switchViewTab(v as "all" | "1" | "2")}
                        >
                            <ToggleGroupItem value="all">全部</ToggleGroupItem>
                            <ToggleGroupItem value="1">现金账户</ToggleGroupItem>
                            <ToggleGroupItem value="2">银行账户</ToggleGroupItem>
                        </ToggleGroup>
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
                                {detailLoading && <Spinner className="size-3.5 text-slate-300" />}
                            </>
                        }
                        bodyClassName="p-0"
                        action={
                            <div className="flex flex-wrap items-center justify-end gap-2">
                                <Button variant="outline" className={CARD_BTN} onClick={() => void downloadFile(`/accountsets/${accountSetId}/Journals/import/template`, "日记账导入模板.xlsx")}>
                                    <Download data-icon="inline-start" />
                                    模板
                                </Button>
                                <Button variant="outline" className={CARD_BTN} onClick={() => { setImportOpen(true); pickImportFile(null) }} disabled={!selectedId}>
                                    <Upload data-icon="inline-start" />
                                    导入
                                </Button>
                                <Button variant="outline" className={CARD_BTN} onClick={() => selectedId && void downloadFile(`/accountsets/${accountSetId}/Journals/${selectedId}/export`, `${selected?.name ?? "日记账"}.xlsx`)} disabled={!selectedId}>
                                    <Download data-icon="inline-start" />
                                    导出
                                </Button>
                                <Button variant="outline" className={CARD_BTN} onClick={() => selected && openEditAccount(selected)} disabled={!selected}>
                                    <Pencil data-icon="inline-start" />
                                    账户设置
                                </Button>
                                <Button variant="ghost" className={CARD_BTN_DANGER} onClick={() => selected && void handleDeleteAccount(selected)} disabled={!selected}>
                                    <Trash2 data-icon="inline-start" />
                                    删除账户
                                </Button>
                                <Button
                                    variant="outline"
                                    className={CARD_BTN}
                                    onClick={openBatchDialog}
                                    disabled={!selectedId || selectedEntryIds.size === 0}
                                    title="勾选左侧流水后批量生成凭证"
                                >
                                    <FileText data-icon="inline-start" />
                                    批量生成凭证{selectedEntryIds.size > 0 ? `（${selectedEntryIds.size}）` : ""}
                                </Button>
                                <Button className={CARD_BTN_PRIMARY} onClick={openCreateEntry} disabled={!selectedId}>
                                    <Plus data-icon="inline-start" />
                                    登记流水
                                </Button>
                            </div>
                        }
                    >
                        <Table>
                            <TableHeader>
                                {headerGroups.map((hg) => (
                                    <TableRow key={hg.id}>
                                        {hg.headers.map((h) => (
                                            <TableHead key={h.id} className={COLUMN_CLASS[h.column.id]?.head}>
                                                <table.FlexRender header={h} />
                                            </TableHead>
                                        ))}
                                    </TableRow>
                                ))}
                            </TableHeader>
                            <TableBody>
                                {rows.map((row) => (
                                    <TableRow key={row.id}>
                                        {row.getAllCells().map((cell) => (
                                            <TableCell key={cell.id} className={COLUMN_CLASS[cell.column.id]?.cell}>
                                                <table.FlexRender cell={cell} />
                                            </TableCell>
                                        ))}
                                    </TableRow>
                                ))}
                                {rows.length === 0 && !detailLoading && (
                                    <TableRow>
                                        <TableCell colSpan={columns.length} className="text-center text-slate-400">
                                            该账户暂无流水，点击「登记流水」或「导入」添加。
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                            {rows.length > 0 && (
                                <TableFooter>
                                    <TableRow>
                                        <TableCell colSpan={6}>本期合计</TableCell>
                                        <TableCell className="text-right tabular-nums">{fmtMoney(detail?.totalIncome ?? 0)}</TableCell>
                                        <TableCell className="text-right tabular-nums">{fmtMoney(detail?.totalExpense ?? 0)}</TableCell>
                                        <TableCell className="text-right tabular-nums">{fmtMoney(detail?.closingBalance ?? 0)}</TableCell>
                                        <TableCell colSpan={2} />
                                    </TableRow>
                                </TableFooter>
                            )}
                        </Table>
                    </SectionCard>
                    </>
                    )}
                    {visibleJournals.length === 0 && (
                        <SectionCard bodyClassName="p-0">
                            <div className="flex flex-col items-center gap-3 py-12">
                                <Landmark size={40} strokeWidth={1.2} className="text-slate-300" />
                                <p className="text-[13px] text-slate-400">
                                    该视图下暂无账户，可切换到「全部」或点击「新建账户」创建。
                                </p>
                            </div>
                        </SectionCard>
                    )}
                </>
            )}

            {/* 新建/编辑账户 */}
            <Dialog open={accountModalOpen} onOpenChange={setAccountModalOpen}>
                <DialogContent aria-describedby={undefined}>
                    <DialogHeader>
                        <DialogTitle>{editingJournal ? "账户设置" : "新建账户"}</DialogTitle>
                    </DialogHeader>
                    <div className="flex flex-col gap-4">
                        <Field data-invalid={Boolean(accountForm.errors.name)}>
                            <FieldLabel htmlFor="account-name">账户名称</FieldLabel>
                            <Input
                                id="account-name"
                                value={accountForm.values.name}
                                onChange={(e) => accountForm.set("name", e.target.value)}
                                placeholder="如：工商银行、农业银行、现金"
                                maxLength={100}
                                aria-invalid={Boolean(accountForm.errors.name)}
                            />
                            <FieldError>{accountForm.errors.name}</FieldError>
                        </Field>
                        <Field>
                            <FieldLabel htmlFor="account-type">账户类型</FieldLabel>
                            <RadioGroup
                                id="account-type"
                                className="flex flex-row items-center gap-4"
                                disabled={!!editingJournal}
                                value={String(accountForm.values.type)}
                                onValueChange={(v) =>
                                    accountForm.patch(v === "1" ? { type: 1, bankName: undefined, bankAccountNo: undefined } : { type: 2 })
                                }
                            >
                                <div className="flex items-center gap-2">
                                    <RadioGroupItem value="2" id="account-type-bank" />
                                    <FieldLabel htmlFor="account-type-bank">银行存款</FieldLabel>
                                </div>
                                <div className="flex items-center gap-2">
                                    <RadioGroupItem value="1" id="account-type-cash" />
                                    <FieldLabel htmlFor="account-type-cash">现金</FieldLabel>
                                </div>
                            </RadioGroup>
                        </Field>
                        {accountForm.values.type === 2 && (
                            <div className="grid grid-cols-2 gap-3">
                                <Field>
                                    <FieldLabel htmlFor="account-bank-name">开户行（选填）</FieldLabel>
                                    <Input
                                        id="account-bank-name"
                                        value={accountForm.values.bankName ?? ""}
                                        onChange={(e) => accountForm.set("bankName", e.target.value)}
                                        placeholder="如：中国工商银行深圳分行"
                                        maxLength={100}
                                    />
                                </Field>
                                <Field>
                                    <FieldLabel htmlFor="account-bank-no">银行账号（选填）</FieldLabel>
                                    <Input
                                        id="account-bank-no"
                                        value={accountForm.values.bankAccountNo ?? ""}
                                        onChange={(e) => accountForm.set("bankAccountNo", e.target.value)}
                                        placeholder="如：6222020200112233445"
                                        maxLength={50}
                                    />
                                </Field>
                            </div>
                        )}
                        <Field>
                            <FieldLabel htmlFor="account-opening-balance">期初余额</FieldLabel>
                            <NumberField
                                id="account-opening-balance"
                                value={accountForm.values.openingBalance}
                                onChange={(v) => accountForm.set("openingBalance", v)}
                                precision={2}
                                min={0}
                                placeholder="0.00"
                            />
                        </Field>
                        {editingJournal && (
                            <Field>
                                <div className="flex items-center gap-2">
                                    <Checkbox
                                        id="account-enabled"
                                        checked={accountForm.values.isEnabled}
                                        onCheckedChange={(v) => accountForm.set("isEnabled", v === true)}
                                    />
                                    <FieldLabel htmlFor="account-enabled">启用此账户</FieldLabel>
                                </div>
                                <FieldDescription>
                                    停用后不能再登记新流水（含导入、内部转账），历史数据与报表照常保留。
                                </FieldDescription>
                            </Field>
                        )}
                        {!editingJournal && (
                            <FieldDescription>
                                创建后系统会自动在「库存现金 1001 / 银行存款 1002」下建立同名明细科目并与账户绑定。
                            </FieldDescription>
                        )}
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setAccountModalOpen(false)}>取消</Button>
                        <Button onClick={() => void handleAccountSubmit()} disabled={accountSaving}>
                            {accountSaving && <Spinner data-icon="inline-start" />}
                            {editingJournal ? "保存" : "创建"}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 登记/编辑流水 */}
            <Dialog open={entryModalOpen} onOpenChange={setEntryModalOpen}>
                <DialogContent aria-describedby={undefined}>
                    <DialogHeader>
                        <DialogTitle>{editingEntry ? "编辑流水" : "登记流水"}</DialogTitle>
                    </DialogHeader>
                    <div className="flex flex-col gap-4">
                        <Field data-invalid={Boolean(entryForm.errors.date)}>
                            <FieldLabel htmlFor="entry-date">日期</FieldLabel>
                            <DateField
                                value={entryForm.values.date}
                                onChange={(v) => entryForm.set("date", v)}
                                allowClear={false}
                                className="w-full"
                            />
                            <FieldError>{entryForm.errors.date}</FieldError>
                        </Field>
                        <Field data-invalid={Boolean(entryForm.errors.summary)}>
                            <FieldLabel htmlFor="entry-summary">摘要</FieldLabel>
                            <Input
                                id="entry-summary"
                                value={entryForm.values.summary}
                                onChange={(e) => entryForm.set("summary", e.target.value)}
                                placeholder="如：收到货款、支付办公费"
                                maxLength={200}
                                aria-invalid={Boolean(entryForm.errors.summary)}
                            />
                            <FieldError>{entryForm.errors.summary}</FieldError>
                        </Field>
                        <div className="grid grid-cols-2 gap-3">
                            <Field>
                                <FieldLabel htmlFor="entry-category">收支类别（选填）</FieldLabel>
                                <Select
                                    value={entryForm.values.category ?? ""}
                                    onValueChange={(v) => entryForm.set("category", v === NO_CATEGORY ? undefined : v)}
                                >
                                    <SelectTrigger id="entry-category" className="w-full">
                                        <SelectValue placeholder={categories.length > 0 ? "选择收支类别" : "先在「收支类别」页新建类别"} />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value={NO_CATEGORY}>请选择</SelectItem>
                                        {categoryGroups.map((g) => (
                                            <SelectGroup key={g.label}>
                                                <SelectLabel>{g.label}</SelectLabel>
                                                {g.items.map((name) => (
                                                    <SelectItem key={name} value={name}>{name}</SelectItem>
                                                ))}
                                            </SelectGroup>
                                        ))}
                                    </SelectContent>
                                </Select>
                            </Field>
                            <Field>
                                <FieldLabel htmlFor="entry-counterparty">往来（选填）</FieldLabel>
                                <Input
                                    id="entry-counterparty"
                                    value={entryForm.values.counterparty ?? ""}
                                    onChange={(e) => entryForm.set("counterparty", e.target.value)}
                                    placeholder="往来单位或个人"
                                    maxLength={100}
                                />
                            </Field>
                        </div>
                        <div className="grid grid-cols-2 gap-3">
                            <Field data-invalid={Boolean(entryForm.errors.income)}>
                                <FieldLabel htmlFor="entry-income">收入</FieldLabel>
                                <NumberField
                                    id="entry-income"
                                    value={entryForm.values.income}
                                    onChange={(v) => entryForm.set("income", v)}
                                    precision={2}
                                    min={0}
                                    placeholder="0.00"
                                />
                                <FieldError>{entryForm.errors.income}</FieldError>
                            </Field>
                            <Field>
                                <FieldLabel htmlFor="entry-expense">支出</FieldLabel>
                                <NumberField
                                    id="entry-expense"
                                    value={entryForm.values.expense}
                                    onChange={(v) => entryForm.set("expense", v)}
                                    precision={2}
                                    min={0}
                                    placeholder="0.00"
                                />
                            </Field>
                        </div>
                    </div>
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setEntryModalOpen(false)}>取消</Button>
                        <Button onClick={() => void handleEntrySubmit()} disabled={entrySaving}>
                            {entrySaving && <Spinner data-icon="inline-start" />}
                            保存
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 由流水生成凭证 */}
            <Dialog open={voucherEntry !== null} onOpenChange={(open) => { if (!open) setVoucherEntry(null) }}>
                <DialogContent aria-describedby={undefined}>
                    <DialogHeader>
                        <DialogTitle>由流水生成凭证</DialogTitle>
                    </DialogHeader>
                    {voucherEntry && (
                        <div className="flex flex-col gap-4">
                            <Alert>
                                <Info />
                                <AlertTitle>
                                    {voucherEntry.income
                                        ? `收入 ${fmtMoney(voucherEntry.income)}：借 ${selected?.accountName ?? "货币资金"} / 贷 对方科目`
                                        : `支出 ${fmtMoney(voucherEntry.expense ?? 0)}：借 对方科目 / 贷 ${selected?.accountName ?? "货币资金"}`}
                                </AlertTitle>
                                <AlertDescription>
                                    {`${toISODate(voucherEntry.date)} ${voucherEntry.summary}`}
                                </AlertDescription>
                            </Alert>
                            <Field data-invalid={Boolean(voucherForm.errors.counterAccountId)}>
                                <FieldLabel htmlFor="voucher-counter-account">对方科目</FieldLabel>
                                <Select
                                    value={voucherForm.values.counterAccountId}
                                    onValueChange={(v) => voucherForm.set("counterAccountId", v)}
                                >
                                    <SelectTrigger
                                        id="voucher-counter-account"
                                        className="w-full"
                                        aria-invalid={Boolean(voucherForm.errors.counterAccountId)}
                                    >
                                        <SelectValue placeholder="选择对方科目（末级科目）" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectGroup>
                                            {counterAccounts
                                                .filter((a) => a.id !== detail?.accountId)
                                                .map((a) => (
                                                    <SelectItem key={a.id} value={a.id ?? ""}>{`${a.code} ${a.name}`}</SelectItem>
                                                ))}
                                        </SelectGroup>
                                    </SelectContent>
                                </Select>
                                <FieldError>{voucherForm.errors.counterAccountId}</FieldError>
                            </Field>
                        </div>
                    )}
                    <DialogFooter>
                        <Button variant="outline" onClick={() => setVoucherEntry(null)}>取消</Button>
                        <Button onClick={() => void handleGenerateVoucher()} disabled={voucherSaving}>
                            {voucherSaving && <Spinner data-icon="inline-start" />}
                            生成凭证
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 批量生成凭证 */}
            <Dialog open={batchOpen} onOpenChange={setBatchOpen}>
                <DialogContent aria-describedby={undefined} className="sm:max-w-lg">
                    <DialogHeader>
                        <DialogTitle>批量生成凭证</DialogTitle>
                        <DialogDescription>
                            {batchResult
                                ? "凭证已生成并回填到对应流水。"
                                : `已选 ${selectedEntryIds.size} 笔流水；同一张凭证内相同科目、相同方向的行将自动汇总为一行。`}
                        </DialogDescription>
                    </DialogHeader>
                    {batchResult ? (
                        <Alert>
                            <CircleCheck />
                            <AlertTitle>
                                {`成功生成 ${batchResult.vouchers?.length ?? 0} 张凭证，共涉及 ${batchResult.entryCount ?? 0} 笔流水。`}
                            </AlertTitle>
                            <AlertDescription>
                                <div className="flex flex-wrap items-center gap-1.5 pt-1">
                                    {batchResult.vouchers?.map((v) => (
                                        <StatusPill key={v.id} tone="blue">{v.label}</StatusPill>
                                    ))}
                                </div>
                                <p className="pt-2">生成的凭证为草稿状态，请到「凭证管理」审核并过账后计入账簿。</p>
                            </AlertDescription>
                        </Alert>
                    ) : (
                        <div className="flex flex-col gap-4">
                            {batchError && (
                                <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                    <CircleAlert />
                                    <AlertTitle>{batchError}</AlertTitle>
                                </Alert>
                            )}
                            <Field>
                                <FieldLabel>合并方式</FieldLabel>
                                <RadioGroup
                                    className="flex flex-col gap-2"
                                    value={String(batchMergeBy)}
                                    onValueChange={(v) => setBatchMergeBy(Number(v))}
                                >
                                    {[
                                        { v: "1", label: "合并成一张凭证" },
                                        { v: "2", label: "按日期分开" },
                                        { v: "3", label: "按收支类别分开" },
                                        { v: "4", label: "按收支方向分开（收入 / 支出各一张）" },
                                    ].map((o) => (
                                        <div key={o.v} className="flex items-center gap-2">
                                            <RadioGroupItem value={o.v} id={`batch-merge-${o.v}`} />
                                            <FieldLabel htmlFor={`batch-merge-${o.v}`}>{o.label}</FieldLabel>
                                        </div>
                                    ))}
                                </RadioGroup>
                            </Field>
                            <Field>
                                <FieldLabel htmlFor="batch-counter-account">对方科目</FieldLabel>
                                <Select value={batchCounterId} onValueChange={setBatchCounterId}>
                                    <SelectTrigger id="batch-counter-account" className="w-full">
                                        <SelectValue placeholder="选择对方科目" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value="auto">自动：取「收支类别」的默认对方科目</SelectItem>
                                        <SelectGroup>
                                            {counterAccounts
                                                .filter((a) => a.id !== detail?.accountId)
                                                .map((a) => (
                                                    <SelectItem key={a.id} value={a.id ?? ""}>{`${a.code} ${a.name}`}</SelectItem>
                                                ))}
                                        </SelectGroup>
                                    </SelectContent>
                                </Select>
                                <FieldDescription>
                                    未配置默认对方科目的流水将拒绝出票；可在此指定统一对方科目一次出票。
                                </FieldDescription>
                            </Field>
                        </div>
                    )}
                    <DialogFooter>
                        {batchResult ? (
                            <Button onClick={() => setBatchOpen(false)}>完成</Button>
                        ) : (
                            <>
                                <Button variant="outline" onClick={() => setBatchOpen(false)}>取消</Button>
                                <Button onClick={() => void handleBatchGenerate()} disabled={batchSaving || selectedEntryIds.size === 0}>
                                    {batchSaving && <Spinner data-icon="inline-start" />}
                                    生成凭证
                                </Button>
                            </>
                        )}
                    </DialogFooter>
                </DialogContent>
            </Dialog>

            {/* 导入流水 */}
            <Dialog open={importOpen} onOpenChange={setImportOpen}>
                <DialogContent>
                    <DialogHeader>
                        <DialogTitle>{`导入流水${selected ? `：${selected.name}` : ""}`}</DialogTitle>
                        <DialogDescription>
                            支持 .xls / .xlsx 文件，列格式与导入模板一致（日期、摘要必填）。非法行会跳过并提示，不影响其余行导入。
                        </DialogDescription>
                    </DialogHeader>
                    <div className="flex flex-col gap-4">
                        <div className="flex flex-wrap items-center gap-2">
                            <Button variant="outline" onClick={() => importInputRef.current?.click()}>
                                <FileSpreadsheet data-icon="inline-start" />
                                {importFile ? importFile.name : "选择文件"}
                            </Button>
                            <input
                                ref={importInputRef}
                                type="file"
                                accept={ACCEPT}
                                className="hidden"
                                onChange={(e) => pickImportFile(e.target.files?.[0] ?? null)}
                            />
                            <Button onClick={() => void handleImport()} disabled={!importFile || importing}>
                                {importing ? <Spinner data-icon="inline-start" /> : <Upload data-icon="inline-start" />}
                                {importing ? "导入中…" : "开始导入"}
                            </Button>
                            {importFile && !importing && (
                                <Button variant="outline" onClick={() => pickImportFile(null)}>清除</Button>
                            )}
                        </div>
                        {importError && (
                            <Alert variant="destructive" className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]">
                                <CircleAlert />
                                <AlertTitle>{importError}</AlertTitle>
                            </Alert>
                        )}
                        {importResult && (
                            <Alert>
                                {(importResult.skippedRows ?? 0) > 0 ? <TriangleAlert /> : <CircleCheck />}
                                <AlertTitle>
                                    {`共 ${importResult.totalRows ?? 0} 行，成功导入 ${importResult.importedEntries ?? 0} 条，跳过 ${importResult.skippedRows ?? 0} 行。`}
                                </AlertTitle>
                                {(importResult.warnings?.length ?? 0) > 0 && (
                                    <AlertDescription>
                                        <ul className="flex list-disc flex-col gap-1 ps-4">
                                            {importResult.warnings?.map((w, i) => <li key={i}>{w}</li>)}
                                        </ul>
                                    </AlertDescription>
                                )}
                            </Alert>
                        )}
                    </div>
                </DialogContent>
            </Dialog>

            {confirmDialog}
        </div>
    )
}
