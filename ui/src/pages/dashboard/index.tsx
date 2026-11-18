import { useEffect, useMemo, useState } from "react"
import type { ComponentType, ReactNode } from "react"
import { useNavigate } from "react-router"
import { Button } from "antd"

import {
    AccountBookOutlined,
    ArrowDownOutlined,
    ArrowRightOutlined,
    ArrowUpOutlined,
    BankOutlined,
    CheckCircleFilled,
    CheckCircleOutlined,
    CloseCircleOutlined,
    FileTextOutlined,
    FundOutlined,
    InfoCircleFilled,
    LeftOutlined,
    LineChartOutlined,
    PieChartOutlined,
    PlusOutlined,
    ReloadOutlined,
    RightOutlined,
    RiseOutlined,
    TableOutlined,
    UploadOutlined,
    WalletOutlined,
} from "@ant-design/icons"

import { fmtAmount } from "@/components/ledger/format"
import {
    makePeriod,
    shiftPeriod,
    useCurrentAccountSet,
    useDefaultAccountingPeriod,
    type AccountingPeriod,
} from "@/accountSets/useCurrentAccountSet"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import type { FinancialSummaryDto, VoucherListItemDto } from "@/apis-gen"
import { setPath } from "@/accountSets/url-id"
import {
    buildCashTrend,
    countByStatus,
    findRow,
    isInPeriod,
    latestVouchers,
    useDashboardData,
    type CashTrendPoint,
    type DashboardData,
} from "./useDashboardData"

/* ------------------------------------------------------------------ */
/*  公共样式与格式化                                                     */
/* ------------------------------------------------------------------ */

const CARD =
    "rounded-md border border-(--pk-border) bg-(--pk-surface) shadow-[0_1px_2px_rgba(16,24,40,0.03)]"

const MUTED = "text-(--pk-text-2)"

/** 元 → 万元文本；null 显示「--」。 */
function wan(value: number | null | undefined): string {
    if (value == null) return "--"
    return (value / 10000).toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 百分比文本。 */
function pct(value: number | null | undefined): string {
    if (value == null) return "--"
    return fmtAmount(value, true) + "%"
}

function pad(n: number) {
    return String(n).padStart(2, "0")
}

/** 后端 DateOnly 序列化成 UTC 零点，展示按 UTC 取日历日。 */
function monthDay(value?: Date | string | null): string {
    if (!value) return "--"
    const d = new Date(value)
    return `${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`
}

function greeting(): string {
    const hour = new Date().getHours()
    if (hour < 6) return "夜深了"
    if (hour < 9) return "早上好"
    if (hour < 12) return "上午好"
    if (hour < 14) return "中午好"
    if (hour < 18) return "下午好"
    return "晚上好"
}

/* ------------------------------------------------------------------ */
/*  顶部：账套与期间概览 + 快捷操作                                       */
/* ------------------------------------------------------------------ */

const HERO_CHIP = {
    neutral: "bg-(--pk-gray-bg) text-(--pk-text-2)",
    ok: "bg-[color-mix(in_srgb,var(--pk-green)_12%,var(--pk-surface))] text-(--pk-green)",
    warn: "bg-[color-mix(in_srgb,var(--pk-orange)_14%,var(--pk-surface))] text-(--pk-orange)",
} as const

function HeroChip({ tone = "neutral", children }: { tone?: keyof typeof HERO_CHIP; children: ReactNode }) {
    return (
        <span className={"inline-flex h-[24px] items-center gap-1 rounded px-2 text-[11.5px] font-medium " + HERO_CHIP[tone]}>
            {children}
        </span>
    )
}

function HeroBanner({
    period,
    closing,
    counts,
    onStepPeriod,
}: {
    period: AccountingPeriod
    closing: DashboardData["closing"]
    counts: ReturnType<typeof countByStatus>
    onStepPeriod: (delta: number) => void
}) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const accountSet = useCurrentAccountSet()
    const { user } = useAuth()
    const companyName = accountSet?.companyName || accountSet?.name || "当前账套"
    const base = setPath(accountSetId)

    const carriedDone = !!closing?.carriedPeriod && closing.carriedPeriod >= period.label
    const closedDone = !!closing?.closedPeriod && closing.closedPeriod >= period.label

    return (
        <section
            className="relative overflow-hidden rounded-md border border-(--pk-border) px-5 py-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)] md:px-6"
            style={{ background: "linear-gradient(112deg, var(--pk-surface) 0%, var(--pk-surface) 45%, color-mix(in srgb, var(--pk-brand) 7%, var(--pk-surface)) 100%)" }}
        >
            {/* 装饰光圈：只做氛围，不承载信息 */}
            <span
                className="pointer-events-none absolute -top-24 -right-16 size-64 rounded-full"
                style={{ background: "color-mix(in srgb, var(--pk-brand) 6%, transparent)" }}
            />
            <span
                className="pointer-events-none absolute -bottom-28 right-24 size-56 rounded-full"
                style={{ background: "color-mix(in srgb, var(--pk-brand) 4%, transparent)" }}
            />

            <div className="relative flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
                <div className="min-w-0">
                    <div className="text-[12.5px] text-(--pk-text-2)">
                        {greeting()}，{user?.displayName || user?.username || "管理员"}
                    </div>
                    <h1 className="mt-1 truncate text-[20px] font-semibold tracking-wide text-(--pk-text)" title={companyName}>
                        {companyName}
                    </h1>
                    <div className="mt-3 flex flex-wrap items-center gap-2">
                        <div className="flex items-center rounded-full border border-(--pk-border) bg-(--pk-gray-bg) p-0.5">
                            <button
                                type="button"
                                aria-label="上一期间"
                                onClick={() => onStepPeriod(-1)}
                                className="flex size-6 cursor-pointer items-center justify-center rounded-full text-(--pk-text-2) transition-colors hover:bg-(--pk-surface) hover:text-(--pk-text)"
                            >
                                <LeftOutlined className="text-[10px]" />
                            </button>
                            <span className="min-w-[86px] text-center text-[12.5px] font-medium tabular-nums text-(--pk-text)">
                                {period.year}年{period.month}月
                            </span>
                            <button
                                type="button"
                                aria-label="下一期间"
                                onClick={() => onStepPeriod(1)}
                                className="flex size-6 cursor-pointer items-center justify-center rounded-full text-(--pk-text-2) transition-colors hover:bg-(--pk-surface) hover:text-(--pk-text)"
                            >
                                <RightOutlined className="text-[10px]" />
                            </button>
                        </div>
                        {counts.total === 0 ? (
                            <HeroChip>本期无凭证</HeroChip>
                        ) : (
                            <HeroChip tone={counts.unposted > 0 ? "warn" : "ok"}>
                                {counts.unposted > 0 ? `${counts.unposted} 张待入账` : "本期已入账"}
                            </HeroChip>
                        )}
                        <HeroChip tone={carriedDone ? "ok" : "neutral"}>
                            {carriedDone ? `已结转 ${closing?.carriedPeriod}` : "待结转"}
                        </HeroChip>
                        <HeroChip tone={closedDone ? "ok" : "neutral"}>
                            {closedDone ? `已结账 ${closing?.closedPeriod}` : "待结账"}
                        </HeroChip>
                    </div>
                </div>

                <div className="flex shrink-0 flex-wrap items-center gap-2">
                    <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate(`${base}/vouchers/new`)}>
                        新增凭证
                    </Button>
                    <button
                        type="button"
                        onClick={() => navigate(`${base}/vouchers/manage?import=1`)}
                        className="inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-md border border-(--pk-border) bg-(--pk-surface) px-3.5 text-[12.5px] text-(--pk-text) transition-colors hover:bg-(--pk-gray-bg)"
                    >
                        <UploadOutlined className="text-[12px]" />
                        导入凭证
                    </button>
                </div>
            </div>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  核心指标卡                                                          */
/* ------------------------------------------------------------------ */

type KpiSpec = {
    label: string
    value: number | null
    change: number | null
    changeLabel: string
    secondary: string
    icon: ComponentType<{ className?: string }>
    tone: string
    /** true：涨为好；false：跌为好；null：中性，不判红绿。 */
    goodWhenUp: boolean | null
    to: string
}

function DeltaChip({ value, goodWhenUp }: { value: number | null; goodWhenUp: boolean | null }) {
    if (value == null) {
        return <span className={"text-[11.5px] " + MUTED}>—</span>
    }
    if (value === 0) {
        return (
            <span className="rounded px-1 py-0.5 text-[11px] tabular-nums text-(--pk-text-2)">0.00%</span>
        )
    }
    const up = value > 0
    const good = goodWhenUp == null ? null : up === goodWhenUp
    const color = good == null ? "var(--pk-brand-text)" : good ? "var(--pk-green)" : "var(--pk-red)"
    return (
        <span
            className="inline-flex items-center gap-0.5 rounded px-1 py-0.5 text-[11px] font-medium tabular-nums"
            style={{ color, background: `color-mix(in srgb, ${color} 10%, var(--pk-surface))` }}
        >
            {up ? <ArrowUpOutlined className="text-[9px]" /> : <ArrowDownOutlined className="text-[9px]" />}
            {Math.abs(value).toFixed(2)}%
        </span>
    )
}

function KpiCard({ spec, onOpen }: { spec: KpiSpec; onOpen: () => void }) {
    const Icon = spec.icon
    const negative = (spec.value ?? 0) < 0
    return (
        <button
            type="button"
            onClick={onOpen}
            className={
                CARD +
                " group min-w-0 p-4 text-left transition-colors hover:border-(--pk-brand-border) hover:shadow-[0_4px_14px_rgba(39,60,243,0.08)]"
            }
        >
            <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                    <div className={"text-[12.5px] " + MUTED}>{spec.label}</div>
                    <div className="mt-2 flex items-baseline gap-1">
                        <span
                            className="truncate text-[23px] leading-none font-semibold tabular-nums"
                            style={negative ? { color: "var(--pk-red)" } : undefined}
                            title={spec.value == null ? undefined : `¥ ${fmtAmount(spec.value, true)}`}
                        >
                            {wan(spec.value)}
                        </span>
                        <span className={"text-[11.5px] " + MUTED}>万元</span>
                    </div>
                </div>
                <span
                    className="flex size-9 shrink-0 items-center justify-center rounded-md text-[17px]"
                    style={{ color: spec.tone, background: `color-mix(in srgb, ${spec.tone} 10%, var(--pk-surface))` }}
                >
                    <Icon />
                </span>
            </div>
            <div className="mt-3 flex items-center justify-between gap-2 border-t border-(--pk-border) pt-2.5">
                <span className={"flex min-w-0 items-center gap-1 text-[11.5px] " + MUTED}>
                    <span className="shrink-0">{spec.changeLabel}</span>
                    <DeltaChip value={spec.change} goodWhenUp={spec.goodWhenUp} />
                </span>
                <span className={"truncate text-[11.5px] " + MUTED} title={spec.secondary}>
                    {spec.secondary}
                </span>
            </div>
        </button>
    )
}

function KpiRow({ summary, onOpen }: { summary: FinancialSummaryDto | null; onOpen: (to: string) => void }) {
    const assets = findRow(summary, "资产总额")?.currentAmount ?? null
    const liabilities = findRow(summary, "负债总额")?.currentAmount ?? null
    const revenueRow = findRow(summary, "营业收入")
    const profitRow = findRow(summary, "净利润")
    const debtRatio = findRow(summary, "资产负债率(%)")?.currentAmount ?? null
    const revenue = revenueRow?.currentAmount ?? null
    const profit = profitRow?.currentAmount ?? null

    const specs: KpiSpec[] = [
        {
            label: "资产总额",
            value: assets,
            change: findRow(summary, "资产总额")?.currentMomPercent ?? null,
            changeLabel: "较上月末",
            secondary: debtRatio == null ? "期末余额" : `资产负债率 ${pct(debtRatio)}`,
            icon: WalletOutlined,
            tone: "var(--pk-brand-text)",
            goodWhenUp: null,
            to: "reports/balance-sheet",
        },
        {
            label: "负债总额",
            value: liabilities,
            change: findRow(summary, "负债总额")?.currentMomPercent ?? null,
            changeLabel: "较上月末",
            secondary: assets && assets !== 0 ? `占总资产 ${pct(((liabilities ?? 0) / assets) * 100)}` : "期末余额",
            icon: BankOutlined,
            tone: "var(--pk-orange)",
            goodWhenUp: false,
            to: "reports/balance-sheet",
        },
        {
            label: "营业收入",
            value: revenue,
            change: revenueRow?.currentMomPercent ?? null,
            changeLabel: "较上月",
            secondary: `本年累计 ${wan(revenueRow?.yearToDateAmount)} 万`,
            icon: RiseOutlined,
            tone: "var(--pk-green)",
            goodWhenUp: true,
            to: "reports/income-statement",
        },
        {
            label: "净利润",
            value: profit,
            change: profitRow?.currentMomPercent ?? null,
            changeLabel: "较上月",
            secondary: revenue && revenue !== 0 ? `净利率 ${pct(((profit ?? 0) / revenue) * 100)}` : "本期经营成果",
            icon: FundOutlined,
            tone: "var(--pk-purple)",
            goodWhenUp: true,
            to: "reports/income-statement",
        },
    ]

    return (
        <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {specs.map((spec) => (
                <KpiCard key={spec.label} spec={spec} onOpen={() => onOpen(spec.to)} />
            ))}
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  资金收支趋势                                                        */
/* ------------------------------------------------------------------ */

const TREND_HEIGHT = 148

function CashTrendCard({
    points,
    summary,
    className,
}: {
    points: CashTrendPoint[]
    summary: FinancialSummaryDto | null
    className?: string
}) {
    const max = Math.max(1, ...points.flatMap((p) => [p.inflow, p.outflow]))
    const empty = points.every((p) => p.inflow === 0 && p.outflow === 0)
    const income = findRow(summary, "资金收入")?.currentAmount ?? null
    const expense = findRow(summary, "资金支付")?.currentAmount ?? null
    const net = findRow(summary, "资金收支")?.currentAmount ?? null

    const barHeight = (value: number) => (value > 0 ? Math.max(2, (value / max) * TREND_HEIGHT) : 2)

    return (
        <section className={CARD + " flex flex-col p-4 " + (className ?? "")}>
            <header className="flex flex-wrap items-start justify-between gap-2">
                <div>
                    <h2 className="text-[14px] font-semibold text-(--pk-text)">资金收支趋势</h2>
                    <p className={"mt-0.5 text-[11.5px] " + MUTED}>近 6 个月 · 单位：万元 · 仅统计已入账凭证</p>
                </div>
                <div className={"flex items-center gap-3 text-[11.5px] " + MUTED}>
                    <span className="inline-flex items-center gap-1.5">
                        <span className="size-2 rounded-sm bg-(--pk-brand)" />
                        资金收入
                    </span>
                    <span className="inline-flex items-center gap-1.5">
                        <span className="size-2 rounded-sm bg-(--pk-orange)" />
                        资金支付
                    </span>
                </div>
            </header>

            <div className="relative mt-4">
                {/* 背景网格：25% / 50% / 75% 三条基准线 */}
                <div className="pointer-events-none absolute inset-x-0 top-0" style={{ height: TREND_HEIGHT }}>
                    {[0, 1, 2, 3].map((i) => (
                        <span
                            key={i}
                            className="absolute inset-x-0 border-t border-dashed border-(--pk-border)"
                            style={{ top: `${(i / 4) * 100}%` }}
                        />
                    ))}
                </div>
                <div className="relative flex items-end gap-2 md:gap-4" style={{ height: TREND_HEIGHT }}>
                    {points.map((p) => (
                        <div key={`${p.year}-${p.month}`} className="flex h-full flex-1 items-end justify-center gap-1.5">
                            <div
                                className="w-[13px] rounded-t-[3px] bg-(--pk-brand) transition-all hover:opacity-85 md:w-[15px]"
                                style={{ height: barHeight(p.inflow), opacity: p.inflow > 0 ? 1 : 0.18 }}
                                title={`${p.year}年${p.month}月 资金收入 ¥${fmtAmount(p.inflow, true)}`}
                            />
                            <div
                                className="w-[13px] rounded-t-[3px] bg-(--pk-orange) transition-all hover:opacity-85 md:w-[15px]"
                                style={{ height: barHeight(p.outflow), opacity: p.outflow > 0 ? 1 : 0.18 }}
                                title={`${p.year}年${p.month}月 资金支付 ¥${fmtAmount(p.outflow, true)}`}
                            />
                        </div>
                    ))}
                    {empty && (
                        <div className={"absolute inset-0 flex items-center justify-center text-[12px] " + MUTED}>
                            本期暂无可统计的资金流水
                        </div>
                    )}
                </div>
                <div className="mt-2 flex gap-2 md:gap-4">
                    {points.map((p, index) => (
                        <div
                            key={`${p.year}-${p.month}-label`}
                            className={
                                "flex-1 text-center text-[11px] " +
                                (index === points.length - 1 ? "font-medium text-(--pk-text)" : MUTED)
                            }
                        >
                            {p.label}
                        </div>
                    ))}
                </div>
            </div>

            <footer className="mt-auto grid grid-cols-3 divide-x divide-(--pk-border) border-t border-(--pk-border) pt-3">
                {[
                    { label: "本期收入", value: income, color: "var(--pk-green)" },
                    { label: "本期支出", value: expense, color: "var(--pk-orange)" },
                    { label: "净流入", value: net, color: (net ?? 0) < 0 ? "var(--pk-red)" : "var(--pk-brand-text)" },
                ].map((item) => (
                    <div key={item.label} className="min-w-0 px-3 first:pl-1 last:pr-1">
                        <div className={"text-[11.5px] " + MUTED}>{item.label}</div>
                        <div className="mt-1 truncate text-[15px] font-semibold tabular-nums" style={{ color: item.color }}>
                            {wan(item.value)}
                            <span className={"ml-1 text-[11px] font-normal " + MUTED}>万</span>
                        </div>
                    </div>
                ))}
            </footer>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  月末待办                                                            */
/* ------------------------------------------------------------------ */

type TodoStep = { label: string; detail: string; done: boolean; to: string }

function TodoCard({
    period,
    closing,
    counts,
    className,
}: {
    period: AccountingPeriod
    closing: DashboardData["closing"]
    counts: ReturnType<typeof countByStatus>
    className?: string
}) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const base = setPath(accountSetId)
    const carried = closing?.carriedPeriod
    const closed = closing?.closedPeriod
    const carriedDone = !!carried && carried >= period.label
    const closedDone = !!closed && closed >= period.label

    const steps: TodoStep[] = [
        {
            label: "本期凭证入账",
            detail: counts.total === 0 ? "本期还没有凭证" : `${counts.total} 张凭证 · ${counts.unposted} 张待入账`,
            done: counts.total > 0 && counts.unposted === 0,
            to: `${base}/vouchers/manage`,
        },
        {
            label: "结转损益",
            detail: carriedDone ? `已结转至 ${carried}` : "结转后利润表与资产负债表口径一致",
            done: carriedDone,
            to: `${base}/closing`,
        },
        {
            label: "期末结账",
            detail: closedDone ? `已结账至 ${closed}` : "结账后本期凭证将被锁定",
            done: closedDone,
            to: `${base}/closing`,
        },
    ]
    const doneCount = steps.filter((step) => step.done).length
    const percent = Math.round((doneCount / steps.length) * 100)

    return (
        <section className={CARD + " flex flex-col p-4 " + (className ?? "")}>
            <header className="flex items-center justify-between gap-2">
                <div>
                    <h2 className="text-[14px] font-semibold text-(--pk-text)">月末待办</h2>
                    <p className={"mt-0.5 text-[11.5px] " + MUTED}>
                        已完成 {doneCount}/{steps.length} 项
                    </p>
                </div>
                <span
                    className="flex size-10 items-center justify-center rounded-full text-[13px] font-semibold tabular-nums"
                    style={{
                        color: percent === 100 ? "var(--pk-green)" : "var(--pk-brand-text)",
                        background:
                            percent === 100
                                ? "color-mix(in srgb, var(--pk-green) 10%, var(--pk-surface))"
                                : "color-mix(in srgb, var(--pk-brand) 9%, var(--pk-surface))",
                    }}
                >
                    {percent}%
                </span>
            </header>

            <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-(--pk-gray-bg)">
                <div
                    className="h-full rounded-full transition-[width] duration-500"
                    style={{
                        width: `${percent}%`,
                        background: percent === 100 ? "var(--pk-green)" : "var(--pk-brand)",
                    }}
                />
            </div>

            <div className="mt-2 flex flex-1 flex-col">
                {steps.map((step) => (
                    <button
                        key={step.label}
                        type="button"
                        onClick={() => navigate(step.to)}
                        className="group flex items-center gap-2.5 rounded-md px-1 py-2.5 text-left transition-colors hover:bg-(--pk-gray-bg)"
                    >
                        <span
                            className="flex size-6 shrink-0 items-center justify-center rounded-full"
                            style={{
                                color: step.done ? "var(--pk-green)" : "var(--pk-text-2)",
                                background: step.done
                                    ? "color-mix(in srgb, var(--pk-green) 10%, var(--pk-surface))"
                                    : "var(--pk-gray-bg)",
                            }}
                        >
                            {step.done ? <CheckCircleFilled className="text-[13px]" /> : <CheckCircleOutlined className="text-[13px]" />}
                        </span>
                        <span className="min-w-0 flex-1">
                            <span className="block truncate text-[12.5px] text-(--pk-text)">{step.label}</span>
                            <span className={"mt-0.5 block truncate text-[11.5px] " + MUTED}>{step.detail}</span>
                        </span>
                        <RightOutlined className="text-[10px] text-(--pk-text-2) opacity-40 transition-opacity group-hover:opacity-90" />
                    </button>
                ))}
            </div>

            {counts.unposted > 0 && (
                <div className="mt-2 flex items-start gap-1.5 rounded-md bg-[color-mix(in_srgb,var(--pk-orange)_8%,var(--pk-surface))] px-2.5 py-2 text-[11.5px] leading-4 text-(--pk-orange)">
                    <InfoCircleFilled className="mt-0.5 shrink-0 text-[11px]" />
                    <span>还有 {counts.unposted} 张草稿未入账，账簿与报表只统计已入账凭证。</span>
                </div>
            )}
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  最近凭证                                                            */
/* ------------------------------------------------------------------ */

function VoucherStatus({ voucher }: { voucher: VoucherListItemDto }) {
    const posted = voucher.status === 3
    return <span className={"pk-tag " + (posted ? "pk-tag-green" : "")}>{voucher.statusText ?? (posted ? "已过账" : "草稿")}</span>
}

function RecentVouchersCard({
    vouchers,
    className,
}: {
    vouchers: VoucherListItemDto[]
    className?: string
}) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const base = setPath(accountSetId)
    const rows = useMemo(() => latestVouchers(vouchers, 8), [vouchers])

    return (
        <section className={CARD + " flex min-w-0 flex-col " + (className ?? "")}>
            <header className="flex items-center justify-between gap-2 px-4 py-3">
                <div className="flex items-baseline gap-2">
                    <h2 className="text-[14px] font-semibold text-(--pk-text)">最近凭证</h2>
                    <span className={"text-[11.5px] " + MUTED}>共 {vouchers.length} 张</span>
                </div>
                <button
                    type="button"
                    onClick={() => navigate(`${base}/vouchers/manage`)}
                    className="inline-flex cursor-pointer items-center gap-0.5 text-[12px] text-(--pk-brand-text) hover:opacity-75"
                >
                    全部凭证
                    <ArrowRightOutlined className="text-[10px]" />
                </button>
            </header>

            {rows.length === 0 ? (
                <div className="flex flex-1 flex-col items-center justify-center gap-2 px-4 py-10">
                    <span className="flex size-10 items-center justify-center rounded-full bg-(--pk-gray-bg) text-[18px] text-(--pk-text-2)">
                        <FileTextOutlined />
                    </span>
                    <p className={"text-[12px] " + MUTED}>本期还没有凭证，新增第一张凭证开始记账。</p>
                    <Button type="primary" icon={<PlusOutlined />} className="mt-1" onClick={() => navigate(`${base}/vouchers/new`)}>
                        新增凭证
                    </Button>
                </div>
            ) : (
                <div className="min-w-0 overflow-x-auto">
                    <div className="min-w-[560px]">
                        <div className={"grid grid-cols-[62px_74px_minmax(0,1fr)_104px_62px] gap-2 bg-(--pk-gray-bg) px-4 py-2 text-[11.5px] " + MUTED}>
                            <span>日期</span>
                            <span>凭证号</span>
                            <span>摘要</span>
                            <span className="text-right">金额（元）</span>
                            <span className="text-right">状态</span>
                        </div>
                        {rows.map((voucher) => (
                            <button
                                key={voucher.id}
                                type="button"
                                onClick={() => navigate(`${base}/vouchers/manage`)}
                                className="grid w-full cursor-pointer grid-cols-[62px_74px_minmax(0,1fr)_104px_62px] items-center gap-2 border-t border-(--pk-border) px-4 py-2.5 text-left transition-colors hover:bg-[color-mix(in_srgb,var(--pk-brand)_4%,var(--pk-surface))]"
                            >
                                <span className={"text-[12px] tabular-nums " + MUTED}>{monthDay(voucher.date)}</span>
                                <span className="truncate text-[12px] font-medium text-(--pk-text)">{voucher.voucherNo}</span>
                                <span className="truncate text-[12.5px] text-(--pk-text)" title={voucher.remark ?? undefined}>
                                    {voucher.remark?.trim() || voucher.entries?.[0]?.summary || "—"}
                                </span>
                                <span className="truncate text-right text-[12.5px] tabular-nums text-(--pk-text)">
                                    {fmtAmount(voucher.totalDebit, true)}
                                </span>
                                <span className="text-right">
                                    <VoucherStatus voucher={voucher} />
                                </span>
                            </button>
                        ))}
                    </div>
                </div>
            )}
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  常用功能 / 往来与税费                                                */
/* ------------------------------------------------------------------ */

const QUICK_LINKS = [
    { label: "新增凭证", to: "vouchers/new", icon: PlusOutlined },
    { label: "凭证管理", to: "vouchers/manage", icon: FileTextOutlined },
    { label: "科目余额表", to: "ledger/trial-balance", icon: TableOutlined },
    { label: "资产负债表", to: "reports/balance-sheet", icon: PieChartOutlined },
    { label: "利润表", to: "reports/income-statement", icon: LineChartOutlined },
    { label: "期末结账", to: "closing", icon: CheckCircleOutlined },
]

function QuickLinksCard({ className }: { className?: string }) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const base = setPath(accountSetId)

    return (
        <section className={CARD + " p-4 " + (className ?? "")}>
            <h2 className="text-[14px] font-semibold text-(--pk-text)">常用功能</h2>
            <div className="mt-3 grid grid-cols-3 gap-1.5">
                {QUICK_LINKS.map((link) => {
                    const Icon = link.icon
                    return (
                        <button
                            key={link.label}
                            type="button"
                            onClick={() => navigate(`${base}/${link.to}`)}
                            className="group flex cursor-pointer flex-col items-center gap-1.5 rounded-md border border-transparent py-3 transition-colors hover:border-(--pk-brand-border) hover:bg-(--pk-brand-soft)"
                        >
                            <span className="flex size-8 items-center justify-center rounded-md bg-(--pk-gray-bg) text-[15px] text-(--pk-text-2) transition-colors group-hover:bg-(--pk-surface) group-hover:text-(--pk-brand-text)">
                                <Icon />
                            </span>
                            <span className={"text-[11.5px] text-(--pk-text) group-hover:text-(--pk-brand-text)"}>{link.label}</span>
                        </button>
                    )
                })}
            </div>
        </section>
    )
}

function BalanceCard({ summary, className }: { summary: FinancialSummaryDto | null; className?: string }) {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const base = setPath(accountSetId)

    const rows = [
        { label: "应收款", value: findRow(summary, "应收款")?.currentAmount ?? null, unit: "万元", to: "ledger/ageing" },
        { label: "应付款", value: findRow(summary, "应付款")?.currentAmount ?? null, unit: "万元", to: "ledger/ageing" },
        { label: "应交税费", value: findRow(summary, "应交税费")?.currentAmount ?? null, unit: "万元", to: "reports/tax" },
        {
            label: "资产负债率",
            value: findRow(summary, "资产负债率(%)")?.currentAmount ?? null,
            unit: "%",
            to: "reports/financial-summary",
            percent: true,
        },
    ] as const

    return (
        <section className={CARD + " p-4 " + (className ?? "")}>
            <header className="flex items-center justify-between gap-2">
                <h2 className="text-[14px] font-semibold text-(--pk-text)">往来与税费</h2>
                <button
                    type="button"
                    onClick={() => navigate(`${base}/reports/financial-summary`)}
                    className="inline-flex cursor-pointer items-center gap-0.5 text-[12px] text-(--pk-brand-text) hover:opacity-75"
                >
                    财务概要
                    <ArrowRightOutlined className="text-[10px]" />
                </button>
            </header>
            <div className="mt-2 divide-y divide-(--pk-border)">
                {rows.map((row) => (
                    <button
                        key={row.label}
                        type="button"
                        onClick={() => navigate(`${base}/${row.to}`)}
                        className="group flex w-full cursor-pointer items-center gap-3 py-2.5 text-left"
                    >
                        <span className={"flex-1 text-[12.5px] " + MUTED}>{row.label}</span>
                        <span className="text-[13px] font-medium tabular-nums text-(--pk-text)">
                            {"percent" in row && row.percent ? pct(row.value) : `${wan(row.value)} ${row.unit}`}
                        </span>
                        <RightOutlined className="text-[10px] text-(--pk-text-2) opacity-40 transition-opacity group-hover:opacity-90" />
                    </button>
                ))}
            </div>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  空账套 / 加载 / 失败                                                 */
/* ------------------------------------------------------------------ */

function OnboardingStrip() {
    const navigate = useNavigate()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const base = setPath(accountSetId)

    return (
        <section className="flex flex-wrap items-center gap-3 rounded-md border border-(--pk-brand-border) bg-(--pk-brand-soft) px-4 py-3 text-[12.5px] text-(--pk-text)">
            <span className="flex size-7 shrink-0 items-center justify-center rounded-md bg-(--pk-surface) text-(--pk-brand-text)">
                <AccountBookOutlined className="text-[14px]" />
            </span>
            <span className="min-w-0 flex-1 leading-5">
                这个账套还没有数据。先录入期初余额，或新增第一张凭证（保存即入账），首页指标与趋势会实时更新。
            </span>
            <div className="flex shrink-0 items-center gap-2">
                <button
                    type="button"
                    onClick={() => navigate(`${base}/settings/opening-balances`)}
                    className="h-7 cursor-pointer rounded-md border border-(--pk-brand-border) bg-(--pk-surface) px-3 text-[12px] text-(--pk-brand-text) hover:bg-(--card)"
                >
                    录入期初
                </button>
                <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate(`${base}/vouchers/new`)}>
                    新增凭证
                </Button>
            </div>
        </section>
    )
}

function SkeletonBlock({ className }: { className: string }) {
    return <div className={"animate-pulse rounded-md bg-(--pk-gray-bg) " + className} />
}

function LoadingView() {
    return (
        <div className="flex flex-col gap-4">
            <SkeletonBlock className="h-[132px]" />
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {[0, 1, 2, 3].map((i) => (
                    <SkeletonBlock key={i} className="h-[112px]" />
                ))}
            </div>
            <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
                <SkeletonBlock className="h-[290px] xl:col-span-8" />
                <SkeletonBlock className="h-[290px] xl:col-span-4" />
            </div>
            <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
                <SkeletonBlock className="h-[320px] xl:col-span-8" />
                <SkeletonBlock className="h-[320px] xl:col-span-4" />
            </div>
        </div>
    )
}

function ErrorView({ message, onRetry }: { message: string; onRetry: () => void }) {
    return (
        <section className={CARD + " flex flex-col items-center gap-3 px-6 py-12 text-center"}>
            <span className="flex size-12 items-center justify-center rounded-full bg-[color-mix(in_srgb,var(--pk-red)_9%,var(--pk-surface))] text-[24px] text-(--pk-red)">
                <CloseCircleOutlined />
            </span>
            <h3 className="text-[15px] font-semibold text-(--pk-text)">首页数据加载失败</h3>
            <p className={"max-w-md text-[13px] leading-5 " + MUTED}>{message}</p>
            <button
                type="button"
                onClick={onRetry}
                className="mt-1 inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-md border border-(--pk-border) bg-(--pk-surface) px-3.5 text-[12.5px] text-(--pk-text) hover:border-(--pk-brand-border) hover:text-(--pk-brand-text)"
            >
                <ReloadOutlined className="text-[12px]" />
                重新加载
            </button>
        </section>
    )
}

/* ------------------------------------------------------------------ */
/*  首页                                                               */
/* ------------------------------------------------------------------ */

export default function DashboardHome() {
    const accountSetId = useCurrentAccountSetId() ?? ""
    const navigate = useNavigate()
    const defaultPeriod = useDefaultAccountingPeriod()
    /** 期间步进器改过的月份；null 表示跟随账套默认期间。 */
    const [override, setOverride] = useState<{ year: number; month: number } | null>(null)
    const period = useMemo(
        () => (override ? makePeriod(override.year, override.month) : defaultPeriod),
        [override, defaultPeriod],
    )

    // 换账套时回到该账套的默认期间，避免把上一个账套的浏览位置带过来。
    useEffect(() => {
        setOverride(null)
    }, [accountSetId])

    const data = useDashboardData(period)
    const counts = useMemo(
        () => countByStatus(data.vouchers.filter((voucher) => isInPeriod(voucher.date, period))),
        [data.vouchers, period],
    )
    const trend = useMemo(() => buildCashTrend(data.vouchers, period), [data.vouchers, period])

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
        <div className={"flex flex-col gap-4 transition-opacity " + (data.loading ? "pointer-events-none opacity-60" : "")}>
            <HeroBanner period={period} closing={data.closing} counts={counts} onStepPeriod={stepPeriod} />

            {!hasData && <OnboardingStrip />}

            <KpiRow summary={data.summary} onOpen={(to) => navigate(setPath(accountSetId, `/${to}`))} />

            <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
                <CashTrendCard points={trend} summary={data.summary} className="xl:col-span-8" />
                <TodoCard period={period} closing={data.closing} counts={counts} className="xl:col-span-4" />
            </div>

            <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
                <RecentVouchersCard vouchers={data.vouchers} className="xl:col-span-8" />
                <div className="flex flex-col gap-4 xl:col-span-4">
                    <QuickLinksCard />
                    <BalanceCard summary={data.summary} />
                </div>
            </div>
        </div>
    )
}
