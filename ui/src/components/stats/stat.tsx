import { cn } from "@/lib/utils"
import { Card, CardContent } from "@/components/ui/card"

/**
 * 指标展示，替代 antd Statistic。多个页面共用，避免各写一份。
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
    <Card className={className}>
      <CardContent>
        <div className="text-muted-foreground text-sm">{label}</div>
        <div
          className={cn(
            "mt-1 text-2xl font-medium tabular-nums",
            muted && "text-muted-foreground",
          )}
        >
          {prefix}
          {display}
        </div>
      </CardContent>
    </Card>
  )
}
