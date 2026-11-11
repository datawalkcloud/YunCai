import { useCallback } from "react"
import type { ReactNode } from "react"

import dayjs from "dayjs"

import { PeriodStepper } from "@/pages/lemon/components/ui"

/** 报表期间（年 + 月）。 */
export interface ReportPeriod {
    year: number
    month: number
}

export interface ReportPeriodToolbarProps {
    /** 当前期间；后端首次解析出期间之前为 null。 */
    value: ReportPeriod | null
    /** 可选期间下界（含），通常为账套启用期间。 */
    min?: Date
    /** 可选期间上界（含），通常为当前自然月。 */
    max: Date
    onChange: (value: ReportPeriod) => void
    /** 右侧操作按钮区（打印 / 导出 / 刷新等）。 */
    actions?: ReactNode
    /** 工具条下方的元信息（编制单位、期间、金额单位等）。 */
    meta?: ReactNode
    /** 首次取数中：期间尚未解析出来时显示「加载中…」，失败后显示「未取到期间」。 */
    loading?: boolean
    disabled?: boolean
}

const toMonthStart = (p: ReportPeriod | null) =>
    p ? dayjs(`${p.year}-${String(p.month).padStart(2, "0")}-01`) : null

/**
 * 报表页顶部期间工具条：左侧「◀ 年月 ▶」步进器（点期间文字弹出月份面板，范围限制在
 * 账套启用期间 ~ 当前自然月），右侧放页面自己的操作按钮，下方一行元信息。
 * 资产负债表与利润表共用同一套排布。
 */
export function ReportPeriodToolbar({
    value,
    min,
    max,
    onChange,
    actions,
    meta,
    loading,
    disabled,
}: ReportPeriodToolbarProps) {
    const current = toMonthStart(value)
    // 期间取自报表接口；接口失败时不再显示一条含义不明的横线。
    const periodLabel = value ? `${value.year}年${value.month}月` : loading ? "加载中…" : "未取到期间"
    const canPrev = current != null && (min == null || current.isAfter(dayjs(min), "month"))
    const canNext = current != null && current.isBefore(dayjs(max), "month")
    const step = useCallback(
        (delta: number) => {
            if (current == null) return
            const next = current.add(delta, "month")
            onChange({ year: next.year(), month: next.month() + 1 })
        },
        [current, onChange],
    )

    return (
        <div className="flex flex-col gap-2">
            <div className="flex flex-wrap items-center justify-between gap-2">
                <PeriodStepper
                    text={periodLabel}
                    onPrev={canPrev ? () => step(-1) : undefined}
                    onNext={canNext ? () => step(1) : undefined}
                    value={value}
                    onPick={(p) => onChange({ year: p.year, month: p.month })}
                    min={min ? dayjs(min) : undefined}
                    max={dayjs(max)}
                    disabled={disabled}
                />
                {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
            </div>
            {meta && (
                <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[12.5px] text-(--muted-foreground)">{meta}</div>
            )}
        </div>
    )
}
