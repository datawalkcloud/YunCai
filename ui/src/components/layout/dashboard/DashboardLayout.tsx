import { useLayoutEffect, useRef } from "react";
import { useLocation, useNavigate, useParams } from "react-router";
import { HomeOutlined, RightOutlined } from "@ant-design/icons";
import { Dropdown } from "antd";

import { WipTag } from "@/pages/lemon/components/ui";
import { cn } from "@/lib/utils";
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
/*  侧边栏：与智能报税工作台同一套深色窄栏导航。                        */
/*  一级为分组，子功能悬浮时从右侧弹出；选中项用左侧蓝色竖条标记。        */
/* ------------------------------------------------------------------ */

function Sidebar() {
    const location = useLocation();
    const navigate = useNavigate();
    const { accountSetId = "" } = useParams();
    const base = `/${accountSetId}`;
    // 带斜杠的 URL 也要能命中「首页」，故先剥掉尾部斜杠再比对。
    const pathname = location.pathname.replace(/\/+$/, "") || "/";

    return (
        <aside className="hidden w-[132px] shrink-0 flex-col bg-[#0f172a] lg:flex">
            <div className="flex h-12 items-center gap-2 px-3">
                <Logo />
                <span className="text-[14px] font-semibold tracking-wide text-white">云财</span>
            </div>
            <nav className="flex-1 overflow-y-auto pb-3 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                <NavButton icon={<HomeOutlined />} label="首页" active={pathname === base} onClick={() => navigate(base)} />
                {menuGroups.map((group) => {
                    if (group.to) {
                        const to = `${base}/${group.to}`;
                        return (
                            <NavButton
                                key={group.label}
                                icon={group.icon}
                                label={group.label}
                                active={pathname === to}
                                onClick={() => navigate(to)}
                            />
                        );
                    }
                    const children = group.children ?? [];
                    return (
                        <NavButton
                            key={group.label}
                            icon={group.icon}
                            label={group.label}
                            hasChildren
                            active={children.some((child) => pathname === `${base}/${child.to}`)}
                            // 分组标题本身不是页面：点击直接进入第一个子功能，子功能由悬浮弹板选择。
                            onClick={() => navigate(`${base}/${children[0].to}`)}
                            popup={{
                                items: children.map((child) => ({
                                    key: `${base}/${child.to}`,
                                    label: <MenuLabel label={child.label} wip={child.wip} />,
                                })),
                                selectable: true,
                                selectedKeys: [pathname],
                                style: { minWidth: 168, maxHeight: "50vh", overflowY: "auto" },
                                onClick: ({ key }) => navigate(key),
                            }}
                        />
                    );
                })}
            </nav>
        </aside>
    );
}

function NavButton({
    icon,
    label,
    active,
    hasChildren,
    onClick,
    popup,
}: {
    icon: React.ReactNode;
    label: string;
    active: boolean;
    hasChildren?: boolean;
    onClick: () => void;
    popup?: React.ComponentProps<typeof Dropdown>["menu"];
}) {
    const button = (
        <button
            type="button"
            onClick={onClick}
            className={cn(
                "relative flex h-10 w-full items-center gap-2 px-3 text-left text-[13px] transition-colors",
                active ? "bg-[#16233d] font-medium text-white" : "text-[#8f9bb3] hover:bg-white/[0.06] hover:text-white",
            )}
        >
            {active && <span className="absolute inset-y-2 left-0 w-[3px] rounded-r bg-[#2f6bff]" />}
            <span className={cn("text-[15px]", active && "text-[#6b93ff]")}>{icon}</span>
            <span className="truncate">{label}</span>
            {hasChildren && <RightOutlined className="ml-auto shrink-0 text-[9px] text-[#8f9bb3]" />}
        </button>
    );

    if (!popup) return button;
    return (
        <Dropdown trigger={["hover"]} placement="rightTop" mouseEnterDelay={0.08} menu={popup}>
            {button}
        </Dropdown>
    );
}

function MenuLabel({ label, wip }: { label: string; wip?: boolean }) {
    if (!wip) return label;
    return (
        <span className="flex items-center justify-between gap-2">
            {label}
            <WipTag className="shrink-0" />
        </span>
    );
}

function Logo() {
    return (
        <svg viewBox="0 0 32 32" className="size-6 shrink-0" fill="none">
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
