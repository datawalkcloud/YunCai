/**
 * 资金日记账账页（现金 / 银行存款共用），布局对齐柠檬云「现金日记账」：
 * 顶部筛选条（账户 / 期间 / 显示全部）+ 右侧动作组，下方账页网格（初始化余额 → 流水 → 合计）；
 * 列头漏斗按关键字筛选，表头齿轮控制列显示；流水行内登记与编辑，勾选后可批量生成凭证。
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import type { ReactNode } from "react"
import { useNavigate } from "react-router"
import {
    Alert,
    App,
    AutoComplete,
    Button,
    Card,
    Checkbox,
    DatePicker,
    Dropdown,
    Empty,
    Flex,
    Input,
    Modal,
    Pagination,
    Popover,
    Radio,
    Select,
    Space,
    Spin,
    Table,
    Tooltip,
    Typography,
} from "antd"
import type { TableColumnsType } from "antd"
import { FilterTriggerButton } from "@/components/page/filter-trigger-button"
import { DownOutlined, DownloadOutlined, EllipsisOutlined, FilterFilled, FullscreenOutlined, PlusOutlined, PrinterOutlined, ReloadOutlined, SettingOutlined } from "@ant-design/icons"
import dayjs from "dayjs"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { setPath } from "@/accountSets/url-id"
import {
    AccountsApi,
    EntryCategoriesApi,
    JournalsApi,
    VoucherWordsApi,
    VouchersApi,
    type AccountDto,
    type EntryCategoryDto,
    type JournalDetailDto,
    type JournalEntryDto,
    type JournalListItemDto,
    type VoucherListItemDto,
    type VoucherWordDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount, toISODate } from "@/lib/format"
import { printPdf } from "@/lib/print-pdf"
import { cn } from "@/lib/utils"
import { CounterpartyPicker } from "@/components/assist/counterparty-picker"

/** 每页条数候选，与账页页脚的分页器一致。 */
const PAGE_SIZE_OPTIONS = [20, 50, 100]

/** 批量生成凭证的合并方式（与后端 MergeBy 对齐）。 */
const MERGE_OPTIONS = [
    { value: 1, label: "合并成一张凭证" },
    { value: 2, label: "按日期分开" },
    { value: 3, label: "按收支类别分开" },
    { value: 4, label: "按收支方向分开（收入 / 支出各一张）" },
]

/** 结算方式候选（与柠檬云现金日记账一致，允许自由输入其它方式）。 */
const SETTLEMENT_METHODS = ["现金", "银行转账", "支票", "银行汇票", "商业汇票", "电汇", "其他"]

/** 流水来源候选；历史数据没有来源时按「手工登记」展示与筛选。 */
const SOURCE_OPTIONS = ["手工登记", "Excel导入", "内部转账", "凭证生成"]

/** 默认收起的可选列：在表头齿轮里勾选后才会显示，并同步出现在行内登记行里。 */
const DEFAULT_HIDDEN_COLUMNS = [
    "settlementMethod",
    "billNo",
    "project",
    "department",
    "remark",
    "source",
    "voucherDate",
    "voucherWord",
]

/** 收支类别下拉的显示名前缀：存的是类别名称，展示时按类别方向补「收- / 支-」。 */
const categoryLabel = (name: string, type?: number) => `${type === 2 ? "支" : "收"}-${name}`

/** 打印列的顺序，与后端 FundsPdfProjector 的账页列定义一致；「操作」「设置」两列不参与打印。 */
const PRINT_COLUMN_KEYS = [
    "date",
    "summary",
    "category",
    "counterparty",
    "income",
    "expense",
    "balance",
    "voucher",
    "seq",
    "settlementMethod",
    "billNo",
    "project",
    "department",
    "remark",
    "source",
    "voucherDate",
    "voucherWord",
]

/** 表头排序取值：数值列按数字比，其余按字符串比，空值统一排最后。 */
function sortValueOf(e: JournalEntryDto, field: string): string | number {
    switch (field) {
        case "date":
            return toISODate(e.date)
        case "voucherDate":
            return toISODate(e.voucherDate)
        case "seq":
            return e.sequence ?? 0
        case "income":
            return e.income ?? 0
        case "expense":
            return e.expense ?? 0
        case "balance":
            return e.balance ?? 0
        case "voucherWord":
            return e.voucherWord ?? ""
        case "voucher":
            return e.voucherNo ?? ""
        case "category":
            return e.category ?? ""
        case "counterparty":
            return e.counterparty ?? ""
        case "settlementMethod":
            return e.settlementMethod ?? ""
        case "billNo":
            return e.billNo ?? ""
        case "project":
            return e.project ?? ""
        case "department":
            return e.department ?? ""
        case "remark":
            return e.remark ?? ""
        case "source":
            return e.source ?? "手工登记"
        case "summary":
        default:
            return e.summary ?? ""
    }
}

/** 可被列头漏斗筛选的列。 */
type FilterKey = "date" | "summary" | "category" | "counterparty" | "income" | "expense" | "balance" | "voucher" | "seq"

const EMPTY_FILTERS: Record<FilterKey, string> = {
    date: "",
    summary: "",
    category: "",
    counterparty: "",
    income: "",
    expense: "",
    balance: "",
    voucher: "",
    seq: "",
}

/** 列设置里可勾选显示的列。 */
const COLUMN_TOGGLES: Array<[string, string]> = [
    ["date", "日期"],
    ["summary", "摘要"],
    ["category", "收支类别"],
    ["counterparty", "往来单位"],
    ["income", "收入（借方）"],
    ["expense", "支出（贷方）"],
    ["balance", "余额"],
    ["voucher", "关联凭证"],
    ["seq", "日记账序号"],
    ["voucherDate", "凭证日期"],
    ["voucherWord", "凭证字"],
    ["settlementMethod", "结算方式"],
    ["billNo", "票据号"],
    ["project", "项目"],
    ["department", "部门"],
    ["remark", "备注"],
    ["source", "流水来源"],
    ["ops", "操作"],
]

/** 区间控件弹出的高级筛选：日期在面板里同步期间，其余字段与账页列头漏斗叠加生效。 */
type VoucherState = "all" | "yes" | "no"

/** 柠檬云「现金日记账」高级筛选的全部条件。 */
interface AdvancedFilters {
    /** 业务日期区间单独放在 period 里，此处只放其余条件。 */
    voucherDate: [string, string] | null
    voucherWord: string
    voucherNoFrom: string
    voucherNoTo: string
    category: string
    settlementMethod: string
    voucher: VoucherState
    counterparty: string
    billNo: string
    project: string
    department: string
    summary: string
    remark: string
    source: string
    amountFrom: string
    amountTo: string
}

const EMPTY_ADVANCED: AdvancedFilters = {
    voucherDate: null,
    voucherWord: "",
    voucherNoFrom: "",
    voucherNoTo: "",
    category: "",
    settlementMethod: "",
    voucher: "all",
    counterparty: "",
    billNo: "",
    project: "",
    department: "",
    summary: "",
    remark: "",
    source: "",
    amountFrom: "",
    amountTo: "",
}

const VOUCHER_STATE_OPTIONS: Array<{ value: VoucherState; label: string }> = [
    { value: "all", label: "全部" },
    { value: "yes", label: "已生成凭证" },
    { value: "no", label: "未生成凭证" },
]

/** 账页行：初始化余额 / 流水 / 行内登记 / 行内编辑 / 合计。 */interface LedgerRow {
    key: string
    kind: "opening" | "entry" | "draft" | "editing" | "total"
    entry?: JournalEntryDto
}

interface DraftValues {
    date: string
    summary: string
    category: string
    counterparty: string
    income: string
    expense: string
    settlementMethod: string
    billNo: string
    project: string
    department: string
    remark: string
    source: string
}

const blankDraft = (): DraftValues => ({
    date: dayjs().format("YYYY-MM-DD"),
    summary: "",
    category: "",
    counterparty: "",
    income: "",
    expense: "",
    settlementMethod: "",
    billNo: "",
    project: "",
    department: "",
    remark: "",
    source: "手工登记",
})

/** 行内登记 / 编辑行的表单值（金额以字符串保存，避免半角小数被 Number 吃掉）。 */
function draftFromEntry(e: JournalEntryDto): DraftValues {
    return {
        date: toISODate(e.date),
        summary: e.summary ?? "",
        category: e.category ?? "",
        counterparty: e.counterparty ?? "",
        income: e.income ? String(e.income) : "",
        expense: e.expense ? String(e.expense) : "",
        settlementMethod: e.settlementMethod ?? "",
        billNo: e.billNo ?? "",
        project: e.project ?? "",
        department: e.department ?? "",
        remark: e.remark ?? "",
        source: e.source ?? "手工登记",
    }
}

export function JournalLedgerPage({ journalType, seqPrefix }: { journalType: 1 | 2; seqPrefix: string }) {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const navigate = useNavigate()
    const { message, modal } = App.useApp()

    const [journals, setJournals] = useState<JournalListItemDto[]>([])
    const [journalId, setJournalId] = useState("")
    const [detail, setDetail] = useState<JournalDetailDto | null>(null)
    const [categories, setCategories] = useState<EntryCategoryDto[]>([])
    const [error, setError] = useState<string | null>(null)
    const [loading, setLoading] = useState(false)
    const [detailLoading, setDetailLoading] = useState(false)

    const [period, setPeriod] = useState<[string, string] | null>(null)
    const [filters, setFilters] = useState<Record<FilterKey, string>>(EMPTY_FILTERS)
    const [advanced, setAdvanced] = useState<AdvancedFilters>(EMPTY_ADVANCED)
    const [advOpen, setAdvOpen] = useState(false)
    const [advDraft, setAdvDraft] = useState<{ period: [string, string] | null } & AdvancedFilters>({
        ...EMPTY_ADVANCED,
        period: null,
    })
    const [hidden, setHidden] = useState<Set<string>>(() => new Set(DEFAULT_HIDDEN_COLUMNS))
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(PAGE_SIZE_OPTIONS[0])
    const [selected, setSelected] = useState<string[]>([])
    /** 表头排序；null 表示按后端返回的自然顺序（日期 + 序号）。 */
    const [sortInfo, setSortInfo] = useState<{ field: string; order: "ascend" | "descend" } | null>(null)

    const [editingId, setEditingId] = useState<string | null>(null)
    const [form, setForm] = useState<DraftValues>(blankDraft)
    const [saving, setSaving] = useState(false)

    /** 往来单位选择弹窗：行内录入 / 批量指定 / 高级筛选共用。 */
    const [pickerTarget, setPickerTarget] = useState<"form" | "bulk" | "filter" | null>(null)
    /** 「+点击添加」快速新增收支类别。 */
    const [categoryQuickOpen, setCategoryQuickOpen] = useState(false)
    const [categoryQuickForm, setCategoryQuickForm] = useState({ type: 1, name: "" })
    const [categoryQuickSaving, setCategoryQuickSaving] = useState(false)
    /** 凭证字候选（高级筛选「凭证字号」用）。 */
    const [voucherWords, setVoucherWords] = useState<VoucherWordDto[]>([])

    const [createOpen, setCreateOpen] = useState(false)
    const [creating, setCreating] = useState(false)
    const [createForm, setCreateForm] = useState({ name: "", openingBalance: "", bankName: "", bankAccountNo: "" })

    const [batchOpen, setBatchOpen] = useState(false)
    const [batchSaving, setBatchSaving] = useState(false)
    const [mergeBy, setMergeBy] = useState(1)
    const [counterId, setCounterId] = useState("auto")
    const [counterAccounts, setCounterAccounts] = useState<AccountDto[]>([])

    const importRef = useRef<HTMLInputElement>(null)
    /** 已按该账簿初始化过期间，保存流水刷新明细时不再覆盖用户选的区间。 */
    const periodInitRef = useRef("")

    /* ---------- 打印 / 批量操作 ---------- */

    const [printing, setPrinting] = useState(false)

    /** 当前批量操作种类：除删除外都需要弹窗输入值。 */
    type BulkKind = "delete" | "summary" | "category" | "counterparty" | "project" | "department" | "unlink-voucher" | "link-voucher"
    /** 自由文本类的批量操作（指定往来单位 / 项目 / 部门）：对应的请求字段与输入框文案。 */
    const TEXT_BULK: Partial<Record<BulkKind, { field: "counterparty" | "project" | "department"; label: string }>> = {
        counterparty: { field: "counterparty", label: "往来单位" },
        project: { field: "project", label: "项目" },
        department: { field: "department", label: "部门" },
    }
    const [bulkKind, setBulkKind] = useState<BulkKind | null>(null)
    const [bulkText, setBulkText] = useState("")
    const [bulkBusy, setBulkBusy] = useState(false)
    const [linkVouchers, setLinkVouchers] = useState<VoucherListItemDto[]>([])
    const [linkVoucherId, setLinkVoucherId] = useState("")
    const [linkLoading, setLinkLoading] = useState(false)

    const isCash = journalType === 1
    const typeLabel = isCash ? "现金日记账" : "银行存款日记账"
    const entries = useMemo(() => detail?.entries ?? [], [detail])

    /* ---------- 数据加载 ---------- */

    const loadJournals = useCallback(async () => {
        if (!token || !accountSetId) {
            setJournals([])
            setJournalId("")
            return
        }
        setLoading(true)
        try {
            const list = (await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId })).filter(
                (j) => j.type === journalType,
            )
            setJournals(list)
            setError(null)
            setJournalId((prev) => (prev && list.some((j) => j.id === prev) ? prev : (list[0]?.id ?? "")))
        } catch (err) {
            setError(await apiErrorMessage(err, "加载日记账失败，请重试。"))
            setJournals([])
            setJournalId("")
        } finally {
            setLoading(false)
        }
    }, [token, accountSetId, journalType])

    const loadDetail = useCallback(
        async (id: string) => {
            if (!token || !accountSetId || !id) {
                setDetail(null)
                return
            }
            setDetailLoading(true)
            try {
                const next = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdGet({ accountSetId, id })
                setDetail(next)
                if (periodInitRef.current !== id) {
                    periodInitRef.current = id
                    // 首屏落在本月：历史账套不会一进来就是全量流水，「显示全部」仍可看全量。
                    setPeriod([
                        dayjs().startOf("month").format("YYYY-MM-DD"),
                        dayjs().endOf("month").format("YYYY-MM-DD"),
                    ])
                }
                setError(null)
            } catch (err) {
                setError(await apiErrorMessage(err, "加载日记账明细失败，请重试。"))
                setDetail(null)
            } finally {
                setDetailLoading(false)
            }
        },
        [token, accountSetId],
    )

    useEffect(() => {
        void loadJournals()
    }, [loadJournals])

    useEffect(() => {
        setSelected([])
        setEditingId(null)
        setForm(blankDraft())
        setPeriod(null)
        setFilters(EMPTY_FILTERS)
        setAdvanced(EMPTY_ADVANCED)
        setSortInfo(null)
        setAdvOpen(false)
        setPage(1)
        periodInitRef.current = ""
        if (journalId) void loadDetail(journalId)
        else setDetail(null)
    }, [journalId, loadDetail])

    useEffect(() => {
        if (!token || !accountSetId) return
        void (async () => {
            try {
                setCategories(
                    await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesGet({
                        accountSetId,
                    }),
                )
            } catch {
                setCategories([])
            }
            try {
                setVoucherWords(
                    await new VoucherWordsApi(apiConfig(token)).accountsetsAccountSetIdVoucherwordsGet({ accountSetId }),
                )
            } catch {
                setVoucherWords([])
            }
        })()
    }, [token, accountSetId])

    useEffect(() => {
        setPage(1)
    }, [period, filters, advanced, sortInfo, pageSize])

    /* ---------- 派生数据 ---------- */

    const seqOf = (e: JournalEntryDto) => `${seqPrefix}-${toISODate(e.date).replaceAll("-", "")}-${e.sequence ?? ""}`

    /** 期间控件关闭（显示全部）时，取消勾选回到有数据的整段区间，没有数据就落到本月。 */
    const dataRange: [string, string] = entries.length
        ? [toISODate(entries[0].date), toISODate(entries[entries.length - 1].date)]
        : [dayjs().startOf("month").format("YYYY-MM-DD"), dayjs().endOf("month").format("YYYY-MM-DD")]

    const counterpartyOptions = useMemo(
        () =>
            [...new Set(entries.map((e) => e.counterparty).filter((v): v is string => Boolean(v)))].map((value) => ({
                value,
            })),
        [entries],
    )

    function openAdvanced() {
        setAdvDraft({ ...advanced, period })
        setAdvOpen(true)
    }

    function applyAdvanced() {
        const trim = (v: string) => v.trim()
        setAdvanced({
            voucherDate: advDraft.voucherDate,
            voucherWord: trim(advDraft.voucherWord),
            voucherNoFrom: trim(advDraft.voucherNoFrom),
            voucherNoTo: trim(advDraft.voucherNoTo),
            category: trim(advDraft.category),
            settlementMethod: trim(advDraft.settlementMethod),
            voucher: advDraft.voucher,
            counterparty: trim(advDraft.counterparty),
            billNo: trim(advDraft.billNo),
            project: trim(advDraft.project),
            department: trim(advDraft.department),
            summary: trim(advDraft.summary),
            remark: trim(advDraft.remark),
            source: trim(advDraft.source),
            amountFrom: trim(advDraft.amountFrom),
            amountTo: trim(advDraft.amountTo),
        })
        setPeriod(advDraft.period)
        setAdvOpen(false)
    }

    function resetAdvanced() {
        setAdvDraft({ ...EMPTY_ADVANCED, period: dataRange })
    }

    const advancedCount = (
        [
            advanced.voucherDate,
            advanced.voucherWord,
            advanced.voucherNoFrom,
            advanced.voucherNoTo,
            advanced.category,
            advanced.settlementMethod,
            advanced.counterparty,
            advanced.billNo,
            advanced.project,
            advanced.department,
            advanced.summary,
            advanced.remark,
            advanced.source,
            advanced.amountFrom,
            advanced.amountTo,
            advanced.voucher !== "all" ? "voucher" : "",
        ] as Array<string | [string, string] | null>
    ).filter(Boolean).length

    const filtered = useMemo(() => {
        const match = (haystack: string | null | undefined, needle: string) =>
            !needle || (haystack ?? "").includes(needle)
        const sourceOf = (e: JournalEntryDto) => e.source ?? "手工登记"
        const amountOf = (e: JournalEntryDto) => (e.income ?? 0) > 0 ? (e.income ?? 0) : (e.expense ?? 0)
        return entries.filter((e) => {
            const date = toISODate(e.date)
            if (period && (date < period[0] || date > period[1])) return false
            if (filters.date && !date.includes(filters.date)) return false
            if (filters.summary && !match(e.summary, filters.summary)) return false
            if (filters.category && !match(e.category, filters.category)) return false
            if (filters.counterparty && !match(e.counterparty, filters.counterparty)) return false
            if (filters.income && !fmtAmount(e.income).includes(filters.income)) return false
            if (filters.expense && !fmtAmount(e.expense).includes(filters.expense)) return false
            if (filters.balance && !fmtAmount(e.balance).includes(filters.balance)) return false
            if (filters.voucher && !match(e.voucherNo, filters.voucher)) return false
            if (filters.seq && !seqOf(e).includes(filters.seq)) return false

            // 高级筛选：凭证日期 / 字号、收支类别、结算方式、往来、票据号、项目、部门、摘要、备注、来源、金额
            const voucherDate = toISODate(e.voucherDate)
            if (advanced.voucherDate) {
                if (!voucherDate || voucherDate < advanced.voucherDate[0] || voucherDate > advanced.voucherDate[1]) return false
            }
            if (advanced.voucherWord && e.voucherWord !== advanced.voucherWord) return false
            if (advanced.voucherNoFrom && (e.voucherNumber ?? 0) < Number(advanced.voucherNoFrom)) return false
            if (advanced.voucherNoTo && (e.voucherNumber ?? 0) > Number(advanced.voucherNoTo)) return false
            if (!match(e.category, advanced.category)) return false
            if (!match(e.settlementMethod, advanced.settlementMethod)) return false
            if (!match(e.counterparty, advanced.counterparty)) return false
            if (!match(e.billNo, advanced.billNo)) return false
            if (!match(e.project, advanced.project)) return false
            if (!match(e.department, advanced.department)) return false
            if (!match(e.summary, advanced.summary)) return false
            if (!match(e.remark, advanced.remark)) return false
            if (advanced.source && sourceOf(e) !== advanced.source) return false
            if (advanced.amountFrom && amountOf(e) < Number(advanced.amountFrom)) return false
            if (advanced.amountTo && amountOf(e) > Number(advanced.amountTo)) return false
            if (advanced.voucher === "yes" && !e.voucherId) return false
            if (advanced.voucher === "no" && e.voucherId) return false
            return true
        })
        // seqOf 只依赖 seqPrefix，随组件固定不变
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [entries, period, filters, advanced, seqPrefix])

    /** 表头排序作用于筛选结果；默认（sortInfo=null）保持后端返回的日期 + 序号顺序。 */
    const sorted = useMemo(() => {
        if (!sortInfo) return filtered
        const dir = sortInfo.order === "ascend" ? 1 : -1
        return [...filtered].sort((a, b) => {
            const av = sortValueOf(a, sortInfo.field)
            const bv = sortValueOf(b, sortInfo.field)
            if (typeof av === "number" && typeof bv === "number") return (av - bv) * dir
            return String(av).localeCompare(String(bv), "zh-CN") * dir
        })
    }, [filtered, sortInfo])

    const pageCount = Math.max(1, Math.ceil(sorted.length / pageSize))
    const current = Math.min(page, pageCount)
    const pageRows = sorted.slice((current - 1) * pageSize, current * pageSize)

    /** 期初取筛选区间前一笔的余额（没有则为账簿期初），合计只累加区间内流水。 */
    const openingBalance = useMemo(() => {
        if (!filtered.length) return detail?.openingBalance ?? 0
        const index = entries.findIndex((e) => e.id === filtered[0].id)
        return index > 0 ? (entries[index - 1].balance ?? 0) : (detail?.openingBalance ?? 0)
    }, [entries, filtered, detail])

    const totals = useMemo(() => {
        const cents = (pick: (e: JournalEntryDto) => number) =>
            filtered.reduce((acc, e) => acc + Math.round(pick(e) * 100), 0)
        const income = cents((e) => e.income ?? 0)
        const expense = cents((e) => e.expense ?? 0)
        return {
            income: income / 100,
            expense: expense / 100,
            closing: (Math.round(openingBalance * 100) + income - expense) / 100,
        }
    }, [filtered, openingBalance])

    const rows = useMemo<LedgerRow[]>(() => {
        const list: LedgerRow[] = []
        if (current === 1) list.push({ key: "__opening__", kind: "opening" })
        for (const e of pageRows) {
            if (editingId && e.id === editingId) list.push({ key: `edit-${e.id}`, kind: "editing", entry: e })
            else list.push({ key: e.id ?? `seq-${e.sequence}`, kind: "entry", entry: e })
        }
        if (!editingId && current === pageCount) list.push({ key: "__draft__", kind: "draft" })
        list.push({ key: "__total__", kind: "total" })
        return list
    }, [current, pageCount, pageRows, editingId])

    const selectableIds = filtered.filter((e) => !e.voucherId).map((e) => e.id ?? "")

    /** 收支类别下拉：柠檬云是一列「收-xxx / 支-xxx」平铺选项，值仍存类别名称。 */
    const categoryOptions = useMemo(
        () =>
            categories
                .filter((c) => c.name)
                .map((c) => ({ value: c.name as string, label: categoryLabel(c.name as string, c.type) })),
        [categories],
    )

    /** 账页展示收支类别时补方向前缀；查不到类别档案的历史数据按原名显示。 */
    function displayCategory(name?: string | null) {
        if (!name) return ""
        const hit = categories.find((c) => c.name === name)
        return hit ? categoryLabel(name, hit.type) : name
    }

    /* ---------- 往来单位选择 / 类别快速新增 ---------- */

    function openPicker(target: "form" | "bulk" | "filter") {
        setPickerTarget(target)
    }

    function handlePickerSelect(name: string) {
        if (pickerTarget === "form") patchForm({ counterparty: name })
        else if (pickerTarget === "bulk") setBulkText(name)
        else if (pickerTarget === "filter") {
            setAdvDraft((prev) => ({ ...prev, counterparty: name }))
        }
        setPickerTarget(null)
    }

    function handlePickerClear() {
        if (pickerTarget === "form") patchForm({ counterparty: "" })
        else if (pickerTarget === "bulk") setBulkText("")
        else if (pickerTarget === "filter") setAdvDraft((prev) => ({ ...prev, counterparty: "" }))
    }

    function openCategoryQuick() {
        // 已填支出时默认建「支出类别」，否则默认「收入类别」。
        setCategoryQuickForm({ type: Number(form.expense) > 0 ? 2 : 1, name: "" })
        setCategoryQuickOpen(true)
    }

    async function submitCategoryQuick() {
        if (!token || !accountSetId || categoryQuickSaving) return
        const name = categoryQuickForm.name.trim()
        if (!name) {
            message.warning("请填写收支类别名称。")
            return
        }
        setCategoryQuickSaving(true)
        try {
            const created = await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesPost({
                accountSetId,
                createEntryCategoryRequest: { type: categoryQuickForm.type, name },
            })
            setCategories((prev) => [...prev, created])
            patchForm({ category: created.name ?? name })
            setCategoryQuickOpen(false)
            message.success(`收支类别「${created.name ?? name}」已新增。`)
        } catch (err) {
            message.error(await apiErrorMessage(err, "新增收支类别失败，请重试。"))
        } finally {
            setCategoryQuickSaving(false)
        }
    }

    /* ---------- 行内登记 / 编辑 ---------- */

    const patchForm = (patch: Partial<DraftValues>) => setForm((prev) => ({ ...prev, ...patch }))

    function startEdit(entry: JournalEntryDto) {
        if (!entry.id) return
        setEditingId(entry.id)
        setForm(draftFromEntry(entry))
    }

    function cancelEdit() {
        setEditingId(null)
        setForm(blankDraft())
    }

    async function saveForm() {
        if (!token || !accountSetId || !journalId || saving) return
        const income = Number(form.income) || 0
        const expense = Number(form.expense) || 0
        if (!form.date) {
            message.warning("请选择日期。")
            return
        }
        if (!form.summary.trim()) {
            message.warning("请填写摘要。")
            return
        }
        if (income === 0 && expense === 0) {
            message.warning("收入与支出请填写其一。")
            return
        }
        if (income > 0 && expense > 0) {
            message.warning("收入与支出只能填写其一。")
            return
        }
        setSaving(true)
        try {
            const api = new JournalsApi(apiConfig(token))
            const request = {
                date: new Date(form.date),
                summary: form.summary.trim(),
                category: form.category.trim() || null,
                counterparty: form.counterparty.trim() || null,
                project: form.project.trim() || null,
                department: form.department.trim() || null,
                settlementMethod: form.settlementMethod.trim() || null,
                billNo: form.billNo.trim() || null,
                remark: form.remark.trim() || null,
                source: form.source.trim() || null,
                income,
                expense,
            }
            const updated = editingId
                ? await api.accountsetsAccountSetIdJournalsIdEntriesEntryIdPut({
                    accountSetId,
                    id: journalId,
                    entryId: editingId,
                    journalEntryRequest: request,
                })
                : await api.accountsetsAccountSetIdJournalsIdEntriesPost({
                    accountSetId,
                    id: journalId,
                    journalEntryRequest: request,
                })
            setDetail(updated)
            // 保存的流水若落在当前期间之外（典型场景：登记当天流水，而区间截止到昨天），
            // 自动把区间扩展到包含它，避免「提示保存成功却在账页看不见」。
            const savedDate = form.date
            if (savedDate) {
                setPeriod((prev) => {
                    if (!prev) return prev
                    if (savedDate >= prev[0] && savedDate <= prev[1]) return prev
                    return [savedDate < prev[0] ? savedDate : prev[0], savedDate > prev[1] ? savedDate : prev[1]]
                })
            }
            setEditingId(null)
            setForm(blankDraft())
            message.success(editingId ? "流水已更新。" : "流水已登记。")
        } catch (err) {
            message.error(await apiErrorMessage(err, "保存流水失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    function removeEntry(entry: JournalEntryDto) {
        if (!token || !accountSetId || !journalId || !entry.id) return
        modal.confirm({
            title: "删除流水",
            content: `确定删除第 ${entry.sequence} 笔流水「${entry.summary}」吗？删除后不可恢复。`,
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: async () => {
                try {
                    setDetail(
                        await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesEntryIdDelete({
                            accountSetId,
                            id: journalId,
                            entryId: entry.id as string,
                        }),
                    )
                    message.success("流水已删除。")
                } catch (err) {
                    message.error(await apiErrorMessage(err, "删除流水失败，请重试。"))
                }
            },
        })
    }

    /* ---------- 生成凭证 ---------- */

    async function ensureCounterAccounts() {
        if (!token || !accountSetId || counterAccounts.length > 0) return
        try {
            setCounterAccounts(
                await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({
                    accountSetId,
                    leafOnly: true,
                }),
            )
        } catch (err) {
            message.error(await apiErrorMessage(err, "加载科目失败，请重试。"))
        }
    }

    function openBatch(merge: number) {
        if (selected.length === 0) {
            message.warning("请先勾选要生成凭证的流水。")
            return
        }
        // 已关联凭证的流水不再重复出票，勾选时若混入则自动剔除。
        const targets = selected.filter((id) => selectableIds.includes(id))
        if (targets.length === 0) {
            message.warning("勾选的流水都已关联凭证，无需重复生成。")
            return
        }
        if (targets.length < selected.length) {
            setSelected(targets)
            message.info(`已忽略 ${selected.length - targets.length} 笔已关联凭证的流水。`)
        }
        setMergeBy(merge)
        setCounterId("auto")
        setBatchOpen(true)
        void ensureCounterAccounts()
    }

    async function submitBatch() {
        if (!token || !accountSetId || !journalId || batchSaving) return
        setBatchSaving(true)
        try {
            const result = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesVouchersPost({
                accountSetId,
                id: journalId,
                generateVouchersBatchRequest: {
                    entryIds: selected,
                    mergeBy,
                    counterAccountId: counterId !== "auto" ? counterId : null,
                },
            })
            const labels = (result.vouchers ?? []).map((v) => v.label).filter(Boolean).join("、")
            message.success(`已生成 ${result.vouchers?.length ?? 0} 张凭证${labels ? `：${labels}` : ""}。`)
            setBatchOpen(false)
            setSelected([])
            await loadDetail(journalId)
        } catch (err) {
            message.error(await apiErrorMessage(err, "生成凭证失败，请重试。"))
        } finally {
            setBatchSaving(false)
        }
    }

    /** 调用后端批量端点后统一刷新账页。返回是否成功。 */
    async function callBulk(
        payload: {
            action: string
            entryIds: string[]
            summary?: string
            category?: string
            counterparty?: string
            project?: string
            department?: string
            voucherId?: string
        },
    ): Promise<boolean> {
        if (!token || !accountSetId || !journalId) return false
        try {
            await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdEntriesBulkPost({
                accountSetId,
                id: journalId,
                bulkJournalEntryRequest: payload as never,
            })
            await loadDetail(journalId)
            setSelected([])
            return true
        } catch (err) {
            message.error(await apiErrorMessage(err, "批量操作失败，请重试。"))
            return false
        }
    }

    function batchDelete() {
        if (!journalId || selected.length === 0) {
            message.warning("请先勾选要删除的流水。")
            return
        }
        modal.confirm({
            title: "批量删除流水",
            content: `确定删除选中的 ${selected.length} 笔流水吗？删除后不可恢复。`,
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: async () => {
                if (await callBulk({ action: "delete", entryIds: selected })) {
                    message.success("流水已删除。")
                }
            },
        })
    }

    /**
     * 打开批量操作弹窗（摘要 / 类别 / 往来 / 项目 / 部门 / 关联凭证）。
     * 删除与取消关联无需输入，直接走确认框。
     */
    function openBulk(kind: BulkKind) {
        if (!journalId) return
        if (selected.length === 0) {
            message.warning("请先勾选要操作的流水。")
            return
        }
        if (kind === "unlink-voucher") {
            modal.confirm({
                title: "删除关联凭证",
                content: `确定清除选中 ${selected.length} 笔流水的关联凭证吗？流水本身保留。`,
                okText: "确定",
                cancelText: "取消",
                onOk: async () => {
                    if (await callBulk({ action: "unlink-voucher", entryIds: selected })) {
                        message.success("已删除关联凭证。")
                    }
                },
            })
            return
        }
        setBulkText("")
        setLinkVoucherId("")
        setBulkKind(kind)
        if (kind === "link-voucher") {
            void loadLinkVouchers()
        }
    }

    /** 关联凭证弹窗的候选凭证（当前账套全部凭证，可按凭证号/日期检索）。 */
    async function loadLinkVouchers() {
        if (!token || !accountSetId) return
        setLinkLoading(true)
        try {
            const list = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersGet({ accountSetId })
            setLinkVouchers(list)
        } catch (err) {
            message.error(await apiErrorMessage(err, "加载凭证失败，请重试。"))
            setLinkVouchers([])
        } finally {
            setLinkLoading(false)
        }
    }

    /** 提交批量操作弹窗。 */
    async function submitBulk() {
        if (!bulkKind || bulkBusy) return
        const textField = TEXT_BULK[bulkKind]?.field
        setBulkBusy(true)
        let ok = false
        if (bulkKind === "summary") {
            if (!bulkText.trim()) {
                message.warning("请填写摘要。")
                setBulkBusy(false)
                return
            }
            ok = await callBulk({ action: "summary", entryIds: selected, summary: bulkText.trim() })
        } else if (bulkKind === "category") {
            ok = await callBulk({ action: "category", entryIds: selected, category: bulkText })
        } else if (textField) {
            ok = await callBulk({ action: bulkKind, entryIds: selected, [textField]: bulkText })
        } else if (bulkKind === "link-voucher") {
            if (!linkVoucherId) {
                message.warning("请选择要关联的凭证。")
                setBulkBusy(false)
                return
            }
            ok = await callBulk({ action: "link-voucher", entryIds: selected, voucherId: linkVoucherId })
        }
        setBulkBusy(false)
        if (ok) {
            setBulkKind(null)
            message.success("批量操作已完成。")
        }
    }

    /** 全屏 / 退出全屏（对齐柠檬云账页工具栏的全屏按钮）。 */
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

    /* ---------- 打印 ---------- */

    /** 打印列表：期间、列头漏斗、高级筛选与可见列原样传给后端，由后端渲染账页 PDF。 */
    async function printList() {
        if (!accountSetId || !journalId || printing) return
        setPrinting(true)
        try {
            const qs = new URLSearchParams()
            if (period) {
                qs.set("startDate", period[0])
                qs.set("endDate", period[1])
            }
            qs.set("seqPrefix", seqPrefix)
            const visible = PRINT_COLUMN_KEYS.filter((key) => !hidden.has(key))
            if (visible.length > 0) qs.set("columns", visible.join(","))
            for (const [key, value] of Object.entries(filters)) {
                if (value.trim()) qs.set(key, value.trim())
            }
            const [vFrom, vTo] = advanced.voucherDate ?? [null, null]
            if (vFrom) qs.set("advVoucherDateFrom", vFrom)
            if (vTo) qs.set("advVoucherDateTo", vTo)
            const advancedPairs: Array<[string, string]> = [
                ["advVoucherWord", advanced.voucherWord],
                ["advVoucherNoFrom", advanced.voucherNoFrom],
                ["advVoucherNoTo", advanced.voucherNoTo],
                ["advCategory", advanced.category],
                ["advSettlementMethod", advanced.settlementMethod],
                ["advCounterparty", advanced.counterparty],
                ["advBillNo", advanced.billNo],
                ["advProject", advanced.project],
                ["advDepartment", advanced.department],
                ["advSummary", advanced.summary],
                ["advRemark", advanced.remark],
                ["advSource", advanced.source],
                ["advAmountFrom", advanced.amountFrom],
                ["advAmountTo", advanced.amountTo],
            ]
            for (const [key, value] of advancedPairs) {
                if (value.trim()) qs.set(key, value.trim())
            }
            if (advanced.voucher !== "all") qs.set("advHasVoucher", advanced.voucher)
            await printPdf(`/api/accountsets/${accountSetId}/journals/${journalId}/pdf?${qs.toString()}`, token)
        } catch (err) {
            message.warning(await apiErrorMessage(err, "生成打印稿失败，请重试。"))
        } finally {
            setPrinting(false)
        }
    }

    /** 打印凭据：为勾选（或当前页）流水各出一张收 / 付款凭据；传 entry 时只打印该笔。 */
    async function printReceipt(entry?: JournalEntryDto) {
        if (!accountSetId || !journalId || printing) return
        const ids = entry
            ? [entry.id ?? ""]
            : selected.length > 0
              ? filtered.filter((e) => selected.includes(e.id ?? "")).map((e) => e.id ?? "")
              : pageRows.map((entry) => entry.id ?? "")
        const targets = ids.filter(Boolean)
        if (targets.length === 0) {
            message.warning("当前没有可打印的流水。")
            return
        }
        setPrinting(true)
        try {
            const qs = new URLSearchParams({ entryIds: targets.join(",") })
            await printPdf(
                `/api/accountsets/${accountSetId}/journals/${journalId}/entries/receipt/pdf?${qs.toString()}`,
                token,
            )
        } catch (err) {
            message.warning(await apiErrorMessage(err, "生成打印稿失败，请重试。"))
        } finally {
            setPrinting(false)
        }
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
            message.error(await apiErrorMessage(err, "下载失败，请重试。"))
        }
    }

    async function importEntries(file: File) {
        if (!token || !accountSetId || !journalId) return
        try {
            const result = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsIdImportPost({
                accountSetId,
                id: journalId,
                file,
            })
            await loadDetail(journalId)
            const summary = `共 ${result.totalRows ?? 0} 行，成功导入 ${result.importedEntries ?? 0} 条，跳过 ${result.skippedRows ?? 0} 行。`
            if ((result.skippedRows ?? 0) > 0) {
                modal.warning({
                    title: "导入完成（部分行被跳过）",
                    content: (
                        <div className="flex flex-col gap-2">
                            <span>{summary}</span>
                            {(result.warnings?.length ?? 0) > 0 && (
                                <ul className="flex list-disc flex-col gap-1 ps-4 text-[12.5px]">
                                    {result.warnings?.map((w, i) => <li key={i}>{w}</li>)}
                                </ul>
                            )}
                        </div>
                    ),
                    okText: "知道了",
                })
            } else {
                message.success(summary)
            }
        } catch (err) {
            message.error(await apiErrorMessage(err, "导入失败，请重试。"))
        }
    }

    /* ---------- 新增日记账（账簿） ---------- */

    async function createJournal() {
        if (!token || !accountSetId || creating) return
        if (!createForm.name.trim()) {
            message.warning("请填写日记账名称。")
            return
        }
        setCreating(true)
        try {
            const created = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsPost({
                accountSetId,
                createJournalRequest: {
                    name: createForm.name.trim(),
                    type: journalType,
                    openingBalance: Number(createForm.openingBalance) || 0,
                    bankName: isCash ? null : createForm.bankName.trim() || null,
                    bankAccountNo: isCash ? null : createForm.bankAccountNo.trim() || null,
                },
            })
            setCreateOpen(false)
            setCreateForm({ name: "", openingBalance: "", bankName: "", bankAccountNo: "" })
            await loadJournals()
            if (created.id) setJournalId(created.id)
            message.success("日记账已创建。")
        } catch (err) {
            message.error(await apiErrorMessage(err, "创建日记账失败，请重试。"))
        } finally {
            setCreating(false)
        }
    }

    /* ---------- 列头筛选 / 列设置 ---------- */

    /** 表头排序统一受控：sortInfo 为空时回到默认的「日期 + 序号」自然顺序。 */
    const sortable = (key: string) => ({
        sorter: true as const,
        sortOrder: sortInfo?.field === key ? sortInfo.order : null,
    })

    const textFilter = (key: FilterKey) => ({
        filterIcon: <FilterFilled style={{ color: filters[key] ? "var(--brand)" : "var(--muted-foreground)" }} />,
        filteredValue: filters[key] ? [filters[key]] : null,
        filterDropdown: ({ confirm }: { confirm: () => void }) => (
            <div className="w-[200px] p-2" onKeyDown={(e) => e.stopPropagation()}>
                <Input
                    autoFocus
                    allowClear
                                       placeholder="输入关键字筛选"
                    value={filters[key]}
                    onChange={(e) => setFilters((prev) => ({ ...prev, [key]: e.target.value }))}
                    onPressEnter={() => confirm()}
                />
                <div className="mt-2 flex justify-end gap-2">
                    <Button
                                               onClick={() => {
                            setFilters((prev) => ({ ...prev, [key]: "" }))
                            confirm()
                        }}
                    >
                        重置
                    </Button>
                    <Button type="primary" onClick={() => confirm()}>
                        筛选
                    </Button>
                </div>
            </div>
        ),
    })

    const isFormRow = (row: LedgerRow) => row.kind === "draft" || row.kind === "editing"

    /** 固定列的背景不随 tr 变，单独给单元格上色，保持与整行一致。 */
    const rowBackground = (row: LedgerRow) =>
        row.kind === "total" ? { background: "var(--table-zebra-bg)" } : isFormRow(row) ? { background: "var(--brand-soft)" } : undefined

    const allColumns: TableColumnsType<LedgerRow> = [
        {
            key: "date",
            title: "日期",
            width: 120,
            ...sortable("date"),
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("date"),
            render: (_, row) => {
                if (isFormRow(row)) {
                    return (
                        <DatePicker
                                                       allowClear={false}
                            suffixIcon={null}
                            className="w-full"
                            value={form.date ? dayjs(form.date) : null}
                            onChange={(v) => patchForm({ date: v ? v.format("YYYY-MM-DD") : "" })}
                        />
                    )
                }
                if (row.kind === "total" || row.kind === "opening") return null
                return <span className="whitespace-nowrap">{toISODate(row.entry?.date)}</span>
            },
        },
        {
            key: "summary",
            title: "摘要",
            width: 200,
            ...sortable("summary"),
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("summary"),
            render: (_, row) => {
                if (row.kind === "opening") return "上笔余额"
                if (row.kind === "total") return <span className="font-semibold">合计</span>
                if (isFormRow(row)) {
                    return (
                        <Input
                                                       value={form.summary}
                            placeholder="填写摘要"
                            style={{ borderColor: "var(--brand)" }}
                            onChange={(e) => patchForm({ summary: e.target.value })}
                        />
                    )
                }
                return (
                    <div className="truncate" title={row.entry?.summary}>
                        {row.entry?.summary}
                    </div>
                )
            },
        },
        {
            key: "category",
            title: "收支类别",
            width: 148,
            ...sortable("category"),
            ...textFilter("category"),
            render: (_, row) => {
                if (isFormRow(row)) {
                    return (
                        <Select
                            allowClear
                            showSearch
                            optionFilterProp="label"
                            popupMatchSelectWidth={false}
                            className="w-full"
                            classNames={{ popup: { root: "journal-category-popup" } }}
                            placeholder="选择类别"
                            value={form.category || undefined}
                            options={categoryOptions}
                            onChange={(v) => patchForm({ category: v ?? "" })}
                            popupRender={(menu) => (
                                <>
                                    {menu}
                                    <div
                                        className="border-t border-(--border) px-2 py-1"
                                        onMouseDown={(e) => e.preventDefault()}
                                    >
                                        <Button
                                            type="link"
                                            size="small"
                                            icon={<PlusOutlined />}
                                            block
                                            onClick={() => {
                                                openCategoryQuick()
                                            }}
                                        >
                                            点击添加
                                        </Button>
                                    </div>
                                </>
                            )}
                        />
                    )
                }
                return row.kind === "entry" ? displayCategory(row.entry?.category) : null
            },
        },
        {
            key: "counterparty",
            title: "往来单位",
            width: 168,
            ...sortable("counterparty"),
            ...textFilter("counterparty"),
            render: (_, row) => {
                if (isFormRow(row)) {
                    return (
                        <Input
                            value={form.counterparty}
                            placeholder="输入或选择"
                            onChange={(e) => patchForm({ counterparty: e.target.value })}
                            suffix={
                                <Button
                                    type="text"
                                    size="small"
                                    icon={<EllipsisOutlined />}
                                    title="选择往来单位"
                                    onClick={(e) => {
                                        e.stopPropagation()
                                        openPicker("form")
                                    }}
                                />
                            }
                        />
                    )
                }
                return row.kind === "entry" ? (row.entry?.counterparty ?? "") : null
            },
        },
        {
            key: "income",
            title: "收入（借方）",
            align: "right",
            width: 116,
            ...sortable("income"),
            className: "tabular-nums",
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("income"),
            render: (_, row) => {
                if (row.kind === "opening") return null
                if (row.kind === "total") {
                    return <span className="font-semibold">{fmtAmount(totals.income, true)}</span>
                }
                if (isFormRow(row)) {
                    return (
                        <Input
                                                       className="text-right"
                            placeholder="0.00"
                            value={form.income}
                            onChange={(e) => patchForm({ income: e.target.value.replace(/[^\d.]/g, "") })}
                        />
                    )
                }
                return fmtAmount(row.entry?.income)
            },
        },
        {
            key: "expense",
            title: "支出（贷方）",
            align: "right",
            width: 116,
            ...sortable("expense"),
            className: "tabular-nums",
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("expense"),
            render: (_, row) => {
                if (row.kind === "opening") return null
                if (row.kind === "total") {
                    return <span className="font-semibold">{fmtAmount(totals.expense, true)}</span>
                }
                if (isFormRow(row)) {
                    return (
                        <Input
                                                       className="text-right"
                            placeholder="0.00"
                            value={form.expense}
                            onChange={(e) => patchForm({ expense: e.target.value.replace(/[^\d.]/g, "") })}
                        />
                    )
                }
                return fmtAmount(row.entry?.expense)
            },
        },
        {
            key: "balance",
            title: "余额",
            align: "right",
            width: 118,
            ...sortable("balance"),
            className: "tabular-nums",
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("balance"),
            render: (_, row) => {
                if (row.kind === "total") {
                    return <span className="font-semibold">{fmtAmount(totals.closing, true)}</span>
                }
                if (row.kind === "opening") return fmtAmount(openingBalance)
                if (isFormRow(row)) return null
                return fmtAmount(row.entry?.balance)
            },
        },
        {
            key: "voucher",
            title: "关联凭证",
            width: 132,
            ...sortable("voucher"),
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("voucher"),
            render: (_, row) =>
                row.kind === "entry" && row.entry?.voucherNo ? (
                    <Typography.Link
                        className="whitespace-nowrap"
                        onClick={() => navigate(setPath(accountSetId, "/vouchers/manage"))}
                        title="前往凭证管理查看"
                    >
                        {row.entry.voucherNo}
                    </Typography.Link>
                ) : null,
        },
        {
            key: "seq",
            title: "日记账序号",
            width: 164,
            ...sortable("seq"),
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("seq"),
            render: (_, row) => {
                if (isFormRow(row)) return <span className="whitespace-nowrap">{`${seqPrefix}-${form.date.replaceAll("-", "")}`}</span>
                return row.kind === "entry" && row.entry ? (
                    <span className="whitespace-nowrap">{seqOf(row.entry)}</span>
                ) : null
            },
        },
        /** 可选列默认收起（表头齿轮开启），开启后行内登记行同步出现输入框。 */
        {
            key: "settlementMethod",
            title: "结算方式",
            width: 120,
            ...sortable("settlementMethod"),
            render: (_, row) => {
                if (isFormRow(row)) {
                    return (
                        <AutoComplete
                            className="w-full"
                            value={form.settlementMethod}
                            options={SETTLEMENT_METHODS.map((v) => ({ value: v }))}
                            placeholder="结算方式"
                            onChange={(v) => patchForm({ settlementMethod: v ?? "" })}
                        />
                    )
                }
                return row.kind === "entry" ? (row.entry?.settlementMethod ?? "") : null
            },
        },
        {
            key: "billNo",
            title: "票据号",
            width: 120,
            ...sortable("billNo"),
            render: (_, row) =>
                isFormRow(row) ? (
                    <Input value={form.billNo} placeholder="票据号" onChange={(e) => patchForm({ billNo: e.target.value })} />
                ) : row.kind === "entry" ? (
                    (row.entry?.billNo ?? "")
                ) : null,
        },
        {
            key: "project",
            title: "项目",
            width: 120,
            ...sortable("project"),
            render: (_, row) =>
                isFormRow(row) ? (
                    <Input value={form.project} placeholder="项目" onChange={(e) => patchForm({ project: e.target.value })} />
                ) : row.kind === "entry" ? (
                    (row.entry?.project ?? "")
                ) : null,
        },
        {
            key: "department",
            title: "部门",
            width: 120,
            ...sortable("department"),
            render: (_, row) =>
                isFormRow(row) ? (
                    <Input value={form.department} placeholder="部门" onChange={(e) => patchForm({ department: e.target.value })} />
                ) : row.kind === "entry" ? (
                    (row.entry?.department ?? "")
                ) : null,
        },
        {
            key: "remark",
            title: "备注",
            width: 160,
            ...sortable("remark"),
            render: (_, row) =>
                isFormRow(row) ? (
                    <Input value={form.remark} placeholder="备注" onChange={(e) => patchForm({ remark: e.target.value })} />
                ) : row.kind === "entry" ? (
                    (row.entry?.remark ?? "")
                ) : null,
        },
        {
            key: "source",
            title: "流水来源",
            width: 120,
            ...sortable("source"),
            render: (_, row) => {
                if (isFormRow(row)) {
                    return (
                        <Select
                            className="w-full"
                            value={form.source || "手工登记"}
                            options={SOURCE_OPTIONS.map((v) => ({ value: v, label: v }))}
                            onChange={(v) => patchForm({ source: v })}
                        />
                    )
                }
                return row.kind === "entry" ? (row.entry?.source ?? "手工登记") : null
            },
        },
        {
            key: "voucherDate",
            title: "凭证日期",
            width: 120,
            ...sortable("voucherDate"),
            render: (_, row) => {
                if (isFormRow(row)) return null
                return row.kind === "entry" && row.entry?.voucherDate ? toISODate(row.entry.voucherDate) : null
            },
        },
        {
            key: "voucherWord",
            title: "凭证字",
            width: 100,
            ...sortable("voucherWord"),
            render: (_, row) => {
                if (isFormRow(row)) return null
                return row.kind === "entry" ? (row.entry?.voucherWord ?? "") : null
            },
        },
        {
            key: "ops",
            title: "操作",
            width: 150,
            fixed: "right",
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            onCell: (row) => ({ style: rowBackground(row) }),
            render: (_, row) => {
                if (isFormRow(row)) {
                    return (
                        <Space size={8} wrap>
                            <Typography.Link disabled={saving} onClick={() => void saveForm()}>
                                保存
                            </Typography.Link>
                            <Typography.Link type="secondary" onClick={cancelEdit}>
                                取消
                            </Typography.Link>
                        </Space>
                    )
                }
                if (row.kind !== "entry" || !row.entry) return null
                return (
                    <Space size={8} wrap>
                        <Typography.Link onClick={() => startEdit(row.entry as JournalEntryDto)}>编辑</Typography.Link>
                        <Typography.Link type="danger" onClick={() => removeEntry(row.entry as JournalEntryDto)}>
                            删除
                        </Typography.Link>
                        <Typography.Link onClick={() => void printReceipt(row.entry as JournalEntryDto)}>打印</Typography.Link>
                        <Typography.Link onClick={() => message.info("附件管理建设中。")}>附件</Typography.Link>
                    </Space>
                )
            },
        },
        {
            key: "__settings__",
            title: (
                <Dropdown
                    trigger={["click"]}
                    popupRender={() => (
                        <div className="flex flex-col gap-1 rounded-xl border border-(--border) bg-(--card) p-3 shadow-lg">
                            {COLUMN_TOGGLES.map(([key, label]) => (
                                <Checkbox
                                    key={key}
                                    checked={!hidden.has(key)}
                                    onChange={(e) =>
                                        setHidden((prev) => {
                                            const next = new Set(prev)
                                            if (e.target.checked) next.delete(key)
                                            else next.add(key)
                                            return next
                                        })
                                    }
                                >
                                    {label}
                                </Checkbox>
                            ))}
                        </div>
                    )}
                >
                    <Button type="text" icon={<SettingOutlined />} title="设置列" />
                </Dropdown>
            ),
            width: 48,
            align: "center",
            fixed: "right",
            onCell: (row) => ({ style: rowBackground(row) }),
            render: () => null,
        },
    ]

    const columns = allColumns.filter((c) => c.key === "__settings__" || !hidden.has(String(c.key)))
    /** 横向滚动宽度随列显示设置变化；勾选额外列后表格不会挤在一起。 */
    const tableWidth = columns.reduce((sum, c) => sum + (typeof c.width === "number" ? c.width : 0), 0) + 44

    /* ---------- 渲染 ---------- */

    const from = sorted.length === 0 ? 0 : (current - 1) * pageSize + 1
    const to = Math.min(current * pageSize, sorted.length)

    /** 批量操作弹窗的文案：自由文本类（往来 / 项目 / 部门）共用一个输入框。 */
    const textBulk = bulkKind ? TEXT_BULK[bulkKind] : undefined
    const bulkTitle =
        bulkKind === "summary" ? "批量修改摘要" : bulkKind === "category" ? "指定收支类别" : textBulk ? `指定${textBulk.label}` : "关联凭证"

    const advRow = (label: string, control: ReactNode) => (
        <div className="flex items-center gap-3">
            <span className="w-[88px] shrink-0 text-right text-[13px] text-(--foreground)">{label}：</span>
            <div className="min-w-0 flex-1">{control}</div>
        </div>
    )

    const advText = (key: keyof AdvancedFilters, placeholder: string) => (
        <Input
            allowClear
            value={String(advDraft[key] ?? "")}
            placeholder={placeholder}
            onChange={(e) => setAdvDraft((prev) => ({ ...prev, [key]: e.target.value }))}
        />
    )

    /** 高级筛选：字段顺序与柠檬云「现金日记账」筛选面板一致。 */
    const advancedPanel = (
        <div className="flex w-[560px] flex-col">
            <div className="flex max-h-[68vh] flex-col gap-3 overflow-y-auto pe-1">
                {advRow(
                    "日期",
                    <DatePicker.RangePicker
                        className="w-full"
                        allowClear={false}
                        value={advDraft.period ? [dayjs(advDraft.period[0]), dayjs(advDraft.period[1])] : null}
                        placeholder={["开始日期", "结束日期"]}
                        onChange={(dates) =>
                            setAdvDraft((prev) => ({
                                ...prev,
                                period:
                                    dates && dates[0] && dates[1]
                                        ? [dates[0].format("YYYY-MM-DD"), dates[1].format("YYYY-MM-DD")]
                                        : null,
                            }))
                        }
                    />,
                )}
                {advRow(
                    "凭证日期",
                    <DatePicker.RangePicker
                        className="w-full"
                        value={
                            advDraft.voucherDate
                                ? [dayjs(advDraft.voucherDate[0]), dayjs(advDraft.voucherDate[1])]
                                : null
                        }
                        placeholder={["开始日期", "结束日期"]}
                        onChange={(dates) =>
                            setAdvDraft((prev) => ({
                                ...prev,
                                voucherDate:
                                    dates && dates[0] && dates[1]
                                        ? [dates[0].format("YYYY-MM-DD"), dates[1].format("YYYY-MM-DD")]
                                        : null,
                            }))
                        }
                    />,
                )}
                {advRow(
                    "凭证字号",
                    <div className="flex items-center gap-2">
                        <Select
                            allowClear
                            className="w-[120px]"
                            placeholder="请选择"
                            value={advDraft.voucherWord || undefined}
                            options={voucherWords.map((w) => ({ value: w.word ?? "", label: w.word ?? "" }))}
                            onChange={(v) => setAdvDraft((prev) => ({ ...prev, voucherWord: v ?? "" }))}
                        />
                        <Input
                            value={advDraft.voucherNoFrom}
                            placeholder="起始号"
                            onChange={(e) =>
                                setAdvDraft((prev) => ({ ...prev, voucherNoFrom: e.target.value.replace(/[^\d]/g, "") }))
                            }
                        />
                        <span className="shrink-0 text-(--muted-foreground)">至</span>
                        <Input
                            value={advDraft.voucherNoTo}
                            placeholder="截止号"
                            onChange={(e) =>
                                setAdvDraft((prev) => ({ ...prev, voucherNoTo: e.target.value.replace(/[^\d]/g, "") }))
                            }
                        />
                    </div>,
                )}
                {advRow("收支类别", advText("category", "输入收支类别关键字"))}
                {advRow(
                    "结算方式",
                    <AutoComplete
                        className="w-full"
                        allowClear
                        value={advDraft.settlementMethod}
                        options={SETTLEMENT_METHODS.map((v) => ({ value: v }))}
                        placeholder="请选择或输入"
                        onChange={(v) => setAdvDraft((prev) => ({ ...prev, settlementMethod: v ?? "" }))}
                    />,
                )}
                {advRow(
                    "生成凭证",
                    <Select<VoucherState>
                        className="w-full"
                        value={advDraft.voucher}
                        options={VOUCHER_STATE_OPTIONS}
                        onChange={(v) => setAdvDraft((prev) => ({ ...prev, voucher: v }))}
                    />,
                )}
                {advRow(
                    "往来单位",
                    <Space.Compact className="flex w-full">
                        <AutoComplete
                            className="w-full"
                            allowClear
                            value={advDraft.counterparty}
                            options={counterpartyOptions}
                            placeholder="请选择或输入"
                            onChange={(v) => setAdvDraft((prev) => ({ ...prev, counterparty: v ?? "" }))}
                        />
                        <Button icon={<EllipsisOutlined />} title="选择往来单位" onClick={() => openPicker("filter")} />
                    </Space.Compact>,
                )}
                {advRow("票据号", advText("billNo", "输入票据号关键字"))}
                {advRow("项目", advText("project", "输入项目关键字"))}
                {advRow("部门", advText("department", "输入部门关键字"))}
                {advRow("摘要", advText("summary", "输入摘要关键字"))}
                {advRow("备注", advText("remark", "输入备注关键字"))}
                {advRow(
                    "流水来源",
                    <Select
                        allowClear
                        className="w-full"
                        placeholder="全部来源"
                        value={advDraft.source || undefined}
                        options={SOURCE_OPTIONS.map((v) => ({ value: v, label: v }))}
                        onChange={(v) => setAdvDraft((prev) => ({ ...prev, source: v ?? "" }))}
                    />,
                )}
                {advRow(
                    "金额",
                    <div className="flex items-center gap-2">
                        <Input
                            value={advDraft.amountFrom}
                            placeholder="最小金额"
                            onChange={(e) =>
                                setAdvDraft((prev) => ({ ...prev, amountFrom: e.target.value.replace(/[^\d.]/g, "") }))
                            }
                        />
                        <span className="shrink-0 text-(--muted-foreground)">至</span>
                        <Input
                            value={advDraft.amountTo}
                            placeholder="最大金额"
                            onChange={(e) =>
                                setAdvDraft((prev) => ({ ...prev, amountTo: e.target.value.replace(/[^\d.]/g, "") }))
                            }
                        />
                    </div>,
                )}
                <p className="ps-[100px] text-[12px] leading-5 text-(--muted-foreground)">
                    结算方式 / 票据号 / 项目 / 部门 / 备注 / 流水来源 等字段可在表头齿轮中开启对应列后录入。
                </p>
            </div>
            <div className="mt-3 flex items-center gap-2 border-t border-(--border) pt-3">
                <Button type="primary" onClick={applyAdvanced}>
                    确定
                </Button>
                <Button onClick={() => setAdvOpen(false)}>取消</Button>
                <Button onClick={resetAdvanced}>重置</Button>
                <Typography.Link
                    className="ms-auto"
                    onClick={() => {
                        setSortInfo(null)
                        message.success("已恢复默认排序（按日期、序号）。")
                    }}
                >
                    恢复默认排序
                </Typography.Link>
            </div>
        </div>
    )

    const toolbarLeft = (
        <Space size={8} wrap>
            <Select
                className="min-w-[210px]"
                value={journalId || undefined}
                placeholder={journals.length === 0 ? "（暂无日记账）" : "选择日记账"}
                onChange={(v) => setJournalId(v)}
                options={journals.map((j, i) => ({
                    value: j.id ?? "",
                    label: `${String(i + 1).padStart(3, "0")} - ${j.name ?? ""}`,
                }))}
                notFoundContent="暂无日记账"
            />
            <Popover
                open={advOpen}
                onOpenChange={(open) => (open ? openAdvanced() : setAdvOpen(false))}
                trigger="click"
                placement="bottomLeft"
                arrow={false}
                content={advancedPanel}
            >
                <FilterTriggerButton
                    title="高级筛选"
                    label={period ? `${period[0]} 至 ${period[1]}` : "全部时间"}
                    count={advancedCount}
                />
            </Popover>
            <Checkbox checked={period === null} onChange={(e) => setPeriod(e.target.checked ? null : dataRange)}>
                显示全部
            </Checkbox>
        </Space>
    )

    const partnerImportHint = (platform: string) => () => {
        modal.info({
            title: `${platform}账单导入`,
            width: 520,
            content: (
                <div className="flex flex-col gap-2 text-[13px] leading-6 text-(--foreground)">
                    <span>
                        在{platform}导出账单明细（Excel / CSV）后，按「日期、摘要、收入、支出」整理成日记账导入模板的表头，
                        再选择文件上传即可。
                    </span>
                    <span className="text-[12px] text-(--muted-foreground)">
                        {platform}原始账单的列名与本系统模板不同，直接上传会被逐行跳过并给出提示。
                    </span>
                </div>
            ),
            okText: "选择文件导入",
            onOk: () => importRef.current?.click(),
        })
    }

    const toolbarRight = (
        <Space size={8} wrap>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)} disabled={!accountSetId}>
                新增日记账
            </Button>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: [
                        { key: "list", label: "打印列表" },
                        { key: "receipt", label: "打印凭据" },
                    ],
                    onClick: ({ key }) => {
                        if (key === "list") void printList()
                        else void printReceipt()
                    },
                }}
            >
                <Button icon={<PrinterOutlined />} disabled={!detail} loading={printing}>
                    打印 <DownOutlined className="text-[10px]" />
                </Button>
            </Dropdown>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: [
                        { key: "one", label: "一键导入" },
                        { key: "alipay", label: "支付宝导入" },
                        { key: "wechat", label: "微信导入" },
                        { key: "wecom", label: "企微导入" },
                        { key: "detail", label: "明细账导入" },
                        { type: "divider" as const },
                        { key: "template", label: "下载导入模板" },
                    ],
                    onClick: ({ key }) => {
                        if (key === "alipay") partnerImportHint("支付宝")()
                        else if (key === "wechat") partnerImportHint("微信")()
                        else if (key === "wecom") partnerImportHint("企业微信")()
                        else if (key === "template") {
                            void downloadFile(`/accountsets/${accountSetId}/Journals/import/template`, "日记账导入模板.xlsx")
                        } else {
                            importRef.current?.click()
                        }
                    },
                }}
            >
                <Button disabled={!journalId}>
                    导入 <DownOutlined className="text-[10px]" />
                </Button>
            </Dropdown>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: [
                        { key: "query", label: "当前查询数据" },
                        { key: "all", label: "所有账户全部数据" },
                    ],
                    onClick: ({ key }) => {
                        if (key === "query") {
                            if (journalId) {
                                void downloadFile(
                                    `/accountsets/${accountSetId}/Journals/${journalId}/export`,
                                    `${detail?.name ?? "日记账"}.xlsx`,
                                )
                            }
                        } else {
                            void downloadFile(
                                `/accountsets/${accountSetId}/Journals/export/all`,
                                `日记账全部数据_${dayjs().format("YYYYMMDDHHmmss")}.xlsx`,
                            )
                        }
                    },
                }}
            >
                <Button icon={<DownloadOutlined />} disabled={!journalId}>
                    导出 <DownOutlined className="text-[10px]" />
                </Button>
            </Dropdown>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: [
                        { key: "delete", label: "批量删除", disabled: selected.length === 0 },
                        { key: "summary", label: "批量修改摘要", disabled: selected.length === 0 },
                        { key: "unlink-voucher", label: "删除关联凭证", disabled: selected.length === 0 },
                        { key: "category", label: "指定收支类别", disabled: selected.length === 0 },
                        { key: "counterparty", label: "指定往来单位", disabled: selected.length === 0 },
                        { key: "project", label: "指定项目", disabled: selected.length === 0 },
                        { key: "department", label: "指定部门", disabled: selected.length === 0 },
                    ],
                    onClick: ({ key }) => {
                        if (key === "delete") batchDelete()
                        else openBulk(key as BulkKind)
                    },
                }}
            >
                <Button>
                    批量操作 <DownOutlined className="text-[10px]" />
                </Button>
            </Dropdown>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: [
                        { key: "link", label: "关联凭证", disabled: selected.length === 0 },
                        { key: "unlink", label: "取消关联", disabled: selected.length === 0 },
                    ],
                    onClick: ({ key }) => openBulk(key === "link" ? "link-voucher" : "unlink-voucher"),
                }}
            >
                <Button>
                    关联凭证 <DownOutlined className="text-[10px]" />
                </Button>
            </Dropdown>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: MERGE_OPTIONS.map((o) => ({ key: String(o.value), label: o.label })),
                    onClick: ({ key }) => openBatch(Number(key)),
                }}
            >
                <Space.Compact>
                    <Button type="primary" onClick={() => openBatch(1)}>
                        生成凭证
                    </Button>
                    <Button type="primary" icon={<DownOutlined className="text-[10px]" />} />
                </Space.Compact>
            </Dropdown>
            <Tooltip title="刷新">
                <Button
                    icon={<ReloadOutlined spin={loading || detailLoading} />}
                    onClick={() => {
                        void loadJournals()
                        if (journalId) void loadDetail(journalId)
                    }}
                />
            </Tooltip>
            <Tooltip title="全屏">
                <Button icon={<FullscreenOutlined />} onClick={() => void toggleFullscreen()} />
            </Tooltip>
        </Space>
    )

    return (
        <div className="flex flex-col gap-3">
            {error && <Alert type="error" showIcon title={error} closable onClose={() => setError(null)} />}

            <Card>
                <Flex justify="space-between" align="center" gap={12} wrap style={{ marginBottom: 16 }}>
                    {toolbarLeft}
                    {toolbarRight}
                </Flex>

                <input
                    ref={importRef}
                    type="file"
                    accept=".xls,.xlsx"
                    className="hidden"
                    onChange={(e) => {
                        const file = e.target.files?.[0]
                        e.target.value = ""
                        if (file) void importEntries(file)
                    }}
                />

                {journals.length === 0 && !loading ? (
                    <div className="flex flex-col items-center gap-3 py-16">
                        <Empty
                            image={Empty.PRESENTED_IMAGE_SIMPLE}
                            description={accountSetId ? `还没有${typeLabel}，点击「新增日记账」创建。` : "请先选择账套。"}
                        />
                        {accountSetId && (
                            <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                                新增日记账
                            </Button>
                        )}
                    </div>
                ) : (
                    <Spin spinning={loading || detailLoading}>
                        {/* 期初/收入/支出/期末固定在表格上方：分页后不用滚到表尾才能看到汇总。 */}
                        <div className="mb-3 flex flex-wrap items-center gap-x-8 gap-y-2 rounded-lg bg-(--table-zebra-bg) px-4 py-2.5 text-[13px]">
                            <span className="text-(--muted-foreground)">
                                期初余额{" "}
                                <span className="ml-1 font-medium tabular-nums text-(--foreground)">
                                    ¥{fmtAmount(openingBalance, true)}
                                </span>
                            </span>
                            <span className="text-(--muted-foreground)">
                                收入合计{" "}
                                <span className="ml-1 font-medium tabular-nums text-(--success)">
                                    {totals.income > 0 ? "+" : ""}¥{fmtAmount(totals.income, true)}
                                </span>
                            </span>
                            <span className="text-(--muted-foreground)">
                                支出合计{" "}
                                <span className="ml-1 font-medium tabular-nums text-(--danger)">
                                    {totals.expense > 0 ? "-" : ""}¥{fmtAmount(totals.expense, true)}
                                </span>
                            </span>
                            <span className="text-(--muted-foreground)">
                                期末余额{" "}
                                <span className="ml-1 text-[14px] font-semibold tabular-nums text-(--foreground)">
                                    ¥{fmtAmount(totals.closing, true)}
                                </span>
                            </span>
                            <span className="ml-auto text-[12px] text-(--muted-foreground)">共 {filtered.length} 笔流水</span>
                        </div>
                        <Table<LedgerRow>
                            rowKey="key"
                            tableLayout="fixed"
                            columns={columns}
                            dataSource={rows}
                            pagination={false}
                            scroll={{ x: tableWidth }}
                            onChange={(_pagination, _tableFilters, sorter) => {
                                const s = Array.isArray(sorter) ? sorter[0] : sorter
                                const field = s?.columnKey != null ? String(s.columnKey) : ""
                                if (!s?.order || !field) setSortInfo(null)
                                else setSortInfo({ field, order: s.order })
                            }}
                            rowClassName={(row) =>
                                cn(
                                    row.kind === "total" && "bg-(--table-zebra-bg)",
                                    isFormRow(row) && "bg-(--brand-soft)",
                                )
                            }
                            rowSelection={{
                                selectedRowKeys: selected,
                                onChange: (keys) => setSelected(keys.map(String)),
                                // 所有流水行都可勾选：批量操作里的「删除关联凭证」「批量删除」要能选中已关联凭证的行。
                                getCheckboxProps: (row) => ({
                                    disabled: row.kind !== "entry" || !row.entry?.id,
                                }),
                                columnWidth: 44,
                            }}
                        />
                    </Spin>
                )}

                {(
                    <Flex justify="space-between" align="center" gap={16} wrap style={{ marginTop: 16 }}>
                        <Typography.Text type="secondary">
                            显示 {from} 到 {to}，共 {sorted.length} 条
                        </Typography.Text>
                        <Pagination
                            current={current}
                            pageSize={pageSize}
                            total={sorted.length}
                            showSizeChanger
                            pageSizeOptions={PAGE_SIZE_OPTIONS}
                            onChange={(nextPage, nextSize) => {
                                if (nextSize !== pageSize) {
                                    setPageSize(nextSize)
                                    setPage(1)
                                } else {
                                    setPage(nextPage)
                                }
                            }}
                        />
                    </Flex>
                )}
            </Card>

            {/* 新增日记账（账簿） */}
            <Modal
                open={createOpen}
                title={`新增${typeLabel}`}
                onCancel={() => setCreateOpen(false)}
                onOk={() => void createJournal()}
                okText="保存"
                cancelText="取消"
                confirmLoading={creating}
            >
                <div className="flex flex-col gap-3 py-2">
                    <label className="flex items-center gap-3 text-[13px]">
                        <span className="w-[84px] text-right">名称：</span>
                        <Input
                            className="flex-1"
                            value={createForm.name}
                            placeholder={isCash ? "如：现金、备用金" : "如：工商银行基本户"}
                            onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
                        />
                    </label>
                    <label className="flex items-center gap-3 text-[13px]">
                        <span className="w-[84px] text-right">期初余额：</span>
                        <Input
                            className="flex-1 text-right"
                            value={createForm.openingBalance}
                            placeholder="0.00"
                            onChange={(e) =>
                                setCreateForm({ ...createForm, openingBalance: e.target.value.replace(/[^\d.]/g, "") })
                            }
                        />
                    </label>
                    {!isCash && (
                        <>
                            <label className="flex items-center gap-3 text-[13px]">
                                <span className="w-[84px] text-right">开户行：</span>
                                <Input
                                    className="flex-1"
                                    value={createForm.bankName}
                                    onChange={(e) => setCreateForm({ ...createForm, bankName: e.target.value })}
                                />
                            </label>
                            <label className="flex items-center gap-3 text-[13px]">
                                <span className="w-[84px] text-right">银行账号：</span>
                                <Input
                                    className="flex-1"
                                    value={createForm.bankAccountNo}
                                    onChange={(e) => setCreateForm({ ...createForm, bankAccountNo: e.target.value })}
                                />
                            </label>
                        </>
                    )}
                    <p className="ps-[96px] text-[12px] leading-5 text-(--muted-foreground)">
                        保存后系统会自动在「{isCash ? "库存现金 1001" : "银行存款 1002"}」下建立同名明细科目并与账簿绑定。
                    </p>
                </div>
            </Modal>

            {/* 批量生成凭证 */}
            <Modal
                open={batchOpen}
                title="生成凭证"
                width={520}
                onCancel={() => setBatchOpen(false)}
                onOk={() => void submitBatch()}
                okText="生成凭证"
                cancelText="取消"
                confirmLoading={batchSaving}
            >
                <p className="mb-4 text-[12.5px] text-(--muted-foreground)">
                    已选 {selected.length} 笔流水；同一张凭证内相同科目、相同方向的行将自动汇总为一行。
                </p>
                <Radio.Group
                    className="flex flex-col gap-2"
                    value={mergeBy}
                    options={MERGE_OPTIONS}
                    onChange={(e) => setMergeBy(e.target.value)}
                />
                <div className="mt-4 flex flex-col gap-1">
                    <span className="text-[13px] font-medium text-(--foreground)">对方科目</span>
                    <Select
                        value={counterId}
                        onChange={setCounterId}
                        options={[
                            { value: "auto", label: "自动：取「收支类别」的默认对方科目" },
                            ...counterAccounts
                                .filter((a) => a.id !== detail?.accountId)
                                .map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
                        ]}
                    />
                    <span className="text-[12px] text-(--muted-foreground)">未配置默认对方科目的流水需要在此指定统一科目。</span>
                </div>
            </Modal>

            {/* 批量操作：修改摘要 / 指定类别 / 指定往来 / 指定项目 / 指定部门 / 关联凭证 */}
            <Modal
                open={bulkKind !== null && bulkKind !== "delete" && bulkKind !== "unlink-voucher"}
                title={bulkTitle}
                width={480}
                onCancel={() => setBulkKind(null)}
                onOk={() => void submitBulk()}
                okText="确定"
                cancelText="取消"
                confirmLoading={bulkBusy}
            >
                <div className="flex flex-col gap-3 py-1">
                    <p className="text-[12.5px] text-(--muted-foreground)">已选 {selected.length} 笔流水</p>
                    {(bulkKind === "summary" || textBulk) && (
                        <Input
                            value={bulkText}
                            placeholder={bulkKind === "summary" ? "请输入新的摘要" : `请输入${textBulk?.label}，留空表示清除`}
                            onChange={(e) => setBulkText(e.target.value)}
                            suffix={
                                bulkKind === "counterparty" ? (
                                    <Button
                                        type="text"
                                        size="small"
                                        icon={<EllipsisOutlined />}
                                        title="选择往来单位"
                                        onClick={() => openPicker("bulk")}
                                    />
                                ) : undefined
                            }
                        />
                    )}
                    {bulkKind === "category" && (
                        <Select
                            className="w-full"
                            value={bulkText || undefined}
                            placeholder="选择收支类别，留空表示清除"
                            allowClear
                            showSearch
                            options={categoryOptions}
                            onChange={(v) => setBulkText(v ?? "")}
                        />
                    )}
                    {bulkKind === "link-voucher" && (
                        <>
                            <Select
                                className="w-full"
                                value={linkVoucherId || undefined}
                                placeholder="选择要关联的凭证"
                                loading={linkLoading}
                                showSearch
                                optionFilterProp="label"
                                onChange={(v) => setLinkVoucherId(v ?? "")}
                                options={linkVouchers.map((v) => ({
                                    value: v.id ?? "",
                                    label: `${v.voucherNo ?? `${v.word ?? ""}-${v.number ?? ""}`} ${toISODate(v.date)} 借 ${fmtAmount(v.totalDebit ?? 0)}`,
                                }))}
                                notFoundContent={linkLoading ? "加载中…" : "当前期间没有凭证"}
                            />
                            <span className="text-[12px] text-(--muted-foreground)">
                                候选为当前账套的全部凭证，可按凭证字号或日期检索；关联后行内「关联凭证」列会显示凭证号。
                            </span>
                        </>
                    )}
                </div>
            </Modal>

            {/* 选择往来单位：客户 / 供应商 / 职员档案 */}
            <CounterpartyPicker
                open={pickerTarget !== null}
                accountSetId={accountSetId}
                token={token ?? ""}
                onSelect={handlePickerSelect}
                onClear={pickerTarget === "form" || pickerTarget === "bulk" ? handlePickerClear : undefined}
                onClose={() => setPickerTarget(null)}
            />

            {/* 「+点击添加」快速新增收支类别 */}
            <Modal
                open={categoryQuickOpen}
                title="新增收支类别"
                width={420}
                onCancel={() => setCategoryQuickOpen(false)}
                onOk={() => void submitCategoryQuick()}
                okText="保存"
                cancelText="取消"
                confirmLoading={categoryQuickSaving}
                mask={{ closable: false }}
            >
                <div className="flex flex-col gap-3 py-2">
                    <div className="flex items-center gap-3 text-[13px]">
                        <span className="w-[84px] shrink-0 text-right text-(--foreground)">类别方向：</span>
                        <Radio.Group
                            value={categoryQuickForm.type}
                            onChange={(e) =>
                                setCategoryQuickForm((prev) => ({ ...prev, type: Number(e.target.value) }))
                            }
                            options={[
                                { value: 1, label: "收入" },
                                { value: 2, label: "支出" },
                            ]}
                        />
                    </div>
                    <label className="flex items-center gap-3 text-[13px]">
                        <span className="w-[84px] shrink-0 text-right text-(--foreground)">类别名称：</span>
                        <Input
                            className="flex-1"
                            autoFocus
                            value={categoryQuickForm.name}
                            placeholder="如：销售收入"
                            onChange={(e) => setCategoryQuickForm((prev) => ({ ...prev, name: e.target.value }))}
                            onPressEnter={() => void submitCategoryQuick()}
                        />
                    </label>
                    <p className="ps-[96px] text-[12px] leading-5 text-(--muted-foreground)">
                        编码由系统自动编号，保存后可在「收支类别」里补充默认对方科目与智能匹配关键字。
                    </p>
                </div>
            </Modal>
        </div>
    )
}
