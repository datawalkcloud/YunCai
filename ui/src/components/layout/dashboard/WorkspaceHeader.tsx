import { useState, type ReactNode } from "react";
import { useLocation, useNavigate, useNavigation, useParams } from "react-router";
import {
    CheckOutlined,
    DownOutlined,
    LogoutOutlined,
    MenuOutlined,
    QuestionCircleOutlined,
} from "@ant-design/icons";
import {
    BanknotesIcon,
    BuildingOfficeIcon,
    CalendarDaysIcon,
    ChartPieIcon,
    ScaleIcon,
    TableCellsIcon,
} from "@heroicons/react/24/solid";
import { App, DatePicker, Dropdown, Tooltip } from "antd";
import dayjs from "dayjs";

import { useAuth } from "@/auth/AuthContext";
import { useAccountSets } from "@/accountSets/AccountSetsContext";
import { makePeriod, useDefaultAccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { AccountSetsApi } from "@/apis-gen";
import { apiConfig } from "@/lib/api-client";
import { cn } from "@/lib/utils";
import { ThemeMenu } from "@/components/theme/theme-menu";
import { CompanyInfoModal } from "@/pages/tax/CompanyInfoModal";
import { setTaxPeriod, useTaxPeriod } from "@/pages/tax/tax-period";
import { menuCategories, findCategoryOf } from "./menu-model";
import { tabKeyOf } from "./route-tabs-context";

/* ------------------------------------------------------------------ */
/*  工作区顶栏（停车平台风格）：品牌 + 记账/报税分类 + 快捷报表 + 主题 + 用户。 */
/* ------------------------------------------------------------------ */

/** 顶栏快捷报表入口：描边胶囊 + 圆圈图标 + 小字，与参考图右上角工具入口同款。 */
const QUICK_REPORTS = [
    { label: "科目余额表", to: "ledger/trial-balance", icon: <TableCellsIcon className="size-[9px]" /> },
    { label: "资产负债表", to: "reports/balance-sheet", icon: <ScaleIcon className="size-[9px]" /> },
    { label: "利润表", to: "reports/income-statement", icon: <ChartPieIcon className="size-[9px]" /> },
    { label: "现金流量表", to: "reports/cash-flow", icon: <BanknotesIcon className="size-[9px]" /> },
];

export function WorkspaceTopBar({ onOpenNav }: { onOpenNav: () => void }) {
    // 路由模块还在加载时导航不会提交，旧页面继续留在屏上；这一条细光带就是加载反馈。
    const loading = useNavigation().state === "loading";
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const location = useLocation();
    const tabKey = tabKeyOf(location.pathname, accountSetId);
    const category = findCategoryOf(tabKey);

    return (
        <header className="relative shrink-0 border-b border-(--pk-border) bg-(--pk-surface)">
            <div className="flex h-[52px] items-center gap-2 px-2 md:gap-3 md:px-4">
                <button
                    type="button"
                    onClick={onOpenNav}
                    aria-label="打开菜单"
                    className="flex size-9 shrink-0 cursor-pointer items-center justify-center text-[17px] text-(--pk-text-2) hover:text-(--pk-brand-text) lg:hidden"
                >
                    <MenuOutlined />
                </button>
                <button
                    type="button"
                    onClick={() => navigate(`/${accountSetId}`)}
                    className="flex shrink-0 cursor-pointer items-center gap-2.5"
                >
                    <Logo />
                    <span className="text-[17px] font-bold tracking-wide">云财</span>
                    <span className="hidden h-6 items-center rounded-full bg-(--pk-brand-soft) px-3 text-[12px] text-(--pk-brand-text) xl:flex">
                        智能财务平台
                    </span>
                </button>
                <div className="ml-1 hidden shrink-0 items-center gap-4 lg:flex">
                    {menuCategories.map((item) => {
                        const active = item.label === category.label;
                        return (
                            <button
                                key={item.label}
                                type="button"
                                onClick={() => navigate(item.to ? `/${accountSetId}/${item.to}` : `/${accountSetId}`)}
                                className={
                                    "cursor-pointer text-[15px] whitespace-nowrap " +
                                    (active
                                        ? "font-semibold text-(--pk-brand-text)"
                                        : "text-(--pk-text-2) hover:text-(--pk-brand-text)")
                                }
                            >
                                {item.label}
                            </button>
                        );
                    })}
                </div>
                <div className="min-w-0 flex-1" />
                {category.to ? <TaxPeriodPicker /> : <PeriodLabel />}
                <div className="hidden shrink-0 items-center gap-3 xl:flex">
                    {QUICK_REPORTS.map((report) => (
                        <button
                            key={report.label}
                            type="button"
                            onClick={() => navigate(`/${accountSetId}/${report.to}`)}
                            className="flex h-6 items-center gap-1.5 rounded-full border border-(--pk-border) px-3 text-[12px] whitespace-nowrap text-(--pk-text) hover:border-(--pk-brand-border) hover:text-(--pk-brand-text)"
                        >
                            <span className="flex size-4 items-center justify-center rounded-full border border-current">
                                {report.icon}
                            </span>
                            {report.label}
                        </button>
                    ))}
                </div>
                <ThemeMenu />
                <UserMenu />
            </div>
            {loading && (
                <div className="pointer-events-none absolute inset-x-0 bottom-0 z-10 h-0.5 overflow-hidden">
                    <div className="kj-route-progress h-full w-1/3 bg-(--pk-brand-text)" />
                </div>
            )}
        </header>
    );
}

function Logo() {
    return (
        <span className="flex size-[34px] shrink-0 items-center justify-center rounded-[9px] bg-[#111318] ring-1 ring-black/5">
            <svg viewBox="0 0 32 32" className="size-[22px]" fill="none">
                <path
                    d="M16 3 27 9.5v13L16 29 5 22.5v-13z"
                    stroke="#fff"
                    strokeWidth="2.2"
                    strokeLinejoin="round"
                />
                <path
                    d="M11.5 16.5 14.8 19.8 20.8 11.8"
                    stroke="#fff"
                    strokeWidth="2.2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                />
            </svg>
        </span>
    );
}

/* ------------------------------------------------------------------ */
/*  当前会计期间                                                       */
/* ------------------------------------------------------------------ */

function PeriodLabel() {
    const period = useDefaultAccountingPeriod();
    return (
        <span className="flex h-6 shrink-0 items-center gap-1.5 rounded-full border border-(--pk-border) px-3 text-[12px] whitespace-nowrap text-(--pk-text-2)">
            <CalendarDaysIcon className="size-3.5" />
            {period.year}年{period.month}月
            <Tooltip title="账套当前会计期间：尚未结账的最新期间。">
                <QuestionCircleOutlined className="cursor-help text-[11px]" />
            </Tooltip>
        </span>
    );
}

/* ------------------------------------------------------------------ */
/*  税款所属期（报税分类）                                              */
/* ------------------------------------------------------------------ */

/** 报税分类的顶栏控件：税款所属期由报税各页共用，公司税务信息对应原工作台的「详情」。 */
function TaxPeriodPicker() {
    const period = useTaxPeriod();
    const { accountSetId = "" } = useParams();
    const { accountSets } = useAccountSets();
    const [detailOpen, setDetailOpen] = useState(false);

    return (
        <>
            <span className="flex h-6 shrink-0 items-center gap-1.5 rounded-full border border-(--pk-border) pr-2 pl-3 text-[12px] whitespace-nowrap text-(--pk-text-2)">
                <CalendarDaysIcon className="size-3.5" />
                <span className="hidden md:inline">税款所属期</span>
                <DatePicker
                    picker="month"
                    allowClear={false}
                    size="small"
                    variant="borderless"
                    className="w-[84px]"
                    value={dayjs(period.startDate)}
                    format="YYYY-MM"
                    suffixIcon={null}
                    onChange={(value) => {
                        if (value) setTaxPeriod(makePeriod(value.year(), value.month() + 1));
                    }}
                />
                <Tooltip title="默认按税费种认定里主税种的申报期限推算：按月申报取上月，按季申报取上一季度末月。认定可在【税费种管理】更新，也可以在这里直接改月份。">
                    <QuestionCircleOutlined className="hidden cursor-help text-[11px] md:inline" />
                </Tooltip>
            </span>
            <button
                type="button"
                onClick={() => setDetailOpen(true)}
                className="hidden h-6 shrink-0 cursor-pointer items-center rounded-full border border-(--pk-border) px-3 text-[12px] whitespace-nowrap text-(--pk-text) hover:border-(--pk-brand-border) hover:text-(--pk-brand-text) md:flex"
            >
                公司税务信息
            </button>
            <CompanyInfoModal
                open={detailOpen}
                accountSet={accountSets.find((set) => set.id === accountSetId)}
                onClose={() => setDetailOpen(false)}
            />
        </>
    );
}

/* ------------------------------------------------------------------ */
/*  用户                                                              */
/* ------------------------------------------------------------------ */

function UserMenu() {
    const { user, logout } = useAuth();
    const navigate = useNavigate();
    const name = user?.displayName || user?.username || "";

    function handleLogout() {
        logout();
        navigate("/login", { replace: true });
    }

    return (
        <Dropdown
            trigger={["click"]}
            placement="bottomRight"
            menu={{
                items: [{ key: "logout", icon: <LogoutOutlined />, label: "退出登录", onClick: handleLogout }],
            }}
        >
            <button type="button" className="flex shrink-0 cursor-pointer items-center gap-2">
                <span className="flex size-7 items-center justify-center rounded-full bg-(--pk-brand) text-[12px] font-medium text-white">
                    {name.slice(0, 1)}
                </span>
                <span className="hidden text-[13px] text-(--pk-text) md:inline">{name}</span>
            </button>
        </Dropdown>
    );
}

/* ------------------------------------------------------------------ */
/*  账套切换（侧栏底部）                                                */
/* ------------------------------------------------------------------ */

/**
 * 账套切换器：把选中的账套设为后端「当前账套」，刷新账套上下文（保证各页面「（当前）」标记不失真），
 * 并跳到该账套下的同一子页面。页面取数以路由第一段为准，因此即使设置当前失败也照常跳转，只弹错误提示。
 * collapsed 为侧栏图标栏形态，只留一个账套图标。
 * action 是同行右侧的附加工具（如侧栏收起按钮），没有账套时也要留着。
 */
export function AccountSetSwitcher({ collapsed = false, action }: { collapsed?: boolean; action?: ReactNode }) {
    const { accountSetId = "" } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const { token } = useAuth();
    const { accountSets, refresh } = useAccountSets();
    const { message } = App.useApp();
    const [busy, setBusy] = useState(false);

    if (accountSets.length === 0)
        return action ? (
            <div
                className={cn(
                    "flex shrink-0 items-center border-t border-(--pk-border) py-2",
                    collapsed ? "justify-center" : "px-4",
                )}
            >
                {action}
            </div>
        ) : null;
    const current = accountSets.find((set) => set.id === accountSetId);

    async function choose(id: string) {
        if (!id || id === accountSetId || busy) return;
        setBusy(true);
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdCurrentPost({ id });
            // 切换成功后刷新上下文，各页面「（当前）」标记随之更新。
            refresh();
        } catch (err) {
            message.error(err instanceof Error ? err.message : "设为当前账套失败。");
        } finally {
            setBusy(false);
        }
        const base = `/${accountSetId}`;
        const rest = location.pathname.startsWith(base) ? location.pathname.slice(base.length) : "";
        navigate(`/${id}${rest}`);
    }

    const switcher = (
        <button
            type="button"
            disabled={busy}
            aria-label="切换账套"
            title={collapsed ? undefined : current?.companyName}
            className={cn(
                "flex min-w-0 flex-1 cursor-pointer text-left text-[12px] text-(--pk-text-2) hover:text-(--pk-brand-text) disabled:cursor-wait disabled:opacity-60",
                collapsed ? "items-center justify-center" : "items-center gap-1.5",
            )}
        >
            <BuildingOfficeIcon className="size-3 shrink-0" />
            {!collapsed && <span className="min-w-0 flex-1 truncate">{current?.name ?? "选择账套"}</span>}
            {!collapsed && <DownOutlined className="text-[9px]" />}
        </button>
    );

    return (
        <div
            className={cn(
                "flex shrink-0 items-center border-t border-(--pk-border) py-2",
                collapsed ? "gap-1 px-2" : "gap-1 px-3",
            )}
        >
            <Dropdown
                trigger={["click"]}
                placement={collapsed ? "top" : "topLeft"}
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
                                    <CheckOutlined className="shrink-0 text-(--pk-brand-text)" />
                                )}
                            </span>
                        ),
                    })),
                    onClick: ({ key }) => void choose(key),
                }}
            >
                {/* 账套切换是脚注式控件：小字、无底色、无块状图标，和 46px 菜单行区分开。 */}
                {collapsed ? (
                    <Tooltip title={current?.name ?? "选择账套"} placement="right">
                        {switcher}
                    </Tooltip>
                ) : (
                    switcher
                )}
            </Dropdown>
            {action}
        </div>
    );
}
