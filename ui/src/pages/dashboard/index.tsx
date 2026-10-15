import { useMemo } from "react"
import { useNavigate } from "react-router"

import {
    AccountBookOutlined,
    BarChartOutlined,
    BankOutlined,
    BookOutlined,
    CalendarOutlined,
    CheckCircleFilled,
    CloseCircleOutlined,
    DollarOutlined,
    FileAddOutlined,
    FileTextOutlined,
    ProfileOutlined,
    ReloadOutlined,
    RightOutlined,
    RiseOutlined,
    FallOutlined,
    UnorderedListOutlined,
    WalletOutlined,
    WarningFilled,
} from "@ant-design/icons"
import { Button, Skeleton } from "antd"

import { fmtAmount } from "@/components/ledger/format"
import { useCurrentAccountSet, useDefaultAccountingPeriod } from "@/accountSets/useCurrentAccountSet"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import type { FinancialSummaryRowDto } from "@/apis-gen"
import { countByStatus, findRow, useDashboardData, type DashboardData } from "./useDashboardData"

/* ------------------------------------------------------------------ */
/*  金额 / 比率展示（统一走账簿 fmtAmount，不重复造轮子）                 */
/* ------------------------------------------------------------------ */

/** 金额：无数据（null/undefined）显示「--」，0 正常显示「0.00」。 */
function money(value: number | null | undefined): string {
    if (value == null) return "--"
    return "¥ " + fmtAmount(value, true)
}

/** 百分比指标（如资产负债率）。 */
function percent(value: number | null | undefined): string {
    if (value == null) return "--"
    return fmtAmount(value, true) + "%"
}

/** 环比标签：null 显示「--」，正为绿、负为红。 */
function DeltaTag({ pct }: { pct?: number | null }) {
    if (pct == null) {
        return <span className="text-[12px] text-slate-400">环比 --</span>
    }
    const up = pct >= 0
    return (
        <span className={"flex items-center gap-0.5 text-[12px] font-medium " + (up ? "text-[#10b981]" : "text-[#ef4444]")}>
            {up ? <RiseOutlined className="text-[14px]" /> : <FallOutlined className="text-[14px]" />}
            {Math.abs(pct).toFixed(1)}%
            <span className="font-normal text-slate-400">环比</span>
        </span>
    )
}

/* ------------------------------------------------------------------ */
/*  顶部：账套 / 会计期间 / 结账结转状态                                 */
/* ------------------------------------------------------------------ */

function AccountStatusBar({ data }: { data: DashboardData }) {
    const accountSet = useCurrentAccountSet()
    const period = useDefaultAccountingPeriod()
    const periodText = data.summary?.periodText ?? period.label

    const carried = data.closing?.carriedPeriod
    const closed = data.closing?.closedPeriod

    return (
        <section className="rounded-2xl border border-slate-100 bg-white px-5 py-4 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <div className="flex flex-wrap items-center gap-x-6 gap-y-3">
                <div className="min-w-0">
                    <h1 className="truncate text-[18px] font-bold text-slate-800">
                        {accountSet?.companyName || accountSet?.name || "工作台"}
                    </h1>
                    <div className="mt-1 flex items-center gap-1.5 text-[12.5px] text-slate-500">
                        <CalendarOutlined className="text-[14px] text-slate-400" />
                        当前会计期间
                        <span className="font-medium text-slate-700">{periodText}</span>
                        <span className="text-slate-400">（{accountSet?.name ?? "—"} · {accountSet?.baseCurrency ?? "人民币"}）</span>
                    </div>
                </div>
                <div className="ml-auto flex flex-wrap items-center gap-2">
                    <StatusBadge
                        done={!!closed}
                        label={closed ? `已结账至 ${closed}` : "本月尚未结账"}
                    />
                    <StatusBadge
                        done={!!carried}
                        label={carried ? `已结转至 ${carried}` : "损益尚未结转"}
                    />
                </div>
            </div>
        </section>
    )
}

function StatusBadge({ done, label }: { done: boolean; label: string }) {
    return (
        <span
            className={
                "inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-[12px] font-medium " +
                (done ? "bg-[#d1fae5] text-[#0f9d63]" : "bg-[#fef3c7] text-[#b45309]")
            }
        >
            {done ? <CheckCircleFilled className="text-[14px]" /> : <WarningFilled className="text-[14px]" />}
            {label}
        </span>
    )
}

/* ------------------------------------------------------------------ */
/*  指标卡（财务概要聚合）                                               */
/* ------------------------------------------------------------------ */

type CardSpec = {
    item: string
    label: string
    icon: React.ComponentType<{ className?: string }>
    accent: string
    cardBg: string
}

const CARDS: CardSpec[] = [
    { item: "营业收入", label: "本月营业收入", icon: BarChartOutlined, accent: "#2f6bff", cardBg: "#eef4ff" },
    { item: "净利润", label: "本月净利润", icon: DollarOutlined, accent: "#8b5cf6", cardBg: "#f1eaff" },
    { item: "资金收支", label: "本月资金净收付", icon: WalletOutlined, accent: "#10b981", cardBg: "#e6f8f0" },
    { item: "资产总额", label: "期末资产总额", icon: BankOutlined, accent: "#f59e0b", cardBg: "#fff4e0" },
]

function StatCards({ data }: { data: DashboardData }) {
    return (
        <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {CARDS.map((c) => {
                const row = findRow(data.summary, c.item)
                const Icon = c.icon
                return (
                    <div key={c.item} className="rounded-2xl p-5" style={{ background: c.cardBg }}>
                        <div className="flex items-center gap-2.5">
                            <span
                                className="flex size-9 items-center justify-center rounded-xl text-white shadow-sm"
                                style={{ background: c.accent }}
                            >
                                <Icon className="text-[20px]" />
                            </span>
                            <span className="text-[13px] font-medium" style={{ color: c.accent }}>
                                {c.label}
                            </span>
                        </div>
                        <div className="mt-4 text-[22px] font-bold tracking-tight text-slate-800 tabular-nums">
                            {money(row?.currentAmount)}
                        </div>
                        <div className="mt-2">
                            <DeltaTag pct={row?.currentMomPercent} />
                        </div>
                    </div>
                )
            })}
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  财务概览明细                                                        */
/* ------------------------------------------------------------------ */

const OVERVIEW_GROUPS: { title: string; items: { item: string; label: string; kind: "money" | "percent" }[] }[] = [
    {
        title: "盈利状况",
        items: [
            { item: "营业收入", label: "营业收入", kind: "money" },
            { item: "营业成本", label: "营业成本", kind: "money" },
            { item: "利润总额", label: "利润总额", kind: "money" },
            { item: "净利润", label: "净利润", kind: "money" },
        ],
    },
    {
        title: "三项费用",
        items: [
            { item: "管理费用", label: "管理费用", kind: "money" },
            { item: "销售费用", label: "销售费用", kind: "money" },
            { item: "财务费用", label: "财务费用", kind: "money" },
        ],
    },
    {
        title: "应收应付与税金",
        items: [
            { item: "应收款", label: "应收款", kind: "money" },
            { item: "应付款", label: "应付款", kind: "money" },
            { item: "应交税费", label: "应交税费", kind: "money" },
        ],
    },
    {
        title: "存量与比率",
        items: [
            { item: "资产总额", label: "资产总额", kind: "money" },
            { item: "负债总额", label: "负债总额", kind: "money" },
            { item: "所有者权益", label: "所有者权益", kind: "money" },
            { item: "资产负债率(%)", label: "资产负债率", kind: "percent" },
        ],
    },
]

function FinanceOverview({ data }: { data: DashboardData }) {
    return (
        <section className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {OVERVIEW_GROUPS.map((g) => (
                <div key={g.title} className="rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                    <h3 className="mb-3 text-[14px] font-semibold text-slate-800">{g.title}</h3>
                    <ul className="divide-y divide-slate-50">
                        {g.items.map((it) => {
                            const row: FinancialSummaryRowDto | null = findRow(data.summary, it.item)
                            const value = it.kind === "percent" ? percent(row?.currentAmount) : money(row?.currentAmount)
                            return (
                                <li key={it.item} className="flex items-center justify-between py-2 text-[13px]">
                                    <span className="text-slate-500">{it.label}</span>
                                    <span className="font-medium tabular-nums text-slate-800">{value}</span>
                                </li>
                            )
                        })}
                    </ul>
                </div>
            ))}
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  待办：真实、可点的检查项                                             */
/* ------------------------------------------------------------------ */

function TodoCard({ data }: { data: DashboardData }) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const counts = useMemo(() => countByStatus(data.vouchers), [data.vouchers])
    const carried = data.closing?.carriedPeriod
    const closed = data.closing?.closedPeriod

    type Todo = { label: string; value: string; done?: boolean; danger?: boolean; to: string; icon: React.ComponentType<{ className?: string }> }

    const todos: Todo[] = [
        {
            label: "未过账凭证",
            value: counts.unposted > 0 ? `${counts.unposted} 张` : "全部已过账",
            done: counts.unposted === 0,
            danger: counts.unposted > 0,
            to: `/${accountSetId}/vouchers/manage`,
            icon: FileTextOutlined,
        },
        {
            label: "待审核凭证（草稿）",
            value: counts.draft > 0 ? `${counts.draft} 张` : "无草稿",
            done: counts.draft === 0,
            danger: counts.draft > 0,
            to: `/${accountSetId}/vouchers/manage`,
            icon: ProfileOutlined,
        },
        {
            label: "期末结转损益",
            value: carried ? `已结转至 ${carried}` : "尚未结转",
            done: !!carried,
            danger: !carried,
            to: `/${accountSetId}/closing`,
            icon: AccountBookOutlined,
        },
        {
            label: "期末结账",
            value: closed ? `已结账至 ${closed}` : "尚未结账",
            done: !!closed,
            danger: !closed,
            to: `/${accountSetId}/closing`,
            icon: UnorderedListOutlined,
        },
    ]

    return (
        <section className="rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <h3 className="mb-2 text-[14px] font-semibold text-slate-800">待办事项</h3>
            <ul className="flex flex-col">
                {todos.map((t) => {
                    const Icon = t.icon
                    return (
                        <li key={t.label} className="border-b border-slate-50 last:border-0">
                            <button
                                type="button"
                                onClick={() => navigate(t.to)}
                                className="flex w-full items-center gap-3 py-2.5 text-left transition-colors hover:bg-slate-50"
                            >
                                <span
                                    className={
                                        "flex size-8 shrink-0 items-center justify-center rounded-lg " +
                                        (t.done ? "bg-[#e6f8f0] text-[#10b981]" : "bg-[#fef2f2] text-[#ef4444]")
                                    }
                                >
                                    {t.done ? <CheckCircleFilled className="text-[16px]" /> : <Icon className="text-[16px]" />}
                                </span>
                                <span className="flex-1 truncate text-[13px] text-slate-600">{t.label}</span>
                                <span
                                    className={
                                        "text-[13px] font-medium tabular-nums " +
                                        (t.danger ? "text-[#ef4444]" : "text-slate-500")
                                    }
                                >
                                    {t.value}
                                </span>
                                <RightOutlined className="text-[14px] text-slate-300" />
                            </button>
                        </li>
                    )
                })}
            </ul>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  快捷入口（真实路由）                                                 */
/* ------------------------------------------------------------------ */

function QuickActions() {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""

    const actions = [
        { label: "新增凭证", to: `/${accountSetId}/vouchers/new`, icon: FileAddOutlined },
        { label: "查看凭证", to: `/${accountSetId}/vouchers/manage`, icon: FileTextOutlined },
        { label: "科目余额表", to: `/${accountSetId}/ledger/trial-balance`, icon: BookOutlined },
        { label: "资产负债表", to: `/${accountSetId}/reports/balance-sheet`, icon: AccountBookOutlined },
        { label: "利润表", to: `/${accountSetId}/reports/income-statement`, icon: BarChartOutlined },
    ]

    return (
        <section className="rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <h3 className="mb-4 text-[14px] font-semibold text-slate-800">快捷入口</h3>
            <div className="grid grid-cols-3 gap-y-4 sm:grid-cols-5">
                {actions.map((a) => {
                    const Icon = a.icon
                    return (
                        <button
                            key={a.label}
                            type="button"
                            onClick={() => navigate(a.to)}
                            className="flex flex-col items-center gap-1.5"
                        >
                            <span className="flex size-[52px] items-center justify-center rounded-2xl bg-[#eef4ff] text-[#2f6bff] transition-colors hover:bg-[#dbe8ff]">
                                <Icon className="text-[24px]" />
                            </span>
                            <span className="text-[12px] text-slate-600">{a.label}</span>
                        </button>
                    )
                })}
            </div>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  空账套 / 未过账提示                                                  */
/* ------------------------------------------------------------------ */

function EmptyNotice() {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    return (
        <section className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-200 bg-white p-10 text-center">
            <span className="flex size-12 items-center justify-center rounded-full bg-[#eef4ff] text-[#2f6bff]">
                <BookOutlined className="text-[24px]" />
            </span>
            <h3 className="text-[15px] font-semibold text-slate-800">这个账套还没有数据</h3>
            <p className="max-w-md text-[13px] leading-relaxed text-slate-500">
                报表与首页指标只统计<b className="text-slate-700">已过账</b>的凭证。请先录入凭证，再到凭证页<b className="text-slate-700">审核并过账</b>，
                随后回到首页即可看到真实的财务数字。
            </p>
            <Button icon={<FileAddOutlined />} className="mt-1" onClick={() => navigate(`/${accountSetId}/vouchers/new`)}>
                去新增凭证
            </Button>
        </section>
    )
}

/** 有过账凭证以外的未过账凭证时，提示报表口径。 */
function PostHint({ unposted }: { unposted: number }) {
    if (unposted <= 0) return null
    return (
        <div className="flex items-center gap-2 rounded-xl bg-[#fef3c7] px-4 py-2.5 text-[12.5px] text-[#b45309]">
            <WarningFilled className="shrink-0 text-[16px]" />
            账簿与报表仅统计已过账凭证；当前有 {unposted} 张凭证未过账，相关指标可能偏低，请先到凭证页审核并过账。
        </div>
    )
}

/* ------------------------------------------------------------------ */
/*  加载 / 失败状态                                                     */
/* ------------------------------------------------------------------ */

/** 加载占位块：antd 骨架屏闪烁 + 原有的圆角与高度节奏。 */
function SkeletonBlock({ height }: { height: number }) {
    return <Skeleton.Button active block style={{ height, borderRadius: 16 }} />
}

function LoadingView() {
    return (
        <div className="flex flex-col gap-5">
            <SkeletonBlock height={72} />
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {[0, 1, 2, 3].map((i) => (
                    <SkeletonBlock key={i} height={124} />
                ))}
            </div>
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                <SkeletonBlock height={200} />
                <SkeletonBlock height={200} />
            </div>
        </div>
    )
}

function ErrorView({ message, onRetry }: { message: string; onRetry: () => void }) {
    return (
        <section className="flex flex-col items-center gap-3 rounded-2xl border border-slate-100 bg-white p-10 text-center">
            <span className="flex size-12 items-center justify-center rounded-full bg-[#fef2f2] text-[#ef4444]">
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
    const data = useDashboardData()
    const counts = useMemo(() => countByStatus(data.vouchers), [data.vouchers])

    if (data.loading) return <LoadingView />
    if (data.error) return <ErrorView message={data.error} onRetry={data.reload} />

    const hasData =
        data.vouchers.length > 0 ||
        (data.summary?.rows?.some((r) => (r.currentAmount ?? 0) !== 0 || (r.yearToDateAmount ?? 0) !== 0) ?? false)

    return (
        <div className="flex flex-col gap-5">
            <AccountStatusBar data={data} />

            {!hasData ? (
                <>
                    <QuickActions />
                    <EmptyNotice />
                </>
            ) : (
                <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(0,1fr)_340px]">
                    {/* 左侧主列 */}
                    <div className="flex min-w-0 flex-col gap-5">
                        <PostHint unposted={counts.unposted} />
                        <StatCards data={data} />
                        <FinanceOverview data={data} />
                    </div>
                    {/* 右侧副列 */}
                    <div className="flex flex-col gap-5">
                        <QuickActions />
                        <TodoCard data={data} />
                    </div>
                </div>
            )}
        </div>
    )
}
