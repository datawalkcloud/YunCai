import type { ReactNode } from "react"

import { Tag } from "antd"

import { PILL, type PillTone } from "./styles"

/**
 * 状态标签：账页与凭证列表共用的小号标签，圆角形状跟随 antd 全局主题。
 * 色调走内联样式而非类名，避免与 antd Tag 自身注入的背景/描边规则争夺优先级。
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
        <Tag
            className={className}
            variant="filled"
            styles={{
                root: {
                    marginInlineEnd: 0,
                    border: "none",
                    padding: "1px 8px",
                    fontSize: 11,
                    lineHeight: "16px",
                    fontWeight: 500,
                    background: PILL[tone].bg,
                    color: PILL[tone].fg,
                },
            }}
        >
            {children}
        </Tag>
    )
}
