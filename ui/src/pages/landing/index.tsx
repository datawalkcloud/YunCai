import { useState } from "react";
import { Link, useNavigate } from "react-router";
import {
    AccountBookOutlined,
    ArrowUpOutlined,
    BarChartOutlined,
    BookOutlined,
    CheckCircleFilled,
    CloudOutlined,
    DashboardOutlined,
    DownOutlined,
    FileTextOutlined,
    LockOutlined,
    RightOutlined,
    SafetyCertificateOutlined,
    TableOutlined,
    ThunderboltOutlined,
    UnorderedListOutlined,
    UploadOutlined,
    WalletOutlined,
} from "@ant-design/icons";

import bannerFinanceImg from "@/assets/banner-finance-3d.png";
import { useAuth } from "@/auth/AuthContext";

/* ============================================================
 * 云财官网落地页 —— 与仪表盘一致的浅色设计体系
 * - 页面底 #f4f6fb，分区在白色/浅蓝之间交替
 * - 卡片：白底 + rounded-2xl + 细边框 + 柔和阴影
 * - 主色 #2f6bff，辅助绿 #22c55e
 * ============================================================ */

const NAV_LINKS = [
    { label: "功能", href: "#features" },
    { label: "亮点", href: "#highlights" },
    { label: "流程", href: "#workflow" },
    { label: "常见问题", href: "#faq" },
];

const INDUSTRIES = ["科技研发", "贸易流通", "咨询服务", "电子商务", "生产制造", "文化创意"];

const BENTO_STATS = [
    { value: "100+", label: "预置标准科目", desc: "遵循企业会计准则的科目模板开箱即用，编码层级、余额方向全部预置，建账无需从零开始。", accent: "#2f6bff", wide: true },
    { value: "4", label: "核心财务报表", desc: "科目余额表、资产负债表、现金日记账、利润表。", accent: "#22c55e", wide: false },
    { value: "实时", label: "数据归集出表", desc: "凭证保存即入账，报表随点随出。", accent: "#8b5cf6", wide: false },
    { value: "多账套", label: "一号管理多公司", desc: "账套之间数据完全隔离，随时切换。", accent: "#f59e0b", wide: false },
    { value: "0", label: "安装成本", desc: "打开浏览器即用，换台设备也能继续工作。", accent: "#0ea5e9", wide: false },
];

const FEATURES = [
    { icon: FileTextOutlined, tag: "凭证处理", title: "凭证管理", desc: "录入、编辑、审核记账凭证，自动校验借贷平衡，支持金额大写。" },
    { icon: UploadOutlined, tag: "批量建账", title: "凭证导入", desc: "从 Excel 一键批量导入历史凭证，快速完成建账初始化。" },
    { icon: TableOutlined, tag: "总账查询", title: "科目余额表", desc: "按期间实时汇总各科目发生额与余额，随时掌握账务全貌。" },
    { icon: AccountBookOutlined, tag: "财务报表", title: "资产负债表", desc: "遵循会企 01 表标准行次，从凭证实时归集，报表随点随出。" },
    { icon: BookOutlined, tag: "资金管理", title: "现金日记账", desc: "自动从库存现金凭证派生序时账，逐笔余额一目了然。" },
    { icon: DashboardOutlined, tag: "多公司", title: "多账套管理", desc: "一个账号管理多家公司账套，随时切换互不干扰。" },
];

/** 单据行 token：kw=蓝色强调（借贷方向/单据名），str=绿色金额，dim=弱化。 */
type Tok = { t: string; c?: "kw" | "str" | "dim" };

const HIGHLIGHTS = [
    {
        key: "voucher",
        tab: "凭证全流程",
        icon: UnorderedListOutlined,
        title: "从录入到审核，",
        titleMuted: "一气呵成。",
        desc: "结构化的分录录入界面，实时校验借贷平衡与金额大写，避免手工核算出错。历史凭证可从 Excel 批量导入，建账不再是负担。",
        points: ["借贷自动平衡校验", "金额大写自动生成", "Excel 批量导入历史凭证"],
        file: "记账凭证 记-0007",
        code: [
            [{ t: "记账凭证", c: "kw" as const }, { t: "  记-0007 · 2026-07-25" }],
            [{ t: "借", c: "kw" as const }, { t: "  银行存款" }, { t: "              120,000.00", c: "str" as const }],
            [{ t: "贷", c: "kw" as const }, { t: "  主营业务收入" }, { t: "          113,207.55", c: "str" as const }],
            [{ t: "贷", c: "kw" as const }, { t: "  应交税费——应交增值税" }, { t: "  6,792.45", c: "str" as const }],
            [{ t: "合计", c: "kw" as const }, { t: "  壹拾贰万元整" }, { t: "        120,000.00", c: "str" as const }],
            [{ t: "✓ 借贷平衡，凭证可保存", c: "dim" as const }],
        ],
        mockLabel: "凭证审核",
        mockRows: [
            { name: "记-0007 银行收款", state: "已审核", ok: true },
            { name: "记-0008 采购付款", state: "待审核", ok: false },
            { name: "记-0009 计提工资", state: "待审核", ok: false },
        ],
    },
    {
        key: "report",
        tab: "报表实时生成",
        icon: BarChartOutlined,
        title: "报表随点随出，",
        titleMuted: "无需等待。",
        desc: "科目余额表、资产负债表、现金日记账全部从凭证实时归集计算，按会计准则标准行次呈现，任意期间随时查看。",
        points: ["会企 01 表标准行次", "任意期间即时切换", "资产负债恒等式自动平衡"],
        file: "资产负债表 2026-07",
        code: [
            [{ t: "资产负债表", c: "kw" as const }, { t: "  会企01表 · 2026年07月" }],
            [{ t: "资产总计" }, { t: "            1,286,400.00", c: "str" as const }],
            [{ t: "负债合计" }, { t: "              486,400.00", c: "str" as const }],
            [{ t: "所有者权益合计" }, { t: "        800,000.00", c: "str" as const }],
            [{ t: "负债和所有者权益总计" }, { t: "  1,286,400.00", c: "str" as const }],
            [{ t: "✓ 资产 = 负债 + 所有者权益", c: "dim" as const }],
        ],
        mockLabel: "期间切换",
        mockRows: [
            { name: "2026 年 07 期", state: "当前", ok: true },
            { name: "2026 年 06 期", state: "已结", ok: false },
            { name: "2026 年 05 期", state: "已结", ok: false },
        ],
    },
    {
        key: "secure",
        tab: "安全与协作",
        icon: SafetyCertificateOutlined,
        title: "多账套隔离，",
        titleMuted: "数据更安心。",
        desc: "一个账号即可管理多家公司账套，数据彼此隔离；登录鉴权保护每一次访问，账务数据只有你能看见。",
        points: ["多账套数据隔离", "登录后才能访问账务", "随时切换互不干扰"],
        file: "我的账套",
        code: [
            [{ t: "账套列表", c: "kw" as const }, { t: "  共 3 个" }],
            [{ t: "杭州数从科技有限公司" }, { t: "   当前在用", c: "str" as const }],
            [{ t: "示例贸易有限公司" }, { t: "       数据隔离", c: "dim" as const }],
            [{ t: "示例咨询有限公司" }, { t: "       数据隔离", c: "dim" as const }],
            [{ t: "✓ 登录鉴权 · 账套间数据完全隔离", c: "dim" as const }],
        ],
        mockLabel: "访问控制",
        mockRows: [
            { name: "登录令牌校验", state: "启用", ok: true },
            { name: "账套数据隔离", state: "启用", ok: true },
            { name: "匿名访问", state: "禁止", ok: false },
        ],
    },
];

const STEPS = [
    { icon: DashboardOutlined, title: "创建账套", desc: "填写公司信息与启用期间，快速建立你的账套。" },
    { icon: UnorderedListOutlined, title: "初始化科目", desc: "使用预置标准科目模板，或按需增删调整。" },
    { icon: FileTextOutlined, title: "录入凭证", desc: "手工录入或从 Excel 导入，系统自动校验平衡。" },
    { icon: BarChartOutlined, title: "查看报表", desc: "余额表、资产负债表实时生成，随时掌握经营。" },
];

const ADVANTAGES = [
    { icon: ThunderboltOutlined, title: "开箱即用", desc: "预置标准科目与报表模板，无需复杂配置即可开始记账。" },
    { icon: CloudOutlined, title: "在线随行", desc: "浏览器即可访问，数据集中存储，换台设备也能继续工作。" },
    { icon: LockOutlined, title: "安全可靠", desc: "登录鉴权 + 账套隔离，保障财务数据的私密与安全。" },
    { icon: WalletOutlined, title: "贴合准则", desc: "遵循企业会计准则科目编码与报表行次，出表即规范。" },
];

const FAQS = [
    { q: "需要安装客户端吗？", a: "不需要。云财是在线系统，打开浏览器登录即可使用。" },
    { q: "可以管理多家公司吗？", a: "可以。一个账号支持创建多个账套，数据彼此隔离，可随时切换。" },
    { q: "历史凭证怎么导入？", a: "在「凭证导入」中上传符合模板的 Excel 文件即可批量导入历史凭证。" },
    { q: "报表数据是实时的吗？", a: "是的。科目余额表、资产负债表、现金日记账均从凭证实时归集计算。" },
];

/* ------------------------------------------------------------------ */
/*  小组件                                                             */
/* ------------------------------------------------------------------ */

function Logo({ className = "size-8" }: { className?: string }) {
    return (
        <svg viewBox="0 0 32 32" className={className} fill="none">
            <defs>
                <linearGradient id="landLogo" x1="4" y1="2" x2="28" y2="30" gradientUnits="userSpaceOnUse">
                    <stop stopColor="#3b82f6" />
                    <stop offset="1" stopColor="#22c55e" />
                </linearGradient>
            </defs>
            <path d="M16 2 28 9v14l-12 7L4 23V9z" fill="url(#landLogo)" />
            <path d="M10.5 16.5 14.5 20.5 22 11.5" stroke="#fff" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
    );
}

/** 药丸按钮：primary=蓝底白字，ghost=白底描边。 */
function Pill({
    children,
    onClick,
    href,
    variant = "primary",
    large,
}: {
    children: React.ReactNode;
    onClick?: () => void;
    href?: string;
    variant?: "primary" | "ghost";
    large?: boolean;
}) {
    const base =
        "inline-flex items-center justify-center gap-2 rounded-full font-medium transition-all " +
        (variant === "primary"
            ? "bg-[#2f6bff] text-white shadow-sm hover:bg-[#255be0]"
            : "border border-slate-200 bg-white text-slate-700 hover:border-[#bcd3ff] hover:text-[#2f6bff]");
    const size = large ? "h-[48px] px-6 text-[15px]" : "h-[40px] px-5 text-sm";
    const cls = base + " " + size;
    if (href) {
        return (
            <a href={href} className={cls}>
                {children}
            </a>
        );
    }
    return (
        <button type="button" onClick={onClick} className={cls}>
            {children}
        </button>
    );
}

/** 居中大标题：深色前半句 + 蓝色后半句。 */
function SectionTitle({ dark, muted }: { dark: string; muted: string }) {
    return (
        <h2 className="mx-auto max-w-3xl text-center text-3xl font-semibold leading-snug tracking-tight text-slate-900 md:text-[40px] md:leading-[1.25]">
            {dark}
            <span className="text-[#2f6bff]">{muted}</span>
        </h2>
    );
}

/** 单据窗口：浅色版式，蓝色强调 / 绿色金额。 */
function DocWindow({ file, code }: { file: string; code: Tok[][] }) {
    return (
        <div className="rounded-2xl border border-slate-100 bg-white p-6 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <span className="inline-flex rounded-lg bg-[#eaf1ff] px-3 py-1.5 font-mono text-[11px] tracking-widest text-[#2f6bff]">
                {file}
            </span>
            <pre className="mt-5 overflow-x-auto font-mono text-[13.5px] leading-7">
                {code.map((line, i) => (
                    <div key={i}>
                        {line.map((tok, j) => (
                            <span
                                key={j}
                                className={
                                    tok.c === "kw"
                                        ? "text-[#2f6bff]"
                                        : tok.c === "str"
                                          ? "text-[#16a34a]"
                                          : tok.c === "dim"
                                            ? "text-slate-400"
                                            : "text-slate-700"
                                }
                            >
                                {tok.t}
                            </span>
                        ))}
                    </div>
                ))}
            </pre>
        </div>
    );
}

/** 状态面板 mock：浅色卡片 + 状态列表。 */
function StatusMock({ h }: { h: (typeof HIGHLIGHTS)[number] }) {
    return (
        <div className="rounded-2xl border border-slate-100 bg-white p-5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <div className="flex items-center gap-2">
                <span className="rounded-full bg-[#eaf1ff] px-3 py-1 text-xs font-medium text-[#2f6bff]">{h.mockLabel}</span>
                <span className="rounded-full border border-slate-200 px-3 py-1 text-xs text-slate-400">明细</span>
                <span className="rounded-full border border-slate-200 px-3 py-1 text-xs text-slate-400">历史</span>
            </div>
            <div className="mt-4 flex flex-col gap-2">
                {h.mockRows.map((r) => (
                    <div key={r.name} className="flex items-center justify-between rounded-xl bg-slate-50 px-4 py-3">
                        <span className="text-sm text-slate-600">{r.name}</span>
                        <span className={"flex items-center gap-1.5 text-[12px] font-medium " + (r.ok ? "text-[#16a34a]" : "text-slate-400")}>
                            <span className={"size-1.5 rounded-full " + (r.ok ? "bg-[#22c55e]" : "bg-slate-300")} />
                            {r.state}
                        </span>
                    </div>
                ))}
            </div>
            <div className="mt-4 h-1.5 overflow-hidden rounded-full bg-slate-100">
                <div className="h-full w-2/3 rounded-full bg-gradient-to-r from-[#2f6bff] to-[#22c55e]" />
            </div>
        </div>
    );
}

/* ------------------------------------------------------------------ */
/*  页面                                                               */
/* ------------------------------------------------------------------ */

export default function LandingPage() {
    const { status } = useAuth();
    const navigate = useNavigate();
    const authed = status === "authenticated";
    const primaryHref = authed ? "/app" : "/login";
    const primaryLabel = authed ? "进入控制台" : "立即登录";
    const [activeTab, setActiveTab] = useState(HIGHLIGHTS[0].key);
    const active = HIGHLIGHTS.find((h) => h.key === activeTab) ?? HIGHLIGHTS[0];

    return (
        <div id="top" className="flex min-h-svh scroll-smooth flex-col bg-[#f4f6fb] text-slate-600 antialiased">
            {/* 顶部导航 */}
            <header className="sticky top-0 z-50 h-16 border-b border-slate-100 bg-white/90 backdrop-blur">
                <div className="mx-auto flex h-full w-full max-w-[1200px] items-center justify-between px-4 md:px-6">
                    <Link to="/" className="flex items-center gap-2.5">
                        <Logo />
                        <span className="text-[17px] font-bold text-slate-800">云财</span>
                    </Link>
                    <nav className="hidden items-center gap-1 md:flex">
                        {NAV_LINKS.map((l) => (
                            <a
                                key={l.href}
                                href={l.href}
                                className="rounded-full px-4 py-2 text-[15px] font-medium text-slate-500 transition-colors hover:bg-slate-50 hover:text-slate-900"
                            >
                                {l.label}
                            </a>
                        ))}
                    </nav>
                    <div className="flex items-center gap-3">
                        {!authed && (
                            <Link to="/login" className="hidden text-[15px] font-medium text-slate-500 transition-colors hover:text-[#2f6bff] sm:block">
                                登录
                            </Link>
                        )}
                        <Pill onClick={() => navigate(primaryHref)}>{authed ? "进入控制台" : "开始使用"}</Pill>
                    </div>
                </div>
            </header>

            <main className="flex-1">
                {/* Hero */}
                <section className="relative overflow-hidden bg-gradient-to-b from-[#e7f0ff] via-[#eef4ff] to-[#f4f6fb]">
                    <div className="mx-auto grid w-full max-w-[1200px] items-center gap-12 px-4 pb-20 pt-16 md:px-6 md:pb-28 md:pt-24 lg:grid-cols-2">
                        <div className="flex flex-col gap-6">
                            <span className="inline-flex w-fit items-center gap-2 rounded-full border border-[#bcd3ff] bg-white px-3 py-1 text-[13px] font-medium text-[#2f6bff]">
                                <span className="size-1.5 rounded-full bg-[#22c55e]" />
                                新上线 · Excel 凭证批量导入
                            </span>
                            <h1 className="text-5xl font-bold leading-[1.1] tracking-tight text-slate-900 md:text-[56px]">
                                让记账<span className="text-[#2f6bff]">更简单</span>
                            </h1>
                            <p className="max-w-md text-lg leading-relaxed text-slate-500">
                                录入、归集、出表，云财打通从凭证到财务报表的全流程，告别繁琐的手工核算。
                            </p>
                            <div className="flex flex-wrap items-center gap-3 pt-1">
                                <Pill large onClick={() => navigate(primaryHref)}>
                                    {primaryLabel}
                                    <RightOutlined className="text-[16px]" />
                                </Pill>
                                <Pill large variant="ghost" href="#features">
                                    了解核心功能
                                </Pill>
                            </div>
                            <ul className="flex flex-wrap gap-x-5 gap-y-2 text-sm text-slate-500">
                                {["无需安装", "实时归集", "多账套"].map((t) => (
                                    <li key={t} className="flex items-center gap-1.5">
                                        <CheckCircleFilled className="text-[16px] text-[#22c55e]" />
                                        {t}
                                    </li>
                                ))}
                            </ul>
                        </div>
                        {/* 右侧插画 + 悬浮小卡 */}
                        <div className="relative hidden min-h-[360px] items-center justify-center lg:flex">
                            <div className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(50%_50%_at_60%_40%,rgba(47,107,255,0.12),transparent)]" />
                            <img src={bannerFinanceImg} alt="" className="h-[340px] w-auto drop-shadow-2xl" />
                            <div className="absolute right-2 top-4 flex items-center gap-2 rounded-2xl border border-slate-100 bg-white px-4 py-3 shadow-lg">
                                <CheckCircleFilled className="text-[20px] text-[#22c55e]" />
                                <span className="text-sm font-medium text-slate-700">借贷平衡</span>
                            </div>
                            <div className="absolute bottom-6 left-0 w-56 rounded-2xl border border-slate-100 bg-white p-4 shadow-lg">
                                <p className="text-[12px] text-slate-400">本月净利润</p>
                                <p className="mt-1 text-2xl font-bold text-slate-800">¥ 448,100</p>
                                <p className="mt-1 flex items-center gap-1 text-[12px] font-medium text-[#16a34a]">
                                    <ArrowUpOutlined className="text-[14px]" /> 18.7% 较上月
                                </p>
                            </div>
                        </div>
                    </div>
                </section>

                {/* 行业词标 */}
                <section className="border-y border-slate-100 bg-white">
                    <div className="mx-auto flex w-full max-w-[1200px] flex-wrap items-center justify-center gap-x-12 gap-y-5 px-4 py-10 md:justify-between md:px-6">
                        {INDUSTRIES.map((it) => (
                            <span key={it} className="text-lg font-medium text-slate-400">
                                {it}
                            </span>
                        ))}
                    </div>
                </section>

                {/* 数据指标 */}
                <section className="mx-auto w-full max-w-[1200px] px-4 py-16 md:px-6 md:py-24">
                    <SectionTitle dark="数字说话。" muted="把专业留给系统，把简单交给你。" />
                    <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                        {BENTO_STATS.map((s) => (
                            <div
                                key={s.label}
                                className={
                                    "rounded-2xl border border-slate-100 bg-white p-8 shadow-[0_1px_2px_rgba(16,24,40,0.04)] " +
                                    (s.wide ? "sm:col-span-2" : "")
                                }
                            >
                                <p className="text-5xl font-bold tracking-tight" style={{ color: s.accent }}>
                                    {s.value}
                                </p>
                                <p className="mt-3 text-[15px] font-semibold text-slate-800">{s.label}</p>
                                <p className="mt-2 max-w-md text-[14px] leading-relaxed text-slate-500">{s.desc}</p>
                            </div>
                        ))}
                    </div>
                </section>

                {/* 功能亮点 */}
                <section id="features" className="scroll-mt-20 border-y border-slate-100 bg-white py-16 md:py-24">
                    <div className="mx-auto w-full max-w-[1200px] px-4 md:px-6">
                        <SectionTitle dark="凭证。报表。账套。" muted="覆盖日常财务的关键环节。" />
                        <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                            {FEATURES.map((f) => (
                                <div
                                    key={f.title}
                                    className="group flex flex-col gap-3 rounded-2xl border border-slate-100 bg-white p-7 shadow-[0_1px_2px_rgba(16,24,40,0.04)] transition-all hover:-translate-y-0.5 hover:shadow-md"
                                >
                                    <span className="flex size-11 items-center justify-center rounded-xl bg-[#eaf1ff] text-[#2f6bff]">
                                        <f.icon className="text-[20px]" />
                                    </span>
                                    <h3 className="mt-1 text-[17px] font-semibold text-slate-800">{f.title}</h3>
                                    <p className="text-sm leading-relaxed text-slate-500">{f.desc}</p>
                                    <span className="mt-auto pt-2 text-[12px] font-medium uppercase tracking-widest text-slate-300">{f.tag}</span>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 功能详解 Tab */}
                <section id="highlights" className="scroll-mt-20 py-16 md:py-24">
                    <div className="mx-auto w-full max-w-[1200px] px-4 md:px-6">
                        <SectionTitle dark="凭证、报表、多账套。" muted="一套系统全部搞定。" />
                        {/* Tab 药丸 */}
                        <div className="mt-10 flex flex-wrap justify-center gap-3">
                            {HIGHLIGHTS.map((h) => (
                                <button
                                    key={h.key}
                                    type="button"
                                    onClick={() => setActiveTab(h.key)}
                                    className={
                                        "flex items-center gap-2 rounded-full px-5 py-2.5 text-sm font-medium transition-colors " +
                                        (h.key === activeTab
                                            ? "bg-[#2f6bff] text-white shadow-sm"
                                            : "border border-slate-200 bg-white text-slate-500 hover:text-slate-800")
                                    }
                                >
                                    <h.icon className="text-[16px]" />
                                    {h.tab}
                                </button>
                            ))}
                        </div>
                        {/* 面板 */}
                        <div className="mt-8 rounded-3xl border border-slate-100 bg-white p-7 shadow-[0_1px_2px_rgba(16,24,40,0.04)] md:p-12">
                            <div className="grid items-center gap-10 lg:grid-cols-[1fr_1.15fr]">
                                <div>
                                    <h3 className="text-2xl font-semibold tracking-tight text-slate-900 md:text-[28px]">
                                        {active.title}
                                        <span className="text-[#2f6bff]">{active.titleMuted}</span>
                                    </h3>
                                    <p className="mt-4 leading-relaxed text-slate-500">{active.desc}</p>
                                    <ul className="mt-7 flex flex-col gap-3">
                                        {active.points.map((p) => (
                                            <li key={p} className="flex items-center gap-2.5 text-[15px] text-slate-700">
                                                <CheckCircleFilled className="shrink-0 text-[20px] text-[#22c55e]" />
                                                {p}
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                                <div className="flex flex-col gap-5">
                                    <DocWindow file={active.file} code={active.code} />
                                    <StatusMock h={active} />
                                </div>
                            </div>
                        </div>
                    </div>
                </section>

                {/* 使用流程 */}
                <section id="workflow" className="scroll-mt-20 border-y border-slate-100 bg-white py-16 md:py-24">
                    <div className="mx-auto w-full max-w-[1200px] px-4 md:px-6">
                        <SectionTitle dark="四步开始记账。" muted="从建账到出表，上手无门槛。" />
                        <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                            {STEPS.map((s, i) => (
                                <div key={s.title} className="relative flex flex-col gap-3 rounded-2xl border border-slate-100 bg-[#f8fafc] p-7">
                                    <span className="absolute right-6 top-6 text-2xl font-bold text-slate-200">{String(i + 1).padStart(2, "0")}</span>
                                    <span className="flex size-11 items-center justify-center rounded-xl bg-[#eaf1ff] text-[#2f6bff]">
                                        <s.icon className="text-[20px]" />
                                    </span>
                                    <h3 className="mt-1 text-[17px] font-semibold text-slate-800">{s.title}</h3>
                                    <p className="text-sm leading-relaxed text-slate-500">{s.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 产品优势 */}
                <section className="py-16 md:py-24">
                    <div className="mx-auto w-full max-w-[1200px] px-4 md:px-6">
                        <SectionTitle dark="为什么选择云财。" muted="每个环节都为省心而设计。" />
                        <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                            {ADVANTAGES.map((a) => (
                                <div key={a.title} className="flex flex-col gap-3 rounded-2xl border border-slate-100 bg-white p-7 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                                    <span className="flex size-11 items-center justify-center rounded-xl bg-[#e6f8f0] text-[#10b981]">
                                        <a.icon className="text-[20px]" />
                                    </span>
                                    <h3 className="mt-1 text-[17px] font-semibold text-slate-800">{a.title}</h3>
                                    <p className="text-sm leading-relaxed text-slate-500">{a.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 常见问题 */}
                <section id="faq" className="scroll-mt-20 pb-16 md:pb-24">
                    <div className="mx-auto w-full max-w-[1200px] px-4 md:px-6">
                        <SectionTitle dark="常见问题。" muted="你可能想了解这些。" />
                        <div className="mx-auto mt-12 max-w-3xl divide-y divide-slate-100 overflow-hidden rounded-2xl border border-slate-100 bg-white shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                            {FAQS.map((f) => (
                                <details key={f.q} className="group px-6 [&_summary]:cursor-pointer">
                                    <summary className="flex min-h-[64px] list-none items-center justify-between gap-4 py-5 text-[16px] font-medium text-slate-800">
                                        {f.q}
                                        <DownOutlined className="text-[18px] shrink-0 text-slate-400 transition-transform group-open:rotate-180" />
                                    </summary>
                                    <p className="max-w-[760px] pb-6 text-[15px] leading-relaxed text-slate-500">{f.a}</p>
                                </details>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 页脚 CTA */}
                <section className="px-4 pb-20 md:px-6">
                    <div className="relative mx-auto flex w-full max-w-[1200px] flex-col items-center gap-6 overflow-hidden rounded-3xl bg-gradient-to-br from-[#2f6bff] to-[#22c55e] px-6 py-16 text-center text-white md:py-20">
                        <div className="pointer-events-none absolute -right-16 -top-16 size-64 rounded-full bg-white/10" />
                        <div className="pointer-events-none absolute -bottom-20 -left-10 size-72 rounded-full bg-white/10" />
                        <h2 className="relative max-w-2xl text-4xl font-bold leading-[1.15] tracking-tight md:text-[48px]">今天就开始使用云财</h2>
                        <p className="relative max-w-xl text-lg text-white/85">登录云财，立即体验高效、准确、清晰的在线记账。</p>
                        <div className="relative flex flex-wrap items-center justify-center gap-3">
                            <button
                                type="button"
                                onClick={() => navigate(primaryHref)}
                                className="inline-flex h-[48px] items-center gap-2 rounded-full bg-white px-6 text-[15px] font-semibold text-[#2f6bff] shadow-sm transition-transform hover:-translate-y-0.5"
                            >
                                {authed ? "进入控制台" : "登录使用"}
                                <RightOutlined className="text-[16px]" />
                            </button>
                            <a
                                href="#workflow"
                                className="inline-flex h-[48px] items-center gap-2 rounded-full border border-white/40 px-6 text-[15px] font-medium text-white transition-colors hover:bg-white/10"
                            >
                                查看使用流程
                            </a>
                        </div>
                    </div>
                </section>
            </main>

            {/* 页脚 */}
            <footer className="border-t border-slate-100 bg-white">
                <div className="mx-auto grid w-full max-w-[1200px] gap-10 px-4 py-14 md:grid-cols-[1.5fr_1fr_1fr_1.5fr] md:px-6">
                    <div className="flex flex-col gap-3">
                        <span className="flex items-center gap-2.5">
                            <Logo className="size-7" />
                            <span className="text-[16px] font-bold text-slate-800">云财</span>
                        </span>
                        <p className="max-w-sm text-sm leading-relaxed text-slate-500">专为中小企业打造的在线记账系统，让财务工作更简单、更规范。</p>
                    </div>
                    <div className="flex flex-col gap-2.5 text-[15px]">
                        <span className="text-[12px] font-medium uppercase tracking-widest text-slate-400">产品</span>
                        {NAV_LINKS.map((l) => (
                            <a key={l.href} href={l.href} className="w-fit text-slate-500 transition-colors hover:text-[#2f6bff]">
                                {l.label}
                            </a>
                        ))}
                    </div>
                    <div className="flex flex-col gap-2.5 text-[15px]">
                        <span className="text-[12px] font-medium uppercase tracking-widest text-slate-400">开始使用</span>
                        <Link to={primaryHref} className="w-fit text-slate-500 transition-colors hover:text-[#2f6bff]">
                            {authed ? "进入控制台" : "登录"}
                        </Link>
                    </div>
                    <div className="flex flex-col gap-3">
                        <span className="text-[12px] font-medium uppercase tracking-widest text-slate-400">订阅更新</span>
                        <form className="flex items-center gap-2" onSubmit={(e) => e.preventDefault()}>
                            <input
                                type="email"
                                placeholder="you@company.com"
                                className="h-9 w-full max-w-52 rounded-full border border-slate-200 bg-white px-4 text-xs text-slate-700 outline-none placeholder:text-slate-400 focus:border-[#2f6bff]"
                            />
                            <button type="submit" className="h-9 shrink-0 rounded-full bg-[#2f6bff] px-4 text-xs font-medium text-white transition-colors hover:bg-[#255be0]">
                                订阅
                            </button>
                        </form>
                        <p className="mt-1 flex items-center gap-2 text-[12px] text-slate-400">
                            <span className="size-2 rounded-full bg-[#22c55e]" />
                            系统运行正常
                        </p>
                    </div>
                </div>
                <div className="border-t border-slate-100">
                    <div className="mx-auto flex w-full max-w-[1200px] items-center justify-between px-4 py-6 text-xs text-slate-400 md:px-6">
                        <span>© {new Date().getFullYear()} 云财. 保留所有权利.</span>
                        <a
                            href="#top"
                            onClick={(e) => {
                                e.preventDefault();
                                window.scrollTo({ top: 0, behavior: "smooth" });
                            }}
                            className="flex items-center gap-1.5 transition-colors hover:text-[#2f6bff]"
                        >
                            回到顶部
                            <ArrowUpOutlined className="text-[14px]" />
                        </a>
                    </div>
                </div>
            </footer>
        </div>
    );
}
