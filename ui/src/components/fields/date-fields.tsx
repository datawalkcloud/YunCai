import dayjs from "dayjs"
import { DatePicker } from "antd"

/** 日期区间；两端均可缺省，沿用调用方既有的形状。 */
export interface DateRange {
    from?: Date
    to?: Date
}

interface FieldProps {
    /** 当前值；undefined 表示未选择。对外统一用 Date，页面无需感知 dayjs。 */
    value?: Date
    /** 选择回调，清除时回传 undefined。 */
    onChange?: (value?: Date) => void
    placeholder?: string
    disabled?: boolean
    allowClear?: boolean
    className?: string
}

/** 单个日期选择器。 */
export function DateField({
    value,
    onChange,
    placeholder = "请选择日期",
    disabled,
    allowClear,
    className,
}: FieldProps) {
    return (
        <DatePicker
            className={className}
            placeholder={placeholder}
            disabled={disabled}
            allowClear={allowClear}
            value={value ? dayjs(value) : null}
            onChange={(d) => onChange?.(d ? d.toDate() : undefined)}
        />
    )
}

/** 期间（年 + 月）选择器；落值归一到当月 1 日，只取年月。 */
export function PeriodField({
    value,
    onChange,
    placeholder = "请选择期间",
    disabled,
    allowClear,
    className,
}: FieldProps) {
    return (
        <DatePicker
            className={className}
            picker="month"
            format="YYYY年M月"
            placeholder={placeholder}
            disabled={disabled}
            allowClear={allowClear}
            value={value ? dayjs(value) : null}
            onChange={(d) => onChange?.(d ? d.startOf("month").toDate() : undefined)}
        />
    )
}

/** 日期区间选择器。 */
export function DateRangeField({
    value,
    onChange,
    placeholder,
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
    return (
        <DatePicker.RangePicker
            className={className}
            placeholder={placeholder ? [placeholder, placeholder] : ["开始日期", "结束日期"]}
            disabled={disabled}
            allowClear={allowClear}
            value={[value?.from ? dayjs(value.from) : null, value?.to ? dayjs(value.to) : null]}
            onChange={(dates) => {
                const [from, to] = dates ?? []
                if (!from && !to) {
                    onChange?.(undefined)
                    return
                }
                onChange?.({ from: from?.toDate(), to: to?.toDate() })
            }}
        />
    )
}

/** 把 yyyy-MM-dd 之类字符串解析为 Date，非法值返回 undefined。 */
export function parseDate(input?: string | null): Date | undefined {
    if (!input) return undefined
    const d = dayjs(input)
    return d.isValid() ? d.toDate() : undefined
}
