import { useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router";
import {
    CheckOutlined,
    CloseOutlined,
    DownOutlined,
    LeftOutlined,
    RightOutlined,
} from "@ant-design/icons";
import { Dropdown } from "antd";

import { cn } from "@/lib/utils";
import { ICON_STYLE } from "@/components/page/styles";
import { ROUTE_TITLES } from "./menu-model";
import { KEEP_ALIVE_STATE, useRouteTabs } from "./route-tabs-context";

/**
 * 顶部页签条（Route Tabs）：首页常驻，进过的页面自动登记，可逐个关闭，溢出时可横向滚动或用「全部页签」下拉。
 * 点击页签带上 keepAlive 状态，由 KeepAliveOutlet 复用已有面板、不重新取数。
 */
export function RouteTabs() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { tabs, activeKey, closeTab } = useRouteTabs();
    const listRef = useRef<HTMLDivElement>(null);
    const [overflow, setOverflow] = useState(false);

    useEffect(() => {
        const el = listRef.current;
        if (!el) return;
        const sync = () => setOverflow(el.scrollWidth > el.clientWidth + 1);
        sync();
        const observer = new ResizeObserver(sync);
        observer.observe(el);
        return () => observer.disconnect();
    }, [tabs]);

    useEffect(() => {
        listRef.current
            ?.querySelector<HTMLElement>(`[data-tab="${activeKey}"]`)
            ?.scrollIntoView({ inline: "nearest", block: "nearest" });
    }, [activeKey, tabs.length]);

    const go = (key: string) =>
        navigate(key ? `/${accountSetId}/${key}` : `/${accountSetId}`, { state: KEEP_ALIVE_STATE });

    function scrollBy(delta: number) {
        listRef.current?.scrollBy({ left: delta, behavior: "smooth" });
    }

    return (
        <div className="flex h-10 items-center gap-1 border-b border-slate-100 bg-[#f7f9fc] px-2">
            {overflow && <ScrollArrow direction="left" onClick={() => scrollBy(-160)} />}
            <div
                ref={listRef}
                className="flex min-w-0 flex-1 items-center gap-1.5 overflow-x-auto py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
            >
                <TabChip label="首页" active={!activeKey} onClick={() => go("")} />
                {tabs.map((key) => (
                    <TabChip
                        key={key}
                        label={ROUTE_TITLES[key]}
                        tabKey={key}
                        active={key === activeKey}
                        onClick={() => go(key)}
                        onClose={() => closeTab(key)}
                    />
                ))}
            </div>
            {overflow && <ScrollArrow direction="right" onClick={() => scrollBy(160)} />}
            {tabs.length > 0 && (
                <Dropdown
                    trigger={["click"]}
                    placement="bottomRight"
                    menu={{
                        style: { minWidth: 160 },
                        selectable: false,
                        items: [
                            {
                                key: "",
                                label: (
                                    <span className="flex items-center justify-between gap-3">
                                        首页
                                        {!activeKey && <CheckOutlined style={ICON_STYLE.brand} />}
                                    </span>
                                ),
                            },
                            ...tabs.map((key) => ({
                                key,
                                label: (
                                    <span className="flex items-center justify-between gap-3">
                                        {ROUTE_TITLES[key]}
                                        {key === activeKey && <CheckOutlined style={ICON_STYLE.brand} />}
                                    </span>
                                ),
                            })),
                        ],
                        onClick: ({ key }) => go(key),
                    }}
                >
                    <button
                        type="button"
                        title="全部页签"
                        className="flex size-6 shrink-0 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 transition-colors hover:border-[#2f6bff]/40 hover:text-[#2f6bff]"
                    >
                        <DownOutlined className="text-[9px]" />
                    </button>
                </Dropdown>
            )}
        </div>
    );
}

function ScrollArrow({ direction, onClick }: { direction: "left" | "right"; onClick: () => void }) {
    return (
        <button
            type="button"
            onClick={onClick}
            className="flex size-6 shrink-0 items-center justify-center rounded-md text-slate-400 transition-colors hover:bg-white hover:text-slate-600"
        >
            {direction === "left" ? <LeftOutlined className="text-[10px]" /> : <RightOutlined className="text-[10px]" />}
        </button>
    );
}

function TabChip({
    label,
    tabKey,
    active,
    onClick,
    onClose,
}: {
    label: string;
    tabKey?: string;
    active: boolean;
    onClick: () => void;
    onClose?: () => void;
}) {
    return (
        <div
            data-tab={tabKey ?? ""}
            className={cn(
                "inline-flex h-7 shrink-0 items-center gap-1 rounded-md border pl-2.5 text-[12.5px] transition-colors",
                onClose ? "pr-1" : "pr-2.5",
                active
                    ? "border-[#2f6bff] bg-[#2f6bff] text-white shadow-[0_1px_2px_rgba(47,107,255,0.3)]"
                    : "border-slate-200 bg-white text-slate-600 hover:border-[#2f6bff]/40 hover:text-[#2f6bff]",
            )}
        >
            <button type="button" className="cursor-pointer" onClick={onClick}>
                {label}
            </button>
            {onClose && (
                <button
                    type="button"
                    title={`关闭${label}`}
                    className={cn(
                        "flex size-4 items-center justify-center rounded-full transition-colors",
                        active
                            ? "text-white/70 hover:bg-white/25 hover:text-white"
                            : "text-slate-400 hover:bg-slate-200/70 hover:text-slate-600",
                    )}
                    onClick={(e) => {
                        e.stopPropagation();
                        onClose();
                    }}
                >
                    <CloseOutlined className="text-[9px]" />
                </button>
            )}
        </div>
    );
}
