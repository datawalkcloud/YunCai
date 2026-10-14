/** 账页专有的显示口径；金额格式化统一走 @/lib/format，此处不另设实现。 */
import { ROW_KIND } from "./types"

export { fmtAmount } from "@/lib/format"

/** 金额列统一右对齐并使用等宽数字。 */
export const amountCls = "text-right tabular-nums"

/** 期初 / 合计行与逐笔行区分开，避免误读成发生额；各分段账页共用此口径。 */
export function kindRowClass(kind?: number): string | undefined {
    if (kind === ROW_KIND.opening) return "text-slate-400"
    if (kind === ROW_KIND.yearTotal) return "bg-[#f1f5f9] font-medium"
    if (kind === ROW_KIND.monthTotal) return "bg-muted"
    return undefined
}
