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
    ConfigProvider,
    DatePicker,
    Dropdown,
    Empty,
    Flex,
    Input,
    InputNumber,
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
import { DownOutlined, FilterFilled, FullscreenOutlined, PlusOutlined, ReloadOutlined, SettingOutlined } from "@ant-design/icons"
import dayjs from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AccountsApi,
    EntryCategoriesApi,
    JournalsApi,
    VouchersApi,
    type AccountDto,
    type EntryCategoryDto,
    type JournalDetailDto,
    type JournalEntryDto,
    type JournalListItemDto,
    type VoucherListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount, toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"

/** 每页条数候选，与账页页脚的分页器一致。 */
const PAGE_SIZE_OPTIONS = [20, 50, 100]

/** 批量生成凭证的合并方式（与后端 MergeBy 对齐）。 */
const MERGE_OPTIONS = [
    { value: 1, label: "合并成一张凭证" },
    { value: 2, label: "按日期分开" },
    { value: 3, label: "按收支类别分开" },
    { value: 4, label: "按收支方向分开（收入 / 支出各一张）" },
]

/** 「打印列表设置」面板字段，与柠檬云的打印方向 / 页边距 / 表尾字段一致。 */
interface ListPrintSettings {
    orientation: "portrait" | "landscape"
    marginTop: number
    marginBottom: number
    marginLeft: number
    marginRight: number
    showPrintDate: boolean
    showPageNo: boolean
    footerFields: string[]
}

/** 「打印凭据设置」面板字段。 */
interface ReceiptPrintSettings {
    showDept: boolean
    showProject: boolean
    showDivider: boolean
    showPrintDate: boolean
    showPageNo: boolean
}

const DEFAULT_LIST_PRINT: ListPrintSettings = {
    orientation: "landscape",
    marginTop: 10,
    marginBottom: 20,
    marginLeft: 0,
    marginRight: 0,
    showPrintDate: true,
    showPageNo: true,
    footerFields: ["账套名称"],
}

const DEFAULT_RECEIPT_PRINT: ReceiptPrintSettings = {
    showDept: false,
    showProject: false,
    showDivider: true,
    showPrintDate: true,
    showPageNo: true,
}

/** 表尾可选字段（打印列表设置里的「表尾字段设置」）。 */
const FOOTER_FIELD_OPTIONS = ["账套名称", "账户名称", "制表人", "审核人", "记账人", "单位负责人"]

/** 读取按账套持久化的打印设置。 */
function readPrintSetting<T>(key: string, fallback: T): T {
    if (typeof window === "undefined") return fallback
    try {
        const raw = window.localStorage.getItem(key)
        return raw ? { ...fallback, ...(JSON.parse(raw) as T) } : fallback
    } catch {
        return fallback
    }
}

/** 写入打印设置。 */
function writePrintSetting(key: string, value: unknown) {
    try {
        window.localStorage.setItem(key, JSON.stringify(value))
    } catch {
        // 隐私模式下写入失败时忽略，仅当次生效。
    }
}

/** 打印用 HTML 转义。 */
function escHtml(text: string): string {
    return text.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c] ?? c)
}

/** 金额千分位。 */
function money(value: number): string {
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 打开打印窗口并写入 HTML，加载完成后自动唤起打印对话框。 */
function openPrintWindow(html: string) {
    const win = window.open("", "_blank", "width=1100,height=800")
    if (!win) return false
    win.document.open()
    win.document.write(html)
    win.document.close()
    win.focus()
    win.setTimeout(() => win.print(), 400)
    return true
}

/** 组装「打印列表」的多页 HTML：按设置的方向、页边距、页脚与日期页码输出。 */
function buildListPrintHtml(
    title: string,
    accountName: string,
    periodText: string,
    entries: JournalEntryDto[],
    opening: number,
    settings: ListPrintSettings,
    footerValues: Record<string, string>,
    seqPrefix: string,
): string {
    const horizontalPadding = settings.orientation === "landscape" ? 12 : 16
    const totalIncome = entries.reduce((s, e) => s + (e.income ?? 0), 0)
    const totalExpense = entries.reduce((s, e) => s + (e.expense ?? 0), 0)

    const rows = [
        `<tr class="opening"><td>初始化余额</td><td></td><td></td><td></td><td></td><td class="num">${money(opening)}</td><td></td><td></td></tr>`,
        ...entries.map((e) => {
            const iso = new Date(e.date ?? "").toISOString().slice(0, 10)
            return `<tr>
                <td>${escHtml(e.summary ?? "")}</td>
                <td>${escHtml(e.category ?? "")}</td>
                <td>${escHtml(e.counterparty ?? "")}</td>
                <td class="num">${e.income ? money(e.income) : ""}</td>
                <td class="num">${e.expense ? money(e.expense) : ""}</td>
                <td class="num">${money(e.balance ?? 0)}</td>
                <td>${escHtml(e.voucherNo ?? "")}</td>
                <td class="num">${iso ? `${seqPrefix}-${iso.replaceAll("-", "")}-${e.sequence ?? ""}` : ""}</td>
            </tr>`
        }),
        `<tr class="total"><td>合计</td><td></td><td></td><td class="num">${money(totalIncome)}</td><td class="num">${money(totalExpense)}</td><td class="num">${money(opening + totalIncome - totalExpense)}</td><td></td><td></td></tr>`,
    ].join("")

    const footer = settings.footerFields
        .map((f) => `<span>${escHtml(f)}：${escHtml(footerValues[f] ?? "")}</span>`)
        .join("")

    return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><title>${escHtml(title)}</title>
<style>
    @page { size: A4 ${settings.orientation}; margin: ${settings.marginTop}mm ${settings.marginRight}mm ${settings.marginBottom}mm ${settings.marginLeft}mm; }
    * { box-sizing: border-box; }
    body { margin: 0; padding: 8mm ${horizontalPadding}mm 0; font-family: "Microsoft YaHei", SimSun, sans-serif; color: #111; }
    h1 { margin: 0 0 4px; font-size: 16pt; text-align: center; font-weight: 600; }
    .meta { display: flex; justify-content: space-between; font-size: 9pt; color: #444; margin-bottom: 6px; }
    table { width: 100%; border-collapse: collapse; font-size: 9pt; }
    th, td { border: 1px solid #333; padding: 3px 4px; }
    th { background: #f1f5f9; font-weight: 600; text-align: center; white-space: nowrap; }
    .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
    tr.opening td, tr.total td { background: #f8fafc; font-weight: 600; }
    .foot { margin-top: 8px; display: flex; justify-content: space-between; font-size: 9pt; color: #333; gap: 16px; flex-wrap: wrap; }
    .pageno { font-size: 9pt; color: #666; text-align: right; margin-top: 2px; }
</style></head><body>
    <h1>${escHtml(title)}</h1>
    <div class="meta"><span>${escHtml(accountName)}</span><span>${escHtml(periodText)}</span></div>
    <table>
        <thead><tr><th>摘要</th><th>收支类别</th><th>往来单位</th><th>收入（借方）</th><th>支出（贷方）</th><th>余额</th><th>关联凭证</th><th>日记账序号</th></tr></thead>
        <tbody>${rows}</tbody>
    </table>
    <div class="foot">${footer}${settings.showPrintDate ? `<span>打印日期：${new Date().toLocaleString("zh-CN")}</span>` : ""}</div>
    ${settings.showPageNo ? '<div class="pageno">第 1 页 / 共 1 页</div>' : ""}
</body></html>`
}

/** 组装「打印凭据」HTML：每笔流水一张收款/付款凭据，可选打印部门、项目、分割线。 */
function buildReceiptPrintHtml(
    title: string,
    accountName: string,
    entries: JournalEntryDto[],
    settings: ReceiptPrintSettings,
    projectName: string,
    deptName: string,
): string {
    const cards = entries
        .map((e, i) => {
            const isIncome = (e.income ?? 0) > 0
            const amount = isIncome ? (e.income ?? 0) : (e.expense ?? 0)
            const date = new Date(e.date ?? "").toISOString().slice(0, 10)
            const extra = [
                settings.showDept ? `<span>部门：${escHtml(deptName)}</span>` : "",
                settings.showProject ? `<span>项目：${escHtml(projectName)}</span>` : "",
            ]
                .filter(Boolean)
                .join("")
            return `<section class="receipt"${i > 0 && settings.showDivider ? ' style="border-top:1px dashed #999;padding-top:10px;margin-top:10px"' : ""}>
                <h2>${escHtml(isIncome ? "收 款 凭 据" : "付 款 凭 据")}</h2>
                <div class="line"><span>日期：${date}</span><span>${escHtml(accountName)}</span>${extra}</div>
                <table>
                    <tr><th style="width:22%">摘要</th><td>${escHtml(e.summary ?? "")}</td><th style="width:16%">收支类别</th><td>${escHtml(e.category ?? "")}</td></tr>
                    <tr><th>往来单位</th><td>${escHtml(e.counterparty ?? "")}</td><th>金额</th><td class="num">￥${money(amount)}</td></tr>
                    <tr><th>大写金额</th><td colspan="3">${escHtml(toChineseAmount(amount))}</td></tr>
                </table>
                <div class="sign"><span>记账：____________</span><span>出纳：____________</span><span>经办人：____________</span></div>
            </section>`
        })
        .join("")

    return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><title>${escHtml(title)}</title>
<style>
    @page { size: A5 portrait; margin: 12mm; }
    body { margin: 0; font-family: "Microsoft YaHei", SimSun, sans-serif; color: #111; }
    .receipt { page-break-inside: avoid; }
    h2 { margin: 0 0 8px; font-size: 15pt; text-align: center; letter-spacing: 4px; }
    .line { display: flex; gap: 16px; font-size: 9pt; color: #333; margin-bottom: 6px; }
    table { width: 100%; border-collapse: collapse; font-size: 9.5pt; }
    th, td { border: 1px solid #333; padding: 5px 6px; }
    th { background: #f8fafc; font-weight: 600; text-align: left; white-space: nowrap; }
    .num { text-align: right; font-variant-numeric: tabular-nums; }
    .sign { display: flex; justify-content: space-between; margin-top: 14px; font-size: 9pt; color: #333; }
    .pageno { font-size: 8.5pt; color: #666; text-align: right; margin-top: 6px; }
    .pdate { font-size: 8.5pt; color: #666; margin-top: 4px; }
</style></head><body>
    ${cards}
    ${settings.showPrintDate ? `<div class="pdate">打印日期：${new Date().toLocaleString("zh-CN")}</div>` : ""}
    ${settings.showPageNo ? '<div class="pageno">第 1 页 / 共 1 页</div>' : ""}
</body></html>`
}

/** 金额转人民币大写（打印凭据里的「大写金额」）。 */
function toChineseAmount(value: number): string {
    if (!Number.isFinite(value) || value === 0) return "零元整"
    const digits = "零壹贰叁肆伍陆柒捌玖"
    const units = ["", "拾", "佰", "仟"]
    const bigUnits = ["", "万", "亿", "兆"]
    const negative = value < 0
    const fixed = Math.abs(value).toFixed(2)
    const [intPart, decPart] = fixed.split(".")
    let result = ""
    let zeroPending = false
    const groups = intPart.replace(/\B(?=(\d{4})+(?!\d))/g, ",").split(",")
    groups.forEach((group, gi) => {
        const groupIndex = groups.length - 1 - gi
        let groupText = ""
        let groupZero = false
        for (let i = 0; i < group.length; i++) {
            const d = Number(group[i])
            const unitIndex = group.length - 1 - i
            if (d === 0) {
                groupZero = true
            } else {
                if ((zeroPending || groupZero) && result) groupText += "零"
                groupText += digits[d] + units[unitIndex]
                groupZero = false
                zeroPending = false
            }
        }
        if (groupText) {
            result += groupText + bigUnits[groupIndex]
        } else if (result) {
            zeroPending = true
        }
    })
    result = result || "零"
    const jiao = Number(decPart[0])
    const fen = Number(decPart[1])
    let decimal = ""
    if (jiao === 0 && fen === 0) decimal = "整"
    else {
        decimal = (jiao > 0 ? digits[jiao] + "角" : fen > 0 ? "零" : "") + (fen > 0 ? digits[fen] + "分" : "")
    }
    return `${negative ? "负" : ""}${result}元${decimal}`
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
    ["ops", "操作"],
]

/** 区间控件弹出的高级筛选：日期在面板里同步期间，其余字段与账页列头漏斗叠加生效。 */
type VoucherState = "all" | "yes" | "no"

interface AdvancedFilters {
    voucherNo: string
    category: string
    counterparty: string
    voucher: VoucherState
}

const EMPTY_ADVANCED: AdvancedFilters = {
    voucherNo: "",
    category: "",
    counterparty: "",
    voucher: "all",
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
}

const blankDraft = (): DraftValues => ({
    date: dayjs().format("YYYY-MM-DD"),
    summary: "",
    category: "",
    counterparty: "",
    income: "",
    expense: "",
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
    }
}

export function JournalLedgerPage({ journalType, seqPrefix }: { journalType: 1 | 2; seqPrefix: string }) {
    const { token, user } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    /** 表尾 / 打印标题用的账套名称。 */
    const accountSetName = accountSets.find((s) => s.id === accountSetId)?.name ?? ""
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
    const [hidden, setHidden] = useState<Set<string>>(() => new Set())
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(PAGE_SIZE_OPTIONS[0])
    const [selected, setSelected] = useState<string[]>([])

    const [editingId, setEditingId] = useState<string | null>(null)
    const [form, setForm] = useState<DraftValues>(blankDraft)
    const [saving, setSaving] = useState(false)

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

    const printKey = `kuaiji.funds.print.${accountSetId}`
    const [listPrintOpen, setListPrintOpen] = useState(false)
    const [receiptPrintOpen, setReceiptPrintOpen] = useState(false)
    const [listPrint, setListPrint] = useState<ListPrintSettings>(() => readPrintSetting(`${printKey}.list`, DEFAULT_LIST_PRINT))
    const [receiptPrint, setReceiptPrint] = useState<ReceiptPrintSettings>(() =>
        readPrintSetting(`${printKey}.receipt`, DEFAULT_RECEIPT_PRINT),
    )

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
                    const list = next.entries ?? []
                    setPeriod(
                        list.length
                            ? [toISODate(list[0].date), toISODate(list[list.length - 1].date)]
                            : [dayjs().startOf("month").format("YYYY-MM-DD"), dayjs().endOf("month").format("YYYY-MM-DD")],
                    )
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
        })()
    }, [token, accountSetId])

    useEffect(() => {
        setPage(1)
    }, [period, filters, pageSize])

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
        setAdvanced({
            voucherNo: advDraft.voucherNo.trim(),
            category: advDraft.category.trim(),
            counterparty: advDraft.counterparty.trim(),
            voucher: advDraft.voucher,
        })
        setPeriod(advDraft.period)
        setAdvOpen(false)
    }

    function resetAdvanced() {
        setAdvDraft({ ...EMPTY_ADVANCED, period: dataRange })
    }

    const advancedCount =
        Number(Boolean(advanced.voucherNo)) +
        Number(Boolean(advanced.category)) +
        Number(Boolean(advanced.counterparty)) +
        Number(advanced.voucher !== "all")

    const filtered = useMemo(() => {
        return entries.filter((e) => {
            const date = toISODate(e.date)
            if (period && (date < period[0] || date > period[1])) return false
            if (filters.date && !date.includes(filters.date)) return false
            if (filters.summary && !(e.summary ?? "").includes(filters.summary)) return false
            if (filters.category && !(e.category ?? "").includes(filters.category)) return false
            if (filters.counterparty && !(e.counterparty ?? "").includes(filters.counterparty)) return false
            if (filters.income && !fmtAmount(e.income).includes(filters.income)) return false
            if (filters.expense && !fmtAmount(e.expense).includes(filters.expense)) return false
            if (filters.balance && !fmtAmount(e.balance).includes(filters.balance)) return false
            if (filters.voucher && !(e.voucherNo ?? "").includes(filters.voucher)) return false
            if (filters.seq && !seqOf(e).includes(filters.seq)) return false
            if (advanced.voucherNo && !(e.voucherNo ?? "").includes(advanced.voucherNo)) return false
            if (advanced.category && !(e.category ?? "").includes(advanced.category)) return false
            if (advanced.counterparty && !(e.counterparty ?? "").includes(advanced.counterparty)) return false
            if (advanced.voucher === "yes" && !e.voucherId) return false
            if (advanced.voucher === "no" && e.voucherId) return false
            return true
        })
        // seqOf 只依赖 seqPrefix，随组件固定不变
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [entries, period, filters, advanced, seqPrefix])

    const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize))
    const current = Math.min(page, pageCount)
    const pageRows = filtered.slice((current - 1) * pageSize, current * pageSize)

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

    const categoryOptions = useMemo(() => {
        const group = (type: number, label: string) => {
            const options = categories
                .filter((c) => c.type === type && c.name)
                .map((c) => ({ value: c.name as string, label: c.name as string }))
            return options.length > 0 ? [{ label, options }] : []
        }
        return [...group(1, "收入类别"), ...group(2, "支出类别")]
    }, [categories])

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

    /** 打印列表：把当前筛选结果按设置输出为可打印账页。 */
    function printList() {
        if (!detail) return
        const accountName = `${detail.accountCode ?? ""} - ${detail.accountName ?? ""}`.replace(/^ - /, "")
        const periodText = period ? `${period[0]} 至 ${period[1]}` : "全部期间"
        const html = buildListPrintHtml(
            `${typeLabel}`,
            accountName,
            periodText,
            filtered,
            openingBalance,
            listPrint,
            {
                账套名称: accountSetName,
                账户名称: accountName,
                制表人: user?.displayName ?? "",
                审核人: "",
                记账人: user?.displayName ?? "",
                单位负责人: "",
            },
            seqPrefix,
        )
        if (!openPrintWindow(html)) {
            message.warning("浏览器拦截了新窗口，请允许弹出窗口后重试。")
        }
    }

    /** 打印凭据：为选中（或当前页）流水各出一张收/付款凭据。 */
    function printReceipt() {
        if (!detail) return
        const targets = selected.length > 0 ? filtered.filter((e) => selected.includes(e.id ?? "")) : pageRows
        if (targets.length === 0) {
            message.warning("当前没有可打印的流水。")
            return
        }
        const accountName = `${detail.accountCode ?? ""} - ${detail.accountName ?? ""}`.replace(/^ - /, "")
        const html = buildReceiptPrintHtml("记账凭据", accountName, targets, receiptPrint, "", "")
        if (!openPrintWindow(html)) {
            message.warning("浏览器拦截了新窗口，请允许弹出窗口后重试。")
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

    const textFilter = (key: FilterKey) => ({
        filterIcon: <FilterFilled style={{ color: filters[key] ? "#2f6bff" : "#c0c4cc", fontSize: 11 }} />,
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
        row.kind === "total" ? { background: "#fafafa" } : isFormRow(row) ? { background: "#f8fbff" } : undefined

    const allColumns: TableColumnsType<LedgerRow> = [
        {
            key: "date",
            title: "日期",
            width: 120,
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
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("summary"),
            render: (_, row) => {
                if (row.kind === "opening") return "初始化余额"
                if (row.kind === "total") return <span className="font-semibold">合计</span>
                if (isFormRow(row)) {
                    return (
                        <Input
                                                       value={form.summary}
                            placeholder="填写摘要"
                            style={{ borderColor: "#2f6bff" }}
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
            width: 128,
            ...textFilter("category"),
            render: (_, row) => {
                if (isFormRow(row)) {
                    return (
                        <Select
                                                       allowClear
                            showSearch
                            className="w-full"
                            placeholder="选择类别"
                            value={form.category || undefined}
                            options={categoryOptions}
                            onChange={(v) => patchForm({ category: v ?? "" })}
                        />
                    )
                }
                return row.kind === "entry" ? (row.entry?.category ?? "") : null
            },
        },
        {
            key: "counterparty",
            title: "往来单位",
            width: 128,
            ...textFilter("counterparty"),
            render: (_, row) => {
                if (isFormRow(row)) {
                    return (
                        <Input
                                                       value={form.counterparty}
                            placeholder="往来单位"
                            onChange={(e) => patchForm({ counterparty: e.target.value })}
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
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("voucher"),
            render: (_, row) =>
                row.kind === "entry" && row.entry?.voucherNo ? (
                    <Typography.Link
                        className="whitespace-nowrap"
                        onClick={() => navigate(`/${accountSetId}/vouchers/manage`)}
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
            onHeaderCell: () => ({ style: { whiteSpace: "nowrap" } }),
            ...textFilter("seq"),
            render: (_, row) => {
                if (isFormRow(row)) return <span className="whitespace-nowrap">{`${seqPrefix}-${form.date.replaceAll("-", "")}`}</span>
                return row.kind === "entry" && row.entry ? (
                    <span className="whitespace-nowrap">{seqOf(row.entry)}</span>
                ) : null
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
                        <Typography.Link onClick={() => window.print()}>打印</Typography.Link>
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
                        <div className="flex flex-col gap-1 rounded-xl border border-slate-200 bg-white p-3 shadow-lg">
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

    /* ---------- 渲染 ---------- */

    const from = filtered.length === 0 ? 0 : (current - 1) * pageSize + 1
    const to = Math.min(current * pageSize, filtered.length)

    /** 批量操作弹窗的文案：自由文本类（往来 / 项目 / 部门）共用一个输入框。 */
    const textBulk = bulkKind ? TEXT_BULK[bulkKind] : undefined
    const bulkTitle =
        bulkKind === "summary" ? "批量修改摘要" : bulkKind === "category" ? "指定收支类别" : textBulk ? `指定${textBulk.label}` : "关联凭证"

    const advRow = (label: string, control: ReactNode) => (
        <div className="flex items-center gap-3">
            <span className="w-20 shrink-0 text-right text-[13px] text-slate-600">{label}：</span>
            <div className="min-w-0 flex-1">{control}</div>
        </div>
    )

    const advancedPanel = (
        <div className="flex w-[500px] flex-col gap-3">
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
                "凭证字号",
                <Input
                    allowClear
                    value={advDraft.voucherNo}
                    placeholder="如 记-1"
                    onChange={(e) => setAdvDraft((prev) => ({ ...prev, voucherNo: e.target.value }))}
                />,
            )}
            {advRow(
                "收支类别",
                <Input
                    allowClear
                    value={advDraft.category}
                    placeholder="输入收支类别关键字"
                    onChange={(e) => setAdvDraft((prev) => ({ ...prev, category: e.target.value }))}
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
                <AutoComplete
                    className="w-full"
                    allowClear
                    value={advDraft.counterparty}
                    options={counterpartyOptions}
                    placeholder="请选择或输入"
                    onChange={(v) => setAdvDraft((prev) => ({ ...prev, counterparty: v ?? "" }))}
                />,
            )}
            <div className="mt-1 flex items-center gap-2 border-t border-slate-100 pt-3">
                <Button type="primary" onClick={applyAdvanced}>
                    确定
                </Button>
                <Button onClick={() => setAdvOpen(false)}>取消</Button>
                <Button onClick={resetAdvanced}>重置</Button>
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
                styles={{ content: { padding: 16 } }}
            >
                <Button
                    type="primary"
                    title="高级筛选"
                    style={{
                        background: "#2f6bff",
                        borderColor: "#2f6bff",
                        boxShadow: "0 1px 2px rgba(47,107,255,0.25)",
                    }}
                    className="flex items-center gap-[0.4em] hover:!bg-[#2458d6] hover:!border-[#2458d6]"
                >
                    <span className="tabular-nums">{period ? `${period[0]} 至 ${period[1]}` : "全部时间"}</span>
                    <span className="h-[1em] w-px bg-white/35" />
                    <FilterFilled />
                    {advancedCount > 0 && (
                        <span className="rounded-[2px] bg-white px-1 text-[0.75em] font-semibold leading-[1.4] text-[#2f6bff]">
                            {advancedCount}
                        </span>
                    )}
                </Button>
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
                <div className="flex flex-col gap-2 text-[13px] leading-6 text-slate-600">
                    <span>
                        在{platform}导出账单明细（Excel / CSV）后，按「日期、摘要、收入、支出」整理成日记账导入模板的表头，
                        再选择文件上传即可。
                    </span>
                    <span className="text-[12px] text-slate-400">
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
            <Button onClick={() => setCreateOpen(true)} disabled={!accountSetId}>
                新增日记账
            </Button>
            <Dropdown
                trigger={["click"]}
                menu={{
                    items: [
                        { key: "list", label: "打印列表" },
                        { key: "list-settings", label: "打印列表设置" },
                        { key: "receipt", label: "打印凭据" },
                        { key: "receipt-settings", label: "打印凭据设置" },
                    ],
                    onClick: ({ key }) => {
                        if (key === "list") printList()
                        else if (key === "list-settings") setListPrintOpen(true)
                        else if (key === "receipt") printReceipt()
                        else setReceiptPrintOpen(true)
                    },
                }}
            >
                <Button disabled={!detail}>
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
                <Button disabled={!journalId}>
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

            <Card
                styles={{
                    root: {
                        border: "1px solid #f1f5f9",
                        boxShadow: "0 1px 2px rgba(16,24,40,0.04)",
                    },
                }}
            >
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
                        <ConfigProvider
                            theme={{
                                token: { fontSize: 13 },
                                components: {
                                    Table: { headerBg: "#f8fafc", headerColor: "#475569", borderColor: "#eef2f7" },
                                },
                            }}
                        >
                            <Table<LedgerRow>
                                                               rowKey="key"
                                tableLayout="fixed"
                                columns={columns}
                                dataSource={rows}
                                pagination={false}
                                scroll={{ x: 1464 }}
                                rowClassName={(row) =>
                                    cn(
                                        row.kind === "total" && "bg-[#fafafa]",
                                        isFormRow(row) && "bg-[#f8fbff]",
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
                        </ConfigProvider>
                    </Spin>
                )}

                {(
                    <Flex justify="space-between" align="center" gap={16} wrap style={{ marginTop: 16 }}>
                        <Typography.Text type="secondary">
                            显示 {from} 到 {to}，共 {filtered.length} 条
                        </Typography.Text>
                        <Pagination
                            current={current}
                            pageSize={pageSize}
                            total={filtered.length}
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
                    <p className="ps-[96px] text-[12px] leading-5 text-slate-400">
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
                <p className="mb-4 text-[12.5px] text-slate-400">
                    已选 {selected.length} 笔流水；同一张凭证内相同科目、相同方向的行将自动汇总为一行。
                </p>
                <Radio.Group
                    className="flex flex-col gap-2"
                    value={mergeBy}
                    options={MERGE_OPTIONS}
                    onChange={(e) => setMergeBy(e.target.value)}
                />
                <div className="mt-4 flex flex-col gap-1">
                    <span className="text-[13px] font-medium text-slate-700">对方科目</span>
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
                    <span className="text-[12px] text-slate-400">未配置默认对方科目的流水需要在此指定统一科目。</span>
                </div>
            </Modal>

            {/* 打印列表设置 */}
            <Modal
                open={listPrintOpen}
                title="打印列表设置"
                width={520}
                onCancel={() => setListPrintOpen(false)}
                onOk={() => {
                    writePrintSetting(`${printKey}.list`, listPrint)
                    setListPrintOpen(false)
                    message.success("打印设置已保存。")
                }}
                okText="确定"
                cancelText="取消"
            >
                <div className="flex flex-col gap-4 py-1">
                    <div className="flex items-center gap-3">
                        <span className="w-[76px] shrink-0 text-right text-[13px] text-slate-600">打印方向：</span>
                        <Radio.Group
                            value={listPrint.orientation}
                            onChange={(e) => setListPrint({ ...listPrint, orientation: e.target.value })}
                            options={[
                                { value: "portrait", label: "纵向" },
                                { value: "landscape", label: "横向" },
                            ]}
                        />
                    </div>
                    <div className="flex items-start gap-3">
                        <span className="w-[76px] shrink-0 pt-1 text-right text-[13px] text-slate-600">页边距：</span>
                        <div className="grid flex-1 grid-cols-2 gap-2">
                            {(
                                [
                                    ["marginTop", "上"],
                                    ["marginBottom", "下"],
                                    ["marginLeft", "左"],
                                    ["marginRight", "右"],
                                ] as const
                            ).map(([key, label]) => (
                                <label key={key} className="flex items-center gap-2 text-[13px]">
                                    <span className="w-4 text-slate-600">{label}</span>
                                    <InputNumber
                                        className="flex-1"
                                        min={0}
                                        max={100}
                                        value={listPrint[key]}
                                        onChange={(v: number | null) => setListPrint({ ...listPrint, [key]: Number(v ?? 0) })}
                                        addonAfter="mm"
                                    />
                                </label>
                            ))}
                        </div>
                    </div>
                    <div className="flex items-start gap-3">
                        <span className="w-[76px] shrink-0 text-right text-[13px] text-slate-600">其他选项：</span>
                        <div className="flex flex-1 flex-col gap-1">
                            <Checkbox
                                checked={listPrint.showPrintDate}
                                onChange={(e) => setListPrint({ ...listPrint, showPrintDate: e.target.checked })}
                            >
                                显示打印日期
                            </Checkbox>
                            <Checkbox
                                checked={listPrint.showPageNo}
                                onChange={(e) => setListPrint({ ...listPrint, showPageNo: e.target.checked })}
                            >
                                显示页码
                            </Checkbox>
                        </div>
                    </div>
                    <div className="flex items-start gap-3">
                        <span className="w-[76px] shrink-0 pt-1 text-right text-[13px] text-slate-600">表尾字段：</span>
                        <Checkbox.Group
                            className="flex flex-1 flex-col gap-1"
                            value={listPrint.footerFields}
                            onChange={(v) => setListPrint({ ...listPrint, footerFields: v as string[] })}
                        >
                            {FOOTER_FIELD_OPTIONS.map((f) => (
                                <Checkbox key={f} value={f}>
                                    {f}
                                </Checkbox>
                            ))}
                        </Checkbox.Group>
                    </div>
                    <p className="ps-[88px] text-[12px] leading-5 text-slate-400">
                        设置按账套保存在本机，打印列表时生效；表尾字段取账套与当前登录用户信息。
                    </p>
                </div>
            </Modal>

            {/* 打印凭据设置 */}
            <Modal
                open={receiptPrintOpen}
                title="打印凭据设置"
                width={420}
                onCancel={() => setReceiptPrintOpen(false)}
                onOk={() => {
                    writePrintSetting(`${printKey}.receipt`, receiptPrint)
                    setReceiptPrintOpen(false)
                    message.success("打印设置已保存。")
                }}
                okText="确定"
                cancelText="取消"
            >
                <div className="flex flex-col gap-3 py-1">
                    <span className="text-[13px] text-slate-600">其他选项：</span>
                    <div className="flex flex-col gap-1 ps-3">
                        {(
                            [
                                ["showDept", "打印部门"],
                                ["showProject", "打印项目"],
                                ["showDivider", "打印分割线"],
                                ["showPrintDate", "显示打印日期"],
                                ["showPageNo", "显示页码"],
                            ] as const
                        ).map(([key, label]) => (
                            <Checkbox
                                key={key}
                                checked={receiptPrint[key]}
                                onChange={(e) => setReceiptPrint({ ...receiptPrint, [key]: e.target.checked })}
                                disabled={key === "showDept" || key === "showProject"}
                            >
                                {label}
                                {(key === "showDept" || key === "showProject") && (
                                    <span className="text-[12px] text-slate-400">（未启用部门 / 项目核算）</span>
                                )}
                            </Checkbox>
                        ))}
                    </div>
                    <p className="text-[12px] leading-5 text-slate-400">
                        打印凭据会为每笔勾选的流水输出一张收款 / 付款凭据；未勾选时打印当前页。
                    </p>
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
                    <p className="text-[12.5px] text-slate-400">已选 {selected.length} 笔流水</p>
                    {(bulkKind === "summary" || textBulk) && (
                        <Input
                            value={bulkText}
                            placeholder={bulkKind === "summary" ? "请输入新的摘要" : `请输入${textBulk?.label}，留空表示清除`}
                            onChange={(e) => setBulkText(e.target.value)}
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
                            <span className="text-[12px] text-slate-400">
                                候选为当前账套的全部凭证，可按凭证字号或日期检索；关联后行内「关联凭证」列会显示凭证号。
                            </span>
                        </>
                    )}
                </div>
            </Modal>
        </div>
    )
}
