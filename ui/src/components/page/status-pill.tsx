import type { ReactNode } from "react"

import { Tag } from "antd"

import { type PillTone } from "./styles"

/** 状态标签色调 → antd Tag 预设色：直接用库自带的浅底彩签，不再自己上色。 */
const PRESET: Record<PillTone, string> = {
    blue: "blue",
    green: "green",
    purple: "purple",
    amber: "orange",
    red: "red",
    gray: "default",
}

/**
 * 状态标签：账页与凭证列表共用的小号标签。
 * 外观完全交给 antd Tag 预设色，深浅底、描边、留白都跟随全局主题，组件不再自己覆盖。
 */
export function StatusPill({
    tone,
    children,
    className,
}: {
    tone: PillTone
    children: ReactNode
    className?: string
}) {
    return (
        <Tag className={className} color={PRESET[tone]}>
            {children}
        </Tag>
    )
}
