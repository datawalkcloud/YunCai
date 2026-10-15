import { InputNumber } from "antd"
import { cn } from "@/lib/utils"

/**
 * 金额/数字输入。对外仍是 Date-free 的 number 契约：空值回传 undefined，
 * precision 决定落值精度与步进，调用方无需接触 dayjs/antd 类型。
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
    return (
        <InputNumber
            id={id}
            className={cn("w-24 [&>input]:text-right [&>input]:tabular-nums", className)}
            placeholder={placeholder}
            disabled={disabled}
            min={min}
            max={max}
            precision={precision}
            controls={false}
            value={value ?? null}
            onChange={(v) => onChange?.(v == null ? undefined : Number(v))}
        />
    )
}
