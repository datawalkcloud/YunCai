import type { ReactNode } from "react"

import { Card } from "antd"

import { cn } from "@/lib/utils"

export interface SectionCardProps {
    title?: ReactNode
    /** 标题右侧的操作区，如筛选器、导出按钮。 */
    action?: ReactNode
    children: ReactNode
    className?: string
    /** 覆盖正文内边距，例如需要表格通栏时传 "p-0"。 */
    bodyClassName?: string
}

/** 白色内容卡：小标题 + 右上角操作 + 正文；圆角由 antd 全局主题统一控制。 */
export function SectionCard({ title, action, children, className, bodyClassName }: SectionCardProps) {
    const hasHeader = title != null || action != null
    return (
        <Card
            className={className}
            title={
                hasHeader ? <span className="flex min-w-0 flex-wrap items-center gap-2">{title}</span> : undefined
            }
            extra={action}
            styles={{
                // antd 的 .ant-card .ant-card-* 是两级类名、压过 Tailwind，故描边/阴影/留白只能内联覆盖；正文 padding 归零后交给 bodyClassName。
                root: {
                    border: "1px solid #f1f5f9",
                    boxShadow: "0 1px 2px rgba(16,24,40,0.04)",
                },
                header: {
                    padding: "16px 16px 0",
                    borderBottom: "none",
                    marginBottom: 0,
                    minHeight: 0,
                    background: "transparent",
                },
                title: { whiteSpace: "normal", overflow: "visible" },
                body: { padding: 0 },
            }}
        >
            <div className={cn(hasHeader ? "px-4 pb-4 pt-3" : "p-4", bodyClassName)}>{children}</div>
        </Card>
    )
}
