import { useEffect, useState, type ReactNode } from "react";
import { useLocation, useOutlet } from "react-router";

import {
    isKeepAliveLocation,
    KeepAliveActiveContext,
    useRouteTabs,
} from "./route-tabs-context";

interface CachedPane {
    path: string;
    /** 换 key 才能让「重新进页」的跳转真正重新挂载，而不是就地更新。 */
    generation: number;
    /** 该面板本次挂载对应的 location.key，用来判断是不是一次新的跳转。 */
    enteredAt: string;
    node: ReactNode;
}

/**
 * Keep-alive 出口：打开过的页面只挂载一次，切走时隐藏、切回时原样复用，不重新挂载也不重新取数。
 * 只有菜单/页内按钮等「重新进页」的跳转才重建目标页面，页签关闭后对应面板随之卸载。
 */
export function KeepAliveOutlet() {
    const location = useLocation();
    const outlet = useOutlet();
    const { cachedPaths } = useRouteTabs();
    const path = location.pathname;
    const keepAlive = isKeepAliveLocation(location.state);
    const [panes, setPanes] = useState<CachedPane[]>([]);

    const current = panes.find((pane) => pane.path === path);

    if (!current) {
        // 首次进入该路径：直接入栈，本次渲染即可出内容（render 期调整 state，不闪空白）。
        setPanes((prev) =>
            prev.some((pane) => pane.path === path)
                ? prev
                : [...prev, { path, generation: 0, enteredAt: location.key, node: outlet }],
        );
    } else if (!keepAlive && current.enteredAt !== location.key) {
        // 非页签跳转（菜单、页内按钮、深链参数变化）：用本次的新 outlet 重建，维持「进页面就取数」。
        setPanes((prev) =>
            prev.map((pane) =>
                pane.path === path && pane.enteredAt !== location.key
                    ? { path, generation: pane.generation + 1, enteredAt: location.key, node: outlet }
                    : pane,
            ),
        );
    }

    useEffect(() => {
        setPanes((prev) => {
            const next = prev.filter((pane) => pane.path === path || cachedPaths.has(pane.path));
            return next.length === prev.length ? prev : next;
        });
    }, [path, cachedPaths]);

    return (
        <>
            {panes.map((pane) => (
                <div
                    // display:contents 让当前面板的根节点与以前直接挂在 main 下等价，页面 min-h-full 不受影响。
                    key={`${pane.path}#${pane.generation}`}
                    style={{ display: pane.path === path ? "contents" : "none" }}
                >
                    <KeepAliveActiveContext value={pane.path === path}>{pane.node}</KeepAliveActiveContext>
                </div>
            ))}
        </>
    );
}
