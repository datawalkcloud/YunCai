import { useCallback, useEffect, useMemo, useState } from "react"
import type { ReactNode } from "react"
import { DownloadOutlined, PrinterOutlined, ReloadOutlined } from "@ant-design/icons";
import { Button, Table } from "antd"
import type { TableColumnsType } from "antd"
import dayjs from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type BalanceSheetLineDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { PeriodField } from "@/components/fields/date-fields"
import { ReportPageShell } from "@/components/report/ReportPageShell"
import { AMOUNT_COL } from "@/components/page/styles"

type RowKind = "section" | "item" | "detail" | "total"

/** 选定的会计期间。 */
interface Period {
    year: number
    month: number
}

/** 报表一行。section 为分类小标题（如“流动资产”），total 为合计行（加粗）。 */
interface SheetLine {
    label: string
    line?: number
    ending: number
    opening: number
    kind: RowKind
}

/** 会企01表明细行定义：名称与报表行次；金额由后端按行次填入。 */
interface LineDef {
    label: string
    line: number
}

const def = (label: string, line: number): LineDef => ({ label, line })

// ============ 会企01表标准行次模板（企业会计准则 2007 版式，与柠檬云一致）============
const CURRENT_ASSETS: LineDef[] = [
    def("货币资金", 1),
    def("以公允价值计量且其变动计入当期损益的金融资产", 2),
    def("应收票据", 3),
    def("应收账款", 4),
    def("预付账款", 5),
    def("应收利息", 6),
    def("应收股利", 7),
    def("其他应收款", 8),
    def("存货", 9),
    def("一年内到期的非流动资产", 10),
    def("其他流动资产", 11),
]
const NON_CURRENT_ASSETS: LineDef[] = [
    def("可供出售金融资产", 13),
    def("持有至到期投资", 14),
    def("长期应收款", 15),
    def("长期股权投资", 16),
    def("投资性房地产", 17),
    def("固定资产", 18),
    def("在建工程", 19),
    def("工程物资", 20),
    def("固定资产清理", 21),
    def("生产性生物资产", 22),
    def("油气资产", 23),
    def("无形资产", 24),
    def("开发支出", 25),
    def("商誉", 26),
    def("长期待摊费用", 27),
    def("递延所得税资产", 28),
    def("其他非流动资产", 29),
]
const CURRENT_LIABILITIES: LineDef[] = [
    def("短期借款", 32),
    def("以公允价值计量且其变动计入当期损益的金融负债", 33),
    def("应付票据", 34),
    def("应付账款", 35),
    def("预收款项", 36),
    def("应付职工薪酬", 37),
    def("应交税费", 38),
    def("应付利息", 39),
    def("应付股利", 40),
    def("其他应付款", 41),
    def("一年内到期的非流动负债", 42),
    def("其他流动负债", 43),
]
const NON_CURRENT_LIABILITIES: LineDef[] = [
    def("长期借款", 45),
    def("应付债券", 46),
    def("长期应付款", 47),
    def("专项应付款", 48),
    def("预计负债", 49),
    def("递延收益", 50),
    def("递延所得税负债", 51),
    def("其他非流动负债", 52),
]
const EQUITY: LineDef[] = [
    def("实收资本（或股本）", 55),
    def("资本公积", 56),
    def("减:库存股", 57),
    def("其他综合收益", 58),
    def("盈余公积", 59),
    def("未分配利润", 60),
]

// ============ 会小企01表标准行次模板（小企业会计准则，与柠檬云一致）============
const SB_CURRENT_ASSETS: LineDef[] = [
    def("货币资金", 1),
    def("短期投资", 2),
    def("应收票据", 3),
    def("应收账款", 4),
    def("预付账款", 5),
    def("应收股利", 6),
    def("应收利息", 7),
    def("其他应收款", 8),
    def("存货", 9),
]
/** 存货明细行（「其中」），金额已含在存货主行 9 中，仅作展示。 */
const SB_INVENTORY_DETAILS: LineDef[] = [
    def("其中：原材料", 10),
    def("在产品", 11),
    def("库存商品", 12),
    def("周转材料", 13),
]
const SB_OTHER_CURRENT_ASSET = def("其他流动资产", 14)
const SB_NON_CURRENT_HEAD: LineDef[] = [
    def("长期债券投资", 16),
    def("长期股权投资", 17),
]
const SB_FIXED_ASSETS: LineDef[] = [
    def("固定资产原价", 18),
    def("减：累计折旧", 19),
]
const SB_NON_CURRENT_TAIL: LineDef[] = [
    def("在建工程", 21),
    def("工程物资", 22),
    def("固定资产清理", 23),
    def("生产性生物资产", 24),
    def("无形资产", 25),
    def("开发支出", 26),
    def("长期待摊费用", 27),
    def("其他非流动资产", 28),
]
const SB_CURRENT_LIABILITIES: LineDef[] = [
    def("短期借款", 31),
    def("应付票据", 32),
    def("应付账款", 33),
    def("预收账款", 34),
    def("应付职工薪酬", 35),
    def("应交税费", 36),
    def("应付利息", 37),
    def("应付利润", 38),
    def("其他应付款", 39),
]
const SB_OTHER_CURRENT_LIABILITY = def("其他流动负债", 40)
const SB_NON_CURRENT_LIABILITIES: LineDef[] = [
    def("长期借款", 42),
    def("长期应付款", 43),
    def("递延收益", 44),
    def("其他非流动负债", 45),
]
const SB_EQUITY: LineDef[] = [
    def("实收资本（或股本）", 48),
    def("资本公积", 49),
    def("盈余公积", 50),
    def("未分配利润", 51),
]

/** 行次 → 金额（期末/年初）。缺失的行次视为 0。 */
type AmountMap = Map<number, { ending: number; opening: number }>

function amountOf(map: AmountMap, line: number) {
    return map.get(line) ?? { ending: 0, opening: 0 }
}

function toItems(defs: LineDef[], map: AmountMap): SheetLine[] {
    return defs.map((d): SheetLine => ({ ...d, ...amountOf(map, d.line), kind: "item" }))
}

function sumEnding(items: SheetLine[]) {
    return items.reduce((s, i) => s + i.ending, 0)
}
function sumOpening(items: SheetLine[]) {
    return items.reduce((s, i) => s + i.opening, 0)
}

function fmt(value: number, force = false): string {
    if (value === 0 && !force) return ""
    return value.toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

/** 期间 → 当月 1 日的 Date，与 PeriodField 的落值形状一致。 */
function toPeriodDate(p: Period | null): Date | undefined {
    return p ? dayjs(`${p.year}-${String(p.month).padStart(2, "0")}-01`).toDate() : undefined
}

function buildAssetRows(map: AmountMap): SheetLine[] {
    const current = toItems(CURRENT_ASSETS, map)
    const nonCurrent = toItems(NON_CURRENT_ASSETS, map)
    const currentTotal = { ending: sumEnding(current), opening: sumOpening(current) }
    const nonCurrentTotal = { ending: sumEnding(nonCurrent), opening: sumOpening(nonCurrent) }
    return [
        { label: "流动资产：", ending: 0, opening: 0, kind: "section" },
        ...current,
        { label: "流动资产合计", line: 12, ...currentTotal, kind: "total" },
        // 与右侧「流动负债合计」对齐用的空行（柠檬云版式）
        { label: "", ending: 0, opening: 0, kind: "section" },
        { label: "非流动资产：", ending: 0, opening: 0, kind: "section" },
        ...nonCurrent,
        { label: "非流动资产合计", line: 30, ...nonCurrentTotal, kind: "total" },
        {
            label: "资产总计",
            line: 31,
            ending: currentTotal.ending + nonCurrentTotal.ending,
            opening: currentTotal.opening + nonCurrentTotal.opening,
            kind: "total",
        },
    ]
}

function buildLiabilityRows(map: AmountMap): SheetLine[] {
    const curLiabItems = toItems(CURRENT_LIABILITIES, map)
    const nonCurLiabItems = toItems(NON_CURRENT_LIABILITIES, map)
    const equityItems = toItems(EQUITY, map)
    const curLiab = { ending: sumEnding(curLiabItems), opening: sumOpening(curLiabItems) }
    const nonCurLiab = { ending: sumEnding(nonCurLiabItems), opening: sumOpening(nonCurLiabItems) }
    const liabTotal = {
        ending: curLiab.ending + nonCurLiab.ending,
        opening: curLiab.opening + nonCurLiab.opening,
    }
    const equityTotal = { ending: sumEnding(equityItems), opening: sumOpening(equityItems) }
    return [
        { label: "流动负债：", ending: 0, opening: 0, kind: "section" },
        ...curLiabItems,
        { label: "流动负债合计", line: 44, ...curLiab, kind: "total" },
        { label: "非流动负债：", ending: 0, opening: 0, kind: "section" },
        ...nonCurLiabItems,
        { label: "非流动负债合计", line: 53, ...nonCurLiab, kind: "total" },
        { label: "负债合计", line: 54, ...liabTotal, kind: "total" },
        { label: "所有者权益（或股东权益）：", ending: 0, opening: 0, kind: "section" },
        ...equityItems,
        { label: "所有者权益（或股东权益）合计", line: 61, ...equityTotal, kind: "total" },
        {
            label: "负债和所有者权益（或股东权益）总计",
            line: 62,
            ending: liabTotal.ending + equityTotal.ending,
            opening: liabTotal.opening + equityTotal.opening,
            kind: "total",
        },
    ]
}

/** 表格一行 = 左右两半各一条报表行；较短的一侧留空。 */
interface SheetRow {
    id: number
    a?: SheetLine
    b?: SheetLine
}

const blankRow = (): SheetLine => ({ label: "", ending: 0, opening: 0, kind: "section" })

/** 会小企01表左半：存货明细与固定资产原价/累计折旧分行列示，固定资产以账面价值参与合计。 */
function buildSmallBusinessAssetRows(map: AmountMap, withInventoryDetails: boolean): SheetLine[] {
    const current = toItems(SB_CURRENT_ASSETS, map)
    const otherCurrent = { ...SB_OTHER_CURRENT_ASSET, ...amountOf(map, SB_OTHER_CURRENT_ASSET.line), kind: "item" as const }
    const currentTotal = {
        ending: sumEnding(current) + otherCurrent.ending,
        opening: sumOpening(current) + otherCurrent.opening,
    }
    const head = toItems(SB_NON_CURRENT_HEAD, map)
    const fixed = toItems(SB_FIXED_ASSETS, map)
    const bookValue = {
        label: "固定资产账面价值",
        line: 20,
        ending: amountOf(map, 18).ending - amountOf(map, 19).ending,
        opening: amountOf(map, 18).opening - amountOf(map, 19).opening,
        kind: "item" as const,
    }
    const tail = toItems(SB_NON_CURRENT_TAIL, map)
    const nonCurrentTotal = {
        ending: sumEnding(head) + bookValue.ending + sumEnding(tail),
        opening: sumOpening(head) + bookValue.opening + sumOpening(tail),
    }
    return [
        { label: "流动资产", ending: 0, opening: 0, kind: "section" },
        ...current,
        ...(withInventoryDetails
            ? SB_INVENTORY_DETAILS.map((d): SheetLine => ({ ...d, ...amountOf(map, d.line), kind: "detail" }))
            : []),
        otherCurrent,
        { label: "流动资产合计", line: 15, ...currentTotal, kind: "total" },
        { label: "非流动资产", ending: 0, opening: 0, kind: "section" },
        ...head,
        ...fixed,
        bookValue,
        ...tail,
        { label: "非流动资产合计", line: 29, ...nonCurrentTotal, kind: "total" },
        {
            label: "资产合计",
            line: 30,
            ending: currentTotal.ending + nonCurrentTotal.ending,
            opening: currentTotal.opening + nonCurrentTotal.opening,
            kind: "total",
        },
    ]
}

/** 会小企01表右半：流动负债 31-41、非流动负债 42-47、所有者权益 48-53。 */
function buildSmallBusinessLiabilityRows(map: AmountMap, withInventoryDetails: boolean): SheetLine[] {
    const curLiab = toItems(SB_CURRENT_LIABILITIES, map)
    const otherCurLiab = { ...SB_OTHER_CURRENT_LIABILITY, ...amountOf(map, SB_OTHER_CURRENT_LIABILITY.line), kind: "item" as const }
    const curLiabTotal = {
        ending: sumEnding(curLiab) + otherCurLiab.ending,
        opening: sumOpening(curLiab) + otherCurLiab.opening,
    }
    const nonCurLiab = toItems(SB_NON_CURRENT_LIABILITIES, map)
    const nonCurLiabTotal = { ending: sumEnding(nonCurLiab), opening: sumOpening(nonCurLiab) }
    const liabTotal = {
        ending: curLiabTotal.ending + nonCurLiabTotal.ending,
        opening: curLiabTotal.opening + nonCurLiabTotal.opening,
    }
    const equity = toItems(SB_EQUITY, map)
    const equityTotal = { ending: sumEnding(equity), opening: sumOpening(equity) }
    return [
        { label: "流动负债", ending: 0, opening: 0, kind: "section" },
        ...curLiab,
        // 与左侧存货明细行对齐的留白（4 行）；明细折叠时一并省掉。
        ...(withInventoryDetails ? [blankRow(), blankRow(), blankRow(), blankRow()] : []),
        otherCurLiab,
        { label: "流动负债合计", line: 41, ...curLiabTotal, kind: "total" },
        { label: "非流动负债", ending: 0, opening: 0, kind: "section" },
        ...nonCurLiab,
        { label: "非流动负债合计", line: 46, ...nonCurLiabTotal, kind: "total" },
        { label: "负债合计", line: 47, ...liabTotal, kind: "total" },
        // 与左侧固定资产清理/生物资产对齐的留白（2 行）
        blankRow(),
        blankRow(),
        { label: "所有者权益（或股东权益）", ending: 0, opening: 0, kind: "section" },
        ...equity,
        { label: "所有者权益（或股东权益）合计", line: 52, ...equityTotal, kind: "total" },
        {
            label: "负债和所有者权益（或股东权益）",
            line: 53,
            ending: liabTotal.ending + equityTotal.ending,
            opening: liabTotal.opening + equityTotal.opening,
            kind: "total",
        },
    ]
}

/**
 * 半张表共用的 4 列（名称 / 行次 / 期末 / 年初）。
 * 资产负债表是左右对排的打印格式，取数按 kind 决定缩进与加粗，空侧渲染空单元格。
 */
function halfColumns(nameTitle: string, pick: (row: SheetRow) => SheetLine | undefined): TableColumnsType<SheetRow> {
    /** 该侧为空时补一个占位单元格，保持网格完整。 */
    const cell = (render: (line: SheetLine) => ReactNode) =>
        (_: unknown, row: SheetRow) => {
            const line = pick(row)
            return line ? render(line) : null
        }

    return [
        {
            title: nameTitle,
            render: cell((line) =>
                line.kind === "section"
                    ? <span className="font-medium">{line.label}</span>
                    : <span className={cn(
                        line.kind === "total" ? "font-medium" : line.kind === "detail" ? "ml-8" : "ml-4",
                    )}>{line.label}</span>,
            ),
        },
        {
            title: "行次",
            align: "center",
            width: 56,
            render: cell((line) => <span className="tabular-nums text-muted-foreground">{line.line}</span>),
        },
        {
            title: "期末余额",
            width: 130,
            ...AMOUNT_COL,
            render: cell((line) => (
                <span className={line.kind === "total" ? "font-medium" : undefined}>{fmt(line.ending, line.kind === "total")}</span>
            )),
        },
        {
            title: "年初余额",
            width: 130,
            ...AMOUNT_COL,
            render: cell((line) => (
                <span className={line.kind === "total" ? "font-medium" : undefined}>{fmt(line.opening, line.kind === "total")}</span>
            )),
        },
    ]
}

export default function BalanceSheetPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [period, setPeriod] = useState<Period | null>(null)
    const [lines, setLines] = useState<BalanceSheetLineDto[]>([])
    const [standard, setStandard] = useState(1)
    const [templateTitle, setTemplateTitle] = useState("会企01表")
    const [endingDate, setEndingDate] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [loaded, setLoaded] = useState(false)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new ReportsApi(apiConfig(token)).accountsetsAccountSetIdReportsBalanceSheetGet({
                accountSetId,
                year: period?.year,
                month: period?.month,
            })
            setLines(res.lines ?? [])
            setStandard(res.standard ?? 1)
            setTemplateTitle(res.templateTitle ?? "会企01表")
            setEndingDate(res.endingDate ?? "")
            setLoaded(true)
            // 首次未指定期间时，采用后端解析出的期间（最近有凭证的月份）。
            if (period === null && res.year && res.month) {
                setPeriod({ year: res.year, month: res.month })
            }
        } catch (err) {
            setError(await apiErrorMessage(err, "加载资产负债表失败，请重试。"))
            setLines([])
            setLoaded(false)
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, period])

    useEffect(() => {
        void load()
    }, [load])

    const { assetRows, liabilityRows, balanced } = useMemo(() => {
        const map: AmountMap = new Map()
        for (const l of lines) {
            if (l.line != null) {
                map.set(l.line, { ending: l.ending ?? 0, opening: l.opening ?? 0 })
            }
        }
        // 版式跟随账套会计标准：小企业准则用会小企01表行次，其余用会企01表。
        const smallBusiness = standard === 2
        // 存货明细（10-13）全为 0 时按柠檬云折叠，省掉该组行与右侧对应留白。
        const withInventoryDetails = SB_INVENTORY_DETAILS.some((d) => {
            const amount = amountOf(map, d.line)
            return amount.ending !== 0 || amount.opening !== 0
        })
        const assets = smallBusiness ? buildSmallBusinessAssetRows(map, withInventoryDetails) : buildAssetRows(map)
        const liabilities = smallBusiness
            ? buildSmallBusinessLiabilityRows(map, withInventoryDetails)
            : buildLiabilityRows(map)
        const assetTotal = assets[assets.length - 1]
        const liabTotal = liabilities[liabilities.length - 1]
        // 金额以分为单位比较，规避浮点误差。
        const eq = (a: number, b: number) => Math.round(a * 100) === Math.round(b * 100)
        return {
            assetRows: assets,
            liabilityRows: liabilities,
            balanced: eq(assetTotal.ending, liabTotal.ending) && eq(assetTotal.opening, liabTotal.opening),
        }
    }, [lines, standard])

    // 左右两半按行号并排，较短的一侧留空。
    const rows: SheetRow[] = useMemo(
        () => Array.from({ length: Math.max(assetRows.length, liabilityRows.length) }, (_, i) => ({
            id: i,
            a: assetRows[i],
            b: liabilityRows[i],
        })),
        [assetRows, liabilityRows],
    )

    /** 打印：后端按账套准则生成 A4 PDF 后在新窗口打开（不含浏览器页眉页脚）。 */
    const printSheet = useCallback(async () => {
        if (!token || !accountSetId || !period) return
        // 先同步开窗，避免取数完成后被浏览器当作非用户操作而拦截。
        const win = window.open("", "_blank")
        try {
            const res = await fetch(
                `/api/accountsets/${accountSetId}/reports/balance-sheet/pdf?year=${period.year}&month=${period.month}`,
                { headers: { Authorization: `Bearer ${token}` } },
            )
            if (!res.ok) {
                win?.close()
                setError("生成打印稿失败，请重试。")
                return
            }
            const url = URL.createObjectURL(await res.blob())
            if (win) {
                win.location.href = url
            } else {
                window.open(url, "_blank")
            }
            window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
        } catch {
            win?.close()
            setError("生成打印稿失败，请重试。")
        }
    }, [token, accountSetId, period])

    /** 导出当前期间的资产负债表（后端按账套准则生成 .xls 两栏版式）。 */
    const exportSheet = useCallback(async () => {
        if (!token || !accountSetId || !period) return
        const res = await fetch(
            `/api/accountsets/${accountSetId}/reports/export?kind=balance-sheet&year=${period.year}&month=${period.month}`,
            { headers: { Authorization: `Bearer ${token}` } },
        )
        if (!res.ok) return
        // 文件名由后端给出（含账套与导出日期），取不到时回退到期间命名。
        const disposition = res.headers.get("content-disposition") ?? ""
        const match = /filename\*=UTF-8''([^;]+)/i.exec(disposition) ?? /filename="?([^";]+)"?/i.exec(disposition)
        const fileName = match
            ? decodeURIComponent(match[1])
            : `资产负债表_${period.year}年${String(period.month).padStart(2, "0")}月.xls`
        const url = URL.createObjectURL(await res.blob())
        const a = document.createElement("a")
        a.href = url
        a.download = fileName
        document.body.appendChild(a)
        a.click()
        a.remove()
        URL.revokeObjectURL(url)
    }, [token, accountSetId, period])

    return (
        <ReportPageShell
            title="资产负债表"
            template={templateTitle}
            period={
                <>
                    <span className="text-[12.5px] text-slate-500">会计期间：</span>
                    <PeriodField
                        value={toPeriodDate(period)}
                        onChange={(d) => d && setPeriod({ year: dayjs(d).year(), month: dayjs(d).month() + 1 })}
                        disabled={!accountSetId}
                        allowClear={false}
                    />
                </>
            }
            actions={
                <>
                    {/* 与柠檬云报表页一致：打印 / 导出 */}
                    <Button
                        icon={<PrinterOutlined />}
                        onClick={() => void printSheet()}
                        disabled={!accountSetId || loading || !period}
                    >
                        打印
                    </Button>
                    <Button
                        icon={<DownloadOutlined />}
                        onClick={() => void exportSheet()}
                        disabled={!accountSetId || loading}
                    >
                        导出
                    </Button>
                    <Button
                        icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                        onClick={() => void load()}
                        disabled={loading || !accountSetId}
                    >
                        刷新
                    </Button>
                </>
            }
            meta={
                <>
                    <span>编制单位：{currentSet?.companyName ?? "——"}</span>
                    <span>{endingDate}</span>
                    <span>单位：元</span>
                </>
            }
            loading={loading && !loaded}
            error={error}
            placeholder={!accountSetId ? "请先选择账套。" : undefined}
            footer={
                loaded && !loading ? (
                    <span className={cn("text-[12.5px]", balanced ? "text-slate-400" : "text-[#ef4444]")}>
                        {balanced ? "资产总计 = 负债和所有者权益总计" : "试算不平衡，请核对数据"}
                    </span>
                ) : undefined
            }
        >
            <Table<SheetRow>
                columns={[...halfColumns("资产", (r) => r.a), ...halfColumns("负债和所有者权益", (r) => r.b)]}
                dataSource={rows}
                rowKey="id"
                pagination={false}
                scroll={{ x: 900 }}
            />
        </ReportPageShell>
    )
}
