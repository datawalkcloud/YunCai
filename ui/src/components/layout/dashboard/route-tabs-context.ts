import { createContext, useContext } from "react";
import { setPath } from "@/accountSets/url-id";

/** 多页签状态（由 RouteTabsProvider 提供，页签条与 KeepAliveOutlet 共享）。 */
export interface RouteTabsValue {
    /** 已打开页面的页签键（账套下的相对子路径，不含首页，按打开顺序）。 */
    tabs: readonly string[];
    /** 当前页签键，首页为空串。 */
    activeKey: string;
    /** keep-alive 需要保持挂载的完整路径（含首页）。 */
    cachedPaths: ReadonlySet<string>;
    closeTab: (key: string) => void;
}

export const RouteTabsContext = createContext<RouteTabsValue | null>(null);

/** 路由第一段（账套）之后的页签键；首页为空串。 */
export function tabKeyOf(pathname: string, accountSetId: string): string {
    const base = setPath(accountSetId);
    if (!pathname.startsWith(base)) return "";
    return pathname.slice(base.length).replace(/^\//, "");
}

export function useRouteTabs(): RouteTabsValue {
    const ctx = useContext(RouteTabsContext);
    if (!ctx) {
        throw new Error("useRouteTabs 必须在 RouteTabsProvider 内使用");
    }
    return ctx;
}

/** 面板是否处于激活状态。keep-alive 缓存的页面切走后仍挂载，需据此停掉全局快捷键等副作用。 */
export const KeepAliveActiveContext = createContext(true);

export function useKeepAliveActive(): boolean {
    return useContext(KeepAliveActiveContext);
}

/** 页签点击 = 切回已打开的页面（保持现场）；菜单、页内按钮等跳转不带它，照旧重新挂载取数。 */
export const KEEP_ALIVE_STATE = { keepAlive: true } as const;

/** 本次路由跳转是否来自顶部页签。 */
export function isKeepAliveLocation(state: unknown): boolean {
    return Boolean((state as { keepAlive?: boolean } | null)?.keepAlive);
}
