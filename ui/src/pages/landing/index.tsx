import { useState } from "react"
import { Link, useNavigate } from "react-router"
import {
    ArrowRight,
    ArrowUp,
    BarChart3,
    BookOpen,
    Check,
    CheckCircle2,
    ChevronDown,
    Cloud,
    Copy,
    FileText,
    Folder,
    GalleryVerticalEnd,
    LayoutDashboard,
    ListChecks,
    Lock,
    ScaleIcon,
    ShieldCheck,
    Table2,
    Upload,
    Wallet,
    Zap,
} from "lucide-react"

import { useAuth } from "@/auth/AuthContext"

/* ============================================================
 * mastra.ai 设计令牌（组件级复刻）
 * - 框卡（antigrid panel）：border-2 #161616 + 40px 圆角 + #0f0f0f 表面
 * - 内层小卡圆角 16px（rounded-2xl）
 * - 药丸按钮：双层渐变描边（padding-box + border-box）
 * - 标签舌（lip）：从面板边沿凸出的圆角舌，两肩用反向圆角内凹
 * ============================================================ */

/** 框卡基础样式。 */
const PANEL = "rounded-[40px] border-2 border-[#161616] bg-[#0f0f0f]"

/** 顶部导航锚点。 */
const NAV_LINKS = [
    { label: "功能", href: "#features", dot: true },
    { label: "亮点", href: "#highlights", dot: true },
    { label: "流程", href: "#workflow", dot: false },
    { label: "常见问题", href: "#faq", dot: false },
]

/** Logo 墙：适用行业词标（单色处理，字体各异模拟品牌字标）。 */
const INDUSTRIES = [
    { name: "科技研发", cls: "font-mono tracking-tight" },
    { name: "贸易流通", cls: "font-serif italic" },
    { name: "咨询服务", cls: "font-bold tracking-[0.3em]" },
    { name: "电子商务", cls: "font-semibold" },
    { name: "生产制造", cls: "font-mono uppercase" },
    { name: "文化创意", cls: "font-serif" },
]

/** 数据指标 bento：首卡跨两列，大数字 + 描述。 */
const BENTO_STATS = [
    {
        value: "100+",
        label: "预置标准科目",
        desc: "遵循企业会计准则的科目模板开箱即用，编码层级、余额方向全部预置，建账无需从零开始。",
        tint: "rgba(42,219,92,0.06)",
        wide: true,
    },
    {
        value: "4",
        label: "核心财务报表",
        desc: "科目余额表、资产负债表、现金日记账、利润表。",
        tint: "rgba(243,173,93,0.05)",
        wide: false,
    },
    {
        value: "实时",
        label: "数据归集出表",
        desc: "凭证保存即入账，报表随点随出。",
        tint: "rgba(93,166,243,0.05)",
        wide: false,
    },
    {
        value: "多账套",
        label: "一号管理多公司",
        desc: "账套之间数据完全隔离，随时切换。",
        tint: "rgba(42,219,92,0.04)",
        wide: false,
    },
    {
        value: "0",
        label: "安装成本",
        desc: "打开浏览器即用，换台设备也能继续工作。",
        tint: "rgba(243,173,93,0.04)",
        wide: false,
    },
]

/** 核心功能亮点（上文下图：底部配骨架示意）。 */
const FEATURES = [
    {
        icon: FileText,
        tag: "VOUCHER",
        title: "凭证管理",
        desc: "录入、编辑、审核记账凭证，自动校验借贷平衡，支持金额大写。",
        bars: [72, 48, 60],
    },
    {
        icon: Upload,
        tag: "IMPORT",
        title: "凭证导入",
        desc: "从 Excel 一键批量导入历史凭证，快速完成建账初始化。",
        bars: [56, 80, 40],
    },
    {
        icon: Table2,
        tag: "TRIAL-BALANCE",
        title: "科目余额表",
        desc: "按期间实时汇总各科目发生额与余额，随时掌握账务全貌。",
        bars: [64, 44, 76],
    },
    {
        icon: ScaleIcon,
        tag: "BALANCE-SHEET",
        title: "资产负债表",
        desc: "遵循会企 01 表标准行次，从凭证实时归集，报表随点随出。",
        bars: [52, 68, 36],
    },
    {
        icon: BookOpen,
        tag: "CASH-JOURNAL",
        title: "现金日记账",
        desc: "自动从库存现金凭证派生序时账，逐笔余额一目了然。",
        bars: [76, 40, 58],
    },
    {
        icon: LayoutDashboard,
        tag: "ACCOUNT-SETS",
        title: "多账套管理",
        desc: "一个账号管理多家公司账套，随时切换互不干扰。",
        bars: [44, 62, 70],
    },
]

/** 代码 token：kw=橙色关键字，str=绿色字符串/金额，dim=弱化。 */
type Tok = { t: string; c?: "kw" | "str" | "dim" }

/** 功能详解 Tab（连体文件夹舌 + 代码窗口 + UI mock）。 */
const HIGHLIGHTS = [
    {
        key: "voucher",
        tab: "凭证全流程",
        icon: ListChecks,
        title: "从录入到审核，",
        titleMuted: "一气呵成。",
        desc: "结构化的分录录入界面，实时校验借贷平衡与金额大写，避免手工核算出错。历史凭证可从 Excel 批量导入，建账不再是负担。",
        points: ["借贷自动平衡校验", "金额大写自动生成", "Excel 批量导入历史凭证"],
        file: "记-0007.PZ",
        code: [
            [{ t: "凭证", c: "kw" as const }, { t: " 记-0007 · 2026-07-25 {" }],
            [{ t: "  借", c: "kw" as const }, { t: "  银行存款" }, { t: "            120,000.00", c: "str" as const }],
            [{ t: "  贷", c: "kw" as const }, { t: "  主营业务收入" }, { t: "        113,207.55", c: "str" as const }],
            [{ t: "  贷", c: "kw" as const }, { t: "  应交税费·增值税" }, { t: "      6,792.45", c: "str" as const }],
            [{ t: "}" }],
            [{ t: "✓ 借贷平衡 · 壹拾贰万元整", c: "dim" as const }],
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
        icon: BarChart3,
        title: "报表随点随出，",
        titleMuted: "无需等待。",
        desc: "科目余额表、资产负债表、现金日记账全部从凭证实时归集计算，按会计准则标准行次呈现，任意期间随时查看。",
        points: ["会企 01 表标准行次", "任意期间即时切换", "资产负债恒等式自动平衡"],
        file: "BALANCE-SHEET.2026-07",
        code: [
            [{ t: "报表", c: "kw" as const }, { t: " 资产负债表 · 2026-07 {" }],
            [{ t: "  资产总计" }, { t: "          1,286,400.00", c: "str" as const }],
            [{ t: "  负债合计" }, { t: "            486,400.00", c: "str" as const }],
            [{ t: "  所有者权益合计" }, { t: "      800,000.00", c: "str" as const }],
            [{ t: "}" }],
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
        icon: ShieldCheck,
        title: "多账套隔离，",
        titleMuted: "数据更安心。",
        desc: "一个账号即可管理多家公司账套，数据彼此隔离；登录鉴权保护每一次访问，账务数据只有你能看见。",
        points: ["多账套数据隔离", "登录令牌鉴权", "随时切换互不干扰"],
        file: "ACCOUNT-SETS.CONF",
        code: [
            [{ t: "账套", c: "kw" as const }, { t: " {" }],
            [{ t: "  杭州数从科技有限公司" }, { t: "   ← 当前", c: "str" as const }],
            [{ t: "  示例贸易有限公司" }, { t: "       已隔离", c: "dim" as const }],
            [{ t: "  示例咨询有限公司" }, { t: "       已隔离", c: "dim" as const }],
            [{ t: "}" }],
            [{ t: "✓ JWT 鉴权 · 账套级数据隔离", c: "dim" as const }],
        ],
        mockLabel: "访问控制",
        mockRows: [
            { name: "登录令牌校验", state: "启用", ok: true },
            { name: "账套数据隔离", state: "启用", ok: true },
            { name: "匿名访问", state: "禁止", ok: false },
        ],
    },
]

/** 使用流程步骤。 */
const STEPS = [
    { icon: LayoutDashboard, title: "创建账套", desc: "填写公司信息与启用期间，快速建立你的账套。" },
    { icon: ListChecks, title: "初始化科目", desc: "使用预置标准科目模板，或按需增删调整。" },
    { icon: FileText, title: "录入凭证", desc: "手工录入或从 Excel 导入，系统自动校验平衡。" },
    { icon: BarChart3, title: "查看报表", desc: "余额表、资产负债表实时生成，随时掌握经营。" },
]

/** 产品优势。 */
const ADVANTAGES = [
    { icon: Zap, title: "开箱即用", desc: "预置标准科目与报表模板，无需复杂配置即可开始记账。" },
    { icon: Cloud, title: "在线随行", desc: "浏览器即可访问，数据集中存储，换台设备也能继续工作。" },
    { icon: Lock, title: "安全可靠", desc: "登录鉴权 + 账套隔离，保障财务数据的私密与安全。" },
    { icon: Wallet, title: "贴合准则", desc: "遵循企业会计准则科目编码与报表行次，出表即规范。" },
]

/** 常见问题。 */
const FAQS = [
    { q: "需要安装客户端吗？", a: "不需要。好会计是在线系统，打开浏览器登录即可使用。" },
    { q: "可以管理多家公司吗？", a: "可以。一个账号支持创建多个账套，数据彼此隔离，可随时切换。" },
    { q: "历史凭证怎么导入？", a: "在「凭证导入」中上传符合模板的 Excel 文件即可批量导入历史凭证。" },
    { q: "报表数据是实时的吗？", a: "是的。科目余额表、资产负债表、现金日记账均从凭证实时归集计算。" },
]

/** 双层渐变描边药丸按钮（mastra 全站按钮签名样式）。 */
function GradientPill({
    children,
    onClick,
    href,
    large,
}: {
    children: React.ReactNode
    onClick?: () => void
    href?: string
    large?: boolean
}) {
    const cls =
        "inline-flex items-center gap-2 rounded-full border-[1.5px] border-transparent text-[#e6e6e6] transition-[filter] hover:brightness-125 " +
        "[background:linear-gradient(#1c1c1c,#1c1c1c)_padding-box,linear-gradient(to_bottom,#383838,rgba(43,43,43,0))_border-box] " +
        (large ? "h-[50px] px-6 text-[15px] font-medium" : "h-[38px] px-[17px] text-sm font-medium")
    if (href) {
        return (
            <a href={href} className={cls}>
                {children}
            </a>
        )
    }
    return (
        <button type="button" onClick={onClick} className={cls}>
            {children}
        </button>
    )
}

/** 命令复制框：等宽字体 + 复制图标（mastra hero 的 npm create 框）。 */
function CommandPill({ command }: { command: string }) {
    const [copied, setCopied] = useState(false)
    const copy = () => {
        navigator.clipboard?.writeText(command).catch(() => {})
        setCopied(true)
        setTimeout(() => setCopied(false), 1500)
    }
    return (
        <button
            type="button"
            onClick={copy}
            title="复制"
            className="inline-flex h-[50px] items-center gap-3 rounded-full border-[1.5px] border-transparent pl-[22px] pr-5 font-mono text-[14.5px] tracking-[0.055em] text-[#e6e6e6] transition-[filter] hover:brightness-125 [background:linear-gradient(#1c1c1c,#1c1c1c)_padding-box,linear-gradient(to_bottom,#383838,rgba(43,43,43,0))_border-box]"
        >
            {command}
            {copied ? <Check className="size-[18px] text-[#98fc82]" /> : <Copy className="size-[18px] text-[#939393]" />}
        </button>
    )
}

/** 标签舌（lip）：从上沿挂出的凸起圆角舌，两肩反向圆角内凹。 */
function Lip({
    children,
    color,
    textCls,
    href,
}: {
    children: React.ReactNode
    color: string
    textCls: string
    href?: string
}) {
    const inner = (
        <span className={"relative flex items-center gap-2 rounded-b-[20px] px-6 py-2.5 text-sm font-medium " + textCls} style={{ backgroundColor: color }}>
            {/* 左右肩：透明方块 + 反向圆角投影，形成内凹过渡 */}
            <span className="pointer-events-none absolute -left-5 top-0 size-5 rounded-tr-[20px]" style={{ boxShadow: `5px -5px 0 ${color}` }} />
            <span className="pointer-events-none absolute -right-5 top-0 size-5 rounded-tl-[20px]" style={{ boxShadow: `-5px -5px 0 ${color}` }} />
            {children}
        </span>
    )
    if (href) {
        return (
            <a href={href} className="group/lip relative z-10 mx-auto w-fit">
                {inner}
            </a>
        )
    }
    return <span className="relative z-10 mx-auto w-fit">{inner}</span>
}

/** 居中大标题：白色前半句 + 灰色后半句（mastra 排版签名）。 */
function SectionTitle({ white, muted }: { white: string; muted: string }) {
    return (
        <h2 className="mx-auto max-w-3xl text-center text-3xl font-semibold leading-snug tracking-tight text-white md:text-[40px] md:leading-[1.25]">
            {white}
            <span className="text-[#939393]">{muted}</span>
        </h2>
    )
}

/** 代码窗口：无红绿灯，左上角文件名胶囊，橙关键字/绿字符串高亮。 */
function CodeWindow({ file, code }: { file: string; code: Tok[][] }) {
    return (
        <div className="rounded-[24px] bg-[#080808] p-6">
            <span className="inline-flex rounded-lg bg-[#1c1c1c] px-3 py-1.5 font-mono text-[11px] uppercase tracking-widest text-[#d9d9d9]">
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
                                        ? "text-[#f3ad5d]"
                                        : tok.c === "str"
                                          ? "text-[#98fc82]"
                                          : tok.c === "dim"
                                            ? "text-[#939393]"
                                            : "text-[#f0f0f0]"
                                }
                            >
                                {tok.t}
                            </span>
                        ))}
                    </div>
                ))}
            </pre>
        </div>
    )
}

/** Studio 风格 UI mock：胶囊小 tab + 状态列表（mastra 代码区右侧面板）。 */
function StudioMock({ h }: { h: (typeof HIGHLIGHTS)[number] }) {
    return (
        <div className="rounded-[24px] bg-[#141414] p-5">
            <div className="flex items-center gap-2">
                <span className="rounded-full border border-white/20 bg-[#1c1c1c] px-3 py-1 text-xs text-[#f0f0f0]">
                    {h.mockLabel}
                </span>
                <span className="rounded-full border border-white/10 px-3 py-1 text-xs text-[#939393]">明细</span>
                <span className="rounded-full border border-white/10 px-3 py-1 text-xs text-[#939393]">历史</span>
            </div>
            <div className="mt-4 flex flex-col gap-2">
                {h.mockRows.map((r) => (
                    <div key={r.name} className="flex items-center justify-between rounded-2xl bg-[#1a1a1a] px-4 py-3">
                        <span className="text-sm text-[#d9d9d9]">{r.name}</span>
                        <span
                            className={
                                "flex items-center gap-1.5 font-mono text-[11px] uppercase tracking-widest " +
                                (r.ok ? "text-[#98fc82]" : "text-[#707070]")
                            }
                        >
                            <span className={"size-1.5 rounded-full " + (r.ok ? "bg-[#98fc82]" : "bg-[#424242]")} />
                            {r.state}
                        </span>
                    </div>
                ))}
            </div>
            <div className="mt-4 h-1.5 overflow-hidden rounded-full bg-[#1c1c1c]">
                <div className="h-full w-2/3 rounded-full bg-gradient-to-r from-[#2adb5c] to-[#98fc82]" />
            </div>
        </div>
    )
}

/** 公开的官网首页：组件级复刻 mastra.ai 设计体系。 */
export default function LandingPage() {
    const { status } = useAuth()
    const navigate = useNavigate()
    const authed = status === "authenticated"
    const primaryHref = authed ? "/app" : "/login"
    const primaryLabel = authed ? "进入控制台" : "立即登录"
    const [activeTab, setActiveTab] = useState(HIGHLIGHTS[0].key)
    const active = HIGHLIGHTS.find((h) => h.key === activeTab) ?? HIGHLIGHTS[0]

    return (
        <div className="flex min-h-svh scroll-smooth flex-col bg-[#020202] text-[#d9d9d9] selection:bg-[#2adb5c]/30">
            {/* 顶部导航：实底 #0f0f0f（非毛玻璃）+ 2px 下边框 + 深投影 */}
            <header className="sticky top-0 z-50 h-16 border-b-2 border-[#161616] bg-[#0f0f0f] shadow-[0_4px_60px_rgba(0,0,0,0.75)]">
                <div className="mx-auto flex h-full w-full max-w-[1328px] items-center justify-between px-4 md:px-6">
                    <Link to="/" className="flex items-center gap-2 font-semibold text-white">
                        <div className="flex size-8 items-center justify-center rounded-lg bg-[#2adb5c] text-black">
                            <GalleryVerticalEnd className="size-4" />
                        </div>
                        <span className="text-lg">好会计</span>
                    </Link>
                    <nav className="hidden items-center gap-1 md:flex">
                        {NAV_LINKS.map((l) => (
                            <a
                                key={l.href}
                                href={l.href}
                                className="flex items-center gap-2 rounded-full px-4 py-2 text-[15px] font-medium text-[#cccccc] transition-colors hover:text-white"
                            >
                                {l.dot && <span className="size-1 rounded-full bg-[#5a5a5a]" />}
                                {l.label}
                            </a>
                        ))}
                    </nav>
                    <div className="flex items-center gap-4">
                        {!authed && (
                            <Link to="/login" className="hidden text-[15px] font-medium text-[#cccccc] transition-colors hover:text-white sm:block">
                                登录
                            </Link>
                        )}
                        <GradientPill onClick={() => navigate(primaryHref)}>
                            {authed ? "进入控制台" : "开始使用"}
                        </GradientPill>
                    </div>
                </div>
            </header>

            {/* 公告标签舌：琥珀实底，从 header 下沿挂出 */}
            <div className="relative h-0">
                <div className="absolute inset-x-0 top-0 flex justify-center">
                    <Lip color="#f3ad5d" textCls="text-black" href="#features">
                        新上线 <span className="size-1 rounded-full bg-black/40" /> Excel 凭证批量导入
                        <ArrowRight className="size-3.5 transition-transform group-hover/lip:translate-x-0.5" />
                    </Lip>
                </div>
            </div>

            <main className="flex-1">
                {/* Hero：左文右 mock，54px 主标题 + 命令复制框 + 药丸组 */}
                <section className="relative overflow-hidden">
                    <div className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(50%_40%_at_70%_20%,rgba(42,219,92,0.06),transparent)]" />
                    <div className="mx-auto grid w-full max-w-[1328px] items-center gap-12 px-4 pb-20 pt-24 md:px-6 md:pb-28 md:pt-32 lg:grid-cols-2">
                        <div className="flex flex-col gap-7">
                            <h1 className="text-5xl font-semibold leading-[1.1] tracking-tight text-[#f0f0f0] md:text-[54px]">
                                让记账更简单
                            </h1>
                            <p className="max-w-md text-lg leading-relaxed text-[#939393]">
                                <span className="text-[#2adb5c]">录入</span>
                                <span className="text-[#f0f0f0]">、归集、出表</span>
                                ，好会计打通从凭证到财务报表的全流程，告别繁琐的手工核算。
                            </p>
                            <div className="flex flex-wrap items-center gap-3 pt-1">
                                <CommandPill command="kuaiji import 凭证.xls" />
                                <GradientPill large onClick={() => navigate(primaryHref)}>
                                    {primaryLabel}
                                    <ArrowRight className="size-4" />
                                </GradientPill>
                            </div>
                            <ul className="flex flex-wrap gap-x-5 gap-y-2 font-mono text-xs uppercase tracking-widest text-[#707070]">
                                {["无需安装", "实时归集", "多账套"].map((t) => (
                                    <li key={t} className="flex items-center gap-1.5">
                                        <CheckCircle2 className="size-3.5 text-[#2adb5c]" />
                                        {t}
                                    </li>
                                ))}
                            </ul>
                        </div>
                        {/* 右侧 mock：文件树卡叠加骨架气泡（mastra-project 母题） */}
                        <div className="relative hidden min-h-[380px] lg:block">
                            <div className="absolute right-0 top-0 flex w-[420px] flex-col gap-3 rounded-[24px] bg-[#0a0a0a] p-8">
                                <div className="h-4 w-3/4 rounded-full bg-[#1a1a1a]" />
                                <div className="h-4 w-1/2 rounded-full bg-[#1a1a1a]" />
                                <div className="mt-2 h-1.5 w-2/5 rounded-full bg-gradient-to-r from-[#2adb5c] to-transparent" />
                                <div className="h-4 w-2/3 rounded-full bg-[#141414]" />
                                <div className="h-4 w-1/3 rounded-full bg-[#141414]" />
                                <div className="mt-4 flex items-center justify-between rounded-2xl bg-[#141414] px-4 py-3">
                                    <div className="h-3 w-24 rounded-full bg-[#262626]" />
                                    <span className="flex size-7 items-center justify-center rounded-full bg-[#262626]">
                                        <ArrowUp className="size-3.5 text-[#939393]" />
                                    </span>
                                </div>
                            </div>
                            <div className="absolute left-0 top-14 w-64 rounded-[24px] bg-[#141414] p-6 shadow-[0_24px_80px_-16px_rgba(0,0,0,0.95)]">
                                <p className="text-[15px] text-[#939393]">我的账套</p>
                                <ul className="mt-4 flex flex-col gap-3.5 text-[15px] text-[#d9d9d9]">
                                    {["记账凭证", "科目余额表", "资产负债表", "现金日记账", "多账套"].map((t, i) => (
                                        <li key={t} className="flex items-center gap-2.5">
                                            <Folder className={"size-4 " + (i === 0 ? "text-[#2adb5c]" : "text-[#707070]")} />
                                            {t}
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        </div>
                    </div>
                </section>

                {/* Logo 墙：单行静态横排，单色词标，无边框单元格 */}
                <section className="border-y-2 border-[#161616] bg-[#050505]">
                    <div className="mx-auto flex w-full max-w-[1328px] flex-wrap items-center justify-center gap-x-12 gap-y-6 px-4 py-12 md:justify-between md:px-10">
                        {INDUSTRIES.map((it) => (
                            <span
                                key={it.name}
                                className={"text-xl text-[#8a8a8a] transition-colors hover:text-white " + it.cls}
                            >
                                {it.name}
                            </span>
                        ))}
                    </div>
                </section>

                {/* 数据指标 bento：独立框卡 + 每卡专属色调渐变底 */}
                <section className="mx-auto w-full max-w-[1328px] px-4 py-16 md:px-6 md:py-24">
                    <SectionTitle white="数字说话。" muted="把专业留给系统，把简单交给你。" />
                    <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                        {BENTO_STATS.map((s) => (
                            <div
                                key={s.label}
                                className={
                                    PANEL +
                                    " relative overflow-hidden p-9 transition-colors hover:border-[#262626] " +
                                    (s.wide ? "sm:col-span-2" : "")
                                }
                            >
                                <div
                                    className="pointer-events-none absolute inset-0"
                                    style={{ background: `radial-gradient(80% 80% at 20% 0%, ${s.tint}, transparent)` }}
                                />
                                <div className="relative">
                                    <p className="text-5xl font-semibold tracking-tight text-white">{s.value}</p>
                                    <p className="mt-3 font-mono text-[11px] uppercase tracking-widest text-[#939393]">
                                        {s.label}
                                    </p>
                                    <p className="mt-3 max-w-md text-[15px] leading-relaxed text-[#939393]">{s.desc}</p>
                                </div>
                            </div>
                        ))}
                    </div>
                </section>

                {/* 功能亮点：上文下图卡片（标题+描述+骨架示意） */}
                <section id="features" className="scroll-mt-20 bg-[#080808] py-16 md:py-24">
                    <div className="mx-auto w-full max-w-[1328px] px-4 md:px-6">
                        <SectionTitle white="凭证。报表。账套。" muted="覆盖日常财务的关键环节。" />
                        <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
                            {FEATURES.map((f) => (
                                <div
                                    key={f.title}
                                    className={PANEL + " group flex flex-col gap-4 p-9 transition-colors hover:border-[#262626]"}
                                >
                                    <div className="flex items-center justify-between">
                                        <f.icon className="size-6 text-[#939393] opacity-60 transition-opacity group-hover:opacity-100" />
                                        <span className="font-mono text-[10px] uppercase tracking-widest text-[#424242] transition-colors group-hover:text-[#707070]">
                                            {f.tag}
                                        </span>
                                    </div>
                                    <h3 className="text-[17px] font-semibold text-white">{f.title}</h3>
                                    <p className="text-sm leading-relaxed text-[#939393]">{f.desc}</p>
                                    {/* 底部骨架示意：一条绿色强调 + 两条灰条 */}
                                    <div className="mt-auto flex flex-col gap-2 rounded-2xl bg-[#0a0a0a] p-4 pt-5">
                                        <div
                                            className="h-2 rounded-full bg-gradient-to-r from-[#2adb5c]/70 to-[#2adb5c]/10"
                                            style={{ width: `${f.bars[0]}%` }}
                                        />
                                        <div className="h-2 rounded-full bg-[#1c1c1c]" style={{ width: `${f.bars[1]}%` }} />
                                        <div className="h-2 rounded-full bg-[#1c1c1c]" style={{ width: `${f.bars[2]}%` }} />
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 功能详解：连体文件夹舌 Tab + 代码窗口 + Studio mock */}
                <section id="highlights" className="scroll-mt-20 py-16 md:py-24">
                    <div className="mx-auto w-full max-w-[1328px] px-4 md:px-6">
                        <SectionTitle white="凭证、报表、多账套。" muted="一套系统全部搞定。" />
                        <div className="mt-14">
                            {/* Tab 行：激活项与下方面板同底连体，未激活为独立药丸 */}
                            <div className="flex items-end gap-3 overflow-x-auto px-6 md:px-12">
                                {HIGHLIGHTS.map((h) =>
                                    h.key === activeTab ? (
                                        <button
                                            key={h.key}
                                            type="button"
                                            className="relative flex h-[64px] shrink-0 items-center gap-2.5 rounded-t-[24px] bg-[#101010] px-7 font-mono text-xs uppercase tracking-widest text-[#f0f0f0]"
                                        >
                                            {/* 连体肩：与面板同色的反向圆角 */}
                                            <span className="pointer-events-none absolute -left-5 bottom-0 size-5 rounded-br-[20px] shadow-[5px_5px_0_#101010]" />
                                            <span className="pointer-events-none absolute -right-5 bottom-0 size-5 rounded-bl-[20px] shadow-[-5px_5px_0_#101010]" />
                                            <h.icon className="size-4" />
                                            {h.tab}
                                        </button>
                                    ) : (
                                        <button
                                            key={h.key}
                                            type="button"
                                            onClick={() => setActiveTab(h.key)}
                                            className="mb-2 flex h-[52px] shrink-0 items-center gap-2.5 rounded-full bg-[#141414] px-7 font-mono text-xs uppercase tracking-widest text-[#939393] transition-colors hover:text-[#d9d9d9]"
                                        >
                                            <h.icon className="size-4 opacity-60" />
                                            {h.tab}
                                        </button>
                                    ),
                                )}
                            </div>
                            <div className="rounded-[40px] bg-[#101010] p-7 md:p-12">
                                <div className="grid items-center gap-10 lg:grid-cols-[1fr_1.15fr]">
                                    <div>
                                        <h3 className="text-2xl font-semibold tracking-tight text-white md:text-[28px]">
                                            {active.title}
                                            <span className="text-[#939393]">{active.titleMuted}</span>
                                        </h3>
                                        <p className="mt-4 leading-relaxed text-[#939393]">{active.desc}</p>
                                        <ul className="mt-7 flex flex-col gap-3">
                                            {active.points.map((p) => (
                                                <li key={p} className="flex items-center gap-2.5 text-[15px] text-[#d9d9d9]">
                                                    <CheckCircle2 className="size-4 shrink-0 text-[#2adb5c]" />
                                                    {p}
                                                </li>
                                            ))}
                                        </ul>
                                    </div>
                                    <div className="flex flex-col gap-5">
                                        <CodeWindow file={active.file} code={active.code} />
                                        <StudioMock h={active} />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </section>

                {/* 使用流程：框卡步骤 + 等宽序号 */}
                <section id="workflow" className="scroll-mt-20 bg-[#080808] py-16 md:py-24">
                    <div className="mx-auto w-full max-w-[1328px] px-4 md:px-6">
                        <SectionTitle white="四步开始记账。" muted="从建账到出表，上手无门槛。" />
                        <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                            {STEPS.map((s, i) => (
                                <div
                                    key={s.title}
                                    className={PANEL + " group relative flex flex-col gap-4 p-9 transition-colors hover:border-[#262626]"}
                                >
                                    <span className="absolute right-8 top-8 font-mono text-2xl font-bold text-[#262626]">
                                        {String(i + 1).padStart(2, "0")}
                                    </span>
                                    <s.icon className="size-6 text-[#939393] opacity-60 transition-opacity group-hover:opacity-100" />
                                    <h3 className="text-[17px] font-semibold text-white">{s.title}</h3>
                                    <p className="text-sm leading-relaxed text-[#939393]">{s.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 产品优势 */}
                <section className="py-16 md:py-24">
                    <div className="mx-auto w-full max-w-[1328px] px-4 md:px-6">
                        <SectionTitle white="为什么选择好会计。" muted="每个环节都为省心而设计。" />
                        <div className="mt-12 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                            {ADVANTAGES.map((a) => (
                                <div
                                    key={a.title}
                                    className={PANEL + " group flex flex-col gap-4 p-9 transition-colors hover:border-[#262626]"}
                                >
                                    <a.icon className="size-6 text-[#939393] opacity-60 transition-opacity group-hover:opacity-100" />
                                    <h3 className="text-[17px] font-semibold text-white">{a.title}</h3>
                                    <p className="text-sm leading-relaxed text-[#939393]">{a.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 常见问题：单一大框卡 accordion，条目间分隔线 */}
                <section id="faq" className="scroll-mt-20 pb-16 md:pb-24">
                    <div className="mx-auto w-full max-w-[1328px] px-4 md:px-6">
                        <SectionTitle white="常见问题。" muted="你可能想了解这些。" />
                        <div className={PANEL + " mx-auto mt-12 max-w-4xl divide-y-2 divide-[#161616] overflow-hidden px-2"}>
                            {FAQS.map((f) => (
                                <details key={f.q} className="group px-5 [&_summary]:cursor-pointer">
                                    <summary className="flex min-h-[70px] list-none items-center justify-between gap-4 py-5 text-[17px] font-medium text-white">
                                        {f.q}
                                        <ChevronDown className="size-[18px] shrink-0 text-[#939393] transition-transform group-open:rotate-180" />
                                    </summary>
                                    <p className="max-w-[760px] pb-6 text-[15px] leading-relaxed text-[#cccccc]">{f.a}</p>
                                </details>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 页脚 CTA：与 hero 同款超大标题 + 命令框 + 药丸组 */}
                <section className="relative overflow-hidden border-t-2 border-[#161616] bg-[#080808] py-20 md:py-28">
                    <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(50%_60%_at_50%_100%,rgba(42,219,92,0.08),transparent)]" />
                    <div className="relative mx-auto flex w-full max-w-[1328px] flex-col items-center gap-8 px-4 text-center md:px-6">
                        <h2 className="max-w-2xl text-4xl font-semibold leading-[1.15] tracking-tight text-white md:text-[54px]">
                            今天就开始使用好会计
                        </h2>
                        <p className="max-w-xl text-lg text-[#939393]">
                            登录好会计，立即体验高效、准确、清晰的在线记账。
                        </p>
                        <div className="flex flex-wrap items-center justify-center gap-3">
                            <CommandPill command="kuaiji import 凭证.xls" />
                            <GradientPill large onClick={() => navigate(primaryHref)}>
                                {authed ? "进入控制台" : "登录使用"}
                                <ArrowRight className="size-4" />
                            </GradientPill>
                        </div>
                    </div>
                </section>
            </main>

            {/* 页脚：多栏导航 + 订阅药丸 + 状态指示 + 回到顶部 */}
            <footer className="border-t-2 border-[#161616] bg-[#0a0a0a]">
                <div className="mx-auto grid w-full max-w-[1328px] gap-10 px-4 py-14 md:grid-cols-[1.5fr_1fr_1fr_1.5fr] md:px-6">
                    <div className="flex flex-col gap-3">
                        <span className="flex items-center gap-2 font-semibold text-white">
                            <div className="flex size-7 items-center justify-center rounded-md bg-[#2adb5c] text-black">
                                <GalleryVerticalEnd className="size-4" />
                            </div>
                            好会计
                        </span>
                        <p className="max-w-sm text-sm leading-relaxed text-[#939393]">
                            专为中小企业打造的在线记账系统，让财务工作更简单、更规范。
                        </p>
                    </div>
                    <div className="flex flex-col gap-2.5 text-[15px]">
                        <span className="font-mono text-[11px] uppercase tracking-widest text-[#707070]">产品</span>
                        {NAV_LINKS.map((l) => (
                            <a key={l.href} href={l.href} className="w-fit text-[#999] transition-colors hover:text-white">
                                {l.label}
                            </a>
                        ))}
                    </div>
                    <div className="flex flex-col gap-2.5 text-[15px]">
                        <span className="font-mono text-[11px] uppercase tracking-widest text-[#707070]">开始使用</span>
                        <Link to={primaryHref} className="w-fit text-[#999] transition-colors hover:text-white">
                            {authed ? "进入控制台" : "登录"}
                        </Link>
                    </div>
                    <div className="flex flex-col gap-3">
                        <span className="font-mono text-[11px] uppercase tracking-widest text-[#707070]">订阅更新</span>
                        <form className="flex items-center gap-2" onSubmit={(e) => e.preventDefault()}>
                            <input
                                type="email"
                                placeholder="you@company.com"
                                className="h-9 w-full max-w-52 rounded-full border border-[#424242] bg-[#262626] px-4 text-xs text-[#f0f0f0] outline-none placeholder:text-[#707070] focus:border-[#5a5a5a]"
                            />
                            <button
                                type="submit"
                                className="h-9 shrink-0 rounded-full bg-[#262626] px-4 text-xs font-medium text-white transition-colors hover:bg-[#333]"
                            >
                                订阅
                            </button>
                        </form>
                        <p className="mt-1 flex items-center gap-2 font-mono text-[11px] uppercase tracking-widest text-[#939393]">
                            <span className="size-2 rounded-full bg-[#2adb5c]" />
                            系统运行正常
                        </p>
                    </div>
                </div>
                <div className="border-t-2 border-[#161616]">
                    <div className="mx-auto flex w-full max-w-[1328px] items-center justify-between px-4 py-6 font-mono text-xs text-[#707070] md:px-6">
                        <span>© {new Date().getFullYear()} 好会计. 保留所有权利.</span>
                        <a
                            href="#top"
                            onClick={(e) => {
                                e.preventDefault()
                                window.scrollTo({ top: 0, behavior: "smooth" })
                            }}
                            className="flex items-center gap-1.5 transition-colors hover:text-white"
                        >
                            回到顶部
                            <ArrowUp className="size-3.5" />
                        </a>
                    </div>
                </div>
            </footer>
        </div>
    )
}
