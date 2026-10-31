import type { ReactNode } from "react"

import { Card } from "antd"

import { cn } from "@/lib/utils"

/** 页头横幅：渐变底 + 图标块 + 标题说明，右侧放本页主动作。 */
export function PageHeader({
    icon: Icon,
    title,
    subtitle,
    actions,
    className,
}: {
    icon: React.ComponentType<{ className?: string }>
    title: ReactNode
    subtitle?: ReactNode
    actions?: ReactNode
    className?: string
}) {
    return (
        <section
            className={cn(
                "bg-gradient-to-r from-[#e7f0ff] via-[#eef4ff] to-[#f6f9ff] px-5 py-4",
                className,
            )}
        >
            <div className="flex flex-wrap items-center gap-3">
                <span className="flex size-10 shrink-0 items-center justify-center bg-gradient-to-br from-[#5b9bff] to-[#2f6bff] text-white shadow-[0_6px_16px_rgba(47,107,255,0.28)] [&_.anticon]:text-[20px]">
                    <Icon className="size-5" />
                </span>
                <div className="min-w-0">
                    <h1 className="text-[18px] font-bold text-slate-800">{title}</h1>
                    {subtitle && <p className="mt-0.5 text-[12.5px] text-slate-500">{subtitle}</p>}
                </div>
                {actions && <div className="ml-auto flex flex-wrap items-center gap-2">{actions}</div>}
            </div>
        </section>
    )
}

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
