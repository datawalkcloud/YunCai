import { useState } from "react"

import { cn } from "@/lib/utils"

/** 金额分位列，从高位到低位（最高支持到亿位，角、分为小数位）。 */
const AMOUNT_COLUMNS = ["亿", "千", "百", "十", "万", "千", "百", "十", "元", "角", "分"]

/** 将金额按分位拆解为与 AMOUNT_COLUMNS 对齐的字符数组，0 时全部留空。 */
function digitCells(value: number): string[] {
    const cells = Array<string>(AMOUNT_COLUMNS.length).fill("")
    if (value === 0) return cells
    const absValue = Math.abs(value)
    const cents = Math.round(absValue * 100)
    if (cents <= 0) return cells
    const str = String(cents)
    for (let i = 0; i < str.length && i < cells.length; i++) {
        cells[cells.length - 1 - i] = str[str.length - 1 - i]
    }
    return cells
}

/** 规整金额输入：仅数字与一个小数点，且小数点后最多两位。 */
function sanitizeAmount(v: string): string {
    let s = v.replace(/[^\d.]/g, "")
    const dot = s.indexOf(".")
    if (dot !== -1) {
        const intPart = s.slice(0, dot).slice(0, 9)
        const decPart = s.slice(dot + 1).replace(/\./g, "").slice(0, 2)
        s = intPart + "." + decPart
    } else {
        s = s.slice(0, 9)
    }
    return s
}

/** 分位竖线：元→角、千→百、百万→十万处用强调色分隔，其余常规边框色；末列（分）不画线。 */
function digitCellCls(i: number): string {
    if (i >= 10) return ""
    if (i === 8) return "border-r border-destructive/40"
    if (i === 5 || i === 2) return "border-r border-primary/40"
    return "border-r border-border"
}

/** 借方 / 贷方金额的 11 个分位表头（与柠檬云相同：单行「亿 千 百 十 万 千 百 十 元 角 分」）。 */
export function AmountColumnHeader({ idPrefix }: { idPrefix: string }) {
    return (
        <div className="grid grid-cols-11">
            {AMOUNT_COLUMNS.map((c, i) => (
                <div
                    key={`${idPrefix}-${i}`}
                    className={cn(
                        "flex h-6 items-center justify-center text-[10px] font-medium text-muted-foreground",
                        digitCellCls(i),
                    )}
                >
                    {c}
                </div>
            ))}
        </div>
    )
}

/** 借方 / 贷方金额列表头：标题在上、11 位分位刻度在下（原表两行表头合并为一个单元格）。 */
export function AmountHeaderCell({ title, idPrefix }: { title: string; idPrefix: string }) {
    return (
        <div className="flex flex-col items-center">
            <span className="text-[13px] font-medium text-foreground">{title}</span>
            <AmountColumnHeader idPrefix={idPrefix} />
        </div>
    )
}

/**
 * 借方 / 贷方金额分位小格；editable 时聚焦可直接输入完整数值，失焦后按位对齐到分位小格。
 * 只读时（凭证预览、合计行）按同一分位刻度展示，负数用红字。
 */
export function AmountCells({
    value,
    editable,
    raw,
    onChange,
}: {
    value: number
    editable?: boolean
    raw?: string
    onChange?: (v: string) => void
}) {
    const [focused, setFocused] = useState(false)
    const cells = digitCells(value)
    const isNegative = value < 0
    return (
        <div className="relative">
            <div className={cn("grid grid-cols-11", isNegative && "text-destructive")}>
                {cells.map((d, i) => (
                    <div key={i} className={cn("flex h-[54px] items-center justify-center tabular-nums", digitCellCls(i))}>
                        {focused ? "" : d}
                    </div>
                ))}
            </div>
            {editable && (
                <input
                    inputMode="decimal"
                    value={raw ?? ""}
                    onChange={(e) => onChange?.(sanitizeAmount(e.target.value))}
                    onFocus={(e) => {
                        setFocused(true)
                        e.target.select()
                    }}
                    onBlur={() => setFocused(false)}
                    className={cn(
                        "absolute inset-0 h-full w-full px-2 text-right outline-none",
                        focused
                            ? "bg-accent/40 text-foreground caret-primary"
                            : "bg-transparent text-transparent caret-transparent",
                    )}
                />
            )}
        </div>
    )
}
