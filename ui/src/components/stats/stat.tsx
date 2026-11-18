import { Statistic } from "antd"

import { SectionCard } from "@/components/page/section"

/**
 * 指标展示：antd Card + Statistic，多个页面共用，避免各写一份。
 * precision 只做小数位补齐，不额外加千分位以外的修饰。
 */
export function Stat({
  label,
  value,
  precision,
  prefix,
  muted,
  className,
}: {
  label: string
  value: number | string
  precision?: number
  prefix?: string
  muted?: boolean
  className?: string
}) {
  const display =
    typeof value === "number" && precision !== undefined
      ? value.toFixed(precision)
      : String(value)

  return (
    <SectionCard className={className}>
      {/* 字号与留白用 antd Statistic 默认，正文颜色跟随全局文字色；muted 只是把整块压暗一档。 */}
      <Statistic
        title={label}
        value={display}
        prefix={prefix}
        className={muted ? "text-(--muted-foreground)" : undefined}
      />
    </SectionCard>
  )
}
