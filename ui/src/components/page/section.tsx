import type { ReactNode } from "react"

import { Card } from "antd"

export interface SectionCardProps {
    title?: ReactNode
    /** 标题右侧的操作区，如筛选器、导出按钮。 */
    action?: ReactNode
    children: ReactNode
    className?: string
    /** 正文内部的排列（如 flex flex-col gap-4）。只放布局类，留白与外观一律用 antd 默认。 */
    bodyClassName?: string
}

/**
 * 内容卡：小标题 + 右上角操作 + 正文。
 * 描边、投影、圆角由 main.tsx 的 Card token 与 index.css 给一次，正文留白用 antd 默认的 24px，组件本身不再覆盖。
 */
export function SectionCard({ title, action, children, className, bodyClassName }: SectionCardProps) {
    const hasHeader = title != null || action != null
    return (
        <Card
            className={className}
            title={
                hasHeader ? <span className="flex min-w-0 flex-wrap items-center gap-2">{title}</span> : undefined
            }
            extra={action}
        >
            {bodyClassName ? <div className={bodyClassName}>{children}</div> : children}
        </Card>
    )
}
