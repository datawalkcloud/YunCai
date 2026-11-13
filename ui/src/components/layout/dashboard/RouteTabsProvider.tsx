import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { useLocation, useNavigate, useParams } from "react-router";

import { findCategoryOf, ROUTE_TITLES } from "./menu-model";
import {
    KEEP_ALIVE_STATE,
    RouteTabsContext,
    tabKeyOf,
    type RouteTabsValue,
} from "./route-tabs-context";

/** 页签持久化：只存页签键，刷新浏览器后恢复同一批页签。 */
const STORAGE_KEY = "yuncai.route-tabs.v1";
/** 页签上限：超出后丢最早的记录（首页不在页签列表里）。 */
const MAX_TABS = 12;

function loadTabs(): string[] {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        const list: unknown = raw ? JSON.parse(raw) : [];
        if (!Array.isArray(list)) return [];
        return list.filter((item): item is string => typeof item === "string" && Object.hasOwn(ROUTE_TITLES, item));
    } catch {
        return [];
    }
}

/**
 * 多页签状态源：页签条渲染它，KeepAliveOutlet 据此决定哪些页面保持挂载。
 * 只存页签键（相对子路径），切换账套后同一批页面跟着落到新账套（旧账套的面板由 cachedPaths 汰换）。
 */
export function RouteTabsProvider({ children }: { children: ReactNode }) {
    const { accountSetId = "" } = useParams();
    const base = `/${accountSetId}`;
    const location = useLocation();
    const navigate = useNavigate();
    const [tabs, setTabs] = useState<string[]>(loadTabs);

    const activeKey = tabKeyOf(location.pathname, accountSetId);

    useEffect(() => {
        if (!Object.hasOwn(ROUTE_TITLES, activeKey)) return;
        setTabs((prev) => (prev.includes(activeKey) ? prev : [...prev, activeKey].slice(-MAX_TABS)));
    }, [activeKey]);

    useEffect(() => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(tabs));
    }, [tabs]);

    const closeTab = useCallback(
        (key: string) => {
            const index = tabs.indexOf(key);
            const neighbor = tabs[index - 1] ?? tabs[index + 1];
            setTabs((prev) => prev.filter((item) => item !== key));
            // 关掉当前页签后落到相邻页签：仍属页签切换，保留对方现场。
            if (key === activeKey) {
                // 报税分类没有首页，没有相邻页签时退回该分类的第一个页面。
                const category = findCategoryOf(key);
                const fallback = category.home === false ? `${base}/${category.to}` : base;
                navigate(neighbor ? `${base}/${neighbor}` : fallback, { state: KEEP_ALIVE_STATE });
            }
        },
        [tabs, activeKey, base, navigate],
    );

    const cachedPaths = useMemo(() => new Set([base, ...tabs.map((key) => `${base}/${key}`)]), [base, tabs]);

    const value = useMemo<RouteTabsValue>(
        () => ({ tabs, activeKey, cachedPaths, closeTab }),
        [tabs, activeKey, cachedPaths, closeTab],
    );

    return <RouteTabsContext value={value}>{children}</RouteTabsContext>;
}
