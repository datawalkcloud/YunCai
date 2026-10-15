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
    <SectionCard className={className} bodyClassName="p-4">
      <Statistic
        title={label}
        value={display}
        prefix={prefix}
        styles={{
          title: { fontSize: 14 },
          content: {
            marginTop: 4,
            fontSize: 24,
            fontWeight: 500,
            fontVariantNumeric: "tabular-nums",
            color: muted ? "#94a3b8" : "#1e293b",
          },
        }}
      />
    </SectionCard>
  )
}
