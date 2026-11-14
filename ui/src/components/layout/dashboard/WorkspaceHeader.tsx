import { useLayoutEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate, useNavigation, useParams } from "react-router";
import {
    CheckOutlined,
    DownOutlined,
    KeyOutlined,
    LogoutOutlined,
    MenuOutlined,
    QuestionCircleOutlined,
    TeamOutlined,
} from "@ant-design/icons";
import {
    BuildingOfficeIcon,
    CalendarDaysIcon,
} from "@heroicons/react/24/solid";
import { App, DatePicker, Dropdown, Tooltip } from "antd";
import dayjs from "dayjs";

import { useAuth } from "@/auth/AuthContext";
import { useAccountSets } from "@/accountSets/AccountSetsContext";
import { usePermissions } from "@/accountSets/PermissionsContext";
import { makePeriod, useDefaultAccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { AccountSetsApi } from "@/apis-gen";
import { apiConfig } from "@/lib/api-client";
import { cn } from "@/lib/utils";
import { BrandLogo } from "@/components/brand/BrandLogo";
import { ChangePasswordModal } from "@/components/auth/ChangePasswordModal";
import { ThemeMenu } from "@/components/theme/theme-menu";
import { CompanyInfoModal } from "@/pages/tax/CompanyInfoModal";
import { setTaxPeriod, useTaxPeriod } from "@/pages/tax/tax-period";
import { filterGroupsByPermission, menuCategories, findCategoryOf } from "./menu-model";
import { tabKeyOf } from "./route-tabs-context";

/* ------------------------------------------------------------------ */
/*  工作区顶栏（停车平台风格）：品牌 + 记账/报税分类 + 期间 + 主题 + 用户。 */
/* ------------------------------------------------------------------ */

export function WorkspaceTopBar({
    onOpenNav,
    sidebarCollapsed = false,
}: {
    onOpenNav: () => void;
    sidebarCollapsed?: boolean;
}) {
    // 路由模块还在加载时导航不会提交，旧页面继续留在屏上；这一条细光带就是加载反馈。
    const loading = useNavigation().state === "loading";
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const location = useLocation();
    const tabKey = tabKeyOf(location.pathname, accountSetId);
    const category = findCategoryOf(tabKey);
    const { has, loading: permissionsLoading } = usePermissions();
    // 没有可用菜单的分类（如未开通税务权限的「报税」）不显示；加载中先按全集渲染，避免闪一下。
    const categories = useMemo(
        () =>
            permissionsLoading
                ? menuCategories
                : menuCategories.filter(
                      (item) => item.home === true || filterGroupsByPermission(item.groups, has).length > 0,
                  ),
        [permissionsLoading, has],
    );

    // 选中指示条是菜单容器里的一个共享元素，随选中项左右滑动（方案 B）。
    const itemRefs = useRef(new Map<string, HTMLButtonElement | null>());
    const [indicator, setIndicator] = useState<{ left: number; width: number } | null>(null);
    useLayoutEffect(() => {
        const el = itemRefs.current.get(category.label);
        if (el?.offsetWidth) setIndicator({ left: el.offsetLeft + 16, width: el.offsetWidth - 32 });
    }, [category.label]);

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
                </button>
                {/* 分类菜单项（方案 B）：满高文字项、无底色，选中=品牌色加粗 + 底边指示条；
                    指示条抬离底边 6px、随选中项滑动（left/width 过渡 300ms），线框图标。
                    左缘=侧栏分割线右 10px（150/56 随折叠联动，过渡与侧栏同 200ms）；
                    收起时分割线跑到品牌结尾左侧，用 max() 兜到品牌结尾后 12px（16+34+10+36+12≈108）避免压logo。 */}
                <div
                    className="absolute inset-y-0 hidden items-stretch gap-1 transition-[left] duration-200 lg:flex"
                    style={{ left: `max(${(sidebarCollapsed ? 56 : 150) + 10}px, 108px)` }}
                >
                    {categories.map((item) => {
                        const active = item.label === category.label;
                        return (
                            <button
                                key={item.label}
                                type="button"
                                ref={(el) => void itemRefs.current.set(item.label, el)}
                                onClick={() => navigate(item.to ? `/${accountSetId}/${item.to}` : `/${accountSetId}`)}
                                className={
                                    "flex cursor-pointer items-center gap-1.5 px-4 text-[14px] whitespace-nowrap transition-colors " +
                                    (active
                                        ? "font-semibold text-(--pk-brand-text)"
                                        : "text-(--pk-text-2) hover:text-(--pk-text)")
                                }
                            >
                                {item.icon}
                                {item.label}
                            </button>
                        );
                    })}
                    {indicator && (
                        <span
                            aria-hidden
                            className="pointer-events-none absolute bottom-[6px] h-[3px] rounded-full bg-(--pk-brand-text) transition-[left,width] duration-300 ease-out"
                            style={{ left: indicator.left, width: indicator.width }}
                        />
                    )}
                </div>
                <div className="min-w-0 flex-1" />
                {category.to ? <TaxPeriodPicker /> : <PeriodLabel />}
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
    return <BrandLogo size={34} radius={9} className="ring-1 ring-black/5" />;
}

/* ------------------------------------------------------------------ */
/*  当前会计期间                                                       */
/* ------------------------------------------------------------------ */

function PeriodLabel() {
    const period = useDefaultAccountingPeriod();
    return (
        <span className="flex h-6 shrink-0 items-center gap-1.5 rounded-full border border-(--pk-border) px-3 text-[12px] whitespace-nowrap text-(--pk-text-2)">
            <CalendarDaysIcon className="size-3.5" />
            <span className="hidden md:inline">会计期间</span>
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

/**
 * 报税分类的顶栏控件：税款所属期由报税各页共用，公司税务信息对应原工作台的「详情」。
 * 文案与【会计期间】同一口径（标签 + YYYY年M月 + 提示），两个分类的顶栏才对齐。
 */
function TaxPeriodPicker() {
    const period = useTaxPeriod();
    const { accountSetId = "" } = useParams();
    const { accountSets } = useAccountSets();
    const [detailOpen, setDetailOpen] = useState(false);

    return (
        <>
            <span className="flex h-6 shrink-0 items-center gap-1.5 rounded-full border border-(--pk-border) px-3 text-[12px] whitespace-nowrap text-(--pk-text-2)">
                <CalendarDaysIcon className="size-3.5" />
                <span className="hidden md:inline">税款所属期</span>
                <DatePicker
                    picker="month"
                    allowClear={false}
                    size="small"
                    variant="borderless"
                    className="w-[86px] [&_.ant-picker-input>input]:text-[12px]"
                    value={dayjs(period.startDate)}
                    format="YYYY年M月"
                    suffixIcon={null}
                    onChange={(value) => {
                        if (value) setTaxPeriod(makePeriod(value.year(), value.month() + 1));
                    }}
                />
                <Tooltip title="默认按税费种认定里主税种的申报期限推算：按月申报取上月，按季申报取上一季度末月。认定可在【税费种管理】更新，也可以在这里直接改月份。">
                    <QuestionCircleOutlined className="cursor-help text-[11px]" />
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
    const { accountSetId = "" } = useParams();
    const [pwdOpen, setPwdOpen] = useState(false);
    const name = user?.displayName || user?.username || "";

    function handleLogout() {
        logout();
        navigate("/login", { replace: true });
    }

    const items = [
        {
            key: "password",
            icon: <KeyOutlined />,
            label: "修改密码",
            onClick: () => setPwdOpen(true),
        },
        ...(user?.isSystemAdmin
            ? [
                  {
                      key: "users",
                      icon: <TeamOutlined />,
                      label: "用户管理",
                      onClick: () => navigate(`/${accountSetId}/settings/users`),
                  },
              ]
            : []),
        { type: "divider" as const },
        { key: "logout", icon: <LogoutOutlined />, label: "退出登录", onClick: handleLogout },
    ];

    return (
        <>
            <Dropdown
                trigger={["click"]}
                placement="bottomRight"
                menu={{ items }}
            >
                <button type="button" className="flex shrink-0 cursor-pointer items-center gap-2">
                    <span className="flex size-7 items-center justify-center rounded-full bg-(--pk-brand) text-[12px] font-medium text-white">
                        {name.slice(0, 1)}
                    </span>
                    <span className="hidden text-[13px] text-(--pk-text) md:inline">{name}</span>
                </button>
            </Dropdown>
            <ChangePasswordModal open={pwdOpen} onClose={() => setPwdOpen(false)} />
        </>
    );
}

/* ------------------------------------------------------------------ */
/*  账套切换（侧栏底部）                                                */
/* ------------------------------------------------------------------ */

/**
 * 账套切换器：把选中的账套设为后端「当前账套」，刷新账套上下文（保证各页面「（当前）」标记不失真），
 * 并跳到该账套下的同一子页面。页面取数以路由第一段为准，因此即使设置当前失败也照常跳转，只弹错误提示。
 * collapsed 为侧栏图标栏形态，只留一个账套图标。
 */
export function AccountSetSwitcher({ collapsed = false }: { collapsed?: boolean }) {
    const { accountSetId = "" } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const { token } = useAuth();
    const { accountSets, refresh } = useAccountSets();
    const { message } = App.useApp();
    const [busy, setBusy] = useState(false);

    if (accountSets.length === 0) return null;
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
        </div>
    );
}
