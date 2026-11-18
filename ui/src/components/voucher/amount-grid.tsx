import { useEffect, useRef, useState } from "react"
import type { CSSProperties, KeyboardEvent as ReactKeyboardEvent, MouseEvent as ReactMouseEvent } from "react"

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

/** 分位竖线：元→角、千→百、百万→十万处用强调色分隔，其余跟纸样格线同色；末列（分）不画线。 */
function digitCellCls(i: number): string {
    if (i >= 10) return ""
    if (i === 8) return "border-r border-destructive/25"
    if (i === 5 || i === 2) return "border-r border-primary/22"
    return "border-r border-(--sheet-line)"
}

/** 借方 / 贷方金额 11 位分位列头；divider 时在刻度带上方补一条横线，与纸质凭证的两段式表头一致。 */
export function AmountColumnHeader({ idPrefix, divider }: { idPrefix: string; divider?: boolean }) {
    return (
        <div className={cn("grid grid-cols-11", divider && "border-t border-(--sheet-line)")}>
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
export function AmountHeaderCell({
    title,
    idPrefix,
    height,
    divider,
}: {
    title: string
    idPrefix: string
    /** 表头单元格总高（px），留空则按内容自适应。 */
    height?: number
    /** 是否在刻度带上方画分隔线。 */
    divider?: boolean
}) {
    return (
        <div className="flex flex-col" style={height ? { height } : undefined}>
            <span className={cn("flex items-center justify-center text-[13px] font-medium text-foreground", height && "flex-1")}>
                {title}
            </span>
            <AmountColumnHeader idPrefix={idPrefix} divider={divider} />
        </div>
    )
}

/** 覆盖在分位格上的输入框附加属性（数据标记与键盘事件由调用方接管）。 */
export interface AmountInputAttrs {
    "data-nav"?: string
    "data-col"?: string
    onKeyDown?: (e: ReactKeyboardEvent<HTMLInputElement>) => void
    onDoubleClick?: (e: ReactMouseEvent<HTMLInputElement>) => void
    onFocus?: () => void
    onBlur?: () => void
    style?: CSSProperties
}

/**
 * 借方 / 贷方金额分位小格；点击即整组进入编辑——分位格合为一格、绿框选中、原值全选，输入即替换。
 * 只读时（凭证预览、合计行）按同一分位刻度展示，负数用红字。
 */
export function AmountCells({
    value,
    editable,
    raw,
    onChange,
    height = 54,
    inputAttrs,
    onInputFocus,
}: {
    value: number
    editable?: boolean
    raw?: string
    onChange?: (v: string) => void
    /** 分位格行高（px）。 */
    height?: number
    /** 输入框上的数据标记与键盘事件。 */
    inputAttrs?: AmountInputAttrs
    /** 输入框聚焦的额外回调（组件内部的编辑态仍会生效）。 */
    onInputFocus?: () => void
}) {
    const [editing, setEditing] = useState(false)
    /** 空金额进入编辑时先显示 0.00 并全选；只影响显示，不写入行数据，避免污染撤销历史。 */
    const [zeroHint, setZeroHint] = useState(false)
    const inputRef = useRef<HTMLInputElement>(null)
    useEffect(() => {
        if (zeroHint) inputRef.current?.select()
    }, [zeroHint])
    const cells = digitCells(value)
    const isNegative = value < 0
    const text = raw ?? ""
    return (
        <div className="relative">
            <div className={cn("grid grid-cols-11", isNegative && "text-destructive")} style={{ height }}>
                {cells.map((d, i) => (
                    <div key={i} className={cn("flex items-center justify-center text-[13px] tabular-nums", digitCellCls(i))}>
                        {editing ? "" : d}
                    </div>
                ))}
            </div>
            {editable && (
                <input
                    ref={inputRef}
                    inputMode="decimal"
                    {...inputAttrs}
                    value={zeroHint && text === "" ? "0.00" : text}
                    onChange={(e) => {
                        setZeroHint(false)
                        onChange?.(sanitizeAmount(e.target.value))
                    }}
                    onFocus={() => {
                        setEditing(true)
                        setZeroHint(text === "")
                        inputAttrs?.onFocus?.()
                        onInputFocus?.()
                    }}
                    onBlur={() => {
                        setEditing(false)
                        setZeroHint(false)
                        inputAttrs?.onBlur?.()
                    }}
                    className={cn(
                        "absolute inset-0 h-full w-full text-[13px] outline-none",
                        // 编辑态用不透明底色盖住分位竖线、整组并成一格；静止态必须透明，否则会盖掉格线。
                        editing
                            ? "rounded-[2px] border-2 border-(--success) bg-(--card) pl-3 text-left text-foreground caret-primary"
                            : "bg-transparent px-2 text-right text-transparent caret-transparent",
                    )}
                />
            )}
        </div>
    )
}
