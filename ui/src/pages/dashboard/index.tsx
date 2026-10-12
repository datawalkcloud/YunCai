import {
    ArrowDownLeft,
    ArrowLeftRight,
    ArrowUpRight,
    CalendarDays,
    CheckCircle2,
    ChevronDown,
    ChevronRight,
    Coins,
    CreditCard,
    FileCheck,
    FilePlus,
    Landmark,
    Receipt,
    ShieldCheck,
    TrendingUp,
    Wallet,
    Zap,
} from "lucide-react";

import bannerFinanceImg from "@/assets/banner-finance-3d.png";
import aiRobotImg from "@/assets/ai-robot-3d.png";
import { ACCENT_BG } from "@/components/page/styles";

/* ------------------------------------------------------------------ */
/*  数据（与截图 1:1 对应的静态展示数据）                                */
/* ------------------------------------------------------------------ */

/** 待办列表项：图标为实心彩色徽标，部分数量以强调胶囊显示。 */
type TodoItem = {
    label: string;
    count: string;
    color: string;
    soft: string;
    icon: React.ComponentType<{ className?: string }>;
    emph?: "red" | "blue";
};

const statCards = [
    {
        label: "本月收入",
        value: "¥ 1,280,560.00",
        delta: "12.5%",
        icon: TrendingUp,
        accent: "#3b82f6",
        cardBg: ACCENT_BG.blue,
        spark: [42, 55, 48, 66, 60, 78, 92],
    },
    {
        label: "本月支出",
        value: "¥ 832,460.00",
        delta: "8.3%",
        icon: ArrowDownLeft,
        accent: "#10b981",
        cardBg: ACCENT_BG.green,
        spark: [30, 44, 40, 52, 48, 60, 66],
    },
    {
        label: "净利润",
        value: "¥ 448,100.00",
        delta: "18.7%",
        icon: Coins,
        accent: "#8b5cf6",
        cardBg: ACCENT_BG.purple,
        spark: [26, 34, 30, 44, 40, 58, 70],
    },
    {
        label: "现金余额",
        value: "¥ 2,860,000.00",
        delta: "6.2%",
        icon: Wallet,
        accent: "#f59e0b",
        cardBg: ACCENT_BG.amber,
        spark: [50, 52, 58, 56, 64, 70, 76],
    },
];

const quickActions = [
    { label: "记一笔", icon: FilePlus, from: "#5b9bff", to: "#2f6bff", soft: "#e8f1ff" },
    { label: "收款", icon: ArrowDownLeft, from: "#34d399", to: "#10b981", soft: "#e6f8f0" },
    { label: "付款", icon: ArrowUpRight, from: "#fbbf24", to: "#f97316", soft: "#fff1e2" },
    { label: "开票", icon: Receipt, from: "#a78bfa", to: "#7c3aed", soft: "#f1eaff" },
    { label: "应收账款", icon: Coins, from: "#c084fc", to: "#9333ea", soft: "#f6ecff" },
    { label: "应付账款", icon: CreditCard, from: "#34d399", to: "#10b981", soft: "#e6f8f0" },
    { label: "费用报销", icon: FileCheck, from: "#a78bfa", to: "#7c3aed", soft: "#f1eaff" },
    { label: "对账", icon: ArrowLeftRight, from: "#818cf8", to: "#6366f1", soft: "#eceafe" },
];

const rightTodos: TodoItem[] = [
    { label: "应收账款逾期", count: "3 笔", color: "#ef4444", soft: "#fee2e2", icon: AlertDot, emph: "red" },
    { label: "待开票的销售单", count: "5 笔", color: "#f59e0b", soft: "#fef3c7", icon: Receipt },
    { label: "待审核的报销单", count: "2 笔", color: "#3b82f6", soft: "#dbeafe", icon: FileCheck, emph: "blue" },
    { label: "银行对账待处理", count: "4 笔", color: "#10b981", soft: "#d1fae5", icon: Landmark },
];

const approvalTodos: TodoItem[] = [
    { label: "待审核报销单", count: "5 笔", color: "#3b82f6", soft: "#dbeafe", icon: FileCheck },
    { label: "待开票订单", count: "8 笔", color: "#f43f5e", soft: "#ffe4e6", icon: Receipt },
    { label: "待确认收款", count: "12 笔", color: "#3b82f6", soft: "#dbeafe", icon: ArrowDownLeft },
    { label: "待处理付款", count: "3 笔", color: "#10b981", soft: "#d1fae5", icon: CreditCard },
];

const receivables = [
    { date: "2025-08-26", customer: "浙江东明科技有限公司", type: "销售发票", amount: "¥ 236,000.00", overdue: true },
    { date: "2025-08-26", customer: "上海云湾贸易有限公司", type: "销售发票", amount: "¥ 128,000.00", overdue: false },
    { date: "2025-08-25", customer: "深圳创力科技有限公司", type: "销售发票", amount: "¥ 96,000.00", overdue: false },
    { date: "2025-08-24", customer: "北京智联科技有限公司", type: "销售发票", amount: "¥ 68,000.00", overdue: false },
];

const spendSlices = [
    { label: "采购成本", pct: 38.6, color: "#3b82f6" },
    { label: "人工成本", pct: 22.4, color: "#10b981" },
    { label: "办公费用", pct: 12.8, color: "#a855f7" },
    { label: "销售费用", pct: 10.2, color: "#f97316" },
    { label: "其他", pct: 16.0, color: "#cbd5e1" },
];

const aiChips = ["本月利润情况如何？", "有哪些应收账款逾期？", "帮我生成现金流趋势分析"];

/* ------------------------------------------------------------------ */
/*  首页内容（外壳与侧边栏见 DashboardLayout）                          */
/* ------------------------------------------------------------------ */

export default function DashboardHome() {
    return (
        <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(0,1fr)_320px]">
            {/* 左侧主列 */}
            <div className="flex min-w-0 flex-col gap-5">
                <WelcomeBanner />
                <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
                    {statCards.map((c) => (
                        <StatCard key={c.label} data={c} />
                    ))}
                </section>
                <section className="grid grid-cols-1 gap-4 lg:grid-cols-[1.35fr_1fr]">
                    <TrendChart />
                    <SpendChart />
                </section>
                <section className="grid grid-cols-1 gap-4 lg:grid-cols-[1.5fr_1fr]">
                    <ReceivableTable />
                    <ApprovalCard />
                </section>
                <HealthIndex />
            </div>
            {/* 右侧副列 */}
            <div className="flex flex-col gap-5">
                <QuickActions />
                <RightTodoCard />
                <AiAssistant />
            </div>
        </div>
    );
}

/* ------------------------------------------------------------------ */
/*  欢迎横幅                                                           */
/* ------------------------------------------------------------------ */

function WelcomeBanner() {
    return (
        <section className="relative overflow-hidden rounded-2xl bg-gradient-to-r from-[#e7f0ff] via-[#eef4ff] to-[#f6f9ff] px-7 py-6">
            <div className="relative z-10">
                <h1 className="text-[26px] font-bold text-slate-800">早上好，张三</h1>
                <p className="mt-1 text-[13px] text-slate-500">把复杂的财务工作，变得简单高效</p>
                <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-[12px] text-slate-500">
                    <span className="flex items-center gap-1.5">
                        <CalendarDays className="size-3.5 text-slate-400" />
                        2025年08月26日
                    </span>
                    <span>星期二</span>
                    <span className="flex items-center gap-1.5">
                        <svg viewBox="0 0 24 24" className="size-3.5 text-slate-400" fill="none" stroke="currentColor" strokeWidth={2}>
                            <circle cx="12" cy="12" r="9" />
                            <path d="M12 7v5l3 2" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                        农历七月初四
                    </span>
                </div>
            </div>
            <div className="pointer-events-none absolute right-6 top-1/2 z-0 flex -translate-y-1/2 items-center gap-2">
                <img src={bannerFinanceImg} alt="" className="h-[128px] w-auto drop-shadow-sm" />
                <div className="relative -rotate-3 text-[14px] font-semibold italic text-[#2f6bff]/80">
                    财务，让经营更有底气
                    <svg viewBox="0 0 120 8" className="mt-0.5 h-2 w-full" preserveAspectRatio="none">
                        <path d="M2 5 C30 1 60 7 118 3" stroke="#2f6bff" strokeWidth="1.5" fill="none" opacity="0.5" strokeLinecap="round" />
                    </svg>
                </div>
            </div>
        </section>
    );
}

/* ------------------------------------------------------------------ */
/*  统计卡片                                                           */
/* ------------------------------------------------------------------ */

function StatCard({ data }: { data: (typeof statCards)[number] }) {
    const Icon = data.icon;
    return (
        <div className="rounded-2xl p-5" style={{ background: data.cardBg }}>
            <div className="flex items-center gap-2.5">
                <span
                    className="flex size-9 items-center justify-center rounded-xl text-white shadow-sm"
                    style={{ background: data.accent }}
                >
                    <Icon className="size-5" />
                </span>
                <span className="text-[13px] font-medium" style={{ color: data.accent }}>
                    {data.label}
                </span>
            </div>
            <div className="mt-4 text-[22px] font-bold tracking-tight text-slate-800">{data.value}</div>
            <div className="mt-2 flex items-end justify-between">
                <div className="flex items-center gap-1 text-[12px]">
                    <span className="flex items-center gap-0.5 font-medium text-[#10b981]">
                        <ArrowUpRight className="size-3.5" />
                        {data.delta}
                    </span>
                    <span className="text-slate-400">较上月</span>
                </div>
                <Sparkline points={data.spark} color={data.accent} />
            </div>
        </div>
    );
}

function Sparkline({ points, color }: { points: number[]; color: string }) {
    const w = 80;
    const h = 34;
    const max = Math.max(...points);
    const min = Math.min(...points);
    const step = w / (points.length - 1);
    const coords = points.map((p, i) => [i * step, h - ((p - min) / (max - min || 1)) * (h - 8) - 4] as [number, number]);
    const line = smoothPath(coords);
    const area = `${line} L ${w},${h} L 0,${h} Z`;
    return (
        <svg viewBox={`0 0 ${w} ${h}`} className="h-[34px] w-[80px]" preserveAspectRatio="none">
            <path d={area} fill={color} opacity={0.12} />
            <path d={line} fill="none" stroke={color} strokeWidth={1.5} opacity={0.5} />
        </svg>
    );
}

/* ------------------------------------------------------------------ */
/*  收入与支出趋势（面积图）                                            */
/* ------------------------------------------------------------------ */

function TrendChart() {
    const labels = ["8.20", "8.21", "8.22", "8.23", "8.24", "8.25", "8.26"];
    const income = [58, 95, 72, 80, 115, 98, 168.7];
    const expense = [35, 62, 50, 68, 82, 98, 112.5];
    const W = 520;
    const H = 220;
    const padL = 36;
    const padR = 12;
    const padT = 16;
    const padB = 28;
    const maxY = 200;
    const xFor = (i: number) => padL + (i * (W - padL - padR)) / (labels.length - 1);
    const yFor = (v: number) => padT + (1 - v / maxY) * (H - padT - padB);
    const toPath = (arr: number[]) => smoothPath(arr.map((v, i) => [xFor(i), yFor(v)]));
    const area = (arr: number[]) => `${toPath(arr)} L ${xFor(arr.length - 1)},${yFor(0)} L ${xFor(0)},${yFor(0)} Z`;
    const gridY = [0, 50, 100, 150, 200];

    return (
        <Card title="收入与支出趋势" action={<PeriodSelect />}>
            <div className="mb-2 flex items-center gap-4 text-[12px] text-slate-500">
                <Legend color="#2f6bff" label="收入" />
                <Legend color="#10b981" label="支出" />
            </div>
            <div className="relative">
                {/* 悬浮数据提示卡（还原设计稿右上角 tooltip） */}
                <div className="absolute right-2 top-1 z-10 w-[124px] rounded-xl border border-slate-100 bg-white p-2.5 shadow-[0_6px_20px_rgba(16,24,40,0.12)]">
                    <div className="mb-1.5 text-[12px] font-semibold text-slate-700">8月26日</div>
                    <div className="flex items-center gap-1.5 text-[11px] text-slate-500">
                        <span className="size-2 rounded-full bg-[#2f6bff]" />
                        <span className="flex-1">收入</span>
                        <span className="font-medium text-slate-700">168.7万</span>
                    </div>
                    <div className="mt-1 flex items-center gap-1.5 text-[11px] text-slate-500">
                        <span className="size-2 rounded-full bg-[#10b981]" />
                        <span className="flex-1">支出</span>
                        <span className="font-medium text-slate-700">112.5万</span>
                    </div>
                </div>
                <svg viewBox={`0 0 ${W} ${H}`} className="h-[220px] w-full">
                    <defs>
                        <linearGradient id="incFill" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#2f6bff" stopOpacity="0.22" />
                            <stop offset="100%" stopColor="#2f6bff" stopOpacity="0" />
                        </linearGradient>
                        <linearGradient id="expFill" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#10b981" stopOpacity="0.2" />
                            <stop offset="100%" stopColor="#10b981" stopOpacity="0" />
                        </linearGradient>
                    </defs>
                    {gridY.map((v) => (
                        <g key={v}>
                            <line x1={padL} y1={yFor(v)} x2={W - padR} y2={yFor(v)} stroke="#eef1f6" strokeWidth={1} />
                            <text x={padL - 8} y={yFor(v) + 3} textAnchor="end" fontSize="9" fill="#a4acba">
                                {v === 0 ? "0" : `${v}万`}
                            </text>
                        </g>
                    ))}
                    <path d={area(expense)} fill="url(#expFill)" />
                    <path d={area(income)} fill="url(#incFill)" />
                    <path d={toPath(expense)} fill="none" stroke="#10b981" strokeWidth={2.5} strokeLinecap="round" />
                    <path d={toPath(income)} fill="none" stroke="#2f6bff" strokeWidth={2.5} strokeLinecap="round" />
                    {income.map((v, i) => (
                        <circle key={i} cx={xFor(i)} cy={yFor(v)} r={2.5} fill="#fff" stroke="#2f6bff" strokeWidth={2} />
                    ))}
                    {labels.map((l, i) => (
                        <text key={l} x={xFor(i)} y={H - 8} textAnchor="middle" fontSize="9" fill="#a4acba">
                            {l}
                        </text>
                    ))}
                </svg>
            </div>
        </Card>
    );
}

function smoothPath(pts: readonly (readonly [number, number])[]) {
    if (pts.length < 2) return "";
    let d = `M ${pts[0][0]},${pts[0][1]}`;
    for (let i = 0; i < pts.length - 1; i++) {
        const p0 = pts[i - 1] || pts[i];
        const p1 = pts[i];
        const p2 = pts[i + 1];
        const p3 = pts[i + 2] || p2;
        const c1x = p1[0] + (p2[0] - p0[0]) / 6;
        const c1y = p1[1] + (p2[1] - p0[1]) / 6;
        const c2x = p2[0] - (p3[0] - p1[0]) / 6;
        const c2y = p2[1] - (p3[1] - p1[1]) / 6;
        d += ` C ${c1x},${c1y} ${c2x},${c2y} ${p2[0]},${p2[1]}`;
    }
    return d;
}

/* ------------------------------------------------------------------ */
/*  支出分类（环形图）                                                  */
/* ------------------------------------------------------------------ */

function SpendChart() {
    const R = 60;
    const C = 2 * Math.PI * R;
    const gap = 1.6; // 段间留白
    // 设计稿顺时针顺序：采购 → 人工 → 销售 → 办公 → 其他（与图例顺序不同）
    const drawOrder = [0, 1, 3, 2, 4];
    let offset = 0;
    return (
        <Card title="支出分类" action={<PeriodSelect />}>
            <div className="flex items-center gap-5">
                <div className="relative shrink-0">
                    <svg viewBox="0 0 160 160" className="h-[160px] w-[160px] -rotate-90">
                        {drawOrder.map((idx) => {
                            const s = spendSlices[idx];
                            const len = (s.pct / 100) * C;
                            const seg = (
                                <circle
                                    key={s.label}
                                    cx="80"
                                    cy="80"
                                    r={R}
                                    fill="none"
                                    stroke={s.color}
                                    strokeWidth={22}
                                    strokeDasharray={`${Math.max(len - gap, 0)} ${C - len + gap}`}
                                    strokeDashoffset={-offset}
                                />
                            );
                            offset += len;
                            return seg;
                        })}
                    </svg>
                    <div className="absolute inset-0 flex flex-col items-center justify-center">
                        <span className="text-[18px] font-bold text-slate-800">¥ 832,460</span>
                        <span className="text-[11px] text-slate-400">总支出</span>
                    </div>
                </div>
                <ul className="flex-1 space-y-2.5">
                    {spendSlices.map((s) => (
                        <li key={s.label} className="flex items-center gap-2 text-[12px]">
                            <span className="size-2.5 rounded-full" style={{ background: s.color }} />
                            <span className="flex-1 text-slate-600">{s.label}</span>
                            <span className="font-medium text-slate-500">{s.pct}%</span>
                        </li>
                    ))}
                </ul>
            </div>
        </Card>
    );
}

/* ------------------------------------------------------------------ */
/*  近期应收账款                                                       */
/* ------------------------------------------------------------------ */

function ReceivableTable() {
    return (
        <Card title="近期应收账款" action={<ViewAll />}>
            <table className="w-full text-[12.5px]">
                <thead>
                    <tr className="text-left text-slate-400">
                        <th className="pb-3 font-normal">日期</th>
                        <th className="pb-3 font-normal">客户名称</th>
                        <th className="pb-3 font-normal">单据类型</th>
                        <th className="pb-3 text-right font-normal">金额</th>
                        <th className="pb-3 text-center font-normal">状态</th>
                        <th className="pb-3 text-center font-normal">操作</th>
                    </tr>
                </thead>
                <tbody>
                    {receivables.map((r) => (
                        <tr key={r.date + r.customer} className="border-t border-slate-50">
                            <td className="py-3 text-slate-500">{r.date}</td>
                            <td className="py-3 text-slate-700">{r.customer}</td>
                            <td className="py-3 text-slate-500">{r.type}</td>
                            <td className="py-3 text-right font-medium text-slate-700">{r.amount}</td>
                            <td className="py-3 text-center">
                                <span
                                    className={
                                        "inline-flex rounded-full px-2 py-0.5 text-[11px] font-medium " +
                                        (r.overdue ? "bg-[#fee2e2] text-[#ef4444]" : "bg-[#d1fae5] text-[#10b981]")
                                    }
                                >
                                    {r.overdue ? "逾期" : "正常"}
                                </span>
                            </td>
                            <td className="py-3 text-center">
                                <a href="#" onClick={(e) => e.preventDefault()} className="text-[12px] text-[#2f6bff] hover:underline">
                                    详情
                                </a>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </Card>
    );
}

/* ------------------------------------------------------------------ */
/*  待办提醒（主列底部，与表格并排）                                    */
/* ------------------------------------------------------------------ */

function ApprovalCard() {
    return (
        <Card title="待办提醒" action={<ViewAll text="全部" />}>
            <ul className="flex flex-col">
                {approvalTodos.map((t) => (
                    <TodoRow key={t.label} {...t} />
                ))}
            </ul>
        </Card>
    );
}

function TodoRow({ label, count, color, icon: Icon, emph }: TodoItem) {
    return (
        <li className="flex items-center gap-3 border-b border-slate-100 py-2.5 last:border-0">
            <span className="flex size-8 shrink-0 items-center justify-center rounded-lg text-white" style={{ background: color }}>
                <Icon className="size-4" />
            </span>
            <span className="flex-1 truncate text-[13px] text-slate-600">{label}</span>
            {emph ? (
                <span
                    className={
                        "rounded-full px-2 py-0.5 text-[12px] font-medium " +
                        (emph === "red" ? "bg-[#fee2e2] text-[#ef4444]" : "bg-[#dbeafe] text-[#3b82f6]")
                    }
                >
                    {count}
                </span>
            ) : (
                <span className="text-[13px] font-medium text-slate-500">{count}</span>
            )}
        </li>
    );
}

/* ------------------------------------------------------------------ */
/*  财务健康指数                                                       */
/* ------------------------------------------------------------------ */

function HealthIndex() {
    const metrics = [
        { label: "现金流", value: "健康" },
        { label: "负债率", value: "合理" },
        { label: "盈利能力", value: "优秀" },
    ];
    return (
        <section className="relative overflow-hidden rounded-2xl bg-gradient-to-r from-white via-[#f2fbf6] to-[#e4f7ec]">
            <div className="flex flex-wrap items-center gap-6 p-6 pr-10">
                <div className="flex items-center gap-3">
                    <span className="flex size-11 items-center justify-center rounded-xl bg-gradient-to-br from-[#34d399] to-[#10b981] text-white">
                        <ShieldCheck className="size-6" />
                    </span>
                    <div>
                        <div className="flex items-center gap-2">
                            <span className="text-[15px] font-semibold text-slate-800">财务健康指数</span>
                            <span className="text-[22px] font-bold text-[#10b981]">92 分</span>
                            <span className="rounded-full bg-white/70 px-2 py-0.5 text-[11px] font-medium text-[#10b981]">优秀</span>
                        </div>
                        <p className="mt-0.5 text-[12px] text-slate-400">企业现金流稳定，经营状况良好，建议继续保持。</p>
                    </div>
                </div>
                <div className="ml-auto flex items-center gap-3">
                    {metrics.map((m) => (
                        <div key={m.label} className="rounded-xl bg-white/70 px-4 py-2 text-center">
                            <div className="text-[12px] text-slate-400">{m.label}</div>
                            <div className="mt-1 flex items-center gap-1 text-[14px] font-semibold text-[#10b981]">
                                <CheckCircle2 className="size-4" />
                                {m.value}
                            </div>
                        </div>
                    ))}
                </div>
                <ChevronRight className="absolute right-3 top-1/2 size-5 -translate-y-1/2 text-slate-300" />
            </div>
            <div className="pointer-events-none absolute -bottom-6 right-0 h-28 w-1/3 opacity-90">
                <svg viewBox="0 0 240 100" className="h-full w-full" preserveAspectRatio="none">
                    <defs>
                        <linearGradient id="healthFill" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#34d399" stopOpacity="0.35" />
                            <stop offset="100%" stopColor="#34d399" stopOpacity="0" />
                        </linearGradient>
                    </defs>
                    <path d="M0,80 C40,72 70,60 110,52 C150,44 190,30 240,10 L240,100 L0,100 Z" fill="url(#healthFill)" />
                    <path d="M0,80 C40,72 70,60 110,52 C150,44 190,30 240,10" fill="none" stroke="#10b981" strokeWidth={2.5} />
                    <path d="M224,14 L240,10 L236,26" fill="none" stroke="#10b981" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" />
                </svg>
            </div>
        </section>
    );
}

/* ------------------------------------------------------------------ */
/*  常用功能（右侧）                                                   */
/* ------------------------------------------------------------------ */

function QuickActions() {
    return (
        <Card
            title="常用功能"
            action={
                <a
                    href="#"
                    onClick={(e) => e.preventDefault()}
                    className="flex items-center gap-0.5 text-[12px] font-medium text-[#2f6bff] hover:underline"
                >
                    编辑
                    <ChevronRight className="size-3.5" />
                </a>
            }
        >
            <div className="grid grid-cols-4 gap-y-4">
                {quickActions.map((a) => {
                    const Icon = a.icon;
                    return (
                        <button key={a.label} className="flex flex-col items-center gap-1.5">
                            <span
                                className="flex size-[52px] items-center justify-center rounded-2xl"
                                style={{ background: a.soft }}
                            >
                                <span
                                    className="flex size-[30px] items-center justify-center rounded-[10px] text-white shadow-sm"
                                    style={{ backgroundImage: `linear-gradient(135deg, ${a.from}, ${a.to})` }}
                                >
                                    <Icon className="size-4" />
                                </span>
                            </span>
                            <span className="text-[11px] text-slate-600">{a.label}</span>
                        </button>
                    );
                })}
            </div>
        </Card>
    );
}

/* ------------------------------------------------------------------ */
/*  待办事项（右侧）                                                   */
/* ------------------------------------------------------------------ */

function RightTodoCard() {
    return (
        <Card title="待办事项" action={<ViewAll text="全部" />}>
            <ul className="flex flex-col">
                {rightTodos.map((t) => (
                    <TodoRow key={t.label} {...t} />
                ))}
            </ul>
        </Card>
    );
}

function AlertDot({ className }: { className?: string }) {
    return (
        <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="9" />
            <path d="M12 8v4M12 16h.01" />
        </svg>
    );
}

/* ------------------------------------------------------------------ */
/*  AI 财务助手                                                        */
/* ------------------------------------------------------------------ */

function AiAssistant() {
    return (
        <section className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-white via-[#eef4ff] to-[#dbe8ff] p-5">
            <div className="flex items-center gap-2">
                <span className="flex size-6 items-center justify-center rounded-full bg-[#2f6bff] text-white">
                    <Zap className="size-3.5" fill="currentColor" strokeWidth={0} />
                </span>
                <span className="text-[14px] font-semibold text-slate-800">AI 财务助手</span>
                <span className="rounded-full bg-white/70 px-1.5 py-0.5 text-[10px] font-medium text-[#6366f1]">Beta</span>
            </div>
            <img
                src={aiRobotImg}
                alt=""
                className="pointer-events-none absolute right-1 top-9 h-[112px] w-auto"
            />
            <p className="mt-3 max-w-[70%] text-[12px] leading-relaxed text-slate-600">
                你好，我是您的财务助手，可以帮您查询数据、分析经营状况、或解答财务相关问题。
            </p>
            <div className="mt-4 flex flex-col items-start gap-2">
                {aiChips.map((c) => (
                    <button
                        key={c}
                        className="rounded-full bg-[#d7e6ff]/80 px-3.5 py-2 text-[12px] text-[#2f6bff] transition-colors hover:bg-[#c9dcff]"
                    >
                        {c}
                    </button>
                ))}
            </div>
            <div className="mt-4 flex items-center gap-2 rounded-xl bg-white/85 py-1.5 pl-4 pr-1.5 shadow-sm">
                <input className="w-full bg-transparent text-[12px] text-slate-600 outline-none placeholder:text-slate-400" placeholder="请输入您的问题..." />
                <button className="flex size-8 shrink-0 items-center justify-center rounded-full bg-[#c7dbff] text-[#2f6bff]">
                    <ChevronRight className="size-4" strokeWidth={2.5} />
                </button>
            </div>
        </section>
    );
}

/* ------------------------------------------------------------------ */
/*  通用小组件                                                         */
/* ------------------------------------------------------------------ */

function Card({
    title,
    action,
    children,
}: {
    title: string;
    action?: React.ReactNode;
    children: React.ReactNode;
}) {
    return (
        <section className="rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <div className="mb-4 flex items-center justify-between">
                <h3 className="text-[14px] font-semibold text-slate-800">{title}</h3>
                {action}
            </div>
            {children}
        </section>
    );
}

function PeriodSelect() {
    return (
        <button className="flex items-center gap-1 rounded-lg border border-slate-200 px-2.5 py-1 text-[12px] text-slate-500 hover:bg-slate-50">
            近 7 天
            <ChevronDown className="size-3.5" />
        </button>
    );
}

function ViewAll({ text = "查看全部" }: { text?: string }) {
    return (
        <a href="#" onClick={(e) => e.preventDefault()} className="flex items-center gap-0.5 text-[12px] text-slate-400 hover:text-[#2f6bff]">
            {text}
            <ChevronRight className="size-3.5" />
        </a>
    );
}

function Legend({ color, label }: { color: string; label: string }) {
    return (
        <span className="flex items-center gap-1.5">
            <span className="size-2 rounded-full" style={{ background: color }} />
            {label}
        </span>
    );
}
