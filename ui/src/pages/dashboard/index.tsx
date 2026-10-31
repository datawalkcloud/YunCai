import { useEffect, useMemo, useState } from "react"
import { useNavigate } from "react-router"

import {
    AccountBookOutlined,
    ArrowRightOutlined,
    CheckCircleFilled,
    CloseCircleOutlined,
    FileTextOutlined,
    InfoCircleFilled,
    LeftOutlined,
    PlusOutlined,
    ProfileOutlined,
    ReloadOutlined,
    RightOutlined,
    UnorderedListOutlined,
    UploadOutlined,
    WarningFilled,
} from "@ant-design/icons"
import { Button, Skeleton } from "antd"

import { fmtAmount } from "@/components/ledger/format"
import {
    makePeriod,
    shiftPeriod,
    useCurrentAccountSet,
    useDefaultAccountingPeriod,
    type AccountingPeriod,
} from "@/accountSets/useCurrentAccountSet"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import type { FinancialSummaryRowDto } from "@/apis-gen"
import { countByStatus, findRow, useDashboardData, type DashboardData } from "./useDashboardData"

/* ------------------------------------------------------------------ */
/*  数值展示                                                            */
/*  首页卡片面积小，金额统一折成「万元」两列小数；完整元数放 title 上。      */
/* ------------------------------------------------------------------ */

/** 元 → 万元文本；null 显示「--」，0 显示「0.00」。 */
function wan(value: number | null | undefined): string {
    if (value == null) return "--"
    return (value / 10000).toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 百分比指标（资产负债率）。 */
function pct(value: number | null | undefined): string {
    if (value == null) return "--"
    return fmtAmount(value, true) + "%"
}

/* ------------------------------------------------------------------ */
/*  顶部提示条：按「未过账 → 未结转 → 未结账 → 已就绪」给出最该做的一件事    */
/* ------------------------------------------------------------------ */

type Notice = {
    tone: "warn" | "info" | "ok"
    text: string
    cta: string
    to: string
}

const NOTICE_STYLE: Record<Notice["tone"], { wrap: string; icon: React.ComponentType<{ className?: string }> }> = {
    warn: { wrap: "bg-[#fff8e6] text-[#b45309] border-[#fde8b4]", icon: WarningFilled },
    info: { wrap: "bg-[#eef4ff] text-[#2f6bff] border-[#d9e6ff]", icon: InfoCircleFilled },
    ok: { wrap: "bg-[#e9f9f1] text-[#0f9d63] border-[#c8efdd]", icon: CheckCircleFilled },
}

function buildNotice(data: DashboardData, counts: ReturnType<typeof countByStatus>, base: string): Notice {
    if (counts.unposted > 0) {
        return {
            tone: "warn",
            text: `账簿与报表只统计已过账凭证，当前还有 ${counts.unposted} 张凭证未过账，首页指标可能偏低。`,
            cta: "去审核过账",
            to: `${base}/vouchers/manage`,
        }
    }
    if (!data.closing?.carriedPeriod) {
        return { tone: "info", text: "本期损益尚未结转，结转后利润表与资产负债表口径才一致。", cta: "去结转损益", to: `${base}/closing` }
    }
    if (!data.closing?.closedPeriod) {
        return { tone: "info", text: "本期尚未结账，结账后该期间将锁定，不再允许录入或修改凭证。", cta: "去结账", to: `${base}/closing` }
    }
    return {
        tone: "ok",
        text: `本期已完成结转与结账（结转至 ${data.closing.carriedPeriod}，结账至 ${data.closing.closedPeriod}）。`,
        cta: "查看财务概要",
        to: `${base}/reports/financial-summary`,
    }
}

function NoticeStrip({ data, counts }: { data: DashboardData; counts: ReturnType<typeof countByStatus> }) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const notice = buildNotice(data, counts, `/${accountSetId}`)
    const style = NOTICE_STYLE[notice.tone]
    const Icon = style.icon

    return (
        <div className={"flex flex-wrap items-center gap-2 rounded-xl border px-4 py-2.5 text-[12.5px] " + style.wrap}>
            <Icon className="shrink-0 text-[15px]" />
            <span className="min-w-0 flex-1 leading-5">{notice.text}</span>
            <button
                type="button"
                onClick={() => navigate(notice.to)}
                className="flex shrink-0 items-center gap-1 font-medium transition-opacity hover:opacity-70"
            >
                {notice.cta}
                <RightOutlined className="text-[10px]" />
            </button>
        </div>
    )
}

/* ------------------------------------------------------------------ */
/*  快捷入口卡（四张一排，卡底左侧留一条色条区分入口）                     */
/* ------------------------------------------------------------------ */

type EntrySpec = { label: string; to: string; accent: string; hint?: (c: ReturnType<typeof countByStatus>) => string }

const ENTRIES: EntrySpec[] = [
    { label: "查看凭证", to: "vouchers/manage", accent: "#2f6bff", hint: (c) => `${c.total} 张 · ${c.unposted} 张待过账` },
    { label: "科目余额表", to: "ledger/trial-balance", accent: "#10b981" },
    { label: "资产负债表", to: "reports/balance-sheet", accent: "#f59e0b" },
    { label: "利润表", to: "reports/income-statement", accent: "#8b5cf6" },
]

function EntryCards({ counts }: { counts: ReturnType<typeof countByStatus> }) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""

    return (
        <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
            {ENTRIES.map((e) => (
                <button
                    key={e.label}
                    type="button"
                    onClick={() => navigate(`/${accountSetId}/${e.to}`)}
                    className="group relative overflow-hidden rounded-2xl border border-slate-100 bg-white px-5 py-4 text-center shadow-[0_1px_2px_rgba(16,24,40,0.04)] transition-all hover:-translate-y-0.5 hover:shadow-[0_6px_18px_rgba(16,24,40,0.08)]"
                >
                    <span className="block truncate text-[14px] font-medium text-slate-700">{e.label}</span>
                    <span className="mt-1 block h-4 truncate text-[11.5px] text-slate-400">{e.hint?.(counts) ?? ""}</span>
                    <span
                        className="absolute bottom-0 left-0 h-[3px] w-16 rounded-tr-full transition-all group-hover:w-24"
                        style={{ background: e.accent }}
                    />
                </button>
            ))}
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  左侧：新增凭证大按钮 + 待办清单                                      */
/* ------------------------------------------------------------------ */

function CreateVoucherCard({ data }: { data: DashboardData }) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const accountSet = useCurrentAccountSet()
    const counts = useMemo(() => countByStatus(data.vouchers), [data.vouchers])
    const carried = data.closing?.carriedPeriod
    const closed = data.closing?.closedPeriod

    type Todo = { label: string; value: string; done: boolean; to: string; icon: React.ComponentType<{ className?: string }> }

    const todos: Todo[] = [
        {
            label: "未过账凭证",
            value: counts.unposted > 0 ? `${counts.unposted} 张` : "全部已过账",
            done: counts.unposted === 0,
            to: `/${accountSetId}/vouchers/manage`,
            icon: FileTextOutlined,
        },
        {
            label: "草稿凭证",
            value: counts.draft > 0 ? `${counts.draft} 张待完善` : "无草稿",
            done: counts.draft === 0,
            to: `/${accountSetId}/vouchers/manage`,
            icon: ProfileOutlined,
        },
        {
            label: "结转损益",
            value: carried ? `已结转至 ${carried}` : "尚未结转",
            done: !!carried,
            to: `/${accountSetId}/closing`,
            icon: AccountBookOutlined,
        },
        {
            label: "期末结账",
            value: closed ? `已结账至 ${closed}` : "尚未结账",
            done: !!closed,
            to: `/${accountSetId}/closing`,
            icon: UnorderedListOutlined,
        },
    ]

    return (
        <section className="flex h-full flex-col rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <div className="flex flex-col items-center justify-center py-8">
                <button
                    type="button"
                    onClick={() => navigate(`/${accountSetId}/vouchers/new`)}
                    className="group flex flex-col items-center gap-3"
                >
                    <span className="flex size-[74px] items-center justify-center rounded-2xl border border-[#d9e6ff] bg-[#eef4ff] text-[#2f6bff] transition-all group-hover:border-[#2f6bff] group-hover:bg-[#2f6bff] group-hover:text-white">
                        <PlusOutlined className="text-[30px]" />
                    </span>
                    <span className="text-[14px] font-medium text-slate-700 group-hover:text-[#2f6bff]">新增凭证</span>
                </button>
            </div>

            <div className="border-t border-slate-100 pt-2">
                {todos.map((t) => {
                    const Icon = t.icon
                    return (
                        <button
                            key={t.label}
                            type="button"
                            onClick={() => navigate(t.to)}
                            className="group flex w-full items-center gap-2.5 rounded-lg px-2 py-2 text-left transition-colors hover:bg-slate-50"
                        >
                            <span
                                className={
                                    "flex size-6 shrink-0 items-center justify-center rounded-md " +
                                    (t.done ? "bg-[#e9f9f1] text-[#0f9d63]" : "bg-[#fef2f2] text-[#ef4444]")
                                }
                            >
                                {t.done ? <CheckCircleFilled className="text-[13px]" /> : <Icon className="text-[13px]" />}
                            </span>
                            <span className="flex-1 truncate text-[12.5px] text-slate-600">{t.label}</span>
                            <span className={"text-[12.5px] tabular-nums " + (t.done ? "text-slate-400" : "font-medium text-[#ef4444]")}>
                                {t.value}
                            </span>
                            <RightOutlined className="text-[10px] text-slate-300 group-hover:text-slate-400" />
                        </button>
                    )
                })}
            </div>

            <p className="mt-auto truncate pt-3 text-center text-[11.5px] text-slate-400">
                欢迎使用「{accountSet?.companyName || accountSet?.name || "当前账套"}」，数据实时取自已过账凭证
            </p>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  右侧：财务指标（两行 × 五列，可切月度 / 年度，可前后翻期间）           */
/* ------------------------------------------------------------------ */

type Metric = { item: string; label: string; kind?: "amount" | "percent" }

const METRICS: Metric[][] = [
    [
        { item: "资产总额", label: "资产总额" },
        { item: "负债总额", label: "负债总额" },
        { item: "所有者权益", label: "所有者权益" },
        { item: "应收款", label: "应收账款" },
        { item: "应付款", label: "应付账款" },
    ],
    [
        { item: "营业收入", label: "营业收入" },
        { item: "营业成本", label: "营业成本" },
        { item: "期间费用", label: "期间费用" },
        { item: "净利润", label: "净利润" },
        { item: "资产负债率(%)", label: "资产负债率", kind: "percent" },
    ],
]

type MetricMode = "month" | "year"

function metricValue(row: FinancialSummaryRowDto | null, mode: MetricMode, kind: Metric["kind"]): string {
    const raw = mode === "month" ? row?.currentAmount : row?.yearToDateAmount
    if (kind === "percent") return pct(raw)
    return wan(raw)
}

function MetricsCard({
    data,
    period,
    mode,
    onModeChange,
    onStepPeriod,
}: {
    data: DashboardData
    period: AccountingPeriod
    mode: MetricMode
    onModeChange: (mode: MetricMode) => void
    onStepPeriod: (delta: number) => void
}) {
    return (
        <section className="flex h-full flex-col rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <header className="flex items-center justify-between gap-3">
                <h3 className="text-[14px] font-semibold text-slate-800">
                    {period.year}年{period.month}月财务指标
                </h3>
                <div className="flex items-center gap-1">
                    <Button
                        type="text"
                        size="small"
                        icon={<LeftOutlined className="text-[11px]" />}
                        title="上一月"
                        onClick={() => onStepPeriod(-1)}
                    />
                    <span className="min-w-[86px] text-center text-[12.5px] tabular-nums text-slate-600">
                        {period.year}年{period.month}月
                    </span>
                    <Button
                        type="text"
                        size="small"
                        icon={<RightOutlined className="text-[11px]" />}
                        title="下一月"
                        onClick={() => onStepPeriod(1)}
                    />
                </div>
            </header>
            <div className="mt-1 text-right text-[11.5px] text-slate-400">单位：万元</div>

            <div className="mt-2 flex flex-1 flex-col justify-center divide-y divide-slate-100">
                {METRICS.map((row, ri) => (
                    <div key={ri} className="grid flex-1 grid-cols-5 items-center gap-2 py-3">
                        {row.map((m) => {
                            const r = findRow(data.summary, m.item)
                            const raw = mode === "month" ? r?.currentAmount : r?.yearToDateAmount
                            return (
                                <div key={m.item} className="min-w-0 text-center">
                                    <div
                                        className="truncate text-[16px] font-semibold tabular-nums text-slate-800"
                                        title={m.kind === "percent" ? undefined : raw == null ? undefined : `¥ ${fmtAmount(raw, true)}`}
                                    >
                                        {metricValue(r, mode, m.kind)}
                                    </div>
                                    <div className="mt-1 truncate text-[11.5px] text-slate-400">{m.label}</div>
                                </div>
                            )
                        })}
                    </div>
                ))}
            </div>

            <footer className="mt-2 flex items-center justify-between gap-3 border-t border-slate-100 pt-3">
                <span className="truncate text-[11.5px] text-slate-400">口径：财务概要 · 仅统计已过账凭证</span>
                <div className="flex shrink-0 items-center gap-0.5 bg-slate-100 p-0.5">
                    {(
                        [
                            { key: "month", label: "月度指标" },
                            { key: "year", label: "年度指标" },
                        ] as { key: MetricMode; label: string }[]
                    ).map((o) => (
                        <button
                            key={o.key}
                            type="button"
                            onClick={() => onModeChange(o.key)}
                            className={
                                "px-3 py-1 text-[12px] transition-colors " +
                                (mode === o.key ? "bg-white font-medium text-[#2f6bff] shadow-sm" : "text-slate-500 hover:text-slate-700")
                            }
                        >
                            {o.label}
                        </button>
                    ))}
                </div>
            </footer>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  底部横幅：推广「凭证批量导入」                                        */
/* ------------------------------------------------------------------ */

function ImportBanner() {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""

    return (
        <section className="flex flex-wrap items-center gap-4 overflow-hidden rounded-2xl bg-gradient-to-r from-[#eaf2ff] via-[#f3f8ff] to-[#eefaf3] px-6 py-5">
            <div className="flex items-center gap-4">
                <span className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-white text-[#2f6bff] shadow-[0_2px_8px_rgba(47,107,255,0.16)]">
                    <UploadOutlined className="text-[22px]" />
                </span>
                <div className="min-w-0">
                    <h3 className="text-[15px] font-semibold text-slate-800">凭证一张张敲太慢？</h3>
                    <p className="mt-0.5 text-[12.5px] leading-5 text-slate-500">
                        支持从其他平台导出的 .xls / .xlsx 凭证文件批量导入，自动补建缺失科目，导入后统一审核过账。
                    </p>
                </div>
            </div>
            <Button
                type="primary"
                className="ml-auto"
                onClick={() => navigate(`/${accountSetId}/vouchers/manage`)}
            >
                去导入凭证
                <ArrowRightOutlined className="ml-1 text-[12px]" />
            </Button>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  空账套提示                                                          */
/* ------------------------------------------------------------------ */

function EmptyNotice() {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    return (
        <section className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-white p-8 text-center">
            <span className="flex size-11 items-center justify-center bg-[#eef4ff] text-[#2f6bff]">
                <AccountBookOutlined className="text-[22px]" />
            </span>
            <h3 className="text-[15px] font-semibold text-slate-800">这个账套还没有数据</h3>
            <p className="max-w-lg text-[13px] leading-relaxed text-slate-500">
                报表与首页指标只统计<b className="text-slate-700">已过账</b>的凭证。先在
                <b className="text-slate-700">设置 · 期初</b>录入期初余额，再录入凭证并审核过账，首页指标就会实时更新。
            </p>
            <div className="mt-1 flex flex-wrap items-center justify-center gap-2">
                <Button onClick={() => navigate(`/${accountSetId}/settings/opening-balances`)}>录入期初</Button>
                <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate(`/${accountSetId}/vouchers/new`)}>
                    新增凭证
                </Button>
            </div>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  加载 / 失败状态                                                     */
/* ------------------------------------------------------------------ */

function SkeletonBlock({ height }: { height: number }) {
    return <Skeleton.Button active block style={{ height }} />
}

function LoadingView() {
    return (
        <div className="flex flex-col gap-5">
            <SkeletonBlock height={44} />
            <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
                {[0, 1, 2, 3].map((i) => (
                    <SkeletonBlock key={i} height={80} />
                ))}
            </div>
            <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(0,0.95fr)_minmax(0,1.05fr)]">
                <SkeletonBlock height={300} />
                <SkeletonBlock height={300} />
            </div>
            <SkeletonBlock height={92} />
        </div>
    )
}

function ErrorView({ message, onRetry }: { message: string; onRetry: () => void }) {
    return (
        <section className="flex flex-col items-center gap-3 rounded-2xl border border-slate-100 bg-white p-10 text-center">
            <span className="flex size-12 items-center justify-center bg-[#fef2f2] text-[#ef4444]">
                <CloseCircleOutlined className="text-[24px]" />
            </span>
            <h3 className="text-[15px] font-semibold text-slate-800">首页数据加载失败</h3>
            <p className="max-w-md text-[13px] text-slate-500">{message}</p>
            <Button icon={<ReloadOutlined />} className="mt-1" onClick={onRetry}>
                重试
            </Button>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  首页                                                               */
/* ------------------------------------------------------------------ */

export default function DashboardHome() {
    const accountSetId = useCurrentAccountSetId() ?? ""
    const defaultPeriod = useDefaultAccountingPeriod()
    /** 期间步进器改过的月份；null 表示跟随账套默认期间。 */
    const [override, setOverride] = useState<{ year: number; month: number } | null>(null)
    /** 月度 / 年度口径放在页面级：切换期间会重新取数，不能跟着卡片重挂被重置。 */
    const [mode, setMode] = useState<MetricMode>("month")
    const period = useMemo(
        () => (override ? makePeriod(override.year, override.month) : defaultPeriod),
        [override, defaultPeriod],
    )

    // 换账套时回到该账套的默认期间，避免把上一个账套的浏览位置带过来。
    useEffect(() => {
        setOverride(null)
    }, [accountSetId])

    const data = useDashboardData(period)
    const counts = useMemo(() => countByStatus(data.vouchers), [data.vouchers])

    // 首次取数才铺整屏骨架；之后切期间只做轻微变暗，避免版面跳动、状态被重置。
    const firstLoad = data.loading && data.summary == null && data.error == null
    if (firstLoad) return <LoadingView />
    if (data.error) return <ErrorView message={data.error} onRetry={data.reload} />

    const hasData =
        data.vouchers.length > 0 ||
        (data.summary?.rows?.some((r) => (r.currentAmount ?? 0) !== 0 || (r.yearToDateAmount ?? 0) !== 0) ?? false)

    function stepPeriod(delta: number) {
        const next = shiftPeriod(period, delta)
        setOverride({ year: next.year, month: next.month })
    }

    return (
        <div className={"flex flex-col gap-5 transition-opacity " + (data.loading ? "pointer-events-none opacity-60" : "")}>
            <NoticeStrip data={data} counts={counts} />
            <EntryCards counts={counts} />

            <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(0,0.95fr)_minmax(0,1.05fr)]">
                <CreateVoucherCard data={data} />
                <MetricsCard data={data} period={period} mode={mode} onModeChange={setMode} onStepPeriod={stepPeriod} />
            </div>

            {!hasData && <EmptyNotice />}

            <ImportBanner />
        </div>
    )
}
