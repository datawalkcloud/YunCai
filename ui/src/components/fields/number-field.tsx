import { cn } from "@/lib/utils"
import { Input } from "@/components/ui/input"

/**
 * 数字输入，替代 antd InputNumber。沿用原生 number 输入的浏览器校验，
 * precision 只控制步进与落值精度。
 */
export function NumberField({
  value,
  onChange,
  precision = 0,
  min,
  max,
  disabled,
  placeholder,
  className,
  id,
}: {
  value?: number
  onChange?: (value?: number) => void
  precision?: number
  min?: number
  max?: number
  disabled?: boolean
  placeholder?: string
  className?: string
  id?: string
}) {
  const step = precision > 0 ? "0." + "0".repeat(precision - 1) + "1" : "1"

  return (
    <Input
      id={id}
      type="number"
      step={step}
      min={min}
      max={max}
      disabled={disabled}
      placeholder={placeholder}
      className={cn("text-right tabular-nums", className)}
      value={value === undefined ? "" : String(value)}
      onChange={(e) => {
        const raw = e.target.value
        if (raw === "") {
          onChange?.(undefined)
          return
        }
        const parsed = Number(raw)
        onChange?.(Number.isNaN(parsed) ? undefined : Number(parsed.toFixed(precision)))
      }}
    />
  )
}
