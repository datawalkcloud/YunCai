import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router";
import { ChevronDownIcon, ChevronRightIcon, HomeIcon } from "@heroicons/react/24/solid";
import { MenuFoldOutlined, MenuUnfoldOutlined } from "@ant-design/icons";
import { Drawer, Popover, Tooltip } from "antd";

import { WipTag } from "@/pages/lemon/components/ui";
import { useMediaQuery } from "@/hooks/use-media-query";
import { cn } from "@/lib/utils";
import "../parking/parking.css";
import { KeepAliveOutlet } from "./KeepAliveOutlet";
import { findCategoryOf, menuCategories, type MenuLeaf } from "./menu-model";
import { isKeepAliveLocation, tabKeyOf } from "./route-tabs-context";
import { RouteTabsProvider } from "./RouteTabsProvider";
import { RouteTabs } from "./RouteTabs";
import { AccountSetSwitcher, WorkspaceTopBar } from "./WorkspaceHeader";

/* ------------------------------------------------------------------ */
/*  布局（停车平台风格）：顶栏 + 分组侧栏 + 页签条 + 内容区。              */
/* ------------------------------------------------------------------ */

export default function DashboardLayout() {
    return (
        <RouteTabsProvider>
            <WorkspaceShell />
        </RouteTabsProvider>
    );
}

function WorkspaceShell() {
    const location = useLocation();
    const mainRef = useRef<HTMLElement>(null);
    const scrollPositions = useRef(new Map<string, number>());
    const path = location.pathname;
    const isDesktopNav = useMediaQuery("(min-width: 1024px)");
    const [navOpen, setNavOpen] = useState(false);

    // 手机上导航是覆盖层，点完菜单必须收起；换页也一并关掉。
    useEffect(() => {
        setNavOpen(false);
    }, [path]);

    // 标签切换还原各自滚动位置；菜单等「重新进页」的跳转回到顶部。
    useLayoutEffect(() => {
        const el = mainRef.current;
        if (!el) return;
        if (isKeepAliveLocation(location.state)) {
            el.scrollTop = scrollPositions.current.get(path) ?? 0;
        } else {
            scrollPositions.current.delete(path);
            el.scrollTop = 0;
        }
    }, [path, location.key, location.state]);

    return (
        <div className="parking-shell admin-shell flex h-dvh w-full flex-col overflow-hidden">
            <WorkspaceTopBar onOpenNav={() => setNavOpen(true)} />
            <div className="flex min-h-0 flex-1">
                <Sidebar className="hidden lg:flex" />
                {!isDesktopNav && (
                    <Drawer
                        open={navOpen}
                        onClose={() => setNavOpen(false)}
                        placement="left"
                        size={280}
                        closable={false}
                        rootClassName="parking-shell"
                        styles={{ body: { padding: 0 } }}
                    >
                        <MobileSidebar onNavigate={() => setNavOpen(false)} />
                    </Drawer>
                )}
                <div className="flex min-w-0 flex-1 flex-col">
                    <RouteTabs />
                    <main
                        ref={mainRef}
                        className="min-h-0 flex-1 overflow-y-auto bg-(--pk-bg) p-3 md:p-4"
                        onScroll={(event) => scrollPositions.current.set(path, event.currentTarget.scrollTop)}
                    >
                        <KeepAliveOutlet />
                    </main>
                </div>
            </div>
        </div>
    );
}

/* ------------------------------------------------------------------ */
/*  侧边栏：垂直菜单（分组子项悬停/点击从右侧浮层弹出）+ 收起为图标栏。    */
/* ------------------------------------------------------------------ */

/** 收起状态是本机偏好，跨账套共用一个开关。 */
const SIDEBAR_COLLAPSED_KEY = "kuaiji.layout.sidebarCollapsed";

function useSidebarCollapsed() {
    const [collapsed, setCollapsed] = useState(() => localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === "1");

    useLayoutEffect(() => {
        localStorage.setItem(SIDEBAR_COLLAPSED_KEY, collapsed ? "1" : "0");
    }, [collapsed]);

    return { collapsed, toggle: () => setCollapsed((next) => !next) };
}

function Sidebar({ className }: { className?: string }) {
    const location = useLocation();
    const navigate = useNavigate();
    const { accountSetId = "" } = useParams();
    const base = `/${accountSetId}`;
    const tabKey = tabKeyOf(location.pathname, accountSetId);
    const { groups, home } = findCategoryOf(tabKey);
    const { collapsed, toggle } = useSidebarCollapsed();

    return (
        <aside
            className={cn(
                "flex shrink-0 flex-col overflow-hidden border-r border-(--pk-border) bg-(--pk-surface) transition-[width] duration-200",
                collapsed ? "w-[56px]" : "w-[200px]",
                className,
            )}
        >
            <div className="menu-slim-scrollbar min-h-0 flex-1 overflow-y-auto">
                {home !== false && (
                    <SidebarLink
                        icon={<HomeIcon className="size-[17px]" />}
                        label="首页"
                        collapsed={collapsed}
                        active={tabKey === ""}
                        onClick={() => navigate(base)}
                    />
                )}
                {groups.map((group) => {
                    if (!group.children) {
                        return (
                            <SidebarLink
                                key={group.label}
                                icon={group.icon}
                                label={group.label}
                                wip={group.wip}
                                collapsed={collapsed}
                                active={tabKey === group.to}
                                onClick={() => navigate(`${base}/${group.to}`)}
                            />
                        );
                    }
                    return (
                        <SidebarGroup
                            key={group.label}
                            icon={group.icon}
                            label={group.label}
                            items={group.children}
                            collapsed={collapsed}
                            containsActive={(group.children ?? []).some((leaf) => leaf.to === tabKey)}
                            activeKey={tabKey}
                            onLeafClick={(to) => navigate(`${base}/${to}`)}
                        />
                    );
                })}
            </div>
            <AccountSetSwitcher
                collapsed={collapsed}
                action={<CollapseButton collapsed={collapsed} onClick={toggle} />}
            />
        </aside>
    );
}

/* ------------------------------------------------------------------ */
/*  手机侧栏：分类切换 + 可折叠分组，行高按触屏点击留够 44px。             */
/* ------------------------------------------------------------------ */

function MobileSidebar({ onNavigate }: { onNavigate: () => void }) {
    const location = useLocation();
    const navigate = useNavigate();
    const { accountSetId = "" } = useParams();
    const base = `/${accountSetId}`;
    const tabKey = tabKeyOf(location.pathname, accountSetId);
    const category = findCategoryOf(tabKey);
    const [openGroups, setOpenGroups] = useState<string[]>([]);

    // 当前页所属分组默认展开，其余保持收起，避免一屏塞几十条。
    useEffect(() => {
        const active = category.groups.find((group) => group.children?.some((leaf) => leaf.to === tabKey));
        if (!active) return;
        setOpenGroups((prev) => (prev.includes(active.label) ? prev : [...prev, active.label]));
    }, [category, tabKey]);

    const go = (to: string) => {
        navigate(to ? `${base}/${to}` : base);
        onNavigate();
    };

    return (
        <div className="flex h-full min-h-0 flex-col bg-(--pk-surface)">
            <div className="flex shrink-0 gap-1 border-b border-(--pk-border) p-2">
                {menuCategories.map((item) => {
                    const active = item.label === category.label;
                    return (
                        <button
                            key={item.label}
                            type="button"
                            onClick={() => go(item.to)}
                            className={cn(
                                "h-9 flex-1 cursor-pointer text-[14px] font-medium",
                                active
                                    ? "bg-(--pk-brand-soft) text-(--pk-brand-text)"
                                    : "text-(--pk-text-2) hover:bg-(--pk-gray-bg)",
                            )}
                        >
                            {item.label}
                        </button>
                    );
                })}
            </div>
            <div className="menu-slim-scrollbar min-h-0 flex-1 overflow-y-auto overscroll-contain py-1">
                {category.home !== false && (
                    <MobileNavLink
                        icon={<HomeIcon className="size-[17px]" />}
                        label="首页"
                        active={tabKey === ""}
                        onClick={() => go("")}
                    />
                )}
                {category.groups.map((group) => {
                    if (!group.children) {
                        return (
                            <MobileNavLink
                                key={group.label}
                                icon={group.icon}
                                label={group.label}
                                wip={group.wip}
                                active={tabKey === group.to}
                                onClick={() => go(group.to ?? "")}
                            />
                        );
                    }
                    const open = openGroups.includes(group.label);
                    const containsActive = group.children.some((leaf) => leaf.to === tabKey);
                    return (
                        <div key={group.label}>
                            <button
                                type="button"
                                onClick={() =>
                                    setOpenGroups((prev) =>
                                        prev.includes(group.label)
                                            ? prev.filter((label) => label !== group.label)
                                            : [...prev, group.label],
                                    )
                                }
                                aria-expanded={open}
                                className={cn(
                                    "flex h-11 w-full cursor-pointer items-center gap-2.5 px-4 text-[14px] font-medium",
                                    containsActive ? "text-(--pk-brand-text)" : "text-(--pk-text)",
                                )}
                            >
                                <span className="flex size-[17px] shrink-0 items-center justify-center">
                                    {group.icon}
                                </span>
                                <span className="flex-1 text-left">{group.label}</span>
                                <ChevronDownIcon
                                    className={cn("size-3.5 opacity-60 transition-transform", open && "rotate-180")}
                                />
                            </button>
                            {open && (
                                <div className="pb-1">
                                    {group.children.map((leaf) => {
                                        const active = leaf.to === tabKey;
                                        return (
                                            <button
                                                key={leaf.to}
                                                type="button"
                                                onClick={() => go(leaf.to)}
                                                className={cn(
                                                    "flex h-11 w-full cursor-pointer items-center gap-2.5 pr-4 pl-11 text-[13px]",
                                                    active
                                                        ? "bg-(--pk-brand-soft) font-medium text-(--pk-brand-text)"
                                                        : "text-(--pk-text) hover:bg-(--pk-gray-bg) hover:text-(--pk-brand-text)",
                                                )}
                                            >
                                                <span className="flex size-[15px] shrink-0 items-center justify-center">
                                                    {leaf.icon}
                                                </span>
                                                <span className="flex-1 truncate text-left">{leaf.label}</span>
                                                {leaf.wip && <WipTag className="shrink-0" />}
                                            </button>
                                        );
                                    })}
                                </div>
                            )}
                        </div>
                    );
                })}
            </div>
            <AccountSetSwitcher />
        </div>
    );
}

function MobileNavLink({
    icon,
    label,
    wip,
    active,
    onClick,
}: {
    icon: React.ReactNode;
    label: string;
    wip?: boolean;
    active: boolean;
    onClick: () => void;
}) {
    return (
        <button
            type="button"
            onClick={onClick}
            className={cn(
                "flex h-11 w-full cursor-pointer items-center gap-2.5 px-4 text-[14px]",
                active
                    ? "bg-(--pk-brand-soft) font-medium text-(--pk-brand-text)"
                    : "text-(--pk-text) hover:bg-(--pk-gray-bg) hover:text-(--pk-brand-text)",
            )}
        >
            <span className="flex size-[17px] shrink-0 items-center justify-center">{icon}</span>
            <span className="flex-1 truncate text-left">{label}</span>
            {wip && <WipTag className="shrink-0" />}
        </button>
    );
}

/** 收起/展开：贴在账套切换那一行的右端，不再单独占一行。 */
function CollapseButton({ collapsed, onClick }: { collapsed: boolean; onClick: () => void }) {
    return (
        <Tooltip title={collapsed ? "展开菜单" : "收起菜单"} placement="right">
            <button
                type="button"
                onClick={onClick}
                aria-label={collapsed ? "展开菜单" : "收起菜单"}
                className="flex size-5 shrink-0 cursor-pointer items-center justify-center rounded-[4px] text-[12px] text-(--pk-text-2) hover:bg-(--pk-gray-bg) hover:text-(--pk-brand-text)"
            >
                {collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            </button>
        </Tooltip>
    );
}

/** 分组行：点击/悬停从右侧弹出子菜单，选中项留在浮层里高亮。 */
function SidebarGroup({
    icon,
    label,
    items,
    collapsed,
    containsActive,
    activeKey,
    onLeafClick,
}: {
    icon: React.ReactNode;
    label: string;
    items: MenuLeaf[];
    collapsed: boolean;
    containsActive: boolean;
    activeKey: string;
    onLeafClick: (to: string) => void;
}) {
    const triggerRef = useRef<HTMLButtonElement>(null);
    const [open, setOpen] = useState(false);
    // 浮层挂在 overflow-hidden 的外壳里，超出视口的部分会被直接裁掉，
    // 所以限高要在打开时按触发项的实际剩余空间算（vh 在窗口缩放后不可靠）。
    const [maxHeight, setMaxHeight] = useState(320);

    const measure = () => {
        const rect = triggerRef.current?.getBoundingClientRect();
        if (!rect) return;
        // 弹层会自动选剩余空间更大的一侧展开，这里取同侧的可用水位。
        const room = Math.max(window.innerHeight - rect.top, rect.bottom) - 24;
        setMaxHeight(Math.max(room, 160));
    };

    return (
        <Popover
            open={open}
            onOpenChange={(next) => {
                if (next) measure();
                setOpen(next);
            }}
            trigger={["hover", "click"]}
            placement="rightTop"
            arrow={false}
            mouseEnterDelay={0.1}
            mouseLeaveDelay={0.15}
            styles={{ container: { padding: "4px 0" }, content: { padding: 0 } }}
            // --pk-* 变量定义在 .parking-shell 上，弹层必须挂回壳内才能取到品牌色。
            getPopupContainer={(node) => (node.closest(".parking-shell") as HTMLElement) ?? document.body}
            content={
                <div
                    className="menu-slim-scrollbar flex min-w-[160px] flex-col overflow-y-auto overscroll-contain"
                    style={{ maxHeight }}
                >
                    {items.map((leaf) => {
                        const active = leaf.to === activeKey;
                        return (
                            <button
                                key={leaf.to}
                                type="button"
                                onClick={() => {
                                    onLeafClick(leaf.to);
                                    setOpen(false);
                                }}
                                className={cn(
                                    "group flex h-[36px] w-full cursor-pointer items-center gap-2.5 px-3 text-[13px]",
                                    active
                                        ? "bg-(--pk-brand-soft) font-medium text-(--pk-brand-text)"
                                        : "text-(--pk-text) hover:bg-(--pk-gray-bg) hover:text-(--pk-brand-text)",
                                )}
                            >
                                <span
                                    className={cn(
                                        "flex size-[15px] shrink-0 items-center justify-center group-hover:text-(--pk-brand-text)",
                                        active ? "text-(--pk-brand-text)" : "text-(--pk-text-2)",
                                    )}
                                >
                                    {leaf.icon}
                                </span>
                                <span className="flex-1 truncate text-left">{leaf.label}</span>
                                {leaf.wip && <WipTag className="shrink-0" />}
                            </button>
                        );
                    })}
                </div>
            }
        >
            <button
                ref={triggerRef}
                type="button"
                aria-label={label}
                className={cn(
                    "group flex h-[46px] w-full cursor-pointer items-center text-[14px] font-medium hover:text-(--pk-brand-text)",
                    collapsed ? "justify-center" : "gap-2.5 px-4",
                    containsActive
                        ? "bg-(--pk-brand-soft) text-(--pk-brand-text) hover:bg-(--pk-brand-soft)"
                        : "text-(--pk-text) hover:bg-(--pk-gray-bg)",
                )}
            >
                <span
                    className={cn(
                        "flex size-[17px] shrink-0 items-center justify-center group-hover:text-(--pk-brand-text)",
                        containsActive ? "text-(--pk-brand-text)" : "text-(--pk-text-2)",
                    )}
                >
                    {icon}
                </span>
                {!collapsed && <span className="flex-1 text-left">{label}</span>}
                {!collapsed && <ChevronRightIcon className="size-3 shrink-0 opacity-60" />}
            </button>
        </Popover>
    );
}

function SidebarLink({
    icon,
    label,
    wip,
    collapsed,
    active,
    onClick,
}: {
    icon: React.ReactNode;
    label: string;
    wip?: boolean;
    collapsed: boolean;
    active: boolean;
    onClick: () => void;
}) {
    const row = (
        <button
            type="button"
            onClick={onClick}
            aria-label={label}
            className={cn(
                "group relative flex h-[46px] w-full cursor-pointer items-center text-[14px]",
                collapsed ? "justify-center" : "gap-2.5 px-4",
                active
                    ? "bg-(--pk-brand-soft) font-medium text-(--pk-brand-text)"
                    : "hover:bg-(--pk-gray-bg) hover:text-(--pk-brand-text)",
            )}
        >
            <span
                className={cn(
                    "flex size-[17px] shrink-0 items-center justify-center group-hover:text-(--pk-brand-text)",
                    active ? "text-(--pk-brand-text)" : "text-(--pk-text-2)",
                )}
            >
                {icon}
            </span>
            {!collapsed && <span className="flex-1 truncate text-left">{label}</span>}
            {!collapsed && wip && <WipTag className="shrink-0" />}
            {active && <span className="absolute top-0 right-0 h-full w-[3px] bg-(--pk-brand-text)" />}
        </button>
    );
    // 图标栏下文字没了，靠 tooltip 补齐名称（含建设中口径）。
    return collapsed ? (
        <Tooltip title={wip ? `${label}（建设中）` : label} placement="right">
            {row}
        </Tooltip>
    ) : (
        row
    );
}
