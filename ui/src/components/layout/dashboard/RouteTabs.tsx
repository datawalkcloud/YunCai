import { useEffect, useRef, useState, type ReactNode } from "react";
import { useNavigate, useParams } from "react-router";
import { CheckOutlined, DownOutlined, LeftOutlined, RightOutlined } from "@ant-design/icons";
import { Dropdown } from "antd";

import { cn } from "@/lib/utils";
import { ICON_STYLE } from "@/components/page/styles";
import { findCategoryOf, ROUTE_TITLES } from "./menu-model";
import { KEEP_ALIVE_STATE, useRouteTabs } from "./route-tabs-context";

/**
 * 顶部页签条（Route Tabs）：记账分类的首页常驻，进过的页面自动登记，可逐个关闭，溢出时可横向滚动或用「全部页签」下拉。
 * 点击页签带上 keepAlive 状态，由 KeepAliveOutlet 复用已有面板、不重新取数。
 * 视觉为停车平台风格：浅灰底上浮白色页签、当前页蓝色文字。
 * leading 是页签左侧的工具位（如侧栏收起按钮），不参与横向滚动。
 */
export function RouteTabs({ leading }: { leading?: ReactNode }) {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { tabs, activeKey, closeTab } = useRouteTabs();
    const category = findCategoryOf(activeKey);
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
        <div className="flex h-8 shrink-0 items-center border-b border-(--pk-border) bg-(--pk-gray-bg)">
            {leading}
            {overflow && <ScrollArrow direction="left" onClick={() => scrollBy(-160)} />}
            <div
                ref={listRef}
                className="flex min-w-0 flex-1 items-center self-stretch overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
            >
                {category.home !== false && <TabChip label="首页" active={!activeKey} onClick={() => go("")} />}
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
            {tabs.length > 0 && (
                <Dropdown
                    trigger={["click"]}
                    placement="bottomRight"
                    menu={{
                        style: { minWidth: 160 },
                        selectable: false,
                        items: [
                            ...(category.home === false
                                ? []
                                : [
                                      {
                                          key: "",
                                          label: (
                                              <span className="flex items-center justify-between gap-3">
                                                  首页
                                                  {!activeKey && <CheckOutlined style={ICON_STYLE.brand} />}
                                              </span>
                                          ),
                                      },
                                  ]),
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
                        className="mx-1.5 flex size-6 shrink-0 items-center justify-center rounded-full border border-(--pk-border) bg-(--pk-surface) text-(--pk-text-2) hover:border-(--pk-brand-text) hover:text-(--pk-brand-text)"
                    >
                        <DownOutlined className="text-[9px]" />
                    </button>
                </Dropdown>
            )}
            {overflow && <ScrollArrow direction="right" onClick={() => scrollBy(160)} />}
        </div>
    );
}

function ScrollArrow({ direction, onClick }: { direction: "left" | "right"; onClick: () => void }) {
    return (
        <button
            type="button"
            onClick={onClick}
            className={cn(
                "hidden h-full w-9 shrink-0 cursor-pointer items-center justify-center self-stretch text-(--pk-text-2) hover:text-(--pk-brand-text) md:flex",
                direction === "left" ? "border-r border-(--pk-border)" : "border-l border-(--pk-border)",
            )}
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
                "flex h-full shrink-0 items-center gap-1.5 border-r border-(--pk-border) bg-(--pk-surface) px-3.5 text-[13px] whitespace-nowrap",
                active ? "text-(--pk-brand-text)" : "text-(--pk-text) hover:text-(--pk-brand-text)",
            )}
        >
            <button type="button" className="cursor-pointer" onClick={onClick}>
                {label}
            </button>
            {onClose && (
                <button
                    type="button"
                    title={`关闭${label}`}
                    className="flex size-4 cursor-pointer items-center justify-center text-[10px] text-(--pk-text-2) hover:text-(--pk-red)"
                    onClick={(e) => {
                        e.stopPropagation();
                        onClose();
                    }}
                >
                    ✕
                </button>
            )}
        </div>
    );
}
