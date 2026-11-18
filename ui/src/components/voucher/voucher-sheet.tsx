import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import type { KeyboardEvent as ReactKeyboardEvent } from "react"
import { App, Button, DatePicker, Dropdown, Empty, Input, InputNumber, Modal, Popconfirm, Select, Switch, Table, Tooltip } from "antd"
import type { InputRef, MenuProps } from "antd"
import {
    ArrowLeftOutlined,
    ArrowRightOutlined,
    CloseOutlined,
    ControlOutlined,
    CopyOutlined,
    DeleteOutlined,
    DownloadOutlined,
    DownOutlined,
    EditOutlined,
    FileTextOutlined,
    FullscreenExitOutlined,
    FullscreenOutlined,
    PlusCircleOutlined,
    PrinterOutlined,
    QuestionCircleOutlined,
    UploadOutlined,
    ZoomInOutlined,
    ZoomOutOutlined,
} from "@ant-design/icons"
import dayjs from "dayjs"
import { useNavigate } from "react-router"

import { useAuth } from "@/auth/AuthContext"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useCurrentAccountSet, useDefaultAccountingPeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet"
import {
    AccountsApi,
    ReportsApi,
    VouchersApi,
    VoucherTemplatesApi,
    type AccountDto,
    type VoucherAttachmentDto,
    type VoucherDetailDto,
    type VoucherEntryDto,
    type VoucherListItemDto,
    type VoucherTemplateDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { amountToChineseUppercase } from "@/lib/amount-uppercase"
import { toISODate, fmtAmount } from "@/lib/format"
import { setPath } from "@/accountSets/url-id"
import { cn } from "@/lib/utils"
import { PrintVoucherSheet } from "@/pages/closing/print-sheet"
import { AmountCells, AmountHeaderCell } from "@/components/voucher/amount-grid"
import { VOUCHER_AMOUNT_WIDTH, VOUCHER_HEAD_H, VOUCHER_ROW_H } from "@/components/voucher/voucher-paper"
import { AssistItemSelect } from "@/components/assist/assist-item-select"
import { assistLabel } from "@/components/assist/constants"
import { useAccountAssistConfigs, useAssistItemsMany } from "@/components/assist/assist-store"
import { useKeepAliveActive } from "@/components/layout/dashboard/route-tabs-context"

/* ================= 常量 ================= */

/** 金额栏与纸样尺寸取自只读纸样组件，避免录入与预览的版式各自漂移。 */
const GROUP_W = VOUCHER_AMOUNT_WIDTH

/** 纸样尺寸：表头 60，分录行与合计行 70，与凭证纸面比例一致。 */
const HEAD_H = VOUCHER_HEAD_H
const ROW_H = VOUCHER_ROW_H

/** 纸样左右留白：行操作浮标悬停时落在这段槽位里，不挤占表体列宽。 */
const GUTTER = 30

/** 纸样表头标题：整格居中，与金额列的两段式表头等高。 */
function SheetHeadTitle({ text }: { text: string }) {
    return (
        <div className="flex items-center justify-center" style={{ height: HEAD_H }}>
            {text}
        </div>
    )
}

/** 分录行：金额以字符串保存，便于「12.」这类半成品输入；assists 为「辅助核算类别 → 档案 id」。 */
interface EntryRow {
    key: number
    summary: string
    accountId: string
    debit: string
    credit: string
    assists: Record<number, string>
}

const blankRow = (key: number): EntryRow => ({ key, summary: "", accountId: "", debit: "", credit: "", assists: {} })
const blankRows = () => [0, 1, 2, 3, 4].map(blankRow)

/** 金额文本 → 数值（空串 / 非法输入按 0）。 */
function toAmount(text: string): number {
    const n = Number(text)
    return Number.isFinite(n) && text.trim() !== "" ? n : 0
}

/** 分录已挂的辅助核算（类别 → 档案 id）；同类型多档案时取首个。 */
function assistsOf(entry: VoucherEntryDto): Record<number, string> {
    const assists: Record<number, string> = {}
    for (const a of entry.assists ?? []) {
        if (a.type != null && a.assistItemId) assists[a.type] = a.assistItemId
    }
    return assists
}

/** 制单默认日期：今天，若不在当前会计期间内则归到期间边界。 */
function defaultVoucherDate(period: AccountingPeriod): string {
    const today = toISODate(new Date())
    if (today < period.startDate) return period.startDate
    if (today > period.endDate) return period.endDate
    return today
}

/** 账套 voucherWords（逗号分隔）→ 去空去重后的凭证字列表。 */
function parseVoucherWords(raw?: string): string[] {
    const list = (raw ?? "")
        .split(",")
        .map((s) => s.trim())
        .filter(Boolean)
    return list.length > 0 ? [...new Set(list)] : ["记"]
}

/** 快捷键说明：[左功能, 左按键, 右功能, 右按键]。 */
const SHORTCUTS: Array<[string, string, string, string]> = [
    ["保存入账", "Ctrl + S", "上一行", "↑"],
    ["入账并新增", "F12", "前一格", "←"],
    ["新增", "Alt + N", "下一行", "↓"],
    ["前一张", "PgUp", "后一格", "→"],
    ["后一张", "PgDn", "余额反算", "F9"],
    ["跳转单元格或换行", "Enter", "新增一行", "Ctrl + '+'"],
    ["自动平衡借贷方金额", "双击鼠标 或 =", "删除一行", "Ctrl + '-'"],
    ["借贷方金额互换", "空格键", "删除当前凭证", "Ctrl + Delete"],
    ["从凭证模板引入", "Ctrl + Q", "另存为凭证模板", "Ctrl + E"],
    ["跳转至附单据数栏", "F4", "复制上一格", "F3"],
    ["跳转至记账日期", "Ctrl + L", "撤销", "Ctrl + Z"],
    ["打印凭证", "Ctrl + P", "恢复", "Ctrl + Y"],
]

/** 科目分类（与后端 AccountCategory 枚举一致）。 */
const ACCOUNT_CATEGORIES = [
    { key: 0, label: "全部" },
    { key: 1, label: "资产" },
    { key: 2, label: "负债" },
    { key: 3, label: "权益" },
    { key: 4, label: "成本" },
    { key: 5, label: "损益" },
]

/** 凭证模板类型（与后端 VoucherTemplateType 枚举一致）。 */
const TEMPLATE_TYPES = [
    { value: 1, label: "日常支出" },
    { value: 2, label: "采购" },
    { value: 3, label: "销售" },
    { value: 4, label: "其他" },
]

function downloadShortcuts() {
    const escape = (text: string) => `"${text.replace(/"/g, '""')}"`
    const csv = ["功能,快捷键,功能,快捷键", ...SHORTCUTS.map((r) => r.map(escape).join(","))].join("\r\n")
    const url = URL.createObjectURL(new Blob([`\uFEFF${csv}`], { type: "text/csv;charset=utf-8" }))
    const link = document.createElement("a")
    link.href = url
    link.download = "凭证快捷键.csv"
    link.click()
    URL.revokeObjectURL(url)
}

/** 损益类科目中，收入类（科目名称含"收入"）正常应记贷方；费用类正常应记借方。 */
function isRevenueAccount(account: AccountDto | undefined): boolean {
    if (!account?.code || !account?.name) return false
    return (account.code.startsWith("5") || account.code.startsWith("6")) && account.name.includes("收入")
}

function isExpenseAccount(account: AccountDto | undefined): boolean {
    if (!account?.code || !account?.name) return false
    return (account.code.startsWith("5") || account.code.startsWith("6")) && !account.name.includes("收入")
}

/** 检测凭证分录中损益类科目方向不规范的条目，返回行号（从 1 开始）数组。 */
function detectDirectionIssues(entries: EntryRow[], accountMap: Map<string, AccountDto>): number[] {
    const issues: number[] = []
    entries.forEach((entry, i) => {
        const account = accountMap.get(entry.accountId)
        const debit = toAmount(entry.debit)
        const credit = toAmount(entry.credit)
        if (isRevenueAccount(account) && debit > 0) {
            issues.push(i + 1)
        } else if (isExpenseAccount(account) && credit > 0) {
            issues.push(i + 1)
        }
    })
    return issues
}

/**
 * 科目选择器：可搜索（编码 / 名称 / 拼音首字母），带分类页签与当前余额展示。
 * 用 popupRender 在选项列表上方插入分类栏，避免每个单元格各写一套下拉。
 */
function AccountSelect({
    value,
    accounts,
    balances,
    accountSetId,
    extraOptions,
    labelSuffix,
    onChange,
    onFocus,
    onOpenChange,
}: {
    value: string
    accounts: AccountDto[]
    balances: Record<string, number>
    accountSetId: string
    /** 历史凭证里已停用 / 非末级的科目：补禁用项保住回显。 */
    extraOptions?: { value: string; label: string; disabled: boolean }[]
    /** 科目名后追加的辅助核算摘要（如 `_2华东客户甲`），。 */
    labelSuffix?: string
    onChange: (value: string) => void
    onFocus?: () => void
    onOpenChange?: (open: boolean) => void
}) {
    const navigate = useNavigate()
    const [category, setCategory] = useState(0)

    const options = useMemo(
        () => [
            ...accounts
                .filter((a) => !category || a.category === category)
                .map((a) => ({
                    value: a.id ?? "",
                    label: `${a.code ?? ""} ${a.name ?? ""}`,
                    search: `${a.code ?? ""} ${a.name ?? ""} ${a.mnemonic ?? ""}`.toLowerCase(),
                    balance: balances[a.code ?? ""] ?? 0,
                })),
            ...(extraOptions ?? []).map((o) => ({ ...o, search: o.label.toLowerCase(), balance: 0 })),
        ],
        [accounts, category, balances, extraOptions],
    )

    const labelOf = (id: string) => {
        const account = accounts.find((a) => a.id === id)
        if (account) return `${account.code ?? ""} ${account.name ?? ""}`
        return extraOptions?.find((o) => o.value === id)?.label ?? ""
    }

    return (
        <Select
            variant="borderless"
            className="pk-account-select w-full"
            value={value || undefined}
            options={options}
            placeholder=""
            showSearch
            optionFilterProp="search"
            suffixIcon={null}
            popupMatchSelectWidth={420}
            onFocus={onFocus}
            onOpenChange={onOpenChange}
            onChange={(v) => onChange(v ?? "")}
            labelRender={(props) => {
                const base = labelOf(String(props.value ?? ""))
                return labelSuffix ? `${base}_${labelSuffix}` : base
            }}
            optionRender={(option) => (
                <div className="flex items-center justify-between gap-4">
                    <span>{option.data.label}</span>
                    {Math.abs(option.data.balance) >= 0.005 && (
                        <span className="text-[12px] tabular-nums text-(--muted-foreground)">
                            {option.data.balance.toFixed(2)}
                        </span>
                    )}
                </div>
            )}
            popupRender={(menu) => (
                <div>
                    <div
                        className="flex items-center gap-4 border-b border-(--border) px-3 py-2 text-[13px]"
                        onMouseDown={(e) => e.preventDefault()}
                    >
                        {ACCOUNT_CATEGORIES.map((c) => (
                            <button
                                key={c.key}
                                type="button"
                                className={cn(
                                    "border-b-2 border-transparent pb-0.5",
                                    category === c.key
                                        ? "border-(--brand) font-medium text-(--brand)"
                                        : "text-(--foreground) hover:text-(--brand)",
                                )}
                                onClick={() => setCategory(c.key)}
                            >
                                {c.label}
                            </button>
                        ))}
                    </div>
                    {menu}
                    <div className="border-t border-(--border) py-1 text-center" onMouseDown={(e) => e.preventDefault()}>
                        <Button
                            type="link"
                            onClick={() => navigate(setPath(accountSetId, "/settings/accounts"))}
                        >
                            + 点击添加
                        </Button>
                    </div>
                </div>
            )}
        />
    )
}

/* ================= 组件 ================= */

/**
 * 纸样凭证录入组件：新增凭证页与凭证管理的新增 / 编辑抽屉共用。
 * 传 voucherId 载入已存凭证进入编辑态；embedded 为抽屉嵌入模式，
 * 隐藏上一张 / 下一张等页面级导航，保存成功后回调 onSaved 交由外层关闭抽屉。
 */
export default function VoucherSheet({
    voucherId,
    initialDate,
    initialWord,
    embedded = false,
    onSaved,
    onCancel,
}: {
    /** 编辑模式：挂载时载入该凭证。 */
    voucherId?: string | null
    /** 新增模式下预设凭证日期（列表「插入」用）。 */
    initialDate?: string
    /** 新增模式下预设凭证字。 */
    initialWord?: string
    /** 抽屉嵌入模式。 */
    embedded?: boolean
    /** 保存成功回调（抽屉用来关闭并刷新列表）。 */
    onSaved?: () => void
    onCancel?: () => void
}) {
    const { token, user } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const accountSet = useCurrentAccountSet()
    const period = useDefaultAccountingPeriod()
    /** 切走页签后本页仍挂载（keep-alive），全局快捷键必须停用，避免在别的页面误保存/误删。 */
    const keepAliveActive = useKeepAliveActive()
    const words = useMemo(() => parseVoucherWords(accountSet?.voucherWords), [accountSet])
    const { message, modal } = App.useApp()

    const [accounts, setAccounts] = useState<AccountDto[]>([])
    const [siblings, setSiblings] = useState<VoucherListItemDto[]>([])
    const [allVouchers, setAllVouchers] = useState<VoucherListItemDto[]>([])
    /** 科目编码 → 本期期末余额（借 - 贷），用于下拉展示与 F9 反算。 */
    const [balances, setBalances] = useState<Record<string, number>>({})
    const [word, setWord] = useState("")
    const [number, setNumber] = useState<number | null>(null)
    const [date, setDate] = useState("")
    const [attachmentCount, setAttachmentCount] = useState("")
    const [rows, setRows] = useState<EntryRow[]>(blankRows)
    const [loading, setLoading] = useState(false)
    const [metaError, setMetaError] = useState<string | null>(null)
    const [saving, setSaving] = useState(false)

    /** 当前表单对应的已存凭证；为空表示新增。上一页 / 下一页 载入后进入编辑态。 */
    const [loadedId, setLoadedId] = useState<string | null>(null)
    /** 已载入凭证的状态：正常（3）的凭证保存前先反过账，更新后后端重新入账。 */
    const [loadedStatus, setLoadedStatus] = useState<number | null>(null)
    /** 历史凭证里不在当前末级候选中的科目（停用 / 非末级），补禁用项保住回显。 */
    const [staleAccounts, setStaleAccounts] = useState<{ value: string; label: string; disabled: boolean }[]>([])
    const [printVoucherId, setPrintVoucherId] = useState<string | null>(null)
    const [activeRow, setActiveRow] = useState(0)
    /** 当前聚焦单元格的行 key 与列，供 F3 复制上一格 / 空格互换等使用。 */
    const [focusInfo, setFocusInfo] = useState<{ rowKey: number; col: string } | null>(null)
    const [zoom, setZoom] = useState(false)
    const [fullscreen, setFullscreen] = useState(false)
    const [shortcutOpen, setShortcutOpen] = useState(false)
    /** 凭证备注：与凭证头一起保存。 */
    const [remark, setRemark] = useState("")
    /** 备注的展开编辑态（默认收起，点击「备注」展开输入框）。 */
    const [remarkOpen, setRemarkOpen] = useState(false)
    /** 附件弹窗与当前凭证的附件列表。 */
    const [attachOpen, setAttachOpen] = useState(false)
    const [attachments, setAttachments] = useState<VoucherAttachmentDto[]>([])
    const [attachUploading, setAttachUploading] = useState(false)
    /** 摘要历史：账套内已用过的摘要，聚焦摘要格时作为候选。 */
    const [summaryHistory, setSummaryHistory] = useState<string[]>([])
    const [summaryPickRow, setSummaryPickRow] = useState<number | null>(null)
    /** 科目格下方辅助核算浮层的行 key；为空表示收起。 */
    const [assistPanelRow, setAssistPanelRow] = useState<number | null>(null)
    /** 录入选项（本机偏好，存 localStorage）。 */
    const [optionsOpen, setOptionsOpen] = useState(false)
    const [optFillGap, setOptFillGap] = useState(() => localStorage.getItem("yuncai.voucher.fillGap") === "1")
    const [optFollowDirection, setOptFollowDirection] = useState(() => localStorage.getItem("yuncai.voucher.followDirection") !== "0")
    /** 损益方向提示：issues 为不规范行号，pending 记下被拦下的保存动作。 */
    const [directionModal, setDirectionModal] = useState<{ issues: number[]; keepEditing: boolean } | null>(null)

    /* 模板 / 暂存 */
    const [templates, setTemplates] = useState<VoucherTemplateDto[]>([])
    const [templateSaveOpen, setTemplateSaveOpen] = useState(false)
    const [templateName, setTemplateName] = useState("")
    const [templateType, setTemplateType] = useState(1)
    const [templateLoadOpen, setTemplateLoadOpen] = useState(false)
    const [templateId, setTemplateId] = useState<string>("")
    const [draftOpen, setDraftOpen] = useState(false)
    const [draftId, setDraftId] = useState<string>("")

    const keySeq = useRef(4)
    const nextKey = useCallback(() => ++keySeq.current, [])
    const sheetRef = useRef<HTMLDivElement>(null)
    const gridRef = useRef<HTMLDivElement>(null)
    const dateRef = useRef<HTMLDivElement>(null)
    const attachmentRef = useRef<InputRef>(null)

    /* 撤销 / 恢复：每次改分录前快照，Ctrl+Z / Ctrl+Y 回放。 */
    const rowsRef = useRef(rows)
    rowsRef.current = rows
    const historyRef = useRef<{ past: EntryRow[][]; future: EntryRow[][] }>({ past: [], future: [] })
    const cloneRows = (list: EntryRow[]) => list.map((r) => ({ ...r }))

    const commitRows = (updater: (prev: EntryRow[]) => EntryRow[]) => {
        const prev = rowsRef.current
        const next = updater(prev)
        if (next === prev) return
        const history = historyRef.current
        history.past.push(cloneRows(prev))
        if (history.past.length > 100) history.past.shift()
        history.future = []
        rowsRef.current = next
        setRows(next)
    }

    const undo = () => {
        const history = historyRef.current
        const prev = history.past.pop()
        if (!prev) return
        history.future.push(cloneRows(rowsRef.current))
        rowsRef.current = prev
        setRows(prev)
    }

    const redo = () => {
        const history = historyRef.current
        const next = history.future.pop()
        if (!next) return
        history.past.push(cloneRows(rowsRef.current))
        rowsRef.current = next
        setRows(next)
    }

    const clearHistory = useCallback(() => {
        historyRef.current = { past: [], future: [] }
    }, [])

    useEffect(() => {
        setWord((prev) => {
            if (prev && words.includes(prev)) return prev
            if (initialWord && words.includes(initialWord)) return initialWord
            return words[0]
        })
    }, [words, initialWord])

    useEffect(() => {
        setDate(initialDate ?? defaultVoucherDate(period))
    }, [period, initialDate])

    // 加载明细科目与全量同类凭证（推下一个凭证号 + 上下条导航）。
    const reloadMeta = useCallback(async () => {
        if (!token || !accountSetId || !word) return
        setLoading(true)
        setMetaError(null)
        try {
            const [leafAccounts, allVouchers, trialBalance] = await Promise.all([
                new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true }),
                // 后端凭证号查重是全账套范围（ExistsByWordAndNumberAsync 不区分期间），
                // 这里必须取全量再取最大号，否则会拿当期号撞上年度的同号凭证。
                new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersGet({ accountSetId }),
                // 科目余额仅用于下拉展示与 F9 反算，取不到不影响录入。
                new ReportsApi(apiConfig(token))
                    .accountsetsAccountSetIdReportsTrialBalanceGet({
                        accountSetId,
                        startDate: new Date(period.startDate),
                        endDate: new Date(period.endDate),
                    })
                    .catch(() => null),
            ])
            setAccounts(leafAccounts ?? [])
            setAllVouchers(allVouchers ?? [])
            const nextBalances: Record<string, number> = {}
            for (const row of trialBalance?.rows ?? []) {
                if (row.code) nextBalances[row.code] = (row.endingDebit ?? 0) - (row.endingCredit ?? 0)
            }
            setBalances(nextBalances)
            const sameWord = (allVouchers ?? [])
                .filter((v) => (v.word ?? "") === word)
                .sort((a, b) => (a.number ?? 0) - (b.number ?? 0))
            // 上一页 / 下一页 只在当前期间内导航。
            setSiblings(
                sameWord.filter((v) => {
                    const day = toISODate(v.date)
                    return day >= period.startDate && day <= period.endDate
                }),
            )
            setNumber((prev) => {
                if (loadedId) return sameWord.find((v) => v.id === loadedId)?.number ?? prev
                const max = sameWord.reduce((m, v) => Math.max(m, v.number ?? 0), 0)
                return max + 1
            })
        } catch (e) {
            const msg = await apiErrorMessage(e, "加载科目或凭证失败")
            setMetaError(msg)
        } finally {
            setLoading(false)
        }
    }, [token, accountSetId, word, period, loadedId])

    useEffect(() => {
        void reloadMeta()
    }, [reloadMeta])

    const totals = useMemo(() => {
        const debit = rows.reduce((s, r) => s + toAmount(r.debit), 0)
        const credit = rows.reduce((s, r) => s + toAmount(r.credit), 0)
        // 红字更正凭证用负数金额，借贷合计可能都是 0；判平衡看「录了金额且借贷相等」，不能看合计是否大于 0。
        const hasAmount = rows.some((r) => toAmount(r.debit) !== 0 || toAmount(r.credit) !== 0)
        return { debit, credit, balanced: hasAmount && Math.abs(debit - credit) < 0.005 }
    }, [rows])

    const patchRow = (key: number, patch: Partial<EntryRow>) =>
        commitRows((prev) => prev.map((r) => (r.key === key ? { ...r, ...patch } : r)))

    const accountsById = useMemo(() => new Map(accounts.map((a) => [a.id ?? "", a])), [accounts])

    // 候选加载前暂存的回显项可能与其重合，按当前候选过滤，避免下拉里出现重复选项。
    const extraAccountOptions = useMemo(
        () => staleAccounts.filter((o) => !accountsById.has(o.value)),
        [staleAccounts, accountsById],
    )

    // 分录所选科目的辅助核算维度：只为实际用到的科目发请求（模块级缓存去重）。
    const entryAccountIds = useMemo(() => rows.map((r) => r.accountId), [rows])
    const { configs: assistConfigs } = useAccountAssistConfigs(accountSetId, entryAccountIds, token)
    // 按类别枚举顺序展示（客户→供应商→职员→部门→项目→存货→现金流）。
    const assistTypesOf = (accountId: string) =>
        [...(assistConfigs[accountId] ?? [])].sort((a, b) => (a.type ?? 0) - (b.type ?? 0))

    // 辅助核算摘要要显示「编码+名称」，分录里只有档案 id，按类别把档案取回来查。
    const assistTypeList = useMemo(() => {
        const types = new Set<number>()
        for (const list of Object.values(assistConfigs)) {
            for (const cfg of list) if (cfg.type != null) types.add(cfg.type)
        }
        return [...types]
    }, [assistConfigs])
    const assistItemsByType = useAssistItemsMany(accountSetId, assistTypeList, token)
    const assistTextOf = (row: EntryRow) =>
        assistTypesOf(row.accountId)
            .map((cfg) => {
                const id = row.assists[cfg.type as number]
                const item = id ? assistItemsByType[cfg.type as number]?.find((i) => i.id === id) : undefined
                return item ? `${item.code ?? ""}${item.name ?? ""}` : ""
            })
            .filter(Boolean)
            .join("_")

    // 辅助核算浮层：点到科目格 / 下拉之外即收起；下拉选项挂在 body 上，点选档案不算离开。
    useEffect(() => {
        if (assistPanelRow == null) return
        const onDown = (e: MouseEvent) => {
            const target = e.target as HTMLElement
            if (target.closest(`[data-assist-host="${assistPanelRow}"]`) || target.closest(".ant-select-dropdown")) return
            setAssistPanelRow(null)
        }
        const onKey = (e: KeyboardEvent) => {
            if (e.key === "Escape") setAssistPanelRow(null)
        }
        document.addEventListener("mousedown", onDown, true)
        window.addEventListener("keydown", onKey)
        return () => {
            document.removeEventListener("mousedown", onDown, true)
            window.removeEventListener("keydown", onKey)
        }
    }, [assistPanelRow])

    /** F9 余额反算：按当前行科目的本期期末余额（借 - 贷）回填借方或贷方。 */
    const fillFromBalance = () => {
        const row = focusInfo?.rowKey != null ? rows.find((r) => r.key === focusInfo.rowKey) : rows[activeRow]
        if (!row) return
        const account = accountsById.get(row.accountId)
        if (!account?.code) {
            message.warning("请先在当前行选择科目，再用 F9 反算余额")
            return
        }
        if (toAmount(row.debit) || toAmount(row.credit)) {
            message.info("当前行已填金额，F9 只填空白行")
            return
        }
        const balance = balances[account.code] ?? 0
        if (Math.abs(balance) < 0.005) {
            message.info(`科目 ${account.code} ${account.name} 期末余额为 0`)
            return
        }
        const amount = Math.abs(balance).toFixed(2)
        // 余额为正记借方、为负记贷方；沿用「按科目方向入栏」选项时以科目方向为准。
        const debitSide = optFollowDirection
            ? (account.direction ?? 1) === 1
            : balance > 0
        patchRow(row.key, debitSide ? { debit: amount } : { credit: amount })
        message.success(`已按 ${account.code} ${account.name} 期末余额反算`)
    }

    // 全局快捷键块只注册一次（避免逐键重复卸载/挂载），其闭包读不到随载入变化的
    // 科目余额，所以 F9 单独用带依赖的监听器。
    useEffect(() => {
        if (!keepAliveActive) return
        const onKey = (e: KeyboardEvent) => {
            if (e.key !== "F9") return
            e.preventDefault()
            fillFromBalance()
        }
        window.addEventListener("keydown", onKey)
        return () => window.removeEventListener("keydown", onKey)
    })

    /* ---------- 行操作 ---------- */

    /** 在当前行下方插入空行。 */
    const insertBelow = (index: number) =>
        commitRows((prev) => {
            const next = [...prev]
            next.splice(index + 1, 0, blankRow(nextKey()))
            return next
        })

    /** 复制当前行（同时带入已填科目）。 */
    const duplicateRow = (index: number) =>
        commitRows((prev) => {
            const next = [...prev]
            next.splice(index + 1, 0, { ...prev[index], key: nextKey() })
            return next
        })

    const removeRow = (index: number) =>
        commitRows((prev) => (prev.length <= 1 ? prev : prev.filter((_, i) => i !== index)))

    /** 借贷方金额互换（空格键）。 */
    const swapRow = (key: number) =>
        commitRows((prev) =>
            prev.map((r) => (r.key === key ? { ...r, debit: r.credit, credit: r.debit } : r)),
        )

    /**
     * 自动平衡（= / 双击金额格）：把「对方合计 - 本方其余合计」填进当前金额格，
     * 例：已录贷方 1000，在借方第一行按 = 即填 1000。
     */
    const autoBalance = (key: number, side: "debit" | "credit") => {
        const list = rowsRef.current
        const other = side === "debit" ? "credit" : "debit"
        const otherTotal = list.reduce((sum, r) => sum + toAmount(r[other]), 0)
        const ownTotal = list.reduce((sum, r) => sum + (r.key === key ? 0 : toAmount(r[side])), 0)
        const target = Math.round((otherTotal - ownTotal) * 100) / 100
        if (otherTotal === 0 || target <= 0) {
            message.info("对方金额为 0，暂无可平衡的差额")
            return
        }
        commitRows((prev) => prev.map((r) => (r.key === key ? { ...r, [side]: target.toFixed(2) } : r)))
    }

    /** 复制上一格（F3）：同列上一行的内容；科目列复制科目。 */
    const copyAbove = () => {
        if (!focusInfo) return
        const list = rowsRef.current
        const index = list.findIndex((r) => r.key === focusInfo.rowKey)
        if (index <= 0) return
        const above = list[index - 1]
        const field = focusInfo.col as keyof EntryRow
        if (field === "key") return
        commitRows((prev) =>
            prev.map((r) => (r.key === focusInfo.rowKey ? { ...r, [field]: above[field] } : r)),
        )
    }

    /** 在聚焦行下方插入一列空行（Ctrl + '+'）。 */
    const insertAtFocus = () => {
        if (!focusInfo) return
        const index = rowsRef.current.findIndex((r) => r.key === focusInfo.rowKey)
        if (index >= 0) insertBelow(index)
    }

    /** 删除聚焦行（Ctrl + '-'）。 */
    const removeAtFocus = () => {
        if (!focusInfo) return
        const index = rowsRef.current.findIndex((r) => r.key === focusInfo.rowKey)
        if (index >= 0) removeRow(index)
    }

    /* ---------- 键盘导航 ---------- */

    /** 按 DOM 顺序取所有 data-nav 单元格。 */
    const navCells = () =>
        Array.from(gridRef.current?.querySelectorAll<HTMLElement>("[data-nav]") ?? [])

    const focusCell = (el: HTMLElement) => {
        if (el.tagName === "INPUT") el.focus()
        else el.querySelector<HTMLInputElement>("input")?.focus()
    }

    const moveFocus = (from: HTMLElement, dir: 1 | -1) => {
        const cells = navCells()
        const index = cells.indexOf(from)
        const target = cells[index + dir]
        if (target) focusCell(target)
    }

    /** 上下移动：同一列的上 / 下一条分录。 */
    const moveRowFocus = (from: HTMLElement, dir: 1 | -1) => {
        const cell = from.closest<HTMLElement>("[data-col]")
        if (!cell?.dataset.col) return
        const tr = from.closest("tr")
        const nextTr = dir > 0 ? tr?.nextElementSibling : tr?.previousElementSibling
        const target = nextTr?.querySelector<HTMLElement>(`[data-col="${cell.dataset.col}"]`)
        if (target) focusCell(target)
    }

    const caretAtStart = (el: HTMLElement) =>
        el instanceof HTMLInputElement && (el.selectionStart ?? 0) === 0

    const caretAtEnd = (el: HTMLElement) =>
        el instanceof HTMLInputElement && (el.selectionStart ?? 0) === el.value.length

    const onCellKeyDown = (e: ReactKeyboardEvent<HTMLElement>) => {
        const el = e.currentTarget
        const col = el.dataset.col ?? ""
        // 科目下拉展开时方向键 / 回车都交给下拉自己处理，不抢焦点。
        if (el.querySelector(".ant-select-open")) return
        if (e.key === "Enter") {
            e.preventDefault()
            moveFocus(el, e.shiftKey ? -1 : 1)
            return
        }
        if (col !== "account" && e.key === "ArrowUp") {
            e.preventDefault()
            moveRowFocus(el, -1)
            return
        }
        if (col !== "account" && e.key === "ArrowDown") {
            e.preventDefault()
            moveRowFocus(el, 1)
            return
        }
        if (e.key === "ArrowLeft" && (col === "debit" || col === "credit" || caretAtStart(el))) {
            e.preventDefault()
            moveFocus(el, -1)
            return
        }
        if (e.key === "ArrowRight" && (col === "debit" || col === "credit" || caretAtEnd(el))) {
            e.preventDefault()
            moveFocus(el, 1)
            return
        }
        if (col === "debit" || col === "credit") {
            const info = focusInfo
            if (!info) return
            if (e.key === "=") {
                e.preventDefault()
                autoBalance(info.rowKey, col)
            } else if (e.key === " ") {
                e.preventDefault()
                swapRow(info.rowKey)
            }
        }
    }

    /* ---------- 全屏 ---------- */

    useEffect(() => {
        const onChange = () => setFullscreen(!!document.fullscreenElement)
        document.addEventListener("fullscreenchange", onChange)
        return () => document.removeEventListener("fullscreenchange", onChange)
    }, [])

    const toggleFullscreen = async () => {
        try {
            if (document.fullscreenElement) await document.exitFullscreen()
            else await sheetRef.current?.requestFullscreen()
        } catch {
            message.warning("当前浏览器不支持全屏")
        }
    }

    /* ---------- 上一页 / 下一页 ---------- */

    /** 载入当前凭证的前一条 / 后一条，进入编辑态。 */
    const loadSibling = async (dir: 1 | -1) => {
        if (!token || !accountSetId || siblings.length === 0) {
            message.info("当期还没有已保存的凭证")
            return
        }
        // 空白新增态排在最后：上一页 = 最后一张已存凭证，下一页无去处。
        const target = loadedId
            ? siblings[siblings.findIndex((v) => v.id === loadedId) + dir]
            : dir > 0
                ? undefined
                : siblings[siblings.length - 1]
        if (!target?.id) {
            message.info(dir > 0 ? "已经是最后一条凭证" : "已经是第一条凭证")
            return
        }
        await loadVoucher(target.id)
    }

    const loadAttachments = useCallback(async (id: string) => {
        if (!token || !accountSetId || !id) {
            setAttachments([])
            return
        }
        try {
            const list = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdAttachmentsGet({
                accountSetId,
                id,
            })
            setAttachments(list ?? [])
        } catch {
            setAttachments([])
        }
    }, [token, accountSetId])

    const loadVoucher = useCallback(async (id: string) => {
        if (!token) return
        setLoading(true)
        setMetaError(null)
        try {
            const detail: VoucherDetailDto = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdGet({
                accountSetId,
                id,
            })
            setLoadedId(detail.id ?? null)
            setLoadedStatus(detail.status ?? null)
            setWord(detail.word ?? word)
            setNumber(detail.number ?? null)
            setDate(detail.date ? toISODate(detail.date) : date)
            setAttachmentCount(String(detail.attachmentCount ?? ""))
            setRemark(detail.remark ?? "")
            setRemarkOpen(!!detail.remark)
            // 分录科目可能已停用或变成非末级而不在候选里，用详情带的编码名称补一项保住回显。
            setStaleAccounts((prev) => {
                const known = new Set(accounts.map((a) => a.id ?? ""))
                const next = [...prev]
                for (const e of detail.entries ?? []) {
                    const aid = e.accountId
                    if (!aid || known.has(aid) || next.some((o) => o.value === aid)) continue
                    next.push({
                        value: aid,
                        label: `${e.accountCode ?? ""} ${e.accountName ?? ""}`.trim() + "（当前不可选）",
                        disabled: true,
                    })
                }
                return next
            })
            void loadAttachments(detail.id ?? "")
            const mapped = (detail.entries ?? []).map((e) => ({
                key: nextKey(),
                summary: e.summary ?? "",
                accountId: e.accountId ?? "",
                debit: e.debit ? String(e.debit) : "",
                credit: e.credit ? String(e.credit) : "",
                assists: assistsOf(e),
            }))
            clearHistory()
            setRows(mapped.length > 0 ? mapped : blankRows())
            setActiveRow(0)
        } catch (e) {
            setMetaError(await apiErrorMessage(e, "加载凭证失败"))
        } finally {
            setLoading(false)
        }
    }, [token, accountSetId, word, date, accounts, nextKey, clearHistory, loadAttachments])

    /** 编辑模式：挂载时载入指定凭证（外层用 key 保证换凭证即重挂载）。 */
    const initialLoadedRef = useRef(false)
    useEffect(() => {
        if (!voucherId || initialLoadedRef.current) return
        initialLoadedRef.current = true
        void loadVoucher(voucherId)
    }, [voucherId, loadVoucher])

    /** 回到空白新增态。 */
    const resetForm = () => {
        clearHistory()
        setLoadedId(null)
        setLoadedStatus(null)
        setStaleAccounts([])
        setRows(blankRows())
        setAttachmentCount("")
        setRemark("")
        setRemarkOpen(false)
        setAttachments([])
        setActiveRow(0)
    }

    /* ---------- 保存 ---------- */

    /** 实际执行保存：正常凭证先反过账再更新，保存后后端重新入账。 */
    const doSave = async (keepEditing: boolean, rowsToSave: EntryRow[], successText = "保存成功") => {
        if (!token || !accountSetId) return
        const entries = rowsToSave
            .filter((r) => r.accountId && (toAmount(r.debit) !== 0 || toAmount(r.credit) !== 0))
            .map((r) => {
                const assists = Object.entries(r.assists)
                    .filter(([, itemId]) => Boolean(itemId))
                    .map(([type, itemId]) => ({ type: Number(type), itemId }))
                return {
                    summary: r.summary || "（无摘要）",
                    accountId: r.accountId,
                    debit: toAmount(r.debit),
                    credit: toAmount(r.credit),
                    assists: assists.length > 0 ? assists : undefined,
                }
            })
        setSaving(true)
        setMetaError(null)
        try {
            const api = new VouchersApi(apiConfig(token))
            const payload = {
                date: new Date(date),
                attachmentCount: Math.max(0, Number(attachmentCount) || 0),
                remark: remark.trim() || undefined,
                entries,
            }
            if (loadedId) {
                // 列表「修改」直接进编辑：正常凭证先反过账，更新后重新入账。
                if (loadedStatus === 3) {
                    await api.accountsetsAccountSetIdVouchersIdUnpostPost({ accountSetId, id: loadedId })
                }
                await api.accountsetsAccountSetIdVouchersIdPut({
                    accountSetId,
                    id: loadedId,
                    updateVoucherRequest: payload,
                })
                setLoadedStatus(3)
            } else {
                const created = await api.accountsetsAccountSetIdVouchersPost({
                    accountSetId,
                    createVoucherRequest: { word, number: number ?? undefined, ...payload },
                })
                if (!keepEditing && created?.id) {
                    setLoadedId(created.id)
                    setLoadedStatus(3)
                    setNumber(created.number ?? number)
                }
            }
            message.success(successText)
            if (onSaved) {
                onSaved()
                return
            }
            if (keepEditing) resetForm()
            void reloadMeta()
        } catch (e) {
            setMetaError(await apiErrorMessage(e, "保存失败"))
        } finally {
            setSaving(false)
        }
    }

    const save = async (keepEditing: boolean, successText = "保存成功", skipDirectionCheck = false) => {
        if (!token || !accountSetId) return
        if (!word || !date) {
            message.warning("凭证字或记账日期尚未就绪，请稍候再试")
            return
        }
        const active = rows.filter(
            (r) => r.summary.trim() || r.accountId || toAmount(r.debit) !== 0 || toAmount(r.credit) !== 0,
        )
        if (active.length === 0) {
            message.warning("请先录入分录（选择科目并填写金额）")
            return
        }
        // 一次性汇总所有缺项，避免逐条改完再报下一条。
        const noAccount: number[] = []
        const badAmount: number[] = []
        const noAssist: string[] = []
        rows.forEach((r, i) => {
            if (!active.includes(r)) return
            const line = i + 1
            if (!r.accountId) noAccount.push(line)
            if ((toAmount(r.debit) !== 0) === (toAmount(r.credit) !== 0)) badAmount.push(line)
            const missing = assistTypesOf(r.accountId)
                .filter((c) => c.isRequired !== false && !r.assists[c.type as number])
                .map((c) => assistLabel(c.type))
            if (missing.length > 0) noAssist.push(`第 ${line} 行缺 ${missing.join("、")}`)
        })
        const problems = [
            noAccount.length > 0 ? `第 ${noAccount.join("、")} 行未选科目` : "",
            badAmount.length > 0 ? `第 ${badAmount.join("、")} 行需且只能填借方或贷方之一` : "",
            ...noAssist,
        ].filter(Boolean)
        if (problems.length > 0) {
            message.error(`${problems.join("；")}。`)
            return
        }
        if (!totals.balanced) {
            message.error("借贷不平衡，不能保存")
            return
        }
        if (!skipDirectionCheck) {
            const issues = detectDirectionIssues(active, accountsById)
            if (issues.length > 0) {
                setDirectionModal({ issues, keepEditing })
                return
            }
        }
        await doSave(keepEditing, rows, successText)
    }

    /** 批量调整损益方向：收入类借方正数 → 贷方正数，费用类贷方正数 → 借方正数。 */
    const autoAdjustEntries = () => {
        if (!directionModal) return
        const adjusted = rowsRef.current.map((entry) => {
            const account = accountsById.get(entry.accountId)
            const debit = toAmount(entry.debit)
            const credit = toAmount(entry.credit)
            if (isRevenueAccount(account) && debit > 0) {
                return { ...entry, debit: "", credit: String(debit) }
            }
            if (isExpenseAccount(account) && credit > 0) {
                return { ...entry, debit: String(credit), credit: "" }
            }
            return entry
        })
        commitRows(() => adjusted)
        const { keepEditing } = directionModal
        setDirectionModal(null)
        void doSave(keepEditing, adjusted)
    }

    /* ---------- 模板 / 暂存 ---------- */

    const loadTemplateList = async () => {
        if (!token || !accountSetId) return
        try {
            const list = await new VoucherTemplatesApi(apiConfig(token)).accountsetsAccountSetIdVouchertemplatesGet({
                accountSetId,
            })
            setTemplates(list ?? [])
        } catch (e) {
            message.error(await apiErrorMessage(e, "加载凭证模板失败"))
        }
    }

    /** 从凭证模板引入（Ctrl+Q）：弹出模板列表。 */
    const openTemplateLoad = () => {
        void loadTemplateList()
        setTemplateId("")
        setTemplateLoadOpen(true)
    }

    /** 另存为凭证模板（Ctrl+E）。 */
    const openTemplateSave = () => {
        setTemplateName("")
        setTemplateType(1)
        setTemplateSaveOpen(true)
    }

    const saveAsTemplate = async () => {
        if (!token || !accountSetId) return
        const name = templateName.trim()
        if (!name) {
            message.warning("请填写模板名称")
            return
        }
        const lines = rowsRef.current
            .filter((r) => r.accountId)
            .map((r) => ({
                summary: r.summary || "（无摘要）",
                accountId: r.accountId,
                // 模板只记借贷方向，不记金额。
                isDebit: toAmount(r.credit) === 0,
            }))
        if (lines.length === 0) {
            message.warning("请先录入带科目的分录")
            return
        }
        try {
            await new VoucherTemplatesApi(apiConfig(token)).accountsetsAccountSetIdVouchertemplatesPost({
                accountSetId,
                saveVoucherTemplateRequest: { name, type: templateType, lines },
            })
            message.success("模板已保存")
            setTemplateSaveOpen(false)
        } catch (e) {
            message.error(await apiErrorMessage(e, "保存模板失败"))
        }
    }

    const applyTemplate = () => {
        const template = templates.find((t) => t.id === templateId)
        if (!template?.lines?.length) {
            message.warning("请选择凭证模板")
            return
        }
        commitRows(() =>
            (template.lines ?? []).map((line) => ({
                key: nextKey(),
                summary: line.summary ?? "",
                accountId: line.accountId ?? "",
                debit: "",
                credit: "",
                assists: {},
            })),
        )
        setActiveRow(0)
        setTemplateLoadOpen(false)
        message.success(`已引入模板「${template.name}」`)
    }

    /** 暂存：反过账退回或导入不平衡的凭证（保存即入账，正常制单不会留下暂存凭证）。 */
    const drafts = useMemo(() => (allVouchers ?? []).filter((v) => v.status === 1), [allVouchers])

    const openDraft = () => {
        if (drafts.length === 0) {
            message.info("当前没有暂存凭证")
            return
        }
        setDraftId("")
        setDraftOpen(true)
    }

    const applyDraft = async () => {
        if (!draftId) {
            message.warning("请选择暂存凭证")
            return
        }
        setDraftOpen(false)
        await loadVoucher(draftId)
    }

    /* ---------- 快捷键 ---------- */

    const printCurrent = () => {
        if (loadedId) setPrintVoucherId(loadedId)
        else message.info("请先保存凭证再打印")
    }

    const confirmDelete = () => {
        if (!loadedId) {
            message.info("当前凭证尚未保存")
            return
        }
        modal.confirm({
            title: "删除当前凭证",
            content: `确定删除「${word}-${number ?? ""}」？删除后不可恢复。`,
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: () => deleteLoaded(),
        })
    }

    useEffect(() => {
        if (!keepAliveActive) return
        const onKey = (e: KeyboardEvent) => {
            const mod = e.ctrlKey || e.metaKey
            const key = e.key.toLowerCase()
            if (mod && key === "s") {
                e.preventDefault()
                void save(false)
            } else if (mod && key === "q") {
                e.preventDefault()
                openTemplateLoad()
            } else if (mod && key === "e") {
                e.preventDefault()
                openTemplateSave()
            } else if (mod && key === "l") {
                e.preventDefault()
                dateRef.current?.querySelector<HTMLInputElement>("input")?.focus()
            } else if (mod && key === "p") {
                e.preventDefault()
                printCurrent()
            } else if (mod && (e.key === "+" || e.key === "=")) {
                e.preventDefault()
                insertAtFocus()
            } else if (mod && e.key === "-") {
                e.preventDefault()
                removeAtFocus()
            } else if (mod && e.key === "Delete") {
                e.preventDefault()
                confirmDelete()
            } else if (mod && key === "z") {
                e.preventDefault()
                undo()
            } else if (mod && key === "y") {
                e.preventDefault()
                redo()
            } else if (e.key === "F12") {
                // 抽屉里没有「入账并新增」，F12 不生效。
                if (embedded) return
                e.preventDefault()
                void save(true)
            } else if (e.key === "F3") {
                e.preventDefault()
                copyAbove()
            } else if (e.key === "F4") {
                e.preventDefault()
                attachmentRef.current?.focus()
            } else if (e.key === "PageUp") {
                if (embedded) return
                e.preventDefault()
                void loadSibling(-1)
            } else if (e.key === "PageDown") {
                if (embedded) return
                e.preventDefault()
                void loadSibling(1)
            } else if (e.altKey && key === "n") {
                if (embedded) return
                e.preventDefault()
                if (isDirty) {
                    confirmReset()
                } else {
                    resetForm()
                    message.info("已新建空白凭证")
                }
            }
        }
        window.addEventListener("keydown", onKey)
        return () => window.removeEventListener("keydown", onKey)
    })

    /** 摘要候选：按当前输入做包含过滤，最多 8 条。 */
    const summaryCandidates = useMemo(() => {
        const kw = (focusInfo?.rowKey != null ? rows.find((r) => r.key === focusInfo.rowKey)?.summary : "") ?? ""
        const key = kw.trim()
        const list = key ? summaryHistory.filter((h) => h.includes(key)) : summaryHistory
        return list.slice(0, 8)
    }, [summaryHistory, focusInfo, rows])

    const canSave = !saving && !loading && !!word && !!date && totals.balanced

    /** 当前凭证在同字号凭证里的位置；空白新增态记为 -1。 */
    const siblingIndex = loadedId ? siblings.findIndex((v) => v.id === loadedId) : -1
    const atFirst = loadedId !== null && siblingIndex <= 0
    const atLast = loadedId === null || siblingIndex < 0 || siblingIndex >= siblings.length - 1
    /** 保存按钮为何不可用：直接写在工具条上，用户不用猜。 */
    const hasAmount = rows.some((r) => toAmount(r.debit) || toAmount(r.credit))
    const saveHint = !totals.balanced
        ? `借贷不平，差额 ${fmtAmount(Math.abs(totals.debit - totals.credit), true)}`
        : !word || !date
          ? "请先填写凭证字与日期"
          : ""

    /** 是否已录入内容：清空前据此决定是否需要确认，避免空表也弹窗。 */
    const isDirty = useMemo(
        () =>
            loadedId !== null ||
            rows.some((r) => r.summary.trim() || r.accountId || toAmount(r.debit) || toAmount(r.credit)) ||
            Boolean(remark) ||
            Boolean(attachmentCount),
        [loadedId, rows, remark, attachmentCount],
    )

    /** 清空凭证：有内容时二次确认，避免误触 Alt+N / 菜单项丢掉刚录的分录。 */
    const confirmReset = () => {
        const clear = () => {
            resetForm()
            message.success("已清空凭证")
        }
        if (!isDirty) {
            clear()
            return
        }
        modal.confirm({
            title: "清空当前凭证？",
            content: "已填写的分录、摘要与附件将被丢弃，且无法撤销。",
            okText: "清空",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: clear,
        })
    }

    const moreItems: MenuProps["items"] = [
        { key: "save-template", label: "保存为模板" },
        { key: "load-template", label: "从模板中加载" },
        { key: "load-draft", label: "从暂存中加载" },
        { key: "clear", label: "清空凭证" },
        { key: "options", label: "选项" },
    ]

    const onMore: MenuProps["onClick"] = ({ key }) => {
        if (key === "save-template") openTemplateSave()
        else if (key === "load-template") openTemplateLoad()
        else if (key === "load-draft") openDraft()
        else if (key === "clear") {
            confirmReset()
        } else if (key === "options") setOptionsOpen(true)
    }

    const deleteLoaded = async () => {
        if (!token || !loadedId) return
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdDelete({ accountSetId, id: loadedId })
            message.success("凭证已删除")
            if (onSaved) {
                onSaved()
                return
            }
            resetForm()
            void reloadMeta()
        } catch (e) {
            setMetaError(await apiErrorMessage(e, "删除失败"))
        }
    }

    /** 附件大小展示：不足 1KB 按字节显示，避免出现 0.0 KB。 */
    function formatBytes(size: number): string {
        if (size < 1024) return `${size} B`
        if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`
        return `${(size / 1024 / 1024).toFixed(1)} MB`
    }

    /* ---------- 附件 ---------- */

    /** 拉取当前凭证的附件列表；未保存的凭证没有附件。 */
    /** 上传附件：未保存的凭证先提示保存，因为附件按凭证 ID 归属。 */
    const uploadAttachment = async (file: File) => {
        if (!token || !accountSetId) return
        if (!loadedId) {
            message.warning("请先保存凭证，再上传附件")
            return
        }
        if (file.size > 10 * 1024 * 1024) {
            message.error("附件不能超过 10MB")
            return
        }
        setAttachUploading(true)
        try {
            const created = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdAttachmentsPost({
                accountSetId,
                id: loadedId,
                file,
            })
            setAttachments((prev) => [...prev, created])
            // 附单据张数随手同步，符合纸质凭证「附几张原始单据」的习惯。
            setAttachmentCount((prev) => {
                const n = Math.max(0, Number(prev) || 0)
                return String(n + 1)
            })
            message.success("附件已上传")
        } catch (e) {
            message.error(await apiErrorMessage(e, "附件上传失败"))
        } finally {
            setAttachUploading(false)
        }
    }

    const removeAttachment = async (attachmentId: string) => {
        if (!token || !accountSetId || !loadedId) return
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdAttachmentsAttachmentIdDelete({
                accountSetId,
                id: loadedId,
                attachmentId,
            })
            setAttachments((prev) => prev.filter((a) => a.id !== attachmentId))
            message.success("附件已删除")
        } catch (e) {
            message.error(await apiErrorMessage(e, "删除附件失败"))
        }
    }

    /** 附件预览/下载地址（带令牌不可行，走同源 /api 由后端鉴权 cookie 之外的 Bearer）。 */
    const openAttachment = async (attachmentId: string, fileName: string, download: boolean) => {
        if (!token || !accountSetId || !loadedId) return
        try {
            const url = `/api/accountsets/${accountSetId}/vouchers/${loadedId}/attachments/${attachmentId}/file${download ? "?download=true" : ""}`
            const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } })
            if (!res.ok) {
                message.error("附件读取失败")
                return
            }
            const blob = await res.blob()
            const href = URL.createObjectURL(blob)
            if (download) {
                const a = document.createElement("a")
                a.href = href
                a.download = fileName
                a.click()
            } else {
                window.open(href, "_blank")
            }
            // 交给浏览器接管后回收，避免内存泄漏。
            setTimeout(() => URL.revokeObjectURL(href), 60_000)
        } catch {
            message.error("附件读取失败")
        }
    }

    /* ---------- 摘要历史 ---------- */

    useEffect(() => {
        if (!token || !accountSetId) return
        let cancelled = false
        void (async () => {
            try {
                const list = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersSummariesGet({
                    accountSetId,
                    limit: 80,
                })
                if (!cancelled) setSummaryHistory(list ?? [])
            } catch {
                if (!cancelled) setSummaryHistory([])
            }
        })()
        return () => {
            cancelled = true
        }
        // 账套切换或保存后（reloadMeta 触发 rows 变化）都要刷新候选
    }, [token, accountSetId])

    /* ---------- 选项 ---------- */

    const saveOptions = (next: { fillGap: boolean; followDirection: boolean }) => {
        setOptFillGap(next.fillGap)
        setOptFollowDirection(next.followDirection)
        localStorage.setItem("yuncai.voucher.fillGap", next.fillGap ? "1" : "0")
        localStorage.setItem("yuncai.voucher.followDirection", next.followDirection ? "1" : "0")
        message.success("选项已保存")
    }

    /* ---------- 渲染 ---------- */

    /** 金额单元格：分位小格打底，点击后整组并成一格直接输入完整数值。 */
    const renderAmountCell = (r: EntryRow, idx: number, side: "debit" | "credit") => (
        <AmountCells
            value={toAmount(side === "debit" ? r.debit : r.credit)}
            raw={side === "debit" ? r.debit : r.credit}
            editable
            height={ROW_H}
            onInputFocus={() => {
                setActiveRow(idx)
                setFocusInfo({ rowKey: r.key, col: side })
                setAssistPanelRow(null)
            }}
            onChange={(v) =>
                patchRow(r.key, side === "debit" ? { debit: v, credit: "" } : { credit: v, debit: "" })
            }
            inputAttrs={{
                "data-nav": side,
                "data-col": side,
                onDoubleClick: () => autoBalance(r.key, side),
                onKeyDown: onCellKeyDown,
            }}
        />
    )

    /** 行操作浮标：悬停本行时出现在纸面左侧槽位，不占表体列宽。 */
    const renderRowTools = (idx: number) => (
        <div
            className="absolute top-1/2 flex -translate-y-1/2 flex-col gap-0.5 opacity-0 group-hover:opacity-100"
            style={{ left: -GUTTER }}
        >
            <Button
                size="small"
                type="text"
                title="在下方插入一行"
                icon={<PlusCircleOutlined />}
                onClick={() => insertBelow(idx)}
            />
            <Button
                size="small"
                type="text"
                title="复制本行"
                icon={<CopyOutlined />}
                onClick={() => duplicateRow(idx)}
            />
            <Button
                size="small"
                type="text"
                title="删除本行"
                disabled={rows.length <= 1}
                className={rows.length <= 1 ? "!text-(--muted-foreground)" : "!text-(--danger)"}
                icon={<CloseOutlined />}
                onClick={() => removeRow(idx)}
            />
        </div>
    )

    return (
        <div ref={sheetRef} className={cn("flex flex-col bg-(--card)", embedded && "h-full min-h-0")}>
            {/* 工具条 */}
            <div className="flex min-h-[56px] flex-wrap items-center gap-2 border-b border-(--border) px-4 py-2">
                {!embedded && (
                    <Button type="primary" disabled={!canSave} loading={saving} onClick={() => void save(true)}>
                        入账并新增
                    </Button>
                )}
                <Button type="primary" disabled={!canSave} loading={saving} onClick={() => void save(false)}>
                    保存入账
                </Button>
                <Button icon={<PrinterOutlined />} disabled={!loadedId} onClick={() => setPrintVoucherId(loadedId)}>
                    打印
                </Button>
                <Dropdown menu={{ items: moreItems, onClick: onMore }} trigger={["click"]}>
                    <Button>
                        更多 <DownOutlined className="text-[10px]" />
                    </Button>
                </Dropdown>
                {/* 平衡状态与不可保存的原因常驻显示，替代「按钮灰着但不说为什么」。 */}
                <span
                    className={cn(
                        "ml-1 shrink-0 rounded px-2 py-0.5 text-[12.5px] whitespace-nowrap",
                        totals.balanced
                            ? "bg-(--success-soft) text-(--success)"
                            : hasAmount && saveHint
                              ? "bg-(--table-neg-bg) text-(--danger)"
                              : "text-(--muted-foreground)",
                    )}
                >
                    {totals.balanced
                        ? `借贷平衡 · 合计 ${fmtAmount(totals.debit, true)}`
                        : hasAmount
                          ? saveHint
                          : "尚未录入金额"}
                </span>
                {embedded && onCancel && (
                    <Button className="ml-auto" disabled={saving} onClick={onCancel}>
                        取消
                    </Button>
                )}
                {!embedded && (
                    <div className="ml-auto flex flex-wrap items-center justify-end gap-3 text-[13px] text-(--foreground)">
                        <Button
                            type="text"
                            icon={<ControlOutlined />}
                            onClick={() => setShortcutOpen(true)}
                        >
                            快捷键
                        </Button>
                        <span className="rounded bg-(--muted) px-2 py-0.5 tabular-nums text-(--muted-foreground)">
                            {loadedId ? `${Math.max(siblingIndex, 0) + 1} / ${siblings.length}` : `新凭证 · 共 ${siblings.length} 张`}
                        </span>
                        <Button
                            type="text"
                            icon={<ArrowLeftOutlined />}
                            iconPosition="end"
                            disabled={atFirst}
                            onClick={() => void loadSibling(-1)}
                        >
                            上一页
                        </Button>
                        <Button
                            type="text"
                            icon={<ArrowRightOutlined />}
                            disabled={atLast}
                            onClick={() => void loadSibling(1)}
                        >
                            下一页
                        </Button>
                    </div>
                )}
            </div>

            {metaError && (
                <div className="mx-auto w-full max-w-[1440px] px-8 py-1 text-[13px] text-(--danger)">{metaError}</div>
            )}

            <div className={cn("overflow-auto", embedded ? "min-h-0 flex-1" : "flex-1")}>
                <div
                    className="mx-auto w-full max-w-[1440px] pt-4 pb-6"
                    style={{ paddingLeft: GUTTER, paddingRight: GUTTER, zoom: zoom ? 1.25 : 1 }}
                >
                    {/* 凭证头 */}
                    <div className="flex flex-wrap items-center gap-2 pb-3 text-[13px] whitespace-nowrap text-(--foreground)">
                        <Select
                            value={word}
                            onChange={setWord}
                            options={words.map((w) => ({ value: w, label: w }))}
                            style={{ width: 76 }}
                            disabled={!!loadedId}
                        />
                        <InputNumber
                            value={number}
                            onChange={(v) => setNumber(typeof v === "number" ? v : null)}
                            min={1}
                            precision={0}
                            controls={false}
                            style={{ width: 62 }}
                            disabled={!!loadedId}
                        />
                        <span>号</span>
                        <span className="ml-1">日期：</span>
                        <span ref={dateRef} className="inline-flex">
                            <DatePicker
                                value={date ? dayjs(date) : null}
                                onChange={(v) => setDate(v ? v.format("YYYY-MM-DD") : "")}
                                allowClear={false}
                                style={{ width: 130 }}
                            />
                        </span>
                        <span className="ml-2 text-(--warn-text)">亲，按回车键，能快速选择单元格哟！</span>
                        <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
                            附单据
                            <Input
                                ref={attachmentRef}
                                value={attachmentCount}
                                onChange={(e) => setAttachmentCount(e.target.value.replace(/\D/g, ""))}
                                style={{ width: 62, textAlign: "center" }}
                            />
                            张
                            <Button
                                type="link"
                                onClick={() => {
                                    if (!loadedId) {
                                        message.warning("请先保存凭证，再上传附件")
                                        return
                                    }
                                    setAttachOpen(true)
                                    void loadAttachments(loadedId)
                                }}
                            >
                                附件
                                {attachments.length > 0 && (
                                    <span className="rounded-full bg-(--brand-fill) px-1.5 text-[11px] leading-4 text-white">
                                        {attachments.length}
                                    </span>
                                )}
                            </Button>
                            {remarkOpen ? (
                                <Input
                                    autoFocus
                                    value={remark}
                                    placeholder="凭证备注"
                                    maxLength={255}
                                    onChange={(e) => setRemark(e.target.value)}
                                    onBlur={() => setRemarkOpen(!remark.trim() ? false : true)}
                                    style={{ width: 200 }}
                                />
                            ) : (
                                <Button
                                    type="link"
                                    title={remark || "添加凭证备注"}
                                    onClick={() => setRemarkOpen(true)}
                                >
                                    备注
                                </Button>
                            )}
                            <Tooltip title="凭证备注随凭证一起保存；附件在保存凭证后上传">
                                <QuestionCircleOutlined className="text-(--muted-foreground)" />
                            </Tooltip>
                            <Button
                                type="text"
                                icon={zoom ? <ZoomOutOutlined /> : <ZoomInOutlined />}
                                onClick={() => setZoom((v) => !v)}
                            >
                                {zoom ? "缩小" : "放大"}
                            </Button>
                            {!embedded && (
                                <Button
                                    type="text"
                                    icon={fullscreen ? <FullscreenExitOutlined /> : <FullscreenOutlined />}
                                    onClick={() => void toggleFullscreen()}
                                >
                                    {fullscreen ? "退出全屏" : "全屏"}
                                </Button>
                            )}
                        </div>
                    </div>

                    {/* 分录表格 */}
                    <div ref={gridRef}>
                        <Table<EntryRow>
                            size="small"
                            pagination={false}
                            tableLayout="fixed"
                            className="voucher-sheet"
                            // 窄屏保底宽度：科目列不压缩，改为整表横向滚动（会计科目在 1200 时约 467px）。
                            scroll={{ x: 1200 }}
                            rowKey={(r) => r.key}
                            dataSource={rows}
                            onRow={(_, idx) => ({ onMouseDown: () => setActiveRow(idx ?? 0) })}
                            rowClassName={(_, idx) =>
                                cn("group", idx === activeRow ? "bg-(--brand-soft)" : "bg-(--card)")
                            }
                            columns={[
                                {
                                    title: <SheetHeadTitle text="序号" />,
                                    width: 33,
                                    onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
                                    onCell: () => ({ className: "pk-sheet-cell", style: { height: ROW_H } }),
                                    render: (_, _r, idx) => (
                                        <div className="relative flex h-full items-center justify-center">
                                            <span className="text-[13px] tabular-nums text-(--muted-foreground)">
                                                {(idx ?? 0) + 1}
                                            </span>
                                            {renderRowTools(idx ?? 0)}
                                        </div>
                                    ),
                                },
                                {
                                    title: <SheetHeadTitle text="摘要" />,
                                    width: 260,
                                    onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
                                    onCell: () => ({
                                        className: "pk-sheet-cell relative",
                                        style: { height: ROW_H },
                                    }),
                                    render: (_, r, idx) => (
                                        <>
                                            <Input
                                                data-nav="summary"
                                                data-col="summary"
                                                variant="borderless"
                                                value={r.summary}
                                                onFocus={() => {
                                                    setActiveRow(idx ?? 0)
                                                    setFocusInfo({ rowKey: r.key, col: "summary" })
                                                    setSummaryPickRow(r.key)
                                                    setAssistPanelRow(null)
                                                }}
                                                onBlur={() => {
                                                    // 延迟收起，留出点击候选的时间
                                                    window.setTimeout(() => setSummaryPickRow(null), 150)
                                                }}
                                                onKeyDown={onCellKeyDown}
                                                onChange={(e) => patchRow(r.key, { summary: e.target.value })}
                                                style={{ height: ROW_H }}
                                            />
                                            {summaryPickRow === r.key && summaryCandidates.length > 0 && (
                                                <div className="absolute left-0 top-full z-20 max-h-[220px] w-[300px] overflow-auto rounded border border-(--border) bg-(--card) py-1 shadow-lg">
                                                    {summaryCandidates.map((item) => (
                                                        <button
                                                            key={item}
                                                            type="button"
                                                            className="block w-full px-3 py-1.5 text-left text-[13px] text-(--foreground) hover:bg-(--brand-soft)"
                                                            onMouseDown={(e) => {
                                                                e.preventDefault()
                                                                patchRow(r.key, { summary: item })
                                                                setSummaryPickRow(null)
                                                            }}
                                                        >
                                                            {item}
                                                        </button>
                                                    ))}
                                                </div>
                                            )}
                                        </>
                                    ),
                                },
                                {
                                    title: <SheetHeadTitle text="会计科目" />,
                                    onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
                                    onCell: () => ({ className: "pk-sheet-cell", style: { height: ROW_H } }),
                                    render: (_, r, idx) => {
                                        const account = accountsById.get(r.accountId)
                                        const assistConfigsOfRow = assistTypesOf(r.accountId)
                                        return (
                                            <div
                                                data-nav="account"
                                                data-col="account"
                                                data-assist-host={r.key}
                                                className="relative flex h-full flex-col"
                                                onKeyDown={onCellKeyDown}
                                            >
                                                <AccountSelect
                                                    value={r.accountId}
                                                    accounts={accounts}
                                                    balances={balances}
                                                    accountSetId={accountSetId}
                                                    extraOptions={extraAccountOptions}
                                                    // 辅助核算摘要跟在科目名后（`1122 应收账款_2华东客户甲`），。
                                                    labelSuffix={assistTextOf(r)}
                                                    onFocus={() => {
                                                        setActiveRow(idx ?? 0)
                                                        setFocusInfo({ rowKey: r.key, col: "account" })
                                                        setAssistPanelRow(r.key)
                                                    }}
                                                    onOpenChange={(open) => {
                                                        if (open) setAssistPanelRow(r.key)
                                                    }}
                                                    // 换科目后原辅助核算取值不再适用，清空重选。
                                                    onChange={(v) => {
                                                        patchRow(r.key, { accountId: v, assists: {} })
                                                        if (v) setAssistPanelRow(r.key)
                                                    }}
                                                />
                                                {account?.code && (
                                                    <div className="shrink-0 pl-3 text-[12px] leading-4 tabular-nums text-(--muted-foreground)">
                                                        余额：{(balances[account.code] ?? 0).toFixed(2)}
                                                    </div>
                                                )}
                                                {assistConfigsOfRow.length > 0 && assistPanelRow === r.key && (
                                                    <div className="absolute left-0 top-full z-30 w-[340px] rounded border border-(--border) bg-(--card) p-2 shadow-lg">
                                                        {assistConfigsOfRow.map((cfg) => (
                                                            <div key={cfg.type} className="flex items-center gap-2 py-0.5">
                                                                <span className="w-10 shrink-0 text-[12px] text-(--muted-foreground)">
                                                                    {assistLabel(cfg.type)}
                                                                    {cfg.isRequired !== false && (
                                                                        <span className="text-(--danger)">*</span>
                                                                    )}
                                                                </span>
                                                                <AssistItemSelect
                                                                    className="flex-1"
                                                                    accountSetId={accountSetId}
                                                                    token={token}
                                                                    type={cfg.type as number}
                                                                    value={r.assists[cfg.type as number] ?? ""}
                                                                    onChange={(itemId) =>
                                                                        patchRow(r.key, {
                                                                            assists: { ...r.assists, [cfg.type as number]: itemId },
                                                                        })
                                                                    }
                                                                />
                                                            </div>
                                                        ))}
                                                    </div>
                                                )}
                                            </div>
                                        )
                                    },
                                },
                                {
                                    title: <AmountHeaderCell title="借方金额" idPrefix="jd" height={HEAD_H} divider />,
                                    width: GROUP_W,
                                    onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
                                    onCell: () => ({ className: "pk-sheet-cell" }),
                                    render: (_, r, idx) => renderAmountCell(r, idx ?? 0, "debit"),
                                },
                                {
                                    title: <AmountHeaderCell title="贷方金额" idPrefix="cd" height={HEAD_H} divider />,
                                    width: GROUP_W,
                                    onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
                                    onCell: () => ({ className: "pk-sheet-cell" }),
                                    render: (_, r, idx) => renderAmountCell(r, idx ?? 0, "credit"),
                                },
                            ]}
                            summary={() => (
                                <Table.Summary.Row>
                                    <Table.Summary.Cell
                                        index={0}
                                        colSpan={3}
                                        className="pk-sheet-label"
                                        align="left"
                                    >
                                        合计：{amountToChineseUppercase(totals.debit)}
                                    </Table.Summary.Cell>
                                    <Table.Summary.Cell index={3}>
                                        <AmountCells value={totals.debit} height={ROW_H} />
                                    </Table.Summary.Cell>
                                    <Table.Summary.Cell index={4}>
                                        <AmountCells value={totals.credit} height={ROW_H} />
                                    </Table.Summary.Cell>
                                </Table.Summary.Row>
                            )}
                        />
                    </div>

                    {/* 制单人 */}
                    <div className="mt-4 flex items-center justify-between text-[13px] text-(--foreground)">
                        <span className="flex items-center gap-1.5">
                            <EditOutlined className="text-(--brand)" />
                            制单人：{user?.displayName || user?.username || "-"}
                        </span>
                        {loadedId && <span className="text-(--muted-foreground)">当前为已保存凭证，保存将更新原凭证并入账</span>}
                    </div>
                </div>
            </div>

            <PrintVoucherSheet
                accountSetId={accountSetId}
                token={token}
                voucherId={printVoucherId}
                open={!!printVoucherId}
                onOpenChange={(o) => {
                    if (!o) setPrintVoucherId(null)
                }}
            />

            <Modal
                open={shortcutOpen}
                title={null}
                footer={null}
                width={640}
                onCancel={() => setShortcutOpen(false)}
            >
                <div className="pt-2 text-[13px]">
                    <div className="border border-(--border)">
                        <div className="bg-(--brand-soft) py-2 text-center text-[14px] font-medium text-(--brand)">
                            凭证快捷键
                        </div>
                        <Table
                            showHeader={false}
                            pagination={false}
                            rowKey={(r) => r.join("|")}
                            columns={[
                                { title: "操作", width: "34%", render: (_, r) => r[0] },
                                {
                                    title: "快捷键",
                                    width: "16%",
                                    align: "center",
                                    render: (_, r) => <span className="font-mono text-[12px] text-(--foreground)">{r[1]}</span>,
                                },
                                { title: "操作", width: "34%", render: (_, r) => r[2] },
                                {
                                    title: "快捷键",
                                    width: "16%",
                                    align: "center",
                                    render: (_, r) => <span className="font-mono text-[12px] text-(--foreground)">{r[3]}</span>,
                                },
                            ]}
                            dataSource={SHORTCUTS}
                        />
                    </div>
                    <div className="flex justify-center gap-4 py-4">
                        <Button icon={<DownloadOutlined />} onClick={downloadShortcuts}>
                            下载
                        </Button>
                    </div>
                </div>
            </Modal>

            <Modal
                open={templateSaveOpen}
                title="保存为模板"
                okText="保存"
                cancelText="取消"
                width={440}
                onOk={() => void saveAsTemplate()}
                onCancel={() => setTemplateSaveOpen(false)}
            >
                <div className="flex flex-col gap-3 py-2 text-[13px]">
                    <label className="flex items-center gap-2">
                        <span className="w-16 shrink-0 text-(--foreground)">模板名称</span>
                        <Input
                            value={templateName}
                            onChange={(e) => setTemplateName(e.target.value)}
                            maxLength={40}
                            placeholder="例如：报销办公费"
                        />
                    </label>
                    <label className="flex items-center gap-2">
                        <span className="w-16 shrink-0 text-(--foreground)">模板类型</span>
                        <Select
                            className="flex-1"
                            value={templateType}
                            onChange={setTemplateType}
                            options={TEMPLATE_TYPES}
                        />
                    </label>
                    <p className="text-[12px] text-(--muted-foreground)">
                        只保存当前分录的摘要、科目与借贷方向，金额不保存。
                    </p>
                </div>
            </Modal>

            <Modal
                open={templateLoadOpen}
                title="从模板中加载"
                okText="引入"
                cancelText="取消"
                width={540}
                onOk={applyTemplate}
                onCancel={() => setTemplateLoadOpen(false)}
            >
                {templates.length === 0 ? (
                    <p className="py-8 text-center text-[13px] text-(--muted-foreground)">
                        暂无凭证模板，可先「保存为模板」
                    </p>
                ) : (
                    <div className="flex max-h-[320px] flex-col gap-1.5 overflow-auto py-1">
                        {templates.map((t) => (
                            <button
                                key={t.id}
                                type="button"
                                className={cn(
                                    "flex flex-col items-start gap-0.5 rounded border px-3 py-2 text-left text-[13px]",
                                    templateId === t.id
                                        ? "border-(--brand) bg-(--brand-soft)"
                                        : "border-(--border) hover:border-(--brand)",
                                )}
                                onClick={() => setTemplateId(t.id ?? "")}
                            >
                                <span className="text-(--foreground)">
                                    {t.name}
                                    <span className="ml-2 text-[12px] text-(--muted-foreground)">{t.typeText}</span>
                                </span>
                                <span className="text-[12px] text-(--muted-foreground)">
                                    {(t.lines ?? [])
                                        .map((l) => `${l.accountCode ?? ""} ${l.accountName ?? ""}`)
                                        .join(" / ")}
                                </span>
                            </button>
                        ))}
                    </div>
                )}
            </Modal>

            <Modal
                open={draftOpen}
                title="从暂存中加载"
                okText="载入"
                cancelText="取消"
                width={540}
                onOk={() => void applyDraft()}
                onCancel={() => setDraftOpen(false)}
            >
                <div className="flex max-h-[320px] flex-col gap-1.5 overflow-auto py-1 text-[13px]">
                    {drafts.map((v) => (
                        <button
                            key={v.id}
                            type="button"
                            className={cn(
                                "flex items-center gap-3 rounded border px-3 py-2 text-left",
                                draftId === v.id
                                    ? "border-(--brand) bg-(--brand-soft)"
                                    : "border-(--border) hover:border-(--brand)",
                            )}
                            onClick={() => setDraftId(v.id ?? "")}
                        >
                            <span className="font-medium text-(--foreground)">{v.voucherNo}</span>
                            <span className="text-(--muted-foreground)">{toISODate(v.date)}</span>
                            <span className="text-(--muted-foreground)">{v.entryCount} 条分录</span>
                            <span className="ml-auto tabular-nums text-(--foreground)">
                                {(v.totalDebit ?? 0).toFixed(2)}
                            </span>
                        </button>
                    ))}
                </div>
            </Modal>

            {/* 附件：原始单据影像的查看、上传与删除 */}
            <Modal
                open={attachOpen}
                title="凭证附件"
                width={560}
                onCancel={() => setAttachOpen(false)}
                footer={
                    <div className="flex items-center justify-between">
                        <span className="text-[12px] text-(--muted-foreground)">支持 PDF / 图片 / Office 文档，单个文件不超过 10MB</span>
                        <Button onClick={() => setAttachOpen(false)}>关闭</Button>
                    </div>
                }
            >
                <div className="flex flex-col gap-3">
                    <label className="flex h-9 w-fit cursor-pointer items-center gap-1 rounded border border-dashed border-(--brand) px-3 text-[13px] text-(--brand) hover:bg-(--brand-soft)">
                        <UploadOutlined />
                        {attachUploading ? "上传中…" : "上传附件"}
                        <input
                            type="file"
                            className="hidden"
                            disabled={attachUploading}
                            onChange={(e) => {
                                const file = e.target.files?.[0]
                                if (file) void uploadAttachment(file)
                                e.target.value = ""
                            }}
                        />
                    </label>
                    {attachments.length === 0 ? (
                        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无附件" />
                    ) : (
                        <div className="flex flex-col divide-y divide-(--border) rounded border border-(--border)">
                            {attachments.map((a) => (
                                <div key={a.id} className="flex items-center gap-2 px-3 py-2 text-[13px]">
                                    <FileTextOutlined className="text-(--muted-foreground)" />
                                    <Button
                                        type="text"
                                        className="pk-link-flush max-w-[280px]"
                                        title={a.fileName}
                                        onClick={() => void openAttachment(a.id ?? "", a.fileName ?? "附件", false)}
                                    >
                                        <span className="truncate">{a.fileName}</span>
                                    </Button>
                                    <span className="ml-auto text-[12px] text-(--muted-foreground)">
                                        {formatBytes(a.sizeBytes ?? 0)}
                                    </span>
                                    <Button
                                        type="text"
                                        title="下载"
                                        icon={<DownloadOutlined />}
                                        onClick={() => void openAttachment(a.id ?? "", a.fileName ?? "附件", true)}
                                    />
                                    <Popconfirm
                                        title="删除该附件？"
                                        okText="删除"
                                        cancelText="取消"
                                        onConfirm={() => void removeAttachment(a.id ?? "")}
                                    >
                                        <Button
                                            type="text"
                                            title="删除"
                                            icon={<DeleteOutlined />}
                                        />
                                    </Popconfirm>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            </Modal>

            {/* 录入选项：本机偏好，保存在 localStorage */}
            <Modal
                open={optionsOpen}
                title="选项"
                width={520}
                onCancel={() => setOptionsOpen(false)}
                footer={
                    <div className="flex justify-end gap-2">
                        <Button onClick={() => setOptionsOpen(false)}>取消</Button>
                        <Button
                            type="primary"
                            onClick={() => {
                                saveOptions({ fillGap: optFillGap, followDirection: optFollowDirection })
                                setOptionsOpen(false)
                            }}
                        >
                            确定
                        </Button>
                    </div>
                }
            >
                <div className="flex flex-col gap-4 py-2 text-[13px] text-(--foreground)">
                    <div className="flex items-center justify-between">
                        <div>
                            <div>自动填补凭证断号</div>
                            <div className="text-[12px] text-(--muted-foreground)">新增凭证时优先填补已删除凭证留下的空号</div>
                        </div>
                        <Switch checked={optFillGap} onChange={setOptFillGap} />
                    </div>
                    <div className="flex items-center justify-between">
                        <div>
                            <div>按科目方向定位借贷方</div>
                            <div className="text-[12px] text-(--muted-foreground)">选完科目后，光标自动跳到该科目的正常余额方向</div>
                        </div>
                        <Switch checked={optFollowDirection} onChange={setOptFollowDirection} />
                    </div>
                </div>
            </Modal>

            {/* 损益类科目方向警告弹窗 */}
            <Modal
                open={directionModal !== null}
                width={512}
                title="凭证录入提示"
                onCancel={() => setDirectionModal(null)}
                footer={
                    <div className="flex flex-col gap-2">
                        <Button type="primary" block onClick={autoAdjustEntries}>
                            批量调整，系统帮您自动调整所有金额方向
                        </Button>
                        <Button
                            block
                            onClick={() => {
                                if (!directionModal) return
                                const { keepEditing } = directionModal
                                setDirectionModal(null)
                                void doSave(keepEditing, rowsRef.current)
                            }}
                        >
                            继续保存，直接保存凭证
                        </Button>
                        <Button block onClick={() => setDirectionModal(null)}>
                            关闭弹窗，自行修改凭证后重新保存
                        </Button>
                    </div>
                }
            >
                <div className="flex flex-col gap-4">
                    <div>
                        <p className="font-medium text-(--foreground)">系统检测到当前凭证存在损益科目录入不规范的凭证行哦~</p>
                        <p className="mt-2 text-[13px] text-(--muted-foreground)">
                            收入类 要做在 贷方 ， 费用类 要做在 借方 。 如果要冲回，应该录入相同方向的红字，即负数。否则科目余额表和利润表数据会对不上
                        </p>
                    </div>
                    <p className="text-[13px]">
                        需要调整金额方向的凭证行：<span className="font-semibold">{directionModal?.issues.join("、")}</span>
                    </p>
                </div>
            </Modal>
        </div>
    )
}
