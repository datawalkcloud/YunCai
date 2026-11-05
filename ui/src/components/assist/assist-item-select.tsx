import { useMemo } from "react"

import { LoadingOutlined } from "@ant-design/icons"
import { Select } from "antd"

import type { AssistItemDto } from "@/apis-gen"
import { cn } from "@/lib/utils"

import { assistSupportsHierarchy, assistItemSelectable } from "./constants"
import { useAssistItems } from "./assist-store"

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
        // 编辑历史凭证时，已选档案可能已被停用或变成非末级，补一个禁用项保住回显。
        const stale =
            value && !selectable.some((i) => i.id === value) ? items.filter((i) => i.id === value) : []
        return [
            ...stale.map((i) => ({
                value: i.id ?? "",
                label: `${optionLabel(i, hierarchical)}（当前不可用）`,
                disabled: true,
            })),
            ...selectable.map((i) => ({ value: i.id ?? "", label: optionLabel(i, hierarchical) })),
        ]
    }, [items, hierarchical, value])

    return (
        <Select
            className={cn("w-44", className)}
            aria-label="选择核算项目"
            placeholder="选择核算项目"
            disabled={disabled}
            allowClear
            showSearch
            optionFilterProp="label"
            suffixIcon={loading ? <LoadingOutlined spin /> : undefined}
            value={value || undefined}
            onChange={(id) => onChange((id as string) ?? "")}
            options={options}
        />
    )
}
