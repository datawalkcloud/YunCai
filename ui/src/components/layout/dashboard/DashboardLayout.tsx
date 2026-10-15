import { useEffect, useMemo, useState } from "react";
import { Outlet, useLocation, useNavigate, useParams } from "react-router";
import {
    BankOutlined,
    BookOutlined,
    CalendarOutlined,
    CheckOutlined,
    DownOutlined,
    FileDoneOutlined,
    FileTextOutlined,
    FundOutlined,
    HomeOutlined,
    LogoutOutlined,
    SettingOutlined,
    TeamOutlined,
    ToolOutlined,
    WalletOutlined,
} from "@ant-design/icons";
import { Avatar, Dropdown, Menu } from "antd";
import type { MenuProps } from "antd";

import { useAuth } from "@/auth/AuthContext";
import { useAccountSets } from "@/accountSets/AccountSetsContext";
import { AccountSetsApi } from "@/apis-gen";
import { apiConfig } from "@/lib/api-client";
import { WipTag } from "@/pages/lemon/components/ui";
import { ICON_STYLE } from "@/components/page/styles";

/* ------------------------------------------------------------------ */
/*  菜单模型：to 为账套下的相对子路径（每项都有真实链接）。                */
/*  带 children 的是折叠分组；无 children 的是直达项（如期末结转）。        */
/*  wip 子项属「建设中 · 未接入」分组（界面复刻、后端未接）。               */
/* ------------------------------------------------------------------ */

type MenuLeaf = { label: string; wip?: boolean; to: string };
type MenuGroup = { label: string; icon: React.ReactNode; to?: string; children?: MenuLeaf[] };

/** 菜单层级：与柠檬云 J6 一致（首页 + 凭证/资金/发票/工资/资产/期末结转/账簿/报表/设置），末尾为「建设中 · 未接入」折叠分组。 */
const menuGroups: MenuGroup[] = [
    {
        label: "凭证",
        icon: <BookOutlined />,
        children: [
            { label: "新增凭证", to: "vouchers/new" },
            { label: "查看凭证", to: "vouchers/manage" },
            { label: "会计电子档案", to: "vouchers/erecord" },
        ],
    },
    {
        label: "资金",
        icon: <WalletOutlined />,
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
        icon: <FileDoneOutlined />,
        children: [
            { label: "销项发票", to: "invoices/sales" },
            { label: "进项发票", to: "invoices/purchase" },
            { label: "发票资金一览表", to: "invoices/summary" },
        ],
    },
    {
        label: "工资",
        icon: <TeamOutlined />,
        children: [
            { label: "工资管理", to: "salary/manage" },
            { label: "员工信息", to: "salary/employee" },
            { label: "工资统计表", to: "salary/summary" },
            { label: "五险一金设置", to: "salary/insurance" },
        ],
    },
    {
        label: "资产",
        icon: <BankOutlined />,
        children: [
            { label: "资产管理", to: "assets/manage" },
            { label: "折旧摊销明细表", to: "assets/depreciation" },
            { label: "资产汇总表", to: "assets/summary" },
            { label: "资产类别设置", to: "assets/category" },
            { label: "资产核对总账", to: "assets/check" },
        ],
    },
    { label: "期末结转", icon: <CalendarOutlined />, to: "closing" },
    {
        label: "账簿",
        icon: <FileTextOutlined />,
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
        icon: <FundOutlined />,
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
        icon: <SettingOutlined />,
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
        icon: <ToolOutlined />,
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
    const navigate = useNavigate();
    const { accountSetId = "" } = useParams();
    const base = `/${accountSetId}`;

    /** 当前路由所在分组：手风琴只展开它，与菜单默认行为一致。 */
    const activeGroupLabel = menuGroups.find((g) =>
        g.children?.some((c) => location.pathname === `${base}/${c.to}`),
    )?.label;
    const [openKeys, setOpenKeys] = useState<string[]>(activeGroupLabel ? [activeGroupLabel] : []);
    useEffect(() => {
        setOpenKeys(activeGroupLabel ? [activeGroupLabel] : []);
    }, [activeGroupLabel]);

    const items = useMemo<MenuProps["items"]>(
        () => [
            { key: base, icon: <HomeOutlined />, label: "首页" },
            ...menuGroups.map((group) =>
                group.to
                    ? { key: `${base}/${group.to}`, icon: group.icon, label: group.label }
                    : {
                          key: group.label,
                          icon: group.icon,
                          label: group.label,
                          children: (group.children ?? []).map((child) => ({
                              key: `${base}/${child.to}`,
                              label: child.wip ? (
                                  <span className="flex items-center justify-between gap-2">
                                      {child.label}
                                      <WipTag className="shrink-0" />
                                  </span>
                              ) : (
                                  child.label
                              ),
                          })),
                      },
            ),
        ],
        [base],
    );

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
            <nav className="flex-1 overflow-y-auto pb-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                <Menu
                    mode="inline"
                    items={items}
                    openKeys={openKeys}
                    onOpenChange={(keys) => {
                        // Menu 没有 accordion 属性，这里自己实现：只保留本次新展开的分组。
                        const list = keys as string[];
                        const opened = list.filter((k) => !openKeys.includes(k));
                        setOpenKeys(opened.length > 0 ? [opened[opened.length - 1]] : list);
                    }}
                    selectedKeys={[location.pathname]}
                    onClick={({ key }) => {
                        if (key.startsWith("/")) navigate(key);
                    }}
                    style={{ borderInlineEnd: "none" }}
                />
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
            <Dropdown
                trigger={["click"]}
                placement="bottomLeft"
                menu={{
                    style: { minWidth: 224 },
                    selectable: false,
                    items: accountSets.map((set) => ({
                        key: set.id,
                        label: (
                            <span className="flex items-start justify-between gap-2">
                                <span className="min-w-0">
                                    <span className="block truncate text-[13px]">{set.name}</span>
                                    {set.companyName && set.companyName !== set.name && (
                                        <span className="block truncate text-[11px] text-slate-400">
                                            {set.companyName}
                                        </span>
                                    )}
                                </span>
                                {set.id === accountSetId && (
                                    <CheckOutlined className="shrink-0" style={ICON_STYLE.brand} />
                                )}
                            </span>
                        ),
                    })),
                    onClick: ({ key }) => void choose(key),
                }}
            >
                <button
                    type="button"
                    className="flex w-full items-center gap-2 rounded-xl border border-slate-200 bg-white px-3 py-2 text-left transition-colors hover:bg-slate-50"
                >
                    <BankOutlined className="shrink-0 text-[17px]" style={ICON_STYLE.brand} />
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
                    <DownOutlined className="shrink-0 text-[11px]" style={ICON_STYLE.muted} />
                </button>
            </Dropdown>
            {error && <p className="mt-1 px-1 text-[11px] leading-4 text-red-500">{error}</p>}
        </div>
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

    function handleLogout() {
        logout();
        navigate("/login", { replace: true });
    }

    return (
        <header className="flex h-16 shrink-0 items-center justify-end border-b border-slate-100 bg-white px-6">
            <Dropdown
                trigger={["click"]}
                placement="bottomRight"
                menu={{ items: [{ key: "logout", icon: <LogoutOutlined />, label: "退出登录", onClick: handleLogout }] }}
            >
                <button
                    type="button"
                    className="flex items-center gap-2 rounded-full py-1 pl-1 pr-2 transition-colors hover:bg-slate-50"
                >
                    <Avatar
                        size={32}
                        style={{ background: "linear-gradient(135deg,#5b9bff,#2f6bff)", fontWeight: 500 }}
                    >
                        {name.slice(0, 1)}
                    </Avatar>
                    <span className="text-[13px] font-medium text-slate-700">{name}</span>
                    <DownOutlined className="text-[11px]" style={ICON_STYLE.muted} />
                </button>
            </Dropdown>
        </header>
    );
}
