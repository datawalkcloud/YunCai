/**
 * 资金报表（对应柠檬云「资金 → 资金报表」）。
 *
 * 六个页签：按账户汇总 / 按收支类别汇总 / 按往来单位汇总 / 按项目汇总 / 按部门汇总 / 按组合汇总。
 * 每个页签按柠檬云的分段结构出表：账户维度分「现金账户｜银行账户」，收支类别维度分「收入类别｜支出类别」，
 * 每段自带小计行（人民币合计 / 收入合计 / 支出合计 …），多段时在最后补一行「合计」。
 * 项目 / 部门维度取流水上登记的值（「批量操作 - 指定项目/指定部门」写入），未登记的归入「未填写」；
 * 组合维度为 收支类别 × 次类别，次类别可选 项目 / 部门 / 往来单位 / 收支类别，可按主类别筛选并显示主类别合计。
 * 工具栏：日期区间 + 查询 + 账户筛选 + 维度值搜索 / 打印、导出、扫码分享、报表订阅推送、刷新。
 */
import { useCallback, useEffect, useMemo, useState } from "react"
import type { ReactNode } from "react"
import {
    App,
    Button,
    Card,
    Checkbox,
    DatePicker,
    Input,
    Modal,
    Pagination,
    QRCode,
    Select,
    Space,
    Table,
    Tabs,
    Tooltip,
    Typography,
} from "antd"
import type { TableColumnsType } from "antd"
import { ReloadOutlined, SearchOutlined } from "@ant-design/icons"
import dayjs, { type Dayjs } from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    FundReportsApi,
    JournalsApi,
    type FundReportDto,
    type FundReportRowDto,
    type FundReportSectionDto,
    type JournalListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

/** 报表维度（与后端 FundReportDimensions 对齐）。 */
type Dimension = "account" | "category" | "counterparty" | "project" | "department" | "combo"

/** 页签顺序与文案（与柠檬云一致）。 */
const TABS: { key: Dimension; label: string }[] = [
    { key: "account", label: "按账户汇总" },
    { key: "category", label: "按收支类别汇总" },
    { key: "counterparty", label: "按往来单位汇总" },
    { key: "project", label: "按项目汇总" },
    { key: "department", label: "按部门汇总" },
    { key: "combo", label: "按组合汇总" },
]

/** 组合维度的次类别（与后端 FundReportService.ComboSecondaries 对齐）。 */
type Secondary = "project" | "department" | "counterparty" | "category"

const SECONDARIES: { key: Secondary; label: string }[] = [
    { key: "project", label: "项目" },
    { key: "department", label: "部门" },
    { key: "counterparty", label: "往来单位" },
    { key: "category", label: "收支类别" },
]

/** 打印表头里名称列的文案（分段名已经作为该表的标题行）。 */
const PRINT_NAME_LABEL: Record<Dimension, string> = {
    account: "账户",
    category: "收支类别",
    counterparty: "往来单位",
    project: "项目",
    department: "部门",
    combo: "收支类别",
}

/** 单页条数候选（与柠檬云分页器一致）。 */
const PAGE_SIZE_OPTIONS = [20, 50, 100, 200]

const SUBSCRIPTION_KEY = "kuaiji.fund-report.subscription"

/** 金额：0 显示为 0，其余保留两位小数（与柠檬云金额列口径一致）。 */
function money(value: number | null | undefined): string {
    if (value === null || value === undefined) return ""
    if (value === 0) return "0"
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 金额：0 显示为空（期初 / 收入 / 支出列，无发生额时留白）。 */
function moneyBlankZero(value: number | null | undefined): string {
    return value ? money(value) : ""
}

/** 按行重算小计（组合维度的主类别合计、搜索后的段合计都用这个口径）。 */
function sumRows(label: string, rows: FundReportRowDto[]): FundReportRowDto {
    const sum = (pick: (row: FundReportRowDto) => number) => rows.reduce((total, row) => total + pick(row), 0)
    return {
        name: label,
        totalIncome: sum((r) => r.totalIncome ?? 0),
        totalExpense: sum((r) => r.totalExpense ?? 0),
        incomeCount: sum((r) => r.incomeCount ?? 0),
        expenseCount: sum((r) => r.expenseCount ?? 0),
    }
}

function countText(value: number | null | undefined): string {
    return String(value ?? 0)
}

const AMOUNT_RIGHT = "whitespace-nowrap text-right tabular-nums"
const COUNT_LEFT = "whitespace-nowrap text-left tabular-nums"

/** 打印用 HTML 转义。 */
function escHtml(text: string): string {
    return text.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c] ?? c)
}

/** 打印窗口的金额口径：0 显示为 0（与页面一致）。 */
function printMoney(value: number | null | undefined): string {
    if (value === null || value === undefined) return ""
    if (value === 0) return "0"
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 组装打印用的整页 HTML（标题 + 期间 + 分段表 + 合计 + 提示）。 */
function buildPrintHtml(report: FundReportDto, title: string, accountSetName: string): string {
    const dim = report.dimension ?? "account"
    const sections = report.sections ?? []
    /** 仅账户（科目编码）与收支类别（序号）有编码列，其余维度的值是流水上登记的自由文本。 */
    const withCode = dim === "account" || dim === "category"
    const withOpening = dim === "account"
    const isCombo = dim === "combo"
    const nameLabel = isCombo ? (report.primaryText ?? PRINT_NAME_LABEL.combo) : (PRINT_NAME_LABEL as Record<string, string>)[dim] ?? "名称"
    const secondaryLabel = report.secondaryText ?? "项目"
    const headCells = [
        ...(withCode ? ["编码"] : []),
        nameLabel,
        ...(isCombo ? [secondaryLabel] : []),
        ...(withOpening ? ["期初余额"] : []),
        "收入总额",
        "支出总额",
        ...(withOpening ? ["余额"] : []),
        "收入笔数",
        "支出笔数",
    ]

    const cellsOf = (row: FundReportRowDto | undefined): string[] => {
        if (!row) return []
        const text = (v: string | null | undefined) => `<td>${escHtml(v ?? "")}</td>`
        const num = (v: number | null | undefined) => `<td class="num">${printMoney(v)}</td>`
        const cnt = (v: number | undefined) => `<td class="num">${v ?? 0}</td>`
        return [
            ...(withCode ? [text(row.code)] : []),
            text(row.name),
            ...(isCombo ? [text(row.secondaryName)] : []),
            ...(withOpening ? [num(row.openingBalance)] : []),
            num(row.totalIncome),
            num(row.totalExpense),
            ...(withOpening ? [num(row.balance)] : []),
            cnt(row.incomeCount),
            cnt(row.expenseCount),
        ]
    }

    const head = `<tr><th colspan="${headCells.length}">${escHtml(sections[0]?.name ?? "")}</th></tr><tr>${headCells.map((h) => `<th>${escHtml(h)}</th>`).join("")}</tr>`
    const body = sections
        .map((section) => {
            const rows = (section.rows ?? []).map((row) => `<tr>${cellsOf(row).join("")}</tr>`).join("")
            const sub = `<tr class="sub">${cellsOf(section.subtotal).join("")}</tr>`
            return `<table>${head}${rows}${sub}</table>`
        })
        .join("")
    const total =
        sections.length > 1 && report.total
            ? `<table><tr class="total">${cellsOf(report.total).join("")}</tr></table>`
            : ""
    const note = report.note ? `<p class="note">提示：${escHtml(report.note)}</p>` : ""
    const currency = report.currencyNote ? `<p class="note">${escHtml(report.currencyNote)}</p>` : ""
    const start = report.startDate ? new Date(report.startDate).toISOString().slice(0, 10) : ""
    const end = report.endDate ? new Date(report.endDate).toISOString().slice(0, 10) : ""

    return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><title>${escHtml(title)}</title>
<style>
    @page { size: A4 landscape; margin: 12mm; }
    * { box-sizing: border-box; }
    body { margin: 0; font-family: "Microsoft YaHei", SimSun, sans-serif; color: #111; }
    h1 { margin: 0 0 4px; font-size: 16pt; text-align: center; font-weight: 600; }
    .meta { display: flex; justify-content: space-between; font-size: 9pt; color: #444; margin-bottom: 8px; }
    table { width: 100%; border-collapse: collapse; font-size: 9pt; margin-bottom: 10px; }
    th, td { border: 1px solid #999; padding: 4px 6px; }
    th { background: #eef4ff; font-weight: 600; text-align: center; white-space: nowrap; }
    .num { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
    tr.sub td, tr.total td { background: #f8fafc; font-weight: 600; }
    .note { font-size: 9pt; color: #555; margin: 6px 0 0; }
</style></head><body>
<h1>${escHtml(title)}</h1>
<div class="meta"><span>账套：${escHtml(accountSetName)}</span><span>期间：${escHtml(start)} 至 ${escHtml(end)}</span></div>
${body}${total}${currency}${note}
</body></html>`
}

/** 打开打印窗口并自动唤起打印对话框。 */
function openPrintWindow(html: string): void {
    const win = window.open("", "_blank", "width=1180,height=760")
    if (!win) return
    win.document.open()
    win.document.write(html)
    win.document.close()
    win.focus()
    win.setTimeout(() => win.print(), 400)
}

/** 从地址栏还原维度与区间（扫码分享打开的链接可直接落到同一张报表）。 */
function readInitialState(): { dimension: Dimension; start: string; end: string } {
    const qs = new URLSearchParams(window.location.search)
    const dim = qs.get("dimension")
    const start = qs.get("start")
    const end = qs.get("end")
    const valid = TABS.some((t) => t.key === dim)
    return {
        dimension: valid ? (dim as Dimension) : "account",
        start: start && /^\d{4}-\d{2}-\d{2}$/.test(start) ? start : dayjs().startOf("month").format("YYYY-MM-DD"),
        end: end && /^\d{4}-\d{2}-\d{2}$/.test(end) ? end : dayjs().format("YYYY-MM-DD"),
    }
}

export default function FundReportPage() {
    const { message } = App.useApp()
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const accountSetName = accountSets.find((s) => s.id === accountSetId)?.name ?? ""

    const initial = useMemo(readInitialState, [])
    const [dimension, setDimension] = useState<Dimension>(initial.dimension)
    /** 已生效区间（点「查询」后提交）。 */
    const [range, setRange] = useState<{ start: string; end: string }>({ start: initial.start, end: initial.end })
    /** 日期控件上的待提交区间。 */
    const [draft, setDraft] = useState<{ start: string; end: string }>({ start: initial.start, end: initial.end })

    const [journalId, setJournalId] = useState<string>("")
    const [keyword, setKeyword] = useState("")
    const [showPrimarySubtotal, setShowPrimarySubtotal] = useState(false)
    const [secondary, setSecondary] = useState<Secondary>("project")
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(20)

    const [report, setReport] = useState<FundReportDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const [journals, setJournals] = useState<JournalListItemDto[]>([])

    const [helpOpen, setHelpOpen] = useState(false)
    const [shareOpen, setShareOpen] = useState(false)
    const [subscribeOpen, setSubscribeOpen] = useState(false)
    const [subscribe, setSubscribe] = useState<{ enabled: boolean; cycle: string; hour: string }>(() => {
        try {
            const raw = window.localStorage.getItem(SUBSCRIPTION_KEY)
            if (raw) return JSON.parse(raw) as { enabled: boolean; cycle: string; hour: string }
        } catch {
            // 本地配置损坏时回落到默认值
        }
        return { enabled: false, cycle: "每日", hour: "09:00" }
    })

    /* ---------------- 下拉数据（账户） ---------------- */

    useEffect(() => {
        if (!accountSetId) return
        void (async () => {
            try {
                const js = await new JournalsApi(apiConfig(token)).accountsetsAccountSetIdJournalsGet({ accountSetId })
                setJournals(js ?? [])
            } catch {
                // 下拉数据失败不阻断报表主体
            }
        })()
    }, [accountSetId, token])

    /* ---------------- 报表加载 ---------------- */

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new FundReportsApi(apiConfig(token)).accountsetsAccountSetIdFundReportsGet({
                accountSetId,
                dimension,
                startDate: new Date(range.start),
                endDate: new Date(range.end),
                journalId: journalId || undefined,
                secondary: dimension === "combo" ? secondary : undefined,
            })
            setReport(res)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资金报表失败，请重试。"))
            setReport(null)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, dimension, range, journalId, secondary])

    useEffect(() => {
        void load()
    }, [load])

    /* ---------------- 交互 ---------------- */

    const sections = useMemo(() => report?.sections ?? [], [report])
    const total = report?.total
    const singleSection = sections.length <= 1
    const dimensionLabel = TABS.find((t) => t.key === dimension)?.label ?? "资金报表"

    const applyQuery = () => {
        setRange({ ...draft })
        setPage(1)
        // 区间未变化时 load 不会因依赖变化重跑，这里显式再取一次。
        if (draft.start === range.start && draft.end === range.end) void load()
    }

    const changeDimension = (key: string) => {
        setDimension(key as Dimension)
        setPage(1)
        setKeyword("")
        setRange({ ...draft })
    }

    const exportReport = async () => {
        if (!accountSetId) return
        const qs = new URLSearchParams({ dimension, startDate: range.start, endDate: range.end })
        if (journalId) qs.set("journalId", journalId)
        if (dimension !== "account" && keyword.trim()) qs.set("keyword", keyword.trim())
        if (dimension === "combo") qs.set("secondary", secondary)
        try {
            const res = await fetch(`/api/accountsets/${accountSetId}/fund-reports/export?${qs.toString()}`, {
                headers: { Authorization: `Bearer ${token}` },
            })
            if (!res.ok) throw new Error(await apiErrorMessage(await res.json().catch(() => null), "导出失败，请重试。"))
            const blob = await res.blob()
            const url = URL.createObjectURL(blob)
            const anchor = document.createElement("a")
            anchor.href = url
            const disposition = res.headers.get("Content-Disposition") ?? ""
            const star = /filename\*=UTF-8''([^;\n]+)/i.exec(disposition)
            const plain = /filename="?([^";\n]+)"?/i.exec(disposition)
            anchor.download = star ? decodeURIComponent(star[1]) : plain ? plain[1] : `${dimensionLabel}.xlsx`
            document.body.appendChild(anchor)
            anchor.click()
            anchor.remove()
            URL.revokeObjectURL(url)
        } catch (err) {
            message.error(err instanceof Error ? err.message : "导出失败，请重试。")
        }
    }

    const saveSubscribe = () => {
        window.localStorage.setItem(SUBSCRIPTION_KEY, JSON.stringify(subscribe))
        setSubscribeOpen(false)
        message.success(subscribe.enabled ? `已开启报表订阅推送（${subscribe.cycle} ${subscribe.hour}）` : "已关闭报表订阅推送")
    }

    /** 各维度的名称列表头：组合用主类别名，其余用分段名（现金账户 / 收入类别 / 项目 …）。 */
    const nameLabel =
        dimension === "combo" ? (report?.primaryText ?? "收支类别") : dimension === "category" ? "收支类别" : (sections[0]?.name ?? "名称")
    const secondaryLabel = report?.secondaryText ?? "项目"

    /* ---------------- 单元格渲染 ---------------- */

    /** 按维度值名称本地过滤（与柠檬云搜索框行为一致；组合维度同时匹配主类别与次类别）。 */
    const visibleSections: FundReportSectionDto[] = useMemo(() => {
        const term = keyword.trim().toLowerCase()
        if (dimension === "account" || !term) return sections
        return sections.map((s) => {
            const rows = (s.rows ?? []).filter((r) => (r.name ?? "").toLowerCase().includes(term) || (r.secondaryName ?? "").toLowerCase().includes(term))
            // 小计与导出口径保持一致：导出是过滤后再汇总的，这里也必须按可见行重算。
            return { ...s, rows, subtotal: sumRows(s.subtotalLabel ?? "合计", rows) }
        })
    }, [sections, dimension, keyword])

    /** 合计行：搜索时同样只累计可见分段的小计。 */
    const totalRow = useMemo(() => {
        const term = keyword.trim().toLowerCase()
        if (dimension === "account" || !term) return total
        return sumRows(total?.name ?? "合计", visibleSections.flatMap((s) => s.rows ?? []))
    }, [total, visibleSections, dimension, keyword])

    /** 当前页的表体行；组合维度勾选「显示主类型合计」时，在主类别组末尾补一行小计。 */
    const rowsOf = (section: FundReportSectionDto): FundReportRowDto[] => {
        const all = section.rows ?? []
        // 多段维度不分页（整表一次列示），单段维度按当前页取。
        const start = singleSection ? (page - 1) * pageSize : 0
        const stop = singleSection ? Math.min(start + pageSize, all.length) : all.length
        if (dimension !== "combo" || !showPrimarySubtotal) return all.slice(start, stop)

        const out: FundReportRowDto[] = []
        let cursor = start
        while (cursor < stop) {
            const primary = all[cursor].name ?? ""
            let next = cursor
            while (next < all.length && (all[next].name ?? "") === primary) next += 1
            // 组被分页切断时不补小计，否则给出的是半段的合计。
            const startsHere = cursor === 0 || (all[cursor - 1].name ?? "") !== primary
            const endsOnThisPage = next <= stop
            out.push(...all.slice(cursor, Math.min(next, stop)))
            if (startsHere && endsOnThisPage) out.push(sumRows(`${primary}合计`, all.slice(cursor, next)))
            cursor = next
        }
        return out
    }

    /** 组合维度的主类别小计行（次类别列为空）加底色加粗。 */
    const subtotalRowStyle = (row: FundReportRowDto) =>
        dimension === "combo" && !row.secondaryName ? { background: "var(--muted)", fontWeight: 600 } : undefined

    const columnsOf = (section: FundReportSectionDto): TableColumnsType<FundReportRowDto> => {
        const nameColumn = {
            title: dimension === "combo" ? (report?.primaryText ?? "收支类别") : (section.name ?? ""),
            dataIndex: "name",
            render: (_: unknown, row: FundReportRowDto) => (
                <span className={dimension === "account" ? "font-medium text-(--brand)" : "text-(--foreground)"}>{row.name ?? ""}</span>
            ),
        }

        const tail = [
            { title: "收入总额", dataIndex: "totalIncome", width: 116, align: "right" as const, className: AMOUNT_RIGHT, render: (_: unknown, r: FundReportRowDto) => moneyBlankZero(r.totalIncome) },
            { title: "支出总额", dataIndex: "totalExpense", width: 116, align: "right" as const, className: AMOUNT_RIGHT, render: (_: unknown, r: FundReportRowDto) => moneyBlankZero(r.totalExpense) },
            { title: "收入笔数", dataIndex: "incomeCount", width: 88, align: "left" as const, className: COUNT_LEFT, render: (_: unknown, r: FundReportRowDto) => countText(r.incomeCount) },
            { title: "支出笔数", dataIndex: "expenseCount", width: 88, align: "left" as const, className: COUNT_LEFT, render: (_: unknown, r: FundReportRowDto) => countText(r.expenseCount) },
        ]

        const codeColumn = {
            title: "编码",
            dataIndex: "code",
            width: 120,
            render: (_: unknown, row: FundReportRowDto) => <span className="text-(--muted-foreground)">{row.code ?? ""}</span>,
        }

        /** 仅账户（科目编码）与收支类别（序号）有编码列；项目 / 部门 / 往来单位登记的是自由文本。 */
        const withCode = dimension === "account" || dimension === "category"

        if (dimension === "account") {
            return [
                codeColumn,
                nameColumn,
                { title: "期初余额", dataIndex: "openingBalance", width: 116, align: "right", className: AMOUNT_RIGHT, render: (_: unknown, r: FundReportRowDto) => moneyBlankZero(r.openingBalance) },
                ...tail.slice(0, 2),
                { title: "余额", dataIndex: "balance", width: 116, align: "right", className: `${AMOUNT_RIGHT} font-medium text-(--foreground)`, render: (_: unknown, r: FundReportRowDto) => money(r.balance) },
                ...tail.slice(2),
            ]
        }

        if (dimension === "combo") {
            return [
                nameColumn,
                {
                    title: secondaryLabel,
                    dataIndex: "secondaryName",
                    render: (_: unknown, row: FundReportRowDto) => row.secondaryName ?? "",
                },
                ...tail,
            ]
        }

        return [...(withCode ? [codeColumn] : []), nameColumn, ...tail]
    }

    /** 分段小计行：与列结构同形，金额落在对应列下，名称列显示小计标签。 */
    const summaryOf = (section: FundReportSectionDto) => {
        const sub = section.subtotal
        const strong = (content: ReactNode) => <span className="font-semibold tabular-nums">{content}</span>
        const cells: ReactNode[] = []
        const push = (content: ReactNode, align?: "left" | "right") =>
            cells.push(
                <Table.Summary.Cell key={cells.length} index={cells.length} align={align}>
                    {content}
                </Table.Summary.Cell>,
            )

        if (dimension === "account" || dimension === "category") push("")
        push(<span className="font-semibold text-(--foreground)">{section.subtotalLabel ?? "合计"}</span>)
        if (dimension === "combo") push("")
        if (dimension === "account") push(strong(moneyBlankZero(sub?.openingBalance)), "right")
        push(strong(moneyBlankZero(sub?.totalIncome)), "right")
        push(strong(moneyBlankZero(sub?.totalExpense)), "right")
        if (dimension === "account") push(strong(money(sub?.balance)), "right")
        push(strong(countText(sub?.incomeCount)))
        push(strong(countText(sub?.expenseCount)))

        return () => (
            <Table.Summary>
                <Table.Summary.Row style={{ background: "var(--muted)" }}>{cells}</Table.Summary.Row>
            </Table.Summary>
        )
    }

    /** 多段维度的「合计」行：列结构与分段表一致。 */
    const renderTotalRow = (sampleSection: FundReportSectionDto) => {
        if (!totalRow) return null
        const columns = columnsOf(sampleSection)
        return (
            <Table
                columns={columns}
                dataSource={[totalRow]}
                rowKey="name"
                pagination={false}
                               showHeader={false}
                onRow={() => ({ style: { background: "var(--brand-soft)", fontWeight: 600 } })}
                locale={{ emptyText: " " }}
            />
        )
    }

    const rowCount = singleSection ? (visibleSections[0]?.rows ?? []).length : 0

    /* ---------------- 渲染 ---------------- */

    return (
        <div className="flex flex-col gap-3">
            {error && <div className="rounded border border-(--danger-border) bg-(--table-neg-bg) px-3 py-2 text-[13px] text-(--danger)">{error}</div>}

            <Card>
                <Tabs
                    className="lemon-sub-tabs"
                    activeKey={dimension}
                    onChange={changeDimension}
                    items={TABS.map((t) => ({ key: t.key, label: t.label }))}
                    tabBarStyle={{ margin: 0, paddingInline: 16 }}
                />

                {/* 工具栏 */}
                <div className="flex min-h-[56px] flex-wrap items-center justify-between gap-3 px-4 py-2.5" style={{ marginBottom: 16 }}>
                    <Space size={10} wrap>
                        <DatePicker.RangePicker
                                                       allowClear={false}
                            value={[dayjs(draft.start), dayjs(draft.end)]}
                            style={{ width: 250 }}
                            onChange={(values: [Dayjs | null, Dayjs | null] | null) => {
                                const [from, to] = values ?? []
                                if (!from || !to) return
                                setDraft({ start: from.format("YYYY-MM-DD"), end: to.format("YYYY-MM-DD") })
                            }}
                        />
                        <Button type="primary" loading={loading} autoInsertSpace={false} onClick={applyQuery}>
                            查询
                        </Button>
                        {dimension !== "account" && (
                            <Select
                                                               className="min-w-[140px]"
                                value={journalId}
                                onChange={(value) => {
                                    setJournalId(value)
                                    setPage(1)
                                }}
                                options={[{ value: "", label: "全部账户" }, ...journals.map((j) => ({ value: j.id ?? "", label: j.name ?? "" }))]}
                            />
                        )}
                        {dimension !== "account" && (
                            <Input
                                                               allowClear
                                className="w-[200px]"
                                placeholder={dimension === "combo" ? `输入${nameLabel}或${secondaryLabel}搜索` : `输入${nameLabel}搜索`}
                                prefix={<SearchOutlined className="text-(--muted-foreground)" />}
                                value={keyword}
                                onChange={(e) => {
                                    setKeyword(e.target.value)
                                    setPage(1)
                                }}
                            />
                        )}
                        {dimension === "combo" && (
                            <Space size={8} wrap>
                                <span className="text-[12.5px] text-(--muted-foreground)">主类别</span>
                                <span className="text-[13px] text-(--foreground)">{report?.primaryText ?? "收支类别"}</span>
                                <span className="text-[12.5px] text-(--muted-foreground)">次类别</span>
                                <Select
                                                                       className="min-w-[110px]"
                                    value={secondary}
                                    onChange={(value: Secondary) => {
                                        setSecondary(value)
                                        setPage(1)
                                    }}
                                    options={SECONDARIES.map((s) => ({ value: s.key, label: s.label }))}
                                />
                            </Space>
                        )}
                    </Space>

                    <Space size={8} wrap>
                        {dimension === "combo" && (
                            <Checkbox
                                className="text-[13px] text-(--foreground)"
                                checked={showPrimarySubtotal}
                                onChange={(e) => {
                                    setShowPrimarySubtotal(e.target.checked)
                                    setPage(1)
                                }}
                            >
                                显示主类型合计
                            </Checkbox>
                        )}
                        <Button
                                                       autoInsertSpace={false}
                            disabled={!report}
                            onClick={() => {
                                // 打印与页面/导出同口径：带上搜索过滤后的分段与合计。
                                if (report) openPrintWindow(buildPrintHtml({ ...report, sections: visibleSections, total: totalRow ?? report.total }, dimensionLabel, accountSetName || "当前账套"))
                            }}
                        >
                            打印
                        </Button>
                        <Button autoInsertSpace={false} onClick={() => void exportReport()}>
                            导出
                        </Button>
                        {dimension !== "combo" && (
                            <>
                                <Button onClick={() => setShareOpen(true)}>
                                    扫码分享
                                </Button>
                                <Button onClick={() => setSubscribeOpen(true)}>
                                    报表订阅推送
                                </Button>
                            </>
                        )}
                        <Tooltip title="刷新">
                            <Button icon={<ReloadIcon spinning={loading} />} onClick={() => void load()} />
                        </Tooltip>
                    </Space>
                </div>

                {/* 表格区 */}
                <div className="p-3">
                    {loading && !report ? (
                        <div className="py-16 text-center text-[13px] text-(--muted-foreground)">正在加载…</div>
                    ) : (
                        <div className="flex flex-col gap-3">
                            {visibleSections.map((section, index) => (
                                <div key={`${section.name}-${index}`}>
                                    <Table
                                        columns={columnsOf(section)}
                                        dataSource={rowsOf(section)}
                                        rowKey={(row) => `${row.code ?? ""}|${row.name ?? ""}|${row.secondaryName ?? ""}`}
                                        pagination={false}
                                                                               loading={loading}
                                        summary={summaryOf(section)}
                                        onRow={(row) => ({ style: subtotalRowStyle(row) })}
                                        locale={{ emptyText: " " }}
                                    />
                                </div>
                            ))}

                            {!singleSection && visibleSections.length > 0 && renderTotalRow(visibleSections[0])}

                            {singleSection && (
                                <div className="flex flex-wrap items-center justify-between gap-2 text-[12.5px] text-(--muted-foreground)" style={{ marginTop: 16 }}>
                                    <span>
                                        显示 {rowCount === 0 ? 0 : (page - 1) * pageSize + 1} 到 {Math.min(page * pageSize, rowCount)}，共 {rowCount} 条
                                    </span>
                                    <Pagination
                                                                               current={page}
                                        pageSize={pageSize}
                                        total={rowCount}
                                        pageSizeOptions={PAGE_SIZE_OPTIONS}
                                        showSizeChanger
                                        onChange={(next, size) => {
                                            if (size !== pageSize) {
                                                setPageSize(size)
                                                setPage(1)
                                                return
                                            }
                                            setPage(next)
                                        }}
                                    />
                                </div>
                            )}
                        </div>
                    )}
                </div>

                {/* 底部说明 */}
                {report?.currencyNote && <div className="px-4 pb-3 text-[12.5px] leading-6 text-(--muted-foreground)">{report.currencyNote}</div>}
                {report?.note && (
                    <div className="border-t border-(--border) px-4 py-3 text-[12.5px] leading-6 text-(--muted-foreground)">
                        <span className="text-(--danger)">提示：</span>
                        {report.note}
                        <Typography.Link className="ml-1 text-[12.5px]" onClick={() => setHelpOpen(true)}>
                            点击查看
                        </Typography.Link>
                    </div>
                )}
            </Card>

            {/* 内部转账说明 */}
            <Modal
                open={helpOpen}
                title="内部转账说明"
                width={520}
                onCancel={() => setHelpOpen(false)}
                footer={<Button autoInsertSpace={false} onClick={() => setHelpOpen(false)}>我知道了</Button>}
            >
                <div className="space-y-2 text-[13px] leading-6 text-(--foreground)">
                    <p>内部转账指账户之间的资金划转（例如从银行提取现金、账户间互转），资金总额不变，不属于收支业务，因此不计入本表各维度的收入与支出合计。</p>
                    <p>如需查看内部转账明细，请到「资金 → 内部转账」中按日期区间查询。</p>
                </div>
            </Modal>

            {/* 扫码分享 */}
            <Modal
                open={shareOpen}
                title="扫码分享"
                width={420}
                onCancel={() => setShareOpen(false)}
                footer={null}
            >
                <div className="flex flex-col items-center gap-3 py-2">
                    <QRCode value={shareUrl(dimension, range)} size={200} />
                    <div className="break-all text-center text-[12px] text-(--muted-foreground)">{shareUrl(dimension, range)}</div>
                    <div className="flex gap-2">
                        <Button
                                                       autoInsertSpace={false}
                            onClick={() => {
                                void navigator.clipboard?.writeText(shareUrl(dimension, range))
                                message.success("链接已复制")
                            }}
                        >
                            复制链接
                        </Button>
                        <Button autoInsertSpace={false} type="primary" onClick={() => setShareOpen(false)}>
                            完成
                        </Button>
                    </div>
                </div>
            </Modal>

            {/* 报表订阅推送 */}
            <Modal
                open={subscribeOpen}
                title="报表订阅推送"
                width={460}
                onCancel={() => setSubscribeOpen(false)}
                footer={
                    <Space>
                        <Button autoInsertSpace={false} onClick={() => setSubscribeOpen(false)}>取消</Button>
                        <Button autoInsertSpace={false} type="primary" onClick={saveSubscribe}>
                            保存
                        </Button>
                    </Space>
                }
            >
                <div className="flex flex-col gap-3 py-1">
                    <Checkbox checked={subscribe.enabled} onChange={(e) => setSubscribe((s) => ({ ...s, enabled: e.target.checked }))}>
                        <span className="text-[13px] text-(--foreground)">开启「{dimensionLabel}」自动推送</span>
                    </Checkbox>
                    <div className="flex items-center gap-2">
                        <span className="w-16 text-[13px] text-(--muted-foreground)">推送周期</span>
                        <Select
                            className="flex-1"
                            value={subscribe.cycle}
                            disabled={!subscribe.enabled}
                            onChange={(cycle) => setSubscribe((s) => ({ ...s, cycle }))}
                            options={["每日", "每周", "每月"].map((v) => ({ value: v, label: v }))}
                        />
                    </div>
                    <div className="flex items-center gap-2">
                        <span className="w-16 text-[13px] text-(--muted-foreground)">推送时间</span>
                        <Select
                            className="flex-1"
                            value={subscribe.hour}
                            disabled={!subscribe.enabled}
                            onChange={(hour) => setSubscribe((s) => ({ ...s, hour }))}
                            options={["08:00", "09:00", "12:00", "18:00", "21:00"].map((v) => ({ value: v, label: v }))}
                        />
                    </div>
                    <p className="text-[12px] leading-5 text-(--muted-foreground)">推送内容为当前页签在所选区间内的汇总结果，推送到站内消息中心。</p>
                </div>
            </Modal>
        </div>
    )
}

/** 分享链接：把维度与区间带进查询串，打开后落到同一张报表。 */
function shareUrl(dimension: Dimension, range: { start: string; end: string }): string {
    const qs = new URLSearchParams({ dimension, start: range.start, end: range.end })
    return `${window.location.origin}${window.location.pathname}?${qs.toString()}`
}

/** 刷新图标：加载中旋转。 */
function ReloadIcon({ spinning }: { spinning: boolean }) {
    return <ReloadOutlined spin={spinning} />
}
