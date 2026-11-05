import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import type { CSSProperties, KeyboardEvent as ReactKeyboardEvent } from "react"
import { App, Button, DatePicker, Dropdown, Empty, Input, InputNumber, Modal, Popconfirm, Select, Switch, Table } from "antd"
import type { InputRef, MenuProps } from "antd"
import {
    ArrowLeftOutlined,
    ArrowRightOutlined,
    CloseOutlined,
    ControlOutlined,
    CopyOutlined,
    DeleteOutlined,
    DownloadOutlined,
    EditOutlined,
    FileTextOutlined,
    FullscreenExitOutlined,
    FullscreenOutlined,
    PaperClipOutlined,
    PlusCircleOutlined,
    PrinterOutlined,
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
    type VoucherListItemDto,
    type VoucherTemplateDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { amountToChineseUppercase } from "@/lib/amount-uppercase"
import { toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { PrintVoucherSheet } from "@/pages/closing/print-sheet"
import { useKeepAliveActive } from "@/components/layout/dashboard/route-tabs-context"

/* ================= 常量 ================= */

/** 金额位（亿 → 分），合计 11 位，与纸质凭证的金额栏对应。 */
const DIGITS = ["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"]

/** 单个金额位宽度（px）；借方 / 贷方各 11 位，组宽 = 11 × 22 = 242。 */
const CELL_W = 22
const GROUP_W = CELL_W * DIGITS.length

/** 金额列表头：标题 + 11 位分位刻度（原表两行表头合并进一个单元格）。 */
function AmountHeaderCell({ title, prefix }: { title: string; prefix: string }) {
    return (
        <div className="py-1">
            <div>{title}</div>
            <div className="flex border-t border-[#e6eaf2]">
                {DIGITS.map((d, i) => (
                    <span
                        key={`${prefix}${i}`}
                        className="h-5 text-center text-[10px] leading-5 text-slate-400"
                        style={{ width: CELL_W }}
                    >
                        {d}
                    </span>
                ))}
            </div>
        </div>
    )
}

/** 元位分界线：第 8 条竖线（亿 千 百 十 万 千 百 十 | 元 角 分）用红色，方便对位到分。 */
const YUAN_X = CELL_W * 8

/** 分录行：金额以字符串保存，便于「12.」这类半成品输入。 */
interface EntryRow {
    key: number
    summary: string
    accountId: string
    debit: string
    credit: string
}

const blankRow = (key: number): EntryRow => ({ key, summary: "", accountId: "", debit: "", credit: "" })
const blankRows = () => [0, 1, 2, 3, 4].map(blankRow)

/** 金额文本 → 数值（空串 / 非法输入按 0）。 */
function toAmount(text: string): number {
    const n = Number(text)
    return Number.isFinite(n) && text.trim() !== "" ? n : 0
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

/** 金额栏底纹：11 等分竖线 + 元位红色分界线。 */
const AMOUNT_GRID_BG: CSSProperties = {
    backgroundImage: [
        `repeating-linear-gradient(to right, #e6eaf2 0 1px, transparent 1px ${CELL_W}px)`,
        `linear-gradient(to right, transparent ${YUAN_X - 1}px, #f0a99e ${YUAN_X - 1}px, #f0a99e ${YUAN_X}px, transparent ${YUAN_X}px)`,
    ].join(", "),
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

function printShortcuts() {
    const rows = SHORTCUTS.map(
        ([a, b, c, d]) =>
            `<tr><td>${a}</td><td class="k">${b}</td><td>${c}</td><td class="k">${d}</td></tr>`,
    ).join("")
    const html = `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><title>凭证快捷键</title>
<style>
body{font-family:"Microsoft YaHei",sans-serif;color:#333;padding:24px}
h1{font-size:16px;text-align:center;margin:0 0 16px}
table{width:100%;border-collapse:collapse;font-size:13px}
td{border:1px solid #d9d9d9;padding:8px 12px}
.k{text-align:center;font-family:Consolas,monospace;color:#2f6bff}
</style></head><body><h1>凭证快捷键</h1><table>${rows}</table></body></html>`
    const url = URL.createObjectURL(new Blob([html], { type: "text/html;charset=utf-8" }))
    const win = window.open(url, "_blank")
    if (!win) return
    setTimeout(() => {
        win.print()
        URL.revokeObjectURL(url)
    }, 400)
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
    onChange,
    onFocus,
}: {
    value: string
    accounts: AccountDto[]
    balances: Record<string, number>
    accountSetId: string
    onChange: (value: string) => void
    onFocus?: () => void
}) {
    const navigate = useNavigate()
    const [category, setCategory] = useState(0)

    const options = useMemo(
        () =>
            accounts
                .filter((a) => !category || a.category === category)
                .map((a) => ({
                    value: a.id ?? "",
                    label: `${a.code ?? ""} ${a.name ?? ""}`,
                    search: `${a.code ?? ""} ${a.name ?? ""} ${a.mnemonic ?? ""}`.toLowerCase(),
                    balance: balances[a.code ?? ""] ?? 0,
                })),
        [accounts, category, balances],
    )

    return (
        <Select
            variant="borderless"
            className="w-full"
            value={value || undefined}
            options={options}
            placeholder="请输入科目名称/科目编码/科目名称拼音首字母快速搜索"
            showSearch
            optionFilterProp="search"
            suffixIcon={null}
            popupMatchSelectWidth={420}
            onFocus={onFocus}
            onChange={(v) => onChange(v ?? "")}
            style={{ height: 44, fontSize: 13 }}
            optionRender={(option) => (
                <div className="flex items-center justify-between gap-4">
                    <span>{option.data.label}</span>
                    {Math.abs(option.data.balance) >= 0.005 && (
                        <span className="text-[12px] tabular-nums text-slate-400">
                            {option.data.balance.toFixed(2)}
                        </span>
                    )}
                </div>
            )}
            popupRender={(menu) => (
                <div>
                    <div
                        className="flex items-center gap-4 border-b border-slate-100 px-3 py-2 text-[13px]"
                        onMouseDown={(e) => e.preventDefault()}
                    >
                        {ACCOUNT_CATEGORIES.map((c) => (
                            <button
                                key={c.key}
                                type="button"
                                className={cn(
                                    "border-b-2 border-transparent pb-0.5",
                                    category === c.key
                                        ? "border-[#2f6bff] font-medium text-[#2f6bff]"
                                        : "text-slate-600 hover:text-[#2f6bff]",
                                )}
                                onClick={() => setCategory(c.key)}
                            >
                                {c.label}
                            </button>
                        ))}
                    </div>
                    {menu}
                    <div className="border-t border-slate-100 py-1 text-center" onMouseDown={(e) => e.preventDefault()}>
                        <Button
                            type="link"
                            onClick={() => navigate(`/${accountSetId}/settings/accounts`)}
                        >
                            + 点击添加
                        </Button>
                    </div>
                </div>
            )}
        />
    )
}

/* ================= 页面 ================= */

export default function VoucherNewPage() {
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
    const [printVoucherId, setPrintVoucherId] = useState<string | null>(null)
    const [activeRow, setActiveRow] = useState(0)
    /** 当前聚焦的金额格（`行key:debit|credit`），用于显示 0.00 与聚焦描边。 */
    const [focusedCell, setFocusedCell] = useState<string | null>(null)
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
    /** 录入选项（本机偏好，存 localStorage）。 */
    const [optionsOpen, setOptionsOpen] = useState(false)
    const [optFillGap, setOptFillGap] = useState(() => localStorage.getItem("kuaiji.voucher.fillGap") === "1")
    const [optFollowDirection, setOptFollowDirection] = useState(() => localStorage.getItem("kuaiji.voucher.followDirection") !== "0")

    /* 模板 / 草稿 */
    const [templates, setTemplates] = useState<VoucherTemplateDto[]>([])
    const [templateSaveOpen, setTemplateSaveOpen] = useState(false)
    const [templateName, setTemplateName] = useState("")
    const [templateType, setTemplateType] = useState(1)
    const [templateLoadOpen, setTemplateLoadOpen] = useState(false)
    const [templateId, setTemplateId] = useState<string>("")
    const [draftOpen, setDraftOpen] = useState(false)
    const [draftId, setDraftId] = useState<string>("")

    const keySeq = useRef(4)
    const nextKey = () => ++keySeq.current
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

    const clearHistory = () => {
        historyRef.current = { past: [], future: [] }
    }

    useEffect(() => {
        setWord((prev) => (prev && words.includes(prev) ? prev : words[0]))
    }, [words])

    useEffect(() => {
        setDate(defaultVoucherDate(period))
    }, [period])

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
        return { debit, credit, balanced: debit > 0 && Math.abs(debit - credit) < 0.005 }
    }, [rows])

    const patchRow = (key: number, patch: Partial<EntryRow>) =>
        commitRows((prev) => prev.map((r) => (r.key === key ? { ...r, ...patch } : r)))

    const accountsById = useMemo(() => new Map(accounts.map((a) => [a.id ?? "", a])), [accounts])

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

    const loadVoucher = async (id: string) => {
        if (!token) return
        setLoading(true)
        setMetaError(null)
        try {
            const detail: VoucherDetailDto = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdGet({
                accountSetId,
                id,
            })
            setLoadedId(detail.id ?? null)
            setWord(detail.word ?? word)
            setNumber(detail.number ?? null)
            setDate(detail.date ? toISODate(detail.date) : date)
            setAttachmentCount(String(detail.attachmentCount ?? ""))
            setRemark(detail.remark ?? "")
            setRemarkOpen(!!detail.remark)
            void loadAttachments(detail.id ?? "")
            const mapped = (detail.entries ?? []).map((e) => ({
                key: nextKey(),
                summary: e.summary ?? "",
                accountId: e.accountId ?? "",
                debit: e.debit ? String(e.debit) : "",
                credit: e.credit ? String(e.credit) : "",
            }))
            clearHistory()
            setRows(mapped.length > 0 ? mapped : blankRows())
            setActiveRow(0)
        } catch (e) {
            setMetaError(await apiErrorMessage(e, "加载凭证失败"))
        } finally {
            setLoading(false)
        }
    }

    /** 回到空白新增态。 */
    const resetForm = () => {
        clearHistory()
        setLoadedId(null)
        setRows(blankRows())
        setAttachmentCount("")
        setRemark("")
        setRemarkOpen(false)
        setAttachments([])
        setActiveRow(0)
    }

    /* ---------- 保存 ---------- */

    const save = async (keepEditing: boolean, successText = "保存成功") => {
        if (!token || !accountSetId) return
        if (!word || !date) {
            message.warning("凭证字或记账日期尚未就绪，请稍候再试")
            return
        }
        const entries = rows
            .filter((r) => r.accountId && (toAmount(r.debit) !== 0 || toAmount(r.credit) !== 0))
            .map((r) => ({
                summary: r.summary || "（无摘要）",
                accountId: r.accountId,
                debit: toAmount(r.debit),
                credit: toAmount(r.credit),
            }))
        if (entries.length === 0) {
            message.warning("请先录入分录（选择科目并填写金额）")
            return
        }
        if (!totals.balanced) {
            message.error("借贷不平衡，不能保存")
            return
        }
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
                await api.accountsetsAccountSetIdVouchersIdPut({
                    accountSetId,
                    id: loadedId,
                    updateVoucherRequest: payload,
                })
            } else {
                const created = await api.accountsetsAccountSetIdVouchersPost({
                    accountSetId,
                    createVoucherRequest: { word, number: number ?? undefined, ...payload },
                })
                if (!keepEditing && created?.id) {
                    setLoadedId(created.id)
                    setNumber(created.number ?? number)
                }
            }
            message.success(successText)
            if (keepEditing) resetForm()
            void reloadMeta()
        } catch (e) {
            setMetaError(await apiErrorMessage(e, "保存失败"))
        } finally {
            setSaving(false)
        }
    }

    /* ---------- 模板 / 草稿 ---------- */

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
            })),
        )
        setActiveRow(0)
        setTemplateLoadOpen(false)
        message.success(`已引入模板「${template.name}」`)
    }

    /** 草稿：反过账退回或导入不平衡的凭证（保存即入账，正常制单不会留下草稿）。 */
    const drafts = useMemo(() => (allVouchers ?? []).filter((v) => v.status === 1), [allVouchers])

    const openDraft = () => {
        if (drafts.length === 0) {
            message.info("当前没有草稿凭证")
            return
        }
        setDraftId("")
        setDraftOpen(true)
    }

    const applyDraft = async () => {
        if (!draftId) {
            message.warning("请选择草稿凭证")
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
                e.preventDefault()
                void save(true)
            } else if (e.key === "F3") {
                e.preventDefault()
                copyAbove()
            } else if (e.key === "F4") {
                e.preventDefault()
                attachmentRef.current?.focus()
            } else if (e.key === "PageUp") {
                e.preventDefault()
                void loadSibling(-1)
            } else if (e.key === "PageDown") {
                e.preventDefault()
                void loadSibling(1)
            } else if (e.altKey && key === "n") {
                e.preventDefault()
                resetForm()
                message.info("已新建空白凭证")
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

    const moreItems: MenuProps["items"] = [
        { key: "save-template", label: "保存为模板" },
        { key: "load-template", label: "从模板中加载" },
        { key: "load-draft", label: "从草稿中加载" },
        { key: "clear", label: "清空凭证" },
        { key: "options", label: "选项" },
    ]

    const onMore: MenuProps["onClick"] = ({ key }) => {
        if (key === "save-template") openTemplateSave()
        else if (key === "load-template") openTemplateLoad()
        else if (key === "load-draft") openDraft()
        else if (key === "clear") {
            resetForm()
            message.success("已清空凭证")
        } else if (key === "options") setOptionsOpen(true)
    }

    const deleteLoaded = async () => {
        if (!token || !loadedId) return
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdDelete({ accountSetId, id: loadedId })
            message.success("凭证已删除")
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
    const loadAttachments = async (voucherId: string) => {
        if (!token || !accountSetId || !voucherId) {
            setAttachments([])
            return
        }
        try {
            const list = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdAttachmentsGet({
                accountSetId,
                id: voucherId,
            })
            setAttachments(list ?? [])
        } catch {
            setAttachments([])
        }
    }

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
        localStorage.setItem("kuaiji.voucher.fillGap", next.fillGap ? "1" : "0")
        localStorage.setItem("kuaiji.voucher.followDirection", next.followDirection ? "1" : "0")
        message.success("选项已保存")
    }

    /* ---------- 渲染 ---------- */

    /** 金额单元格：底层铺分位网格线，上层是透明输入框（聚焦时显示蓝色选框）。 */
    const renderAmountCell = (r: EntryRow, idx: number, side: "debit" | "credit") => {
        const focused = focusedCell === `${r.key}:${side}`
        return (
            <div className="relative">
                <div aria-hidden className="pointer-events-none absolute inset-0" style={AMOUNT_GRID_BG} />
                <input
                    data-nav={side}
                    data-col={side}
                    value={side === "debit" ? r.debit : r.credit}
                    placeholder={focused ? "0.00" : ""}
                    onFocus={() => {
                        setActiveRow(idx ?? 0)
                        setFocusedCell(`${r.key}:${side}`)
                        setFocusInfo({ rowKey: r.key, col: side })
                    }}
                    onBlur={() => setFocusedCell(null)}
                    onDoubleClick={() => autoBalance(r.key, side)}
                    onKeyDown={onCellKeyDown}
                    onChange={(e) =>
                        patchRow(
                            r.key,
                            side === "debit"
                                ? { debit: e.target.value.replace(/[^\d.]/g, ""), credit: "" }
                                : { credit: e.target.value.replace(/[^\d.]/g, ""), debit: "" },
                        )
                    }
                    className="relative w-full bg-transparent px-2 text-right text-[13px] tabular-nums text-slate-700 outline-none placeholder:text-slate-300"
                    style={{ height: 44, boxShadow: focused ? "inset 0 0 0 1px #2f6bff" : undefined }}
                />
            </div>
        )
    }

    return (
        <div ref={sheetRef} className="flex flex-col bg-white">
            {/* 工具条 */}
            <div className="relative flex min-h-[56px] items-center border-b border-slate-100 px-4 py-2">
                <div className="flex flex-1 items-center justify-center gap-3">
                    <Button type="primary" disabled={!canSave} loading={saving} onClick={() => void save(true)}>
                        入账并新增
                    </Button>
                    <Button type="primary" ghost disabled={!canSave} loading={saving} onClick={() => void save(false)}>
                        保存入账
                    </Button>
                    <Button
                        icon={<PrinterOutlined />}
                        onClick={() =>
                            loadedId ? setPrintVoucherId(loadedId) : message.info("请先保存凭证再打印")
                        }
                    >
                        打印
                    </Button>
                    <Dropdown menu={{ items: moreItems, onClick: onMore }} trigger={["click"]}>
                        <Button>更多</Button>
                    </Dropdown>
                </div>
                <div className="absolute right-4 flex items-center gap-5 text-[13px] text-slate-600">
                    <Button
                        type="text"
                        className="!text-slate-600 hover:!text-[#2f6bff]"
                        icon={<ControlOutlined />}
                        onClick={() => setShortcutOpen(true)}
                    >
                        快捷键
                    </Button>
                    <Button
                        type="text"
                        className="!text-slate-600 hover:!text-[#2f6bff]"
                        icon={<ArrowLeftOutlined />}
                        iconPosition="end"
                        onClick={() => void loadSibling(-1)}
                    >
                        上一页
                    </Button>
                    <Button
                        type="text"
                        className="!text-slate-600 hover:!text-[#2f6bff]"
                        icon={<ArrowRightOutlined />}
                        iconPosition="end"
                        onClick={() => void loadSibling(1)}
                    >
                        下一页
                    </Button>
                </div>
            </div>

            {metaError && (
                <div className="mx-auto w-[1080px] py-1 text-[13px] text-[#ef4444]">{metaError}</div>
            )}

            <div className="flex-1 overflow-auto">
                <div
                    className="mx-auto w-[1080px] pt-4 pb-6"
                    style={{ zoom: zoom ? 1.25 : 1 }}
                >
                    {/* 凭证头 */}
                    <div className="flex flex-wrap items-center gap-2 pb-3">
                        <Select
                            value={word}
                            onChange={setWord}
                            options={words.map((w) => ({ value: w, label: w }))}
                            style={{ width: 84 }}
                            disabled={!!loadedId}
                        />
                        <InputNumber
                            value={number}
                            onChange={(v) => setNumber(typeof v === "number" ? v : null)}
                            min={1}
                            precision={0}
                            controls={false}
                            style={{ width: 64 }}
                            disabled={!!loadedId}
                        />
                        <span className="text-[13px] text-slate-700">号</span>
                        <span className="ml-2 text-[13px] text-slate-700">日期：</span>
                        <span ref={dateRef} className="inline-flex">
                            <DatePicker
                                value={date ? dayjs(date) : null}
                                onChange={(v) => setDate(v ? v.format("YYYY-MM-DD") : "")}
                                allowClear={false}
                                style={{ width: 140 }}
                            />
                        </span>
                        <span className="ml-3 text-[13px] text-[#92640b]">亲，按回车键，能快速选择单元格哟！</span>
                        <div className="ml-auto flex items-center gap-2 text-[13px] text-slate-700">
                            附单据
                            <Input
                                ref={attachmentRef}
                                value={attachmentCount}
                                onChange={(e) => setAttachmentCount(e.target.value.replace(/\D/g, ""))}
                                style={{ width: 56, textAlign: "center" }}
                            />
                            张
                            <Button
                                type="link"
                                className="hover:!underline"
                                icon={<PaperClipOutlined />}
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
                                    <span className="rounded-full bg-[#2f6bff] px-1.5 text-[11px] leading-4 text-white">
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
                                    className="hover:!underline"
                                    title={remark || "添加凭证备注"}
                                    onClick={() => setRemarkOpen(true)}
                                >
                                    备注 ⓘ
                                </Button>
                            )}
                            <Button
                                type="text"
                                className="!text-slate-600 hover:!text-[#2f6bff]"
                                icon={zoom ? <ZoomOutOutlined /> : <ZoomInOutlined />}
                                onClick={() => setZoom((v) => !v)}
                            >
                                {zoom ? "缩小" : "放大"}
                            </Button>
                            <Button
                                type="text"
                                className="!text-slate-600 hover:!text-[#2f6bff]"
                                icon={fullscreen ? <FullscreenExitOutlined /> : <FullscreenOutlined />}
                                onClick={() => void toggleFullscreen()}
                            >
                                {fullscreen ? "退出全屏" : "全屏"}
                            </Button>
                        </div>
                    </div>

                    {/* 分录表格 */}
                    <div ref={gridRef}>
                        <Table<EntryRow>
                            size="small"
                            pagination={false}
                            rowKey={(r) => r.key}
                            dataSource={rows}
                            onRow={(_, idx) => ({ onMouseDown: () => setActiveRow(idx ?? 0) })}
                            rowClassName={(_, idx) =>
                                cn("group", idx === activeRow ? "bg-[#f5f9ff]" : "bg-white")
                            }
                            columns={[
                                {
                                    title: "",
                                    width: 22,
                                    onCell: () => ({ style: { padding: 0, border: 0 } }),
                                    render: (_, _r, idx) => {
                                        const active = idx === activeRow
                                        return (
                                            <div
                                                className={cn(
                                                    "flex flex-col items-center gap-0.5",
                                                    active ? "opacity-100" : "opacity-0 group-hover:opacity-100",
                                                )}
                                            >
                                                <Button
                                                    type="text"
                                                    title="在下方插入一行"
                                                    className="!text-[#22c55e]"
                                                    icon={<PlusCircleOutlined />}
                                                    onClick={() => insertBelow(idx ?? 0)}
                                                />
                                                <Button
                                                    type="text"
                                                    title="复制本行"
                                                    className="!text-[#2f6bff]"
                                                    icon={<CopyOutlined />}
                                                    onClick={() => duplicateRow(idx ?? 0)}
                                                />
                                            </div>
                                        )
                                    },
                                },
                                {
                                    title: "序号",
                                    width: 44,
                                    onCell: () => ({ style: { padding: 0 } }),
                                    render: (_, _r, idx) => (
                                        <span className="text-center text-[13px] text-slate-500 tabular-nums">
                                            {(idx ?? 0) + 1}
                                        </span>
                                    ),
                                },
                                {
                                    title: "摘要",
                                    width: 200,
                                    onCell: () => ({ style: { padding: 0 }, className: "relative" }),
                                    render: (_, r, idx) => (
                                        <>
                                            <Input
                                                data-nav="summary"
                                                data-col="summary"
                                                variant="borderless"
                                                value={r.summary}
                                                placeholder="摘要"
                                                onFocus={() => {
                                                    setActiveRow(idx ?? 0)
                                                    setFocusInfo({ rowKey: r.key, col: "summary" })
                                                    setSummaryPickRow(r.key)
                                                }}
                                                onBlur={() => {
                                                    // 延迟收起，留出点击候选的时间
                                                    window.setTimeout(() => setSummaryPickRow(null), 150)
                                                }}
                                                onKeyDown={onCellKeyDown}
                                                onChange={(e) => patchRow(r.key, { summary: e.target.value })}
                                                style={{ height: 44, fontSize: 13 }}
                                            />
                                            {summaryPickRow === r.key && summaryCandidates.length > 0 && (
                                                <div className="absolute left-0 top-full z-20 max-h-[220px] w-[300px] overflow-auto rounded border border-slate-200 bg-white py-1 shadow-lg">
                                                    {summaryCandidates.map((item) => (
                                                        <button
                                                            key={item}
                                                            type="button"
                                                            className="block w-full px-3 py-1.5 text-left text-[13px] text-slate-700 hover:bg-[#f2f6ff]"
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
                                    title: "会计科目",
                                    onCell: () => ({ style: { padding: 0 } }),
                                    render: (_, r, idx) => (
                                        <div data-nav="account" data-col="account" onKeyDown={onCellKeyDown}>
                                            <AccountSelect
                                                value={r.accountId}
                                                accounts={accounts}
                                                balances={balances}
                                                accountSetId={accountSetId}
                                                onFocus={() => {
                                                    setActiveRow(idx ?? 0)
                                                    setFocusInfo({ rowKey: r.key, col: "account" })
                                                }}
                                                onChange={(v) => patchRow(r.key, { accountId: v })}
                                            />
                                        </div>
                                    ),
                                },
                                {
                                    title: <AmountHeaderCell title="借方金额" prefix="jd" />,
                                    width: GROUP_W,
                                    onCell: () => ({ style: { padding: 0 } }),
                                    render: (_, r, idx) => renderAmountCell(r, idx ?? 0, "debit"),
                                },
                                {
                                    title: <AmountHeaderCell title="贷方金额" prefix="cd" />,
                                    width: GROUP_W,
                                    onCell: () => ({ style: { padding: 0 } }),
                                    render: (_, r, idx) => renderAmountCell(r, idx ?? 0, "credit"),
                                },
                                {
                                    title: "",
                                    width: 28,
                                    onCell: () => ({ style: { padding: 0, border: 0 } }),
                                    render: (_, _r, idx) => {
                                        const active = idx === activeRow
                                        return (
                                            <Button
                                                type="text"
                                                title="删除本行"
                                                disabled={rows.length <= 1}
                                                className={cn(
                                                    rows.length <= 1
                                                        ? "!text-slate-200"
                                                        : cn(
                                                              "!text-[#ef4444]",
                                                              active
                                                                  ? "opacity-100"
                                                                  : "opacity-0 group-hover:opacity-100",
                                                          ),
                                                )}
                                                onClick={() => removeRow(idx ?? 0)}
                                                icon={<CloseOutlined />}
                                            />
                                        )
                                    },
                                },
                            ]}
                            summary={() => (
                                <Table.Summary.Row>
                                    <Table.Summary.Cell index={0} className="!border-0" />
                                    <Table.Summary.Cell index={1} colSpan={3} className="px-3 text-[13px] font-medium text-slate-700">
                                        合计：{amountToChineseUppercase(totals.debit)}
                                    </Table.Summary.Cell>
                                    <Table.Summary.Cell index={4} align="right" className="px-2 text-[13px] font-medium tabular-nums text-slate-700">
                                        {totals.debit > 0 ? totals.debit.toFixed(2) : ""}
                                    </Table.Summary.Cell>
                                    <Table.Summary.Cell index={5} align="right" className="px-2 text-[13px] font-medium tabular-nums text-slate-700">
                                        {totals.credit > 0 ? totals.credit.toFixed(2) : ""}
                                    </Table.Summary.Cell>
                                    <Table.Summary.Cell index={6} className="!border-0" />
                                </Table.Summary.Row>
                            )}
                        />
                    </div>

                    {/* 制单人 */}
                    <div className="mt-4 flex items-center justify-between text-[13px] text-slate-500">
                        <span className="flex items-center gap-1.5">
                            <EditOutlined />
                            制单人：{user?.displayName || user?.username || "-"}
                        </span>
                        {loadedId && <span className="text-slate-400">当前为已保存凭证，保存将更新原凭证并入账</span>}
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
                styles={{ body: { padding: 0 } }}
            >
                <div className="pt-2 text-[13px]">
                    <div className="border border-slate-200">
                        <div className="bg-[#eef4ff] py-2 text-center text-[14px] font-medium text-[#2f6bff]">
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
                                    render: (_, r) => <span className="font-mono text-[12px] text-slate-600">{r[1]}</span>,
                                },
                                { title: "操作", width: "34%", render: (_, r) => r[2] },
                                {
                                    title: "快捷键",
                                    width: "16%",
                                    align: "center",
                                    render: (_, r) => <span className="font-mono text-[12px] text-slate-600">{r[3]}</span>,
                                },
                            ]}
                            dataSource={SHORTCUTS}
                        />
                    </div>
                    <div className="flex justify-center gap-4 py-4">
                        <Button icon={<DownloadOutlined />} onClick={downloadShortcuts}>
                            下载
                        </Button>
                        <Button type="primary" onClick={printShortcuts}>
                            打印
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
                        <span className="w-16 shrink-0 text-slate-600">模板名称</span>
                        <Input
                            value={templateName}
                            onChange={(e) => setTemplateName(e.target.value)}
                            maxLength={40}
                            placeholder="例如：报销办公费"
                        />
                    </label>
                    <label className="flex items-center gap-2">
                        <span className="w-16 shrink-0 text-slate-600">模板类型</span>
                        <Select
                            className="flex-1"
                            value={templateType}
                            onChange={setTemplateType}
                            options={TEMPLATE_TYPES}
                        />
                    </label>
                    <p className="text-[12px] text-slate-400">
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
                    <p className="py-8 text-center text-[13px] text-slate-400">
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
                                        ? "border-[#2f6bff] bg-[#f5f9ff]"
                                        : "border-slate-200 hover:border-[#2f6bff]",
                                )}
                                onClick={() => setTemplateId(t.id ?? "")}
                            >
                                <span className="text-slate-700">
                                    {t.name}
                                    <span className="ml-2 text-[12px] text-slate-400">{t.typeText}</span>
                                </span>
                                <span className="text-[12px] text-slate-400">
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
                title="从草稿中加载"
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
                                    ? "border-[#2f6bff] bg-[#f5f9ff]"
                                    : "border-slate-200 hover:border-[#2f6bff]",
                            )}
                            onClick={() => setDraftId(v.id ?? "")}
                        >
                            <span className="font-medium text-slate-700">{v.voucherNo}</span>
                            <span className="text-slate-500">{toISODate(v.date)}</span>
                            <span className="text-slate-400">{v.entryCount} 条分录</span>
                            <span className="ml-auto tabular-nums text-slate-600">
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
                        <span className="text-[12px] text-slate-400">支持 PDF / 图片 / Office 文档，单个文件不超过 10MB</span>
                        <Button onClick={() => setAttachOpen(false)}>关闭</Button>
                    </div>
                }
            >
                <div className="flex flex-col gap-3">
                    <label className="flex h-9 w-fit cursor-pointer items-center gap-1 rounded border border-dashed border-[#2f6bff] px-3 text-[13px] text-[#2f6bff] hover:bg-[#f2f6ff]">
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
                        <div className="flex flex-col divide-y divide-slate-100 rounded border border-slate-200">
                            {attachments.map((a) => (
                                <div key={a.id} className="flex items-center gap-2 px-3 py-2 text-[13px]">
                                    <FileTextOutlined className="text-slate-400" />
                                    <Button
                                        type="text"
                                        className="max-w-[280px] !justify-start !px-0 text-left text-slate-700 hover:!text-[#2f6bff]"
                                        title={a.fileName}
                                        onClick={() => void openAttachment(a.id ?? "", a.fileName ?? "附件", false)}
                                    >
                                        <span className="truncate">{a.fileName}</span>
                                    </Button>
                                    <span className="ml-auto text-[12px] text-slate-400">
                                        {formatBytes(a.sizeBytes ?? 0)}
                                    </span>
                                    <Button
                                        type="text"
                                        title="下载"
                                        className="!text-slate-400 hover:!text-[#2f6bff]"
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
                                            className="!text-slate-400 hover:!text-[#ef4444]"
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
                <div className="flex flex-col gap-4 py-2 text-[13px] text-slate-700">
                    <div className="flex items-center justify-between">
                        <div>
                            <div>自动填补凭证断号</div>
                            <div className="text-[12px] text-slate-400">新增凭证时优先填补已删除凭证留下的空号</div>
                        </div>
                        <Switch checked={optFillGap} onChange={setOptFillGap} />
                    </div>
                    <div className="flex items-center justify-between">
                        <div>
                            <div>按科目方向定位借贷方</div>
                            <div className="text-[12px] text-slate-400">选完科目后，光标自动跳到该科目的正常余额方向</div>
                        </div>
                        <Switch checked={optFollowDirection} onChange={setOptFollowDirection} />
                    </div>
                </div>
            </Modal>
        </div>
    )
}
