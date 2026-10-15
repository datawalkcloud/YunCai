import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

export interface FormFieldProps {
    label: ReactNode
    /** 控件的 id，用于 label 关联。 */
    htmlFor?: string
    /** 必填标记（红色星号），只做视觉提示。 */
    required?: boolean
    /** 控件下方的浅色说明文案。 */
    description?: ReactNode
    /** 校验错误文案，非空时以错误色显示。 */
    error?: ReactNode
    children: ReactNode
    className?: string
}

/**
 * 表单字段骨架：标签 + 控件 + 说明/错误。控件本身交给 antd，这里只负责排版，
 * 因此校验状态仍由各页面自己维护（不必引入 antd Form 上下文）。
 */
export function FormField({
    label,
    htmlFor,
    required,
    description,
    error,
    children,
    className,
}: FormFieldProps) {
    return (
        <div className={cn("flex w-full flex-col gap-2", className)}>
            <label
                htmlFor={htmlFor}
                className="flex w-fit items-center gap-1 text-sm font-medium leading-none text-slate-700"
            >
                {label}
                {required && <span className="text-[#ef4444]">*</span>}
            </label>
            {children}
            {description && (
                <p className="text-sm leading-normal font-normal text-slate-400">{description}</p>
            )}
            {error && (
                <p role="alert" className="text-sm font-normal text-[#ef4444]">
                    {error}
                </p>
            )}
        </div>
    )
}
