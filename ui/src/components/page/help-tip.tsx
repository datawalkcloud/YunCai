import type { ReactNode } from "react"
import { Tooltip } from "antd"
import { InfoCircleOutlined, QuestionCircleOutlined } from "@ant-design/icons"

/**
 * 字段/功能旁的说明图标：悬停或键盘聚焦时显示完整说明。
 * 原生 title 的提示延迟长、样式与全站不一致，也不支持键盘，统一走 antd Tooltip。
 */
export function HelpTip({
    title,
    kind = "question",
    className = "",
}: {
    title: ReactNode
    kind?: "question" | "info"
    className?: string
}) {
    if (!title) return null
    const Icon = kind === "info" ? InfoCircleOutlined : QuestionCircleOutlined
    return (
        <Tooltip title={title} trigger={["hover", "focus"]}>
            <Icon
                tabIndex={0}
                aria-label={typeof title === "string" ? title : "功能说明"}
                className={`cursor-help text-[12px] text-(--muted-foreground) ${className}`}
            />
        </Tooltip>
    )
}
