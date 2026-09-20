import dayjs from "dayjs"
import { Calendar as CalendarIcon, X } from "lucide-react"
import { type DateRange } from "react-day-picker"
import { zhCN } from "react-day-picker/locale"

import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import { Calendar } from "@/components/ui/calendar"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"

// react-day-picker v10 只做「日」粒度选择，中文文案用其自带的 zhCN locale。
const locale = zhCN

interface FieldProps {
  /** 当前值；undefined 表示未选择。 */
  value?: Date
  /** 选择回调，清除时回传 undefined。 */
  onChange?: (value?: Date) => void
  placeholder?: string
  disabled?: boolean
  /** 已选择时是否给出清除入口，对应 antd 的 allowClear。 */
  allowClear?: boolean
  className?: string
}

/** 触发按钮 + 可选清除按钮；label 为空即未选择。 */
function PickerTrigger({
  label,
  placeholder,
  disabled,
  allowClear,
  onClear,
  className,
}: {
  label: string
  placeholder: string
  disabled?: boolean
  allowClear?: boolean
  onClear: () => void
  className?: string
}) {
  return (
    <div className={cn("flex items-center gap-1", className)}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          disabled={disabled}
          className={cn("w-full justify-start px-2.5 font-normal", !label && "text-muted-foreground")}
        >
          <CalendarIcon data-icon="inline-start" />
          {label || placeholder}
        </Button>
      </PopoverTrigger>
      {allowClear && Boolean(label) && !disabled && (
        <Button type="button" variant="ghost" size="icon-xs" onClick={onClear} title="清除">
          <X />
        </Button>
      )}
    </div>
  )
}

/** 单个日期选择器，替代 antd DatePicker。 */
export function DateField({
  value,
  onChange,
  placeholder = "请选择日期",
  disabled,
  allowClear,
  className,
}: FieldProps) {
  return (
    <Popover>
      <PickerTrigger
        className={className}
        label={value ? dayjs(value).format("YYYY-MM-DD") : ""}
        placeholder={placeholder}
        disabled={disabled}
        allowClear={allowClear}
        onClear={() => onChange?.(undefined)}
      />
      <PopoverContent align="start" className="w-auto p-0">
        <Calendar
          mode="single"
          locale={locale}
          captionLayout="dropdown"
          selected={value}
          defaultMonth={value}
          onSelect={(d) => onChange?.(d ?? undefined)}
        />
      </PopoverContent>
    </Popover>
  )
}

/** 期间（年 + 月）选择器，替代 antd DatePicker picker="month"。 */
export function PeriodField({
  value,
  onChange,
  placeholder = "请选择期间",
  disabled,
  allowClear,
  className,
}: FieldProps) {
  return (
    <Popover>
      <PickerTrigger
        className={className}
        label={value ? dayjs(value).format("YYYY年M月") : ""}
        placeholder={placeholder}
        disabled={disabled}
        allowClear={allowClear}
        onClear={() => onChange?.(undefined)}
      />
      <PopoverContent align="start" className="w-auto p-0">
        <Calendar
          mode="single"
          locale={locale}
          captionLayout="dropdown"
          selected={value}
          defaultMonth={value}
          // v10 无「月」粒度选项，落值统一归一到当月 1 日，只取年月。
          onSelect={(d) => onChange?.(d ? dayjs(d).startOf("month").toDate() : undefined)}
        />
      </PopoverContent>
    </Popover>
  )
}

/** 日期区间选择器，替代 antd DatePicker.RangePicker。 */
export function DateRangeField({
  value,
  onChange,
  placeholder = "请选择区间",
  disabled,
  allowClear = true,
  className,
}: {
  value?: DateRange
  onChange?: (value?: DateRange) => void
  placeholder?: string
  disabled?: boolean
  allowClear?: boolean
  className?: string
}) {
  const label = value?.from
    ? `${dayjs(value.from).format("YYYY-MM-DD")} ~ ${value.to ? dayjs(value.to).format("YYYY-MM-DD") : ""}`
    : ""

  return (
    <Popover>
      <PickerTrigger
        className={className}
        label={label}
        placeholder={placeholder}
        disabled={disabled}
        allowClear={allowClear}
        onClear={() => onChange?.(undefined)}
      />
      <PopoverContent align="start" className="w-auto p-0">
        <Calendar
          mode="range"
          numberOfMonths={2}
          locale={locale}
          captionLayout="dropdown"
          selected={value}
          defaultMonth={value?.from}
          onSelect={(range) => onChange?.(range ?? undefined)}
        />
      </PopoverContent>
    </Popover>
  )
}

/** 把 yyyy-MM-dd 之类字符串解析为 Date，非法值返回 undefined。 */
export function parseDate(input?: string | null): Date | undefined {
  if (!input) return undefined
  const d = dayjs(input)
  return d.isValid() ? d.toDate() : undefined
}
