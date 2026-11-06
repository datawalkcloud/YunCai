import type { ReactNode } from "react";
import { BellOutlined, DownOutlined, LeftOutlined, RightOutlined, UpOutlined, UserOutlined } from "@ant-design/icons";

import "./parking.css";

export type ParkingMenu = { key: string; label: string };
export type ParkingSidebarItem = { key: string; label: string; icon?: ReactNode };
export type ParkingSidebarSection = { key: string; label: string; icon?: ReactNode; items: ParkingSidebarItem[] };
export type ParkingTab = { key: string; label: string; active?: boolean };
export type ParkingUtilityApp = { key: string; label: string; icon: ReactNode };

type ParkingLayoutProps = {
    brand?: string;
    menus?: ParkingMenu[];
    activeMenu?: string;
    pillMenu?: string;
    sidebarTitle?: string;
    sections?: ParkingSidebarSection[];
    selectedKey?: string;
    tabs?: ParkingTab[];
    utilityApps?: ParkingUtilityApp[];
    userName?: string;
    noticeCount?: number;
    aside?: ReactNode;
    children: ReactNode;
};

/* 云中停·智慧停车风格外壳：顶栏 + 分组侧栏 + 页签条 + 内容区 + 可选右侧面板。 */
export default function ParkingLayout({
    brand = "云中停·智慧停车",
    menus = [],
    activeMenu,
    pillMenu,
    sidebarTitle = "运营数据中心",
    sections = [],
    selectedKey,
    tabs = [],
    utilityApps = [],
    userName = "",
    noticeCount = 0,
    aside,
    children,
}: ParkingLayoutProps) {
    return (
        <div className="parking-shell flex h-screen w-screen flex-col overflow-hidden bg-white">
            <TopBar
                brand={brand}
                menus={menus}
                activeMenu={activeMenu}
                pillMenu={pillMenu}
                utilityApps={utilityApps}
                userName={userName}
                noticeCount={noticeCount}
            />
            <div className="flex min-h-0 flex-1">
                <Sidebar title={sidebarTitle} sections={sections} selectedKey={selectedKey} />
                <div className="flex min-w-0 flex-1 flex-col">
                    <TabStrip tabs={tabs} />
                    <main className="pk-main min-h-0 flex-1 overflow-y-auto bg-(--pk-gray-bg) p-3">{children}</main>
                </div>
                {aside && (
                    <aside className="hidden w-[380px] shrink-0 overflow-y-auto border-l border-(--pk-border) bg-white p-3 xl:block">
                        {aside}
                    </aside>
                )}
            </div>
        </div>
    );
}

/* ------------------------------------------------------------------ */
/*  顶栏                                                               */
/* ------------------------------------------------------------------ */

function TopBar({
    brand,
    menus,
    activeMenu,
    pillMenu,
    utilityApps,
    userName,
    noticeCount,
}: {
    brand: string;
    menus: ParkingMenu[];
    activeMenu?: string;
    pillMenu?: string;
    utilityApps: ParkingUtilityApp[];
    userName: string;
    noticeCount: number;
}) {
    return (
        <header className="flex h-[52px] shrink-0 items-center gap-4 border-b border-(--pk-border) bg-white px-4">
            <div className="flex items-center gap-2">
                <LogoMark />
                <span className="text-[15px] font-semibold tracking-wide">{brand}</span>
            </div>
            <nav className="flex min-w-0 flex-1 items-center gap-1 overflow-hidden">
                {menus.map((menu) => {
                    const isActive = menu.key === activeMenu;
                    const isPill = menu.key === pillMenu;
                    return (
                        <span
                            key={menu.key}
                            className={
                                "relative flex h-[52px] cursor-pointer items-center whitespace-nowrap px-2.5 text-[13px] " +
                                (isPill
                                    ? "mt-2.5 mb-2.5 h-auto rounded-full bg-(--pk-brand-tint) px-3.5 py-1 font-medium text-(--pk-brand)"
                                    : isActive
                                      ? "font-semibold text-(--pk-brand)"
                                      : "text-(--pk-text) hover:text-(--pk-brand)")
                            }
                        >
                            {menu.label}
                            {isActive && !isPill && (
                                <span className="absolute inset-x-2 bottom-0 h-[3px] rounded-t bg-(--pk-brand)" />
                            )}
                        </span>
                    );
                })}
            </nav>
            <span className="pk-ai-pill">
                <SparkIcon />
                <span className="pk-ai-label">AI大模型</span>
                <span>· AI大模型分析</span>
            </span>
            <div className="flex items-center gap-3">
                {utilityApps.map((app) => (
                    <span key={app.key} className="flex cursor-pointer flex-col items-center gap-0.5">
                        <span className="flex size-7 items-center justify-center rounded-full bg-(--pk-gray-bg) text-[13px] text-(--pk-text-2)">
                            {app.icon}
                        </span>
                        <span className="text-[10px] leading-none text-(--pk-text-2)">{app.label}</span>
                    </span>
                ))}
            </div>
            <span className="relative cursor-pointer text-[16px] text-(--pk-text-2)">
                <BellOutlined />
                {noticeCount > 0 && (
                    <span className="absolute -top-1.5 -right-2 flex h-3.5 min-w-3.5 items-center justify-center rounded-full bg-(--pk-red) px-0.5 text-[9px] leading-none font-medium text-white">
                        {noticeCount}
                    </span>
                )}
            </span>
            <span className="flex cursor-pointer items-center gap-1.5">
                <span className="flex size-[26px] items-center justify-center rounded-full bg-(--pk-brand) text-[13px] text-white">
                    <UserOutlined />
                </span>
                <span className="text-[12px] text-(--pk-text)">{userName}</span>
            </span>
        </header>
    );
}

function LogoMark() {
    return (
        <span className="flex size-[26px] items-center justify-center rounded-md bg-black">
            <svg viewBox="0 0 16 16" className="size-4" fill="none">
                <path d="M9.5 2.5 14 7l-4.5 4.5" stroke="#fff" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                <path d="M14 7H2.5" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
            </svg>
        </span>
    );
}

function SparkIcon() {
    return (
        <svg viewBox="0 0 14 14" className="size-3.5 shrink-0" fill="none">
            <path
                d="M7 1.5c.6 2.8 1.7 3.9 4.5 4.5-2.8.6-3.9 1.7-4.5 4.5-.6-2.8-1.7-3.9-4.5-4.5 2.8-.6 3.9-1.7 4.5-4.5Z"
                fill="url(#pkSpark)"
            />
            <defs>
                <linearGradient id="pkSpark" x1="2.5" y1="2" x2="11.5" y2="11" gradientUnits="userSpaceOnUse">
                    <stop stopColor="#273CF3" />
                    <stop offset="1" stopColor="#B620E0" />
                </linearGradient>
            </defs>
        </svg>
    );
}

/* ------------------------------------------------------------------ */
/*  侧边栏                                                             */
/* ------------------------------------------------------------------ */

function Sidebar({
    title,
    sections,
    selectedKey,
}: {
    title: string;
    sections: ParkingSidebarSection[];
    selectedKey?: string;
}) {
    return (
        <aside className="flex w-[236px] shrink-0 flex-col overflow-hidden border-r border-(--pk-border) bg-white">
            <div className="flex h-[44px] shrink-0 items-center gap-1.5 bg-(--pk-gray-bg) px-4 text-[13px] font-medium">
                <svg viewBox="0 0 16 16" className="size-3.5" fill="none">
                    <path d="M2 8.5h3.5V14H2V8.5Zm4.5-6H10V14H6.5V2.5Zm4.5 9.5H14.5V11H11v1Z" fill="#1A1A1A" />
                </svg>
                <span className="flex-1">{title}</span>
                <UpOutlined className="text-[10px] text-(--pk-text-2)" />
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto">
                {sections.map((section) => (
                    <div key={section.key}>
                        {section.label && (
                            <div className="flex h-[42px] items-center gap-1.5 bg-(--pk-gray-bg) px-4 text-[13px] font-medium">
                                {section.icon && <span className="text-[13px]">{section.icon}</span>}
                                <span className="flex-1">{section.label}</span>
                                <DownOutlined className="text-[10px] text-(--pk-text-2)" />
                            </div>
                        )}
                        {section.items.map((item) => {
                            const selected = item.key === selectedKey;
                            return (
                                <div
                                    key={item.key}
                                    className={
                                        "relative flex h-[42px] cursor-pointer items-center gap-2 px-4 text-[13px] " +
                                        (selected
                                            ? "bg-(--pk-brand-soft) font-medium text-(--pk-brand)"
                                            : "text-(--pk-text) hover:bg-(--pk-gray-bg)")
                                    }
                                >
                                    {item.icon && <span className="text-[14px]">{item.icon}</span>}
                                    <span className="truncate">{item.label}</span>
                                    {selected && (
                                        <span className="absolute top-0 right-0 h-full w-[3px] bg-(--pk-brand)" />
                                    )}
                                </div>
                            );
                        })}
                    </div>
                ))}
            </div>
        </aside>
    );
}

/* ------------------------------------------------------------------ */
/*  页签条                                                             */
/* ------------------------------------------------------------------ */

function TabStrip({ tabs }: { tabs: ParkingTab[] }) {
    return (
        <div className="flex h-[44px] shrink-0 items-center border-b border-(--pk-border) bg-white">
            <span className="flex h-full w-9 cursor-pointer items-center justify-center border-r border-(--pk-border) text-[11px] text-(--pk-text-2)">
                <LeftOutlined />
            </span>
            <div className="flex min-w-0 flex-1 items-center self-stretch overflow-hidden">
                {tabs.map((tab) => (
                    <span
                        key={tab.key}
                        className={
                            "flex h-full cursor-pointer items-center gap-2 border-r border-(--pk-border) px-4 text-[13px] whitespace-nowrap " +
                            (tab.active ? "text-(--pk-brand)" : "text-(--pk-text)")
                        }
                    >
                        {tab.label}
                        <span className="text-[10px] text-(--pk-text-2) hover:text-(--pk-red)">✕</span>
                    </span>
                ))}
            </div>
            <span className="flex h-full w-9 cursor-pointer items-center justify-center border-l border-(--pk-border) text-[11px] text-(--pk-text-2)">
                <RightOutlined />
            </span>
        </div>
    );
}
