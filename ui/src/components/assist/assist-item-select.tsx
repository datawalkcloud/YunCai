import { useMemo } from "react"

import type { AssistItemDto } from "@/apis-gen"
import { cn } from "@/lib/utils"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Spinner } from "@/components/ui/spinner"

import { assistSupportsHierarchy, assistItemSelectable } from "./constants"
import { useAssistItems } from "./assist-store"

/** Radix Select 不接受空串作为选项值，用哨兵表示「未选择」。 */
const NONE = "__assist_none__"

/** 层级型档案按级次缩进，下拉里也能一眼看出上下级关系（全角空格占位）。 */
function optionLabel(item: AssistItemDto, hierarchical: boolean): string {
    const pad = hierarchical && (item.level ?? 1) > 1 ? "　".repeat((item.level ?? 1) - 1) : ""
    return `${pad}${item.code} ${item.name}`
}

/**
 * 单个辅助核算维度的档案选择器：凭证分录、科目设置等处共用。
 * 只列出「启用中的末级档案」（平铺类别本身就是末级），非末级与已停用档案不可选用。
 */
export function AssistItemSelect({
    accountSetId,
    type,
    value,
    onChange,
    token,
    disabled,
    className,
}: {
    accountSetId: string
    /** 辅助核算类别枚举值（1–7）。 */
    type: number
    /** 已选档案 id，空串表示未选择。 */
    value: string
    onChange: (itemId: string) => void
    token?: string | null
    disabled?: boolean
    className?: string
}) {
    const hierarchical = assistSupportsHierarchy(type)
    const { items, loading } = useAssistItems(accountSetId, type, token)

    const options = useMemo(() => {
        const selectable = items.filter((i) => assistItemSelectable(i, hierarchical))
        // 编辑历史凭证时，已选档案可能已被停用或变成非末级，补一个不可选项保住回显。
        if (value && !selectable.some((i) => i.id === value)) {
            const stale = items.find((i) => i.id === value)
            if (stale) return { selectable, stale: [stale] }
        }
        return { selectable, stale: [] as AssistItemDto[] }
    }, [items, hierarchical, value])

    return (
        <Select value={value || NONE} onValueChange={(v) => onChange(v === NONE ? "" : v)} disabled={disabled}>
            <SelectTrigger size="sm" className={cn("w-44", className)} aria-label="选择核算项目">
                {loading ? <Spinner className="size-3" /> : <SelectValue placeholder="选择核算项目" />}
            </SelectTrigger>
            <SelectContent className="max-h-60">
                <SelectItem value={NONE}>（未选择）</SelectItem>
                {options.stale.map((item) => (
                    <SelectItem key={`stale-${item.id}`} value={item.id ?? ""} disabled>
                        {optionLabel(item, hierarchical)}（当前不可用）
                    </SelectItem>
                ))}
                {options.selectable.map((item) => (
                    <SelectItem key={item.id} value={item.id ?? ""}>
                        {optionLabel(item, hierarchical)}
                    </SelectItem>
                ))}
            </SelectContent>
        </Select>
    )
}
