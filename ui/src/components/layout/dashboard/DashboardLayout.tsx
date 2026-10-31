import { useLayoutEffect, useMemo, useRef } from "react";
import { useLocation, useNavigate, useParams } from "react-router";
import { HomeOutlined } from "@ant-design/icons";
import { Menu } from "antd";
import type { MenuProps } from "antd";

import { WipTag } from "@/pages/lemon/components/ui";
import { KeepAliveOutlet } from "./KeepAliveOutlet";
import { menuGroups } from "./menu-model";
import { isKeepAliveLocation } from "./route-tabs-context";
import { RouteTabsProvider } from "./RouteTabsProvider";
import { WorkspaceHeader } from "./WorkspaceHeader";

/* ------------------------------------------------------------------ */
/*  布局                                                              */
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
        <div className="admin-shell flex h-screen w-screen overflow-hidden bg-[#f4f6fb] font-sans text-slate-700 antialiased">
            <Sidebar />
            <div className="flex min-w-0 flex-1 flex-col">
                <WorkspaceHeader />
                <main
                    ref={mainRef}
                    className="flex-1 overflow-y-auto px-5 py-4"
                    onScroll={(event) => scrollPositions.current.set(path, event.currentTarget.scrollTop)}
                >
                    <KeepAliveOutlet />
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
                          // 菜单项左右各有 8px 内缩，右移 9px 让弹出面板贴住侧栏右边。
                          popupOffset: [9, 0],
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
        <aside className="hidden w-[208px] shrink-0 flex-col border-r border-slate-100 bg-white lg:flex">
            <div className="flex items-center gap-2.5 px-4 pt-5 pb-3">
                <Logo />
                <div className="leading-tight">
                    <div className="text-[17px] font-bold text-slate-800">云财</div>
                    <div className="text-[11px] text-slate-400">让企业财务更简单</div>
                </div>
            </div>
            <nav
                className={[
                    "flex-1 overflow-y-auto pb-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden",
                    // 行高与字号放大到与 40px 弹出项一致；图标同步放大，避免窄栏里显得空。
                    "[&_.ant-menu-item]:h-10! [&_.ant-menu-submenu-title]:h-10!",
                    "[&_.ant-menu-submenu-title]:leading-10!",
                    "[&_.ant-menu-item]:text-[15px]! [&_.ant-menu-submenu-title]:text-[15px]!",
                    "[&_.anticon]:text-[18px]!",
                    // 悬浮分组与「当前路由所在分组」都按主色高亮，避免飞出的子项列表和触发项脱节。
                    "[&_.ant-menu-submenu-active>.ant-menu-submenu-title]:bg-[#eaf1ff]!",
                    "[&_.ant-menu-submenu-active>.ant-menu-submenu-title]:text-[#2f6bff]!",
                    "[&_.ant-menu-submenu-selected>.ant-menu-submenu-title]:text-[#2f6bff]!",
                ].join(" ")}
            >
                <Menu
                    mode="vertical"
                    triggerSubMenuAction="hover"
                    items={items}
                    selectedKeys={[location.pathname]}
                    onClick={({ key }) => {
                        if (key.startsWith("/")) navigate(key);
                    }}
                    style={{ borderInlineEnd: "none" }}
                    classNames={{
                        popup: {
                            root: [
                                // 高度上限取半屏：分组再多也不会顶出视口，超出部分面板内滚动。
                                "min-w-[220px] max-h-[50vh] overflow-y-auto",
                                "shadow-[0_14px_38px_-12px_rgba(15,23,42,0.28)]",
                                "[&_.ant-menu-item]:h-10! [&_.ant-menu-item]:leading-10!",
                                "[&_.ant-menu-item]:px-5! [&_.ant-menu-item]:text-[15px]!",
                            ].join(" "),
                        },
                    }}
                    styles={{
                        popup: { root: { paddingBlock: 6 } },
                    }}
                />
            </nav>
        </aside>
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
