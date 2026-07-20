import { Link } from "react-router"
import {
    ArrowRight,
    BarChart3,
    BookOpen,
    CheckCircle2,
    Cloud,
    FileText,
    GalleryVerticalEnd,
    LayoutDashboard,
    ListChecks,
    Lock,
    ScaleIcon,
    ShieldCheck,
    Sparkles,
    Table2,
    Upload,
    Wallet,
    Zap,
} from "lucide-react"

import { Button } from "@/components/ui/button"
import { ModeToggle } from "@/components/theme/mode-toggle"
import { useAuth } from "@/auth/AuthContext"
import heroImg from "@/assets/hero.png"

/** 顶部导航锚点。 */
const NAV_LINKS = [
    { label: "功能", href: "#features" },
    { label: "亮点", href: "#highlights" },
    { label: "流程", href: "#workflow" },
    { label: "常见问题", href: "#faq" },
]

/** 数据指标带。 */
const STATS = [
    { value: "100+", label: "预置标准科目" },
    { value: "4", label: "核心财务报表" },
    { value: "实时", label: "数据归集出表" },
    { value: "多账套", label: "一号管理多公司" },
]

/** 核心功能亮点。 */
const FEATURES = [
    {
        icon: FileText,
        title: "凭证管理",
        desc: "录入、编辑、审核记账凭证，自动校验借贷平衡，支持金额大写。",
    },
    {
        icon: Upload,
        title: "凭证导入",
        desc: "从 Excel 一键批量导入历史凭证，快速完成建账初始化。",
    },
    {
        icon: Table2,
        title: "科目余额表",
        desc: "按期间实时汇总各科目发生额与余额，随时掌握账务全貌。",
    },
    {
        icon: ScaleIcon,
        title: "资产负债表",
        desc: "遵循会企 01 表标准行次，从凭证实时归集，报表随点随出。",
    },
    {
        icon: BookOpen,
        title: "现金日记账",
        desc: "自动从库存现金凭证派生序时账，逐笔余额一目了然。",
    },
    {
        icon: LayoutDashboard,
        title: "多账套管理",
        desc: "一个账号管理多家公司账套，随时切换互不干扰。",
    },
]

/** 图文交错的功能详解块。 */
const HIGHLIGHTS = [
    {
        icon: ListChecks,
        tag: "凭证全流程",
        title: "从录入到审核，一气呵成",
        desc: "结构化的分录录入界面，实时校验借贷平衡与金额大写，避免手工核算出错。历史凭证可从 Excel 批量导入，建账不再是负担。",
        points: ["借贷自动平衡校验", "金额大写自动生成", "Excel 批量导入历史凭证"],
    },
    {
        icon: BarChart3,
        tag: "报表实时生成",
        title: "报表随点随出，无需等待",
        desc: "科目余额表、资产负债表、现金日记账全部从凭证实时归集计算，按会计准则标准行次呈现，任意期间随时查看。",
        points: ["会企 01 表标准行次", "任意期间即时切换", "资产负债恒等式自动平衡"],
    },
    {
        icon: ShieldCheck,
        tag: "安全与协作",
        title: "多账套隔离，数据更安心",
        desc: "一个账号即可管理多家公司账套，数据彼此隔离；登录鉴权保护每一次访问，账务数据只有你能看见。",
        points: ["多账套数据隔离", "登录令牌鉴权", "随时切换互不干扰"],
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

/** 公开的官网首页：右上角提供登录入口，已登录用户显示进入控制台。 */
export default function LandingPage() {
    const { status } = useAuth()
    const authed = status === "authenticated"
    const primaryHref = authed ? "/app" : "/login"
    const primaryLabel = authed ? "进入控制台" : "立即登录"

    return (
        <div className="flex min-h-svh flex-col bg-background">
            {/* 顶部导航：品牌 + 锚点 + 右上角登录入口 */}
            <header className="sticky top-0 z-20 border-b bg-background/80 backdrop-blur">
                <div className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between px-4 md:px-6">
                    <Link to="/" className="flex items-center gap-2 font-semibold">
                        <div className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                            <GalleryVerticalEnd className="size-4" />
                        </div>
                        <span className="text-lg">好会计</span>
                    </Link>
                    <nav className="hidden items-center gap-1 md:flex">
                        {NAV_LINKS.map((l) => (
                            <a
                                key={l.href}
                                href={l.href}
                                className="rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
                            >
                                {l.label}
                            </a>
                        ))}
                    </nav>
                    <div className="flex items-center gap-2">
                        <ModeToggle />
                        {authed ? (
                            <Button asChild>
                                <Link to="/app">
                                    进入控制台
                                    <ArrowRight />
                                </Link>
                            </Button>
                        ) : (
                            <Button asChild>
                                <Link to="/login">登录</Link>
                            </Button>
                        )}
                    </div>
                </div>
            </header>

            <main className="flex-1">
                {/* 主视觉区 */}
                <section className="relative overflow-hidden">
                    <div className="pointer-events-none absolute inset-0 -z-10 bg-[radial-gradient(60%_50%_at_50%_0%,theme(colors.primary/10),transparent)]" />
                    <div className="mx-auto grid w-full max-w-6xl items-center gap-10 px-4 py-16 md:px-6 md:py-24 lg:grid-cols-2">
                        <div className="flex flex-col gap-6">
                            <span className="inline-flex w-fit items-center gap-2 rounded-full border px-3 py-1 text-sm text-muted-foreground">
                                <Sparkles className="size-4 text-primary" />
                                专为中小企业打造的在线记账系统
                            </span>
                            <h1 className="text-4xl font-bold tracking-tight md:text-5xl">
                                让记账更简单，
                                <br className="hidden md:block" />
                                让账务一目了然
                            </h1>
                            <p className="max-w-md text-lg text-muted-foreground">
                                从凭证录入到财务报表，好会计帮你打通全流程。数据实时归集，报表随点随出，告别繁琐的手工核算。
                            </p>
                            <div className="flex flex-wrap items-center gap-3">
                                <Button asChild size="lg">
                                    <Link to={primaryHref}>
                                        {primaryLabel}
                                        <ArrowRight />
                                    </Link>
                                </Button>
                                <Button asChild size="lg" variant="outline">
                                    <a href="#features">了解功能</a>
                                </Button>
                            </div>
                            <ul className="flex flex-wrap gap-x-5 gap-y-2 text-sm text-muted-foreground">
                                {["无需安装", "数据实时归集", "支持多账套"].map((t) => (
                                    <li key={t} className="flex items-center gap-1.5">
                                        <CheckCircle2 className="size-4 text-primary" />
                                        {t}
                                    </li>
                                ))}
                            </ul>
                        </div>
                        <div className="relative">
                            <div className="overflow-hidden rounded-xl border shadow-sm">
                                <img
                                    src={heroImg}
                                    alt="好会计产品预览"
                                    className="h-full w-full object-cover dark:brightness-90"
                                />
                            </div>
                        </div>
                    </div>
                </section>

                {/* 数据指标带 */}
                <section className="border-y bg-muted/30">
                    <div className="mx-auto grid w-full max-w-6xl grid-cols-2 gap-6 px-4 py-10 md:grid-cols-4 md:px-6">
                        {STATS.map((s) => (
                            <div key={s.label} className="flex flex-col items-center gap-1 text-center">
                                <span className="text-3xl font-bold text-primary">{s.value}</span>
                                <span className="text-sm text-muted-foreground">{s.label}</span>
                            </div>
                        ))}
                    </div>
                </section>

                {/* 功能亮点 */}
                <section id="features" className="scroll-mt-16">
                    <div className="mx-auto w-full max-w-6xl px-4 py-16 md:px-6 md:py-20">
                        <div className="flex flex-col items-center gap-3 text-center">
                            <h2 className="text-3xl font-bold tracking-tight">核心功能</h2>
                            <p className="max-w-xl text-muted-foreground">
                                覆盖日常财务工作的关键环节，让每一笔账都清清楚楚。
                            </p>
                        </div>
                        <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                            {FEATURES.map((f) => (
                                <div
                                    key={f.title}
                                    className="flex flex-col gap-3 rounded-xl border bg-background p-6 transition-shadow hover:shadow-md"
                                >
                                    <div className="flex size-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
                                        <f.icon className="size-5" />
                                    </div>
                                    <h3 className="text-lg font-semibold">{f.title}</h3>
                                    <p className="text-sm text-muted-foreground">{f.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 功能详解（图文交错） */}
                <section id="highlights" className="scroll-mt-16 border-t bg-muted/30">
                    <div className="mx-auto flex w-full max-w-6xl flex-col gap-16 px-4 py-16 md:px-6 md:py-20">
                        {HIGHLIGHTS.map((h, i) => (
                            <div
                                key={h.tag}
                                className="grid items-center gap-8 lg:grid-cols-2"
                            >
                                <div className={i % 2 === 1 ? "lg:order-2" : ""}>
                                    <span className="text-sm font-medium text-primary">{h.tag}</span>
                                    <h3 className="mt-2 text-2xl font-bold tracking-tight">{h.title}</h3>
                                    <p className="mt-3 text-muted-foreground">{h.desc}</p>
                                    <ul className="mt-5 flex flex-col gap-2">
                                        {h.points.map((p) => (
                                            <li key={p} className="flex items-center gap-2 text-sm">
                                                <CheckCircle2 className="size-4 shrink-0 text-primary" />
                                                {p}
                                            </li>
                                        ))}
                                    </ul>
                                </div>
                                <div className={i % 2 === 1 ? "lg:order-1" : ""}>
                                    <div className="flex aspect-[4/3] items-center justify-center rounded-xl border bg-gradient-to-br from-primary/10 to-background">
                                        <h.icon className="size-20 text-primary/70" strokeWidth={1.2} />
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                </section>

                {/* 使用流程 */}
                <section id="workflow" className="scroll-mt-16">
                    <div className="mx-auto w-full max-w-6xl px-4 py-16 md:px-6 md:py-20">
                        <div className="flex flex-col items-center gap-3 text-center">
                            <h2 className="text-3xl font-bold tracking-tight">四步开始记账</h2>
                            <p className="max-w-xl text-muted-foreground">
                                简单几步，从建账到出表，快速上手无门槛。
                            </p>
                        </div>
                        <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
                            {STEPS.map((s, i) => (
                                <div key={s.title} className="relative flex flex-col gap-3 rounded-xl border bg-background p-6">
                                    <span className="absolute right-4 top-4 text-3xl font-bold text-muted/40">
                                        {String(i + 1).padStart(2, "0")}
                                    </span>
                                    <div className="flex size-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
                                        <s.icon className="size-5" />
                                    </div>
                                    <h3 className="text-lg font-semibold">{s.title}</h3>
                                    <p className="text-sm text-muted-foreground">{s.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 产品优势 */}
                <section className="border-t bg-muted/30">
                    <div className="mx-auto w-full max-w-6xl px-4 py-16 md:px-6 md:py-20">
                        <div className="flex flex-col items-center gap-3 text-center">
                            <h2 className="text-3xl font-bold tracking-tight">为什么选择好会计</h2>
                            <p className="max-w-xl text-muted-foreground">
                                把专业留给系统，把简单交给你。
                            </p>
                        </div>
                        <div className="mt-12 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
                            {ADVANTAGES.map((a) => (
                                <div key={a.title} className="flex flex-col gap-3 rounded-xl border bg-background p-6">
                                    <div className="flex size-11 items-center justify-center rounded-lg bg-primary/10 text-primary">
                                        <a.icon className="size-5" />
                                    </div>
                                    <h3 className="text-lg font-semibold">{a.title}</h3>
                                    <p className="text-sm text-muted-foreground">{a.desc}</p>
                                </div>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 常见问题 */}
                <section id="faq" className="scroll-mt-16">
                    <div className="mx-auto w-full max-w-3xl px-4 py-16 md:px-6 md:py-20">
                        <div className="flex flex-col items-center gap-3 text-center">
                            <h2 className="text-3xl font-bold tracking-tight">常见问题</h2>
                            <p className="max-w-xl text-muted-foreground">关于好会计，你可能想了解这些。</p>
                        </div>
                        <div className="mt-10 flex flex-col gap-3">
                            {FAQS.map((f) => (
                                <details
                                    key={f.q}
                                    className="group rounded-xl border bg-background p-5 [&_summary]:cursor-pointer"
                                >
                                    <summary className="flex list-none items-center justify-between gap-2 font-medium">
                                        {f.q}
                                        <ArrowRight className="size-4 shrink-0 text-muted-foreground transition-transform group-open:rotate-90" />
                                    </summary>
                                    <p className="mt-3 text-sm text-muted-foreground">{f.a}</p>
                                </details>
                            ))}
                        </div>
                    </div>
                </section>

                {/* 行动号召 */}
                <section className="mx-auto w-full max-w-6xl px-4 pb-16 md:px-6 md:pb-20">
                    <div className="flex flex-col items-center gap-6 rounded-2xl border bg-primary/5 px-6 py-12 text-center">
                        <h2 className="text-3xl font-bold tracking-tight">准备好开始了吗？</h2>
                        <p className="max-w-xl text-muted-foreground">
                            登录好会计，立即体验高效、准确、清晰的在线记账。
                        </p>
                        <Button asChild size="lg">
                            <Link to={primaryHref}>
                                {authed ? "进入控制台" : "登录使用"}
                                <ArrowRight />
                            </Link>
                        </Button>
                    </div>
                </section>
            </main>

            {/* 页脚 */}
            <footer className="border-t">
                <div className="mx-auto grid w-full max-w-6xl gap-8 px-4 py-12 md:grid-cols-4 md:px-6">
                    <div className="flex flex-col gap-3 md:col-span-2">
                        <span className="flex items-center gap-2 font-semibold">
                            <div className="flex size-7 items-center justify-center rounded-md bg-primary text-primary-foreground">
                                <GalleryVerticalEnd className="size-4" />
                            </div>
                            好会计
                        </span>
                        <p className="max-w-sm text-sm text-muted-foreground">
                            专为中小企业打造的在线记账系统，让财务工作更简单、更规范。
                        </p>
                    </div>
                    <div className="flex flex-col gap-2 text-sm">
                        <span className="font-medium">产品</span>
                        {NAV_LINKS.map((l) => (
                            <a key={l.href} href={l.href} className="text-muted-foreground hover:text-foreground">
                                {l.label}
                            </a>
                        ))}
                    </div>
                    <div className="flex flex-col gap-2 text-sm">
                        <span className="font-medium">开始使用</span>
                        <Link to={primaryHref} className="text-muted-foreground hover:text-foreground">
                            {authed ? "进入控制台" : "登录"}
                        </Link>
                    </div>
                </div>
                <div className="border-t">
                    <div className="mx-auto w-full max-w-6xl px-4 py-6 text-center text-sm text-muted-foreground md:px-6">
                        © {new Date().getFullYear()} 好会计. 保留所有权利.
                    </div>
                </div>
            </footer>
        </div>
    )
}
