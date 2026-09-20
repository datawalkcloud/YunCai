import { useCallback, useEffect, useRef, useState } from "react"
import {
    CircleAlert,
    CircleCheck,
    Download,
    FileSpreadsheet,
    FileText,
    Info,
    Pencil,
    Plus,
    RefreshCw,
    Trash2,
    TriangleAlert,
    Upload,
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
    type JournalDetailDto,
    type JournalEntryDto,
    type JournalImportResult,
    type JournalListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { Alert, AlertAction, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { DateField, parseDate } from "@/components/fields/date-fields"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { NumberField } from "@/components/fields/number-field"
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group"
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Spinner } from "@/components/ui/spinner"
import { Stat } from "@/components/stats/stat"
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs"

const ACCEPT = ".xls,.xlsx"

/** Radix Select 不允许空串作为选项值，用这个哨兵表示「不选收支类别」。 */
const NO_CATEGORY = "__none__"

function formatMoney(value: number): string {
    return value.toLocaleString("zh-CN", {
        style: "currency",
        currency: "CNY",
        minimumFractionDigits: 2,
    })
}

/** 金额为 0 时留空，避免表格被 0.00 填满。 */
function fmt(value: number): string {
    if (!value) return ""
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 统计卡片用的金额文本：千分位 + 两位小数，0 也要显示为 0.00。 */
function fmtStat(value: number): string {
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 将后端返回的日期（解析为 UTC 零点的 Date）格式化为 yyyy-MM-dd。 */
function formatDate(d?: Date): string {
    return d ? d.toISOString().slice(0, 10) : ""
}

/** 新建/编辑账户表单值。 */
interface AccountFormValues {
    name: string
    type: 1 | 2
    openingBalance?: number
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

const emptyAccountForm: AccountFormValues = { name: "", type: 2, openingBalance: 0 }
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

/**
 * 极简受控表单，替代原 antd Form 实例：值 + 错误 + 提交前校验。
 * 校验只在提交时执行；一旦出现错误，后续改动即重算，等价于原 dependencies 联动。
 */
function useLocalForm<T extends object>(
    initial: T,
    validate: (values: T) => Partial<Record<keyof T, string>>,
) {
    const [values, setValues] = useState<T>(initial)
    const [errors, setErrors] = useState<Partial<Record<keyof T, string>>>({})

    /** 打开弹窗时整体预填，对应原 setFieldsValue。 */
    function fill(next: T) {
        setValues(next)
        setErrors({})
    }

    /** 改单个字段，对应原控件的 onChange。 */
    function set<K extends keyof T>(key: K, value: T[K]) {
        const next: T = { ...values, [key]: value }
        setValues(next)
        if (Object.keys(errors).length > 0) setErrors(validate(next))
    }

    /** 提交前校验，返回是否通过，对应原 validateFields。 */
    function check() {
        const next = validate(values)
        setErrors(next)
        return Object.keys(next).length === 0
    }

    return { values, errors, fill, set, check }
}

const features = tableFeatures({})
const entryColumn = createColumnHelper<typeof features, JournalEntryDto>()

/** 各列的对齐方式按列 id 取用，避免写行内样式。 */
const COLUMN_CLASS: Record<string, { head: string, cell: string }> = {
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

    const [journals, setJournals] = useState<JournalListItemDto[]>([])
    const [selectedId, setSelectedId] = useState<string | null>(null)
    const [detail, setDetail] = useState<JournalDetailDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [detailLoading, setDetailLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    // ── 账户（账簿）新建/编辑 ──
    const [accountModalOpen, setAccountModalOpen] = useState(false)
    const [editingJournal, setEditingJournal] = useState<JournalListItemDto | null>(null)
    const [accountSaving, setAccountSaving] = useState(false)
    const accountForm = useLocalForm(emptyAccountForm, validateAccount)

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
        accountForm.fill({ name: "", type: 2, openingBalance: 0 })
        setAccountModalOpen(true)
    }

    function openEditAccount(j: JournalListItemDto) {
        setEditingJournal(j)
        accountForm.fill({
            name: j.name ?? "",
            type: (j.type as 1 | 2) ?? 2,
            openingBalance: j.openingBalance ?? 0,
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
                const updated = await api.accountsetsAccountSetIdJournalsIdPut({
                    accountSetId,
                    id: editingJournal.id,
                    updateJournalRequest: { name: values.name, openingBalance: values.openingBalance },
                })
                applyDetail(updated)
                setJournals((prev) => prev.map((j) => (j.id === editingJournal.id ? { ...j, name: updated.name, openingBalance: updated.openingBalance, closingBalance: updated.closingBalance } : j)))
            } else {
                // 不传 accountId，由后端自动在 1001/1002 下创建同名明细科目并绑定。
                const created = await api.accountsetsAccountSetIdJournalsPost({
                    accountSetId,
                    createJournalRequest: { name: values.name, type: values.type, openingBalance: values.openingBalance },
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
        if (!window.confirm(`确定删除账户「${j.name}」吗？其全部流水将一并删除，此操作不可撤销。`)) return
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
            date: parseDate(formatDate(e.date)),
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
        if (!window.confirm(`确定删除第 ${e.sequence} 笔流水「${e.summary}」吗？`)) return
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

    async function openVoucherModal(e: JournalEntryDto) {
        setVoucherEntry(e)
        voucherForm.fill(emptyVoucherForm)
        if (counterAccounts.length === 0) {
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

    const summary = [
        { label: "期初余额", value: detail?.openingBalance ?? 0 },
        { label: "本期收入", value: detail?.totalIncome ?? 0 },
        { label: "本期支出", value: detail?.totalExpense ?? 0 },
        { label: "期末余额", value: detail?.closingBalance ?? 0 },
    ]

    const columns = [
        entryColumn.display({
            id: "sequence",
            header: "序号",
            cell: ({ row }) => <span>{row.original.sequence}</span>,
        }),
        entryColumn.display({
            id: "date",
            header: "日期",
            cell: ({ row }) => <span>{formatDate(row.original.date)}</span>,
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
            cell: ({ row }) => <span>{fmt(row.original.income ?? 0)}</span>,
        }),
        entryColumn.display({
            id: "expense",
            header: "支出",
            cell: ({ row }) => <span>{fmt(row.original.expense ?? 0)}</span>,
        }),
        entryColumn.display({
            id: "balance",
            header: "余额",
            cell: ({ row }) => <span className="font-medium">{formatMoney(row.original.balance ?? 0)}</span>,
        }),
        entryColumn.display({
            id: "voucherNo",
            header: "凭证",
            cell: ({ row }) =>
                row.original.voucherNo
                    ? <Badge>{row.original.voucherNo}</Badge>
                    : <span className="text-muted-foreground">—</span>,
        }),
        entryColumn.display({
            id: "actions",
            header: "操作",
            cell: ({ row }) => {
                const e = row.original
                return (
                    <div className="flex items-center justify-end gap-0">
                        <Button variant="ghost" size="icon-xs" title="编辑流水" onClick={() => openEditEntry(e)}>
                            <Pencil />
                        </Button>
                        {!e.voucherId && (
                            <Button variant="ghost" size="icon-xs" title="由此流水生成凭证" onClick={() => void openVoucherModal(e)}>
                                <FileText />
                            </Button>
                        )}
                        <Button variant="destructive" size="icon-xs" title="删除流水" onClick={() => void handleDeleteEntry(e)}>
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
        <div className="flex w-full flex-col gap-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex min-w-0 flex-col gap-1">
                    <h1 className="text-lg font-semibold">现金日记账</h1>
                    <p className="text-sm text-muted-foreground">
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。可创建多个资金账户（现金、各银行账户）分别登记序时流水，流水可一键生成记账凭证。
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <Button variant="outline" onClick={() => { void loadJournals(); void loadDetail() }} disabled={!accountSetId || loading}>
                        {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                        刷新
                    </Button>
                    <Button onClick={openCreateAccount} disabled={!accountSetId}>
                        <Plus data-icon="inline-start" />
                        新建账户
                    </Button>
                </div>
            </div>

            {error && (
                <Alert variant="destructive">
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
                <Card>
                    <CardContent>
                        <div className="flex flex-col items-center gap-3 py-10">
                            <FileSpreadsheet size={40} strokeWidth={1.2} />
                            <p className="text-sm text-muted-foreground">
                                {accountSetId ? "还没有资金账户。点击「新建账户」创建现金或银行账户，开始登记流水。" : "请先选择账套。"}
                            </p>
                            {accountSetId && (
                                <Button onClick={openCreateAccount}>
                                    <Plus data-icon="inline-start" />
                                    新建账户
                                </Button>
                            )}
                        </div>
                    </CardContent>
                </Card>
            ) : (
                <>
                    <Tabs value={selectedId ?? ""} onValueChange={setSelectedId} className="min-w-0">
                        <TabsList className="w-full justify-start overflow-auto">
                            {journals.map((j) => (
                                <TabsTrigger key={j.id} value={j.id ?? ""}>
                                    <span className="truncate">{j.name}</span>
                                    <Badge variant={j.type === 1 ? "secondary" : "outline"}>
                                        {j.type === 1 ? "现金" : "银行"}
                                    </Badge>
                                </TabsTrigger>
                            ))}
                        </TabsList>
                    </Tabs>

                    <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
                        {summary.map((item) => (
                            <Stat key={item.label} label={item.label} value={fmtStat(item.value)} prefix="¥" />
                        ))}
                    </div>

                    <Card>
                        <CardHeader>
                            <div className="flex min-w-0 items-center gap-2">
                                <CardTitle className="truncate">{selected?.name ?? "明细流水"}</CardTitle>
                                {selected?.accountCode && (
                                    <CardDescription className="truncate">
                                        对应科目：{selected.accountCode} {selected.accountName}
                                    </CardDescription>
                                )}
                            </div>
                            <CardAction className="flex flex-wrap items-center justify-end gap-2">
                                {detailLoading && <Spinner className="text-muted-foreground" />}
                                <Button size="sm" variant="outline" onClick={() => void downloadFile(`/accountsets/${accountSetId}/Journals/import/template`, "日记账导入模板.xlsx")}>
                                    <Download data-icon="inline-start" />
                                    模板
                                </Button>
                                <Button size="sm" variant="outline" onClick={() => { setImportOpen(true); pickImportFile(null) }} disabled={!selectedId}>
                                    <Upload data-icon="inline-start" />
                                    导入
                                </Button>
                                <Button size="sm" variant="outline" onClick={() => selectedId && void downloadFile(`/accountsets/${accountSetId}/Journals/${selectedId}/export`, `${selected?.name ?? "日记账"}.xlsx`)} disabled={!selectedId}>
                                    <Download data-icon="inline-start" />
                                    导出
                                </Button>
                                <Button size="sm" variant="outline" onClick={() => selected && openEditAccount(selected)} disabled={!selected}>
                                    <Pencil data-icon="inline-start" />
                                    账户设置
                                </Button>
                                <Button size="sm" variant="destructive" onClick={() => selected && void handleDeleteAccount(selected)} disabled={!selected}>
                                    <Trash2 data-icon="inline-start" />
                                    删除账户
                                </Button>
                                <Button size="sm" onClick={openCreateEntry} disabled={!selectedId}>
                                    <Plus data-icon="inline-start" />
                                    登记流水
                                </Button>
                            </CardAction>
                        </CardHeader>
                        <CardContent>
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
                                            <TableCell colSpan={columns.length} className="text-center text-muted-foreground">
                                                该账户暂无流水，点击「登记流水」或「导入」添加。
                                            </TableCell>
                                        </TableRow>
                                    )}
                                </TableBody>
                                {rows.length > 0 && (
                                    <TableFooter>
                                        <TableRow>
                                            <TableCell colSpan={5}>本期合计</TableCell>
                                            <TableCell className="text-right tabular-nums">{formatMoney(detail?.totalIncome ?? 0)}</TableCell>
                                            <TableCell className="text-right tabular-nums">{formatMoney(detail?.totalExpense ?? 0)}</TableCell>
                                            <TableCell className="text-right tabular-nums">{formatMoney(detail?.closingBalance ?? 0)}</TableCell>
                                            <TableCell colSpan={2} />
                                        </TableRow>
                                    </TableFooter>
                                )}
                            </Table>
                        </CardContent>
                    </Card>
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
                                onValueChange={(v) => accountForm.set("type", v === "1" ? 1 : 2)}
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
                                        ? `收入 ${formatMoney(voucherEntry.income)}：借 ${selected?.accountName ?? "货币资金"} / 贷 对方科目`
                                        : `支出 ${formatMoney(voucherEntry.expense ?? 0)}：借 对方科目 / 贷 ${selected?.accountName ?? "货币资金"}`}
                                </AlertTitle>
                                <AlertDescription>
                                    {`${formatDate(voucherEntry.date)} ${voucherEntry.summary}`}
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
                            <Alert variant="destructive">
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
        </div>
    )
}
