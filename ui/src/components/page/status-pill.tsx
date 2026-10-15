import type { ReactNode } from "react"

import { Tag } from "antd"

import { PILL, type PillTone } from "./styles"

/**
 * 状态胶囊：账页与凭证列表共用的小号圆角标签。
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
                    borderRadius: 999,
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
