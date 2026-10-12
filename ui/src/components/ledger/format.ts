/** 账页金额显示口径，与科目余额表保持一致。 */
import { ROW_KIND } from "./types"

/** 金额格式化：0 显示为空，其余按千分位保留两位小数。 */
export function fmtAmount(value: number | null | undefined, force = false): string {
    if ((value ?? 0) === 0 && !force) return ""
    return (value ?? 0).toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

/** 金额列统一右对齐并使用等宽数字。 */
export const amountCls = "text-right tabular-nums"

/** 期初 / 合计行与逐笔行区分开，避免误读成发生额；各分段账页共用此口径。 */
export function kindRowClass(kind?: number): string | undefined {
    if (kind === ROW_KIND.opening) return "text-slate-400"
    if (kind === ROW_KIND.yearTotal) return "bg-[#f1f5f9] font-medium"
    if (kind === ROW_KIND.monthTotal) return "bg-muted"
    return undefined
}
