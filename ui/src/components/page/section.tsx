import type { ReactNode } from "react"

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
                "rounded-2xl bg-gradient-to-r from-[#e7f0ff] via-[#eef4ff] to-[#f6f9ff] px-6 py-5",
                className,
            )}
        >
            <div className="flex flex-wrap items-center gap-4">
                <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-[#5b9bff] to-[#2f6bff] text-white shadow-[0_6px_16px_rgba(47,107,255,0.28)]">
                    <Icon className="size-5" />
                </span>
                <div className="min-w-0">
                    <h1 className="text-[20px] font-bold text-slate-800">{title}</h1>
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
    /** 覆盖正文内边距，例如需要表格通栏时传 "px-2 pb-2"。 */
    bodyClassName?: string
}

/** 白色圆角内容卡：小标题 + 右上角操作 + 正文。 */
export function SectionCard({ title, action, children, className, bodyClassName }: SectionCardProps) {
    const hasHeader = title != null || action != null
    return (
        <section
            className={cn(
                "rounded-2xl border border-slate-100 bg-white shadow-[0_1px_2px_rgba(16,24,40,0.04)]",
                className,
            )}
        >
            {hasHeader && (
                <div className="flex flex-wrap items-center justify-between gap-3 px-5 pt-5 pb-4">
                    <div className="flex min-w-0 items-center gap-2">{title}</div>
                    {action}
                </div>
            )}
            <div className={cn(hasHeader ? "px-5 pb-5" : "p-5", bodyClassName)}>{children}</div>
        </section>
    )
}
