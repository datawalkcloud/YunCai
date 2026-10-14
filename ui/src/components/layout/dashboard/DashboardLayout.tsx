import { useEffect, useState } from "react";
import { Link, Outlet, useLocation, useNavigate, useParams } from "react-router";
import {
    BookOpen,
    Building2,
    CalendarDays,
    Check,
    ChevronDown,
    ChevronRight,
    ChevronsUpDown,
    Construction,
    FileText,
    Home,
    LogOut,
    Receipt,
    Settings,
    TrendingUp,
    Users,
    Wallet,
} from "lucide-react";

import { useAuth } from "@/auth/AuthContext";
import { useAccountSets } from "@/accountSets/AccountSetsContext";
import { AccountSetsApi } from "@/apis-gen";
import { apiConfig } from "@/lib/api-client";
import { WipTag } from "@/pages/lemon/components/ui";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

/* ------------------------------------------------------------------ */
/*  菜单模型：to 为账套下的相对子路径（每项都有真实链接）。                */
/*  分组带 to 表示直接跳转项（无子菜单）；否则为手风琴分组；              */
/*  wip 子项属「建设中 · 未接入」分组（界面复刻、后端未接）。             */
/* ------------------------------------------------------------------ */

type MenuIconType = React.ComponentType<{ className?: string; strokeWidth?: number }>;
type MenuLeaf = { label: string; icon?: MenuIconType; expand?: boolean; wip?: boolean; to: string };
type MenuGroup = { label: string; icon: MenuIconType; to?: string; children?: MenuLeaf[] };

/*
 * 原菜单层级（暂时注释，已按柠檬云菜单层级重建，见下方 menuGroups）。
 *
 * const menuGroups: MenuGroup[] = [
 *     {
 *         label: "总账管理",
 *         icon: BookOpen,
 *         children: [
 *             { label: "凭证管理", icon: FileText, expand: true, to: "vouchers/manage" },
 *             { label: "账簿查询", icon: Search, expand: true, to: "ledger/trial-balance" },
 *             { label: "期末结转", icon: CalendarDays, expand: true, to: "closing" },
 *             { label: "财务报表", icon: BarChart3 },
 *         ],
 *     },
 *     {
 *         label: "应收应付",
 *         icon: ArrowLeftRight,
 *         children: [
 *             { label: "应收管理", icon: Wallet, expand: true },
 *             { label: "应付管理", icon: CreditCard, expand: true },
 *             { label: "客户管理", icon: Users, expand: true },
 *             { label: "供应商管理", icon: Building2 },
 *         ],
 *     },
 *     {
 *         label: "资金管理",
 *         icon: Wallet,
 *         children: [
 *             { label: "银行账户", icon: Landmark, expand: true },
 *             { label: "资金流水", icon: ArrowRightLeft, expand: true, to: "funds/cash-journal" },
 *             { label: "内部转账", icon: ArrowLeftRight, expand: true, to: "funds/transfer" },
 *             { label: "资金报表", icon: BarChart3, expand: true, to: "funds/report" },
 *             { label: "核对总账", icon: Scale, expand: true, to: "funds/reconciliation" },
 *             { label: "收支类别", icon: Tags, expand: true, to: "funds/entry-categories" },
 *             { label: "银企对账", icon: FileCheck },
 *             { label: "资金计划", icon: Calculator },
 *         ],
 *     },
 *     {
 *         label: "发票管理",
 *         icon: Receipt,
 *         children: [
 *             { label: "发票开具", icon: Receipt, expand: true },
 *             { label: "发票管理", icon: FileText },
 *             { label: "发票统计", icon: PieChart },
 *             { label: "资金计划", icon: Calculator },
 *         ],
 *     },
 *     {
 *         label: "报表中心",
 *         icon: FileSpreadsheet,
 *         children: [
 *             { label: "财务报表", icon: BarChart3, expand: true, to: "reports/balance-sheet" },
 *             { label: "经营分析", icon: TrendingUp, expand: true },
 *             { label: "税务报表", icon: FileText, expand: true },
 *         ],
 *     },
 *     {
 *         label: "系统设置",
 *         icon: Settings,
 *         children: [
 *             { label: "基础设置", icon: Settings, expand: true, to: "settings/accounts" },
 *             { label: "辅助核算", icon: Layers, expand: true, to: "settings/assist-items" },
 *             { label: "用户管理", icon: UserCog },
 *             { label: "权限管理", icon: ShieldCheck },
 *         ],
 *     },
 * ];
 */

/** 菜单层级：与柠檬云 J6 一致（首页 + 凭证/资金/发票/工资/资产/期末结转/账簿/报表/设置），末尾为「建设中 · 未接入」折叠分组。 */
const menuGroups: MenuGroup[] = [
    {
        label: "凭证",
        icon: BookOpen,
        children: [
            { label: "新增凭证", to: "vouchers/new" },
            { label: "查看凭证", to: "vouchers/manage" },
            { label: "会计电子档案", to: "vouchers/erecord" },
        ],
    },
    {
        label: "资金",
        icon: Wallet,
        children: [
            { label: "现金日记账", to: "funds/cash-journal" },
            { label: "银行日记账", to: "funds/deposit-journal" },
            { label: "内部转账", to: "funds/transfer" },
            { label: "票据管理", to: "funds/draft" },
            { label: "资金报表", to: "funds/report" },
            { label: "核对总账", to: "funds/reconciliation" },
            { label: "收支类别", to: "funds/entry-categories" },
            { label: "账户设置", to: "funds/account" },
        ],
    },
    {
        label: "发票",
        icon: Receipt,
        children: [
            { label: "销项发票", to: "invoices/sales" },
            { label: "进项发票", to: "invoices/purchase" },
            { label: "发票资金一览表", to: "invoices/summary" },
        ],
    },
    {
        label: "工资",
        icon: Users,
        children: [
            { label: "工资管理", to: "salary/manage" },
            { label: "员工信息", to: "salary/employee" },
            { label: "工资统计表", to: "salary/summary" },
            { label: "五险一金设置", to: "salary/insurance" },
        ],
    },
    {
        label: "资产",
        icon: Building2,
        children: [
            { label: "资产管理", to: "assets/manage" },
            { label: "折旧摊销明细表", to: "assets/depreciation" },
            { label: "资产汇总表", to: "assets/summary" },
            { label: "资产类别设置", to: "assets/category" },
            { label: "资产核对总账", to: "assets/check" },
        ],
    },
    { label: "期末结转", icon: CalendarDays, to: "closing" },
    {
        label: "账簿",
        icon: FileText,
        children: [
            { label: "明细账", to: "ledger/subsidiary" },
            { label: "总账", to: "ledger/general" },
            { label: "科目余额表", to: "ledger/trial-balance" },
            { label: "凭证汇总表", to: "ledger/voucher-summary" },
            { label: "序时账", to: "ledger/journal" },
            { label: "核算项目明细账", to: "ledger/assist-subsidiary" },
            { label: "核算项目余额表", to: "ledger/assist-balance" },
            { label: "核算项目组合表", to: "ledger/assist-combine" },
            { label: "多栏账", to: "ledger/multi-column" },
            { label: "账龄分析表", to: "ledger/ageing" },
        ],
    },
    {
        label: "报表",
        icon: TrendingUp,
        children: [
            { label: "资产负债表", to: "reports/balance-sheet" },
            { label: "利润表", to: "reports/income-statement" },
            { label: "利润表季报", to: "reports/income-quarter" },
            { label: "现金流量表", to: "reports/cash-flow" },
            { label: "现金流量表季报", to: "reports/cash-flow-quarter" },
            { label: "标准现金流量表", to: "reports/standard-cash-flow" },
            { label: "部门利润表", to: "reports/department-income" },
            { label: "项目利润表", to: "reports/project-income" },
            { label: "数据透视表", to: "reports/pivot" },
            { label: "费用明细表", to: "reports/expense" },
            { label: "所有者权益变动表", to: "reports/equity-change" },
            { label: "纳税统计表", to: "reports/tax" },
            { label: "财务概要信息", to: "reports/financial-summary" },
        ],
    },
    {
        label: "设置",
        icon: Settings,
        children: [
            { label: "账套", to: "settings/account-sets" },
            { label: "科目", to: "settings/accounts" },
            { label: "期初", to: "settings/opening-balances" },
            { label: "币别", to: "settings/currency" },
            { label: "凭证字", to: "settings/voucher-word" },
            { label: "辅助核算", to: "settings/assist-items" },
            { label: "凭证模板", to: "settings/voucher-template" },
            { label: "操作日志", to: "settings/log" },
            { label: "重新初始化", to: "settings/reinit" },
        ],
    },
    {
        label: "建设中 · 未接入",
        icon: Construction,
        children: [
            { label: "收票宝", to: "vouchers/spb", wip: true },
            { label: "银企互联", to: "funds/bank-link", wip: true },
            { label: "一键取票", to: "invoices/fetch", wip: true },
            { label: "费用单据", to: "invoices/expense", wip: true },
            { label: "发票风险分析", to: "invoices/risk", wip: true },
            { label: "一键登录税局", to: "tax/login", wip: true },
            { label: "权限设置", to: "settings/permission", wip: true },
            { label: "老板看账", to: "settings/boss", wip: true },
            { label: "备份恢复", to: "settings/backup", wip: true },
            { label: "关联云发票", to: "settings/eis-relation", wip: true },
            { label: "关联进销存", to: "settings/scm-relation", wip: true },
            { label: "旧账导入", to: "settings/import-old", wip: true },
            { label: "升级专业版", to: "pro", wip: true },
        ],
    },
];

/* ------------------------------------------------------------------ */
/*  布局                                                              */
/* ------------------------------------------------------------------ */

export default function DashboardLayout() {
    return (
        <div className="flex h-screen w-screen overflow-hidden bg-[#f4f6fb] font-sans text-slate-700 antialiased">
            <Sidebar />
            <div className="flex min-w-0 flex-1 flex-col">
                <TopBar />
                <main className="flex-1 overflow-y-auto px-6 py-5">
                    <Outlet />
                </main>
            </div>
        </div>
    );
}

/* ------------------------------------------------------------------ */
/*  侧边栏                                                            */
/* ------------------------------------------------------------------ */

function Sidebar() {
    const location = useLocation();
    const { accountSetId = "" } = useParams();
    const base = `/${accountSetId}`;

    // 当前路由所在分组：手风琴默认展开它
    const activeGroupLabel = menuGroups.find((g) =>
        g.children?.some((c) => location.pathname === `${base}/${c.to}`),
    )?.label;
    const [expanded, setExpanded] = useState<string | undefined>(activeGroupLabel);
    useEffect(() => {
        setExpanded(activeGroupLabel);
    }, [activeGroupLabel]);

    return (
        <aside className="hidden w-[232px] shrink-0 flex-col border-r border-slate-100 bg-white lg:flex">
            <div className="flex items-center gap-2.5 px-5 pt-5 pb-3">
                <Logo />
                <div className="leading-tight">
                    <div className="text-[17px] font-bold text-slate-800">云财</div>
                    <div className="text-[11px] text-slate-400">让企业财务更简单</div>
                </div>
            </div>
            <AccountSetSwitcher />
            <nav className="flex-1 overflow-y-auto px-3 pb-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                <HomeRow />
                {menuGroups.map((group) =>
                    group.to ? (
                        <DirectRow
                            key={group.label}
                            label={group.label}
                            icon={group.icon}
                            to={group.to}
                        />
                    ) : (
                        <div key={group.label} className="mt-0.5">
                            <GroupRow
                                group={group}
                                expanded={expanded === group.label}
                                onToggle={() =>
                                    setExpanded((e) => (e === group.label ? undefined : group.label))
                                }
                            />
                            {expanded === group.label && (
                                <div className="mt-0.5 space-y-0.5">
                                    {(group.children ?? []).map((child) => (
                                        <ChildRow key={child.label} item={child} />
                                    ))}
                                </div>
                            )}
                        </div>
                    ),
                )}
            </nav>
        </aside>
    );
}

/**
 * 账套切换器：把选中的账套设为后端「当前账套」，刷新账套上下文（保证各页面「（当前）」标记不失真），
 * 并跳到该账套下的同一子页面。页面取数以路由第一段为准，因此即使设置当前失败也照常跳转，但要内联提示错误。
 */
function AccountSetSwitcher() {
    const { accountSetId = "" } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const { token } = useAuth();
    const { accountSets, refresh } = useAccountSets();
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState<string | null>(null);

    if (accountSets.length === 0) return null;
    const current = accountSets.find((s) => s.id === accountSetId);

    async function choose(id: string) {
        if (!id || id === accountSetId || busy) return;
        setBusy(true);
        setError(null);
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdCurrentPost({ id });
            // 切换成功后刷新上下文，各页面「（当前）」标记随之更新。
            refresh();
        } catch (err) {
            setError(err instanceof Error ? err.message : "设为当前账套失败。");
        } finally {
            setBusy(false);
        }
        const base = `/${accountSetId}`;
        const rest = location.pathname.startsWith(base) ? location.pathname.slice(base.length) : "";
        navigate(`/${id}${rest}`);
    }

    return (
        <div className="px-3 pb-3">
            <DropdownMenu>
                <DropdownMenuTrigger asChild>
                    <button
                        type="button"
                        className="flex w-full items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-2 text-left transition-colors hover:bg-slate-50"
                    >
                        <Building2 className="size-[17px] shrink-0 text-[#2f6bff]" />
                        <span className="min-w-0 flex-1">
                            <span className="block truncate text-[13px] font-medium text-slate-800">
                                {current?.name ?? "选择账套"}
                            </span>
                            {current?.companyName && current.companyName !== current.name && (
                                <span className="block truncate text-[11px] text-slate-400">
                                    {current.companyName}
                                </span>
                            )}
                        </span>
                        <ChevronsUpDown className="size-3.5 shrink-0 text-slate-400" />
                    </button>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                    align="start"
                    className="w-(--radix-dropdown-menu-trigger-width) min-w-56"
                >
                    <DropdownMenuLabel className="text-[12px] font-normal text-slate-400">
                        切换账套
                    </DropdownMenuLabel>
                    {accountSets.map((set) => (
                        <DropdownMenuItem key={set.id} onClick={() => void choose(set.id)}>
                            <span className="min-w-0 flex-1">
                                <span className="block truncate text-[13px]">{set.name}</span>
                                {set.companyName && set.companyName !== set.name && (
                                    <span className="block truncate text-[11px] text-slate-400">
                                        {set.companyName}
                                    </span>
                                )}
                            </span>
                            {set.id === accountSetId && (
                                <Check className="size-4 shrink-0 text-[#2f6bff]" />
                            )}
                        </DropdownMenuItem>
                    ))}
                </DropdownMenuContent>
            </DropdownMenu>
            {error && (
                <p className="mt-1 px-1 text-[11px] leading-4 text-red-500">{error}</p>
            )}
        </div>
    );
}

/** 解析某个叶子项的目标链接与是否激活。 */
function useNavTarget(item: MenuLeaf) {
    const { accountSetId = "" } = useParams();
    const location = useLocation();
    const href = `/${accountSetId}/${item.to}`;
    return { href, active: location.pathname === href };
}

/** 首页：浅蓝胶囊 + 实心蓝色房子图标，右侧贴边蓝色高亮条。 */
function HomeRow() {
    const { accountSetId = "" } = useParams();
    const location = useLocation();
    const base = `/${accountSetId}`;
    const active = location.pathname === base;
    return (
        <Link
            to={base}
            className={
                "relative mb-1 flex items-center gap-3 rounded-xl px-3.5 py-2.5 text-[13px] font-semibold transition-colors " +
                (active ? "bg-[#e7f0ff] text-[#2f6bff]" : "text-slate-600 hover:bg-slate-50")
            }
        >
            <Home
                className="size-[18px] shrink-0"
                fill={active ? "currentColor" : "none"}
                strokeWidth={active ? 0 : 2}
            />
            <span>首页</span>
            {active && (
                <span className="absolute -right-3 top-1/2 h-5 w-[3px] -translate-y-1/2 rounded-l-full bg-[#2f6bff]" />
            )}
        </Link>
    );
}

/** 分组标题：手风琴开关，点击展开/收起子项。 */
function GroupRow({
    group,
    expanded,
    onToggle,
}: {
    group: MenuGroup;
    expanded: boolean;
    onToggle: () => void;
}) {
    const { accountSetId = "" } = useParams();
    const location = useLocation();
    const base = `/${accountSetId}`;
    const active = (group.children ?? []).some(
        (c) => location.pathname === `${base}/${c.to}`,
    );
    return (
        <button
            type="button"
            onClick={onToggle}
            className={
                "flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-[13px] transition-colors " +
                (active
                    ? "font-medium text-[#2f6bff]"
                    : "text-slate-700 hover:bg-slate-50")
            }
        >
            <MenuGlyph icon={group.icon} active={active} />
            <span className="flex-1 truncate">{group.label}</span>
            <ChevronDown
                className={
                    "size-3.5 shrink-0 text-slate-300 transition-transform " +
                    (expanded ? "rotate-180" : "")
                }
            />
        </button>
    );
}

/** 直达项（无子菜单，如期末结转）：样式同分组标题但可跳转。 */
function DirectRow({
    label,
    icon,
    to,
}: {
    label: string;
    icon: MenuIconType;
    to: string;
}) {
    const { href, active } = useNavTarget({ label, to });
    return (
        <Link
            to={href}
            className={
                "mt-0.5 flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-[13px] transition-colors " +
                (active
                    ? "bg-[#e7f0ff] font-medium text-[#2f6bff]"
                    : "text-slate-700 hover:bg-slate-50")
            }
        >
            <MenuGlyph icon={icon} active={active} />
            <span className="flex-1 truncate">{label}</span>
        </Link>
    );
}

/** 子项：可跳转；「建设中」项挂小标。子项不带图标时以小圆点占位。 */
function ChildRow({ item }: { item: MenuLeaf }) {
    const { href, active } = useNavTarget(item);
    return (
        <Link
            to={href}
            className={
                "flex items-center gap-2.5 rounded-lg py-[7px] pl-[30px] pr-2.5 text-[13px] transition-colors " +
                (active
                    ? "bg-[#e7f0ff] font-medium text-[#2f6bff]"
                    : "text-slate-500 hover:bg-slate-50 hover:text-slate-800")
            }
        >
            {item.icon ? (
                <MenuGlyph icon={item.icon} active={active} />
            ) : (
                <span className="flex size-[17px] shrink-0 items-center justify-center">
                    <span className="size-1 rounded-full bg-slate-300" />
                </span>
            )}
            <span className="flex-1 truncate">{item.label}</span>
            {item.wip && <WipTag className="shrink-0" />}
            {item.expand && <ChevronRight className="size-3.5 shrink-0 text-slate-300" />}
        </Link>
    );
}

/** 菜单图标：直接使用 lucide 线性图标。 */
function MenuGlyph({ icon: Icon, active }: { icon: MenuIconType; active?: boolean }) {
    return (
        <Icon
            className={"size-[17px] shrink-0 " + (active ? "text-[#2f6bff]" : "text-slate-500")}
            strokeWidth={2}
        />
    );
}

function Logo() {
    return (
        <svg viewBox="0 0 32 32" className="size-9 shrink-0" fill="none">
            <defs>
                <linearGradient id="kuaijiLogo" x1="4" y1="2" x2="28" y2="30" gradientUnits="userSpaceOnUse">
                    <stop stopColor="#3b82f6" />
                    <stop offset="1" stopColor="#22c55e" />
                </linearGradient>
            </defs>
            <path d="M16 2 28 9v14l-12 7L4 23V9z" fill="url(#kuaijiLogo)" />
            <path
                d="M10.5 16.5 14.5 20.5 22 11.5"
                stroke="#fff"
                strokeWidth="2.6"
                strokeLinecap="round"
                strokeLinejoin="round"
            />
        </svg>
    );
}

/* ------------------------------------------------------------------ */
/*  顶栏                                                              */
/* ------------------------------------------------------------------ */

function TopBar() {
    const { user, logout } = useAuth();
    const navigate = useNavigate();
    const name = user?.displayName || user?.username || "";
    const initial = name.slice(0, 1);

    function handleLogout() {
        logout();
        navigate("/login", { replace: true });
    }

    return (
        <header className="flex h-16 shrink-0 items-center justify-end border-b border-slate-100 bg-white px-6">
            <DropdownMenu>
                <DropdownMenuTrigger asChild>
                    <button
                        type="button"
                        className="flex items-center gap-2 rounded-full py-1 pl-1 pr-2 transition-colors hover:bg-slate-50"
                    >
                        <span className="flex size-8 items-center justify-center rounded-full bg-gradient-to-br from-[#5b9bff] to-[#2f6bff] text-[13px] font-medium text-white">
                            {initial}
                        </span>
                        <span className="text-[13px] font-medium text-slate-700">{name}</span>
                        <ChevronDown className="size-4 text-slate-400" />
                    </button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="min-w-40">
                    <DropdownMenuItem onClick={handleLogout}>
                        <LogOut className="size-4" />
                        退出登录
                    </DropdownMenuItem>
                </DropdownMenuContent>
            </DropdownMenu>
        </header>
    );
}
