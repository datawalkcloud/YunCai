import type { ReactNode } from "react"

import { Alert, Spin } from "antd"

import { cn } from "@/lib/utils"
import { SectionCard } from "@/components/page/section"
import { SOFT_PANEL } from "@/components/page/styles"

export interface ReportPageShellProps {
    /** 报表名：卡片区顶端的打印标题。 */
    title: string
    /** 卡片区右上角的表单模板角标，如「会企01表」。 */
    template?: ReactNode
    /** 期间选择区（工具条左段）。 */
    period?: ReactNode
    /** 操作按钮区（跟在期间选择区之后）。 */
    actions?: ReactNode
    /** 工具条右侧的元信息（编制单位、期间、金额单位等）。 */
    meta?: ReactNode
    /** 首次加载且尚无数据时显示居中占位。 */
    loading?: boolean
    /** 接口返回的中文错误，直接展示。 */
    error?: string | null
    /** 非加载中又拿不出数据时的提示，如「请先选择账套。」。 */
    placeholder?: ReactNode
    /** 表格下方的补充说明，如试算平衡结果。 */
    footer?: ReactNode
    children: ReactNode
}

/**
 * 报表页统一骨架：白色卡片（打印标题 / 工具条 / 错误 / 正文 / 补充说明）。
 * 取数与行次归集留在各报表页里，外壳只负责布局与状态占位。
 */
export function ReportPageShell({
    title,
    template,
    period,
    actions,
    meta,
    loading,
    error,
    placeholder,
    footer,
    children,
}: ReportPageShellProps) {
    return (
        <div className="flex flex-col gap-4">
            <SectionCard bodyClassName="flex flex-col gap-4">
                <div className="relative text-center">
                    <div className="text-[17px] font-bold text-slate-800">{title}</div>
                    <div className="absolute right-0 top-0 text-[12.5px] text-slate-400">{template}</div>
                </div>

                <div className={cn(SOFT_PANEL, "flex flex-wrap items-center justify-between gap-2 px-4 py-3")}>
                    <div className="flex flex-wrap items-center gap-1.5">
                        {period}
                        {actions}
                    </div>
                    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[12.5px] text-slate-400">
                        {meta}
                    </div>
                </div>

                {error && <Alert type="error" showIcon title={error} />}

                {loading ? (
                    <div className="flex items-center justify-center py-16">
                        <Spin />
                    </div>
                ) : placeholder != null ? (
                    <div className="py-16 text-center text-[13px] text-slate-400">{placeholder}</div>
                ) : (
                    children
                )}

                {footer}
            </SectionCard>
        </div>
    )
}
