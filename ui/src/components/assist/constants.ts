/**
 * 辅助核算类别常量。
 * 数值与后端 Domain/Assistances/AssistType 枚举一致（生成的客户端里 type 只是 number，没有 TS 枚举），
 * 顺序即档案页与凭证录入里的展示顺序。
 */
export interface AssistTypeMeta {
    /** 枚举值（1–7）。 */
    value: number
    /** 中文展示名，与后端 AssistTypeText.Of 保持一致。 */
    label: string
    /** 是否支持层级（仅部门/项目/存货/现金流可挂下级档案）。 */
    hierarchical: boolean
}

export const ASSIST_TYPES: readonly AssistTypeMeta[] = [
    { value: 1, label: "客户", hierarchical: false },
    { value: 2, label: "供应商", hierarchical: false },
    { value: 3, label: "职员", hierarchical: false },
    { value: 4, label: "部门", hierarchical: true },
    { value: 5, label: "项目", hierarchical: true },
    { value: 6, label: "存货", hierarchical: true },
    { value: 7, label: "现金流", hierarchical: true },
] as const

const LABELS: Record<number, string> = Object.fromEntries(ASSIST_TYPES.map((t) => [t.value, t.label]))
const HIERARCHICAL: Record<number, boolean> = Object.fromEntries(
    ASSIST_TYPES.map((t) => [t.value, t.hierarchical]),
)

/** 类别中文名；未知取值返回空串，交由调用方兜底。 */
export function assistLabel(type?: number | null): string {
    if (type == null) return ""
    return LABELS[type] ?? ""
}

/** 该类别是否可建树挂下级。 */
export function assistSupportsHierarchy(type?: number | null): boolean {
    if (type == null) return false
    return HIERARCHICAL[type] === true
}

/** 档案编码长度上限（后端 AssistItem.MaxCodeLength）。 */
export const ASSIST_CODE_MAX_LENGTH = 32

/** 层级型每级编码位数 / 平铺型流水号位数（与后端 AssistCodingScheme 一致）。 */
export const ASSIST_HIERARCHY_LEVEL_LENGTH = 2
export const ASSIST_SEQUENCE_CODE_LENGTH = 4

/**
 * 新增第 level 级（从 1 开始）档案时的期望编码位数：
 * 层级型 = 级次 × 2（顶级 2 位、二级 4 位）；平铺型恒为 4 位流水号。
 */
export function assistExpectedCodeLength(type: number | undefined, level: number): number {
    if (!assistSupportsHierarchy(type)) return ASSIST_SEQUENCE_CODE_LENGTH
    return Math.max(1, level) * ASSIST_HIERARCHY_LEVEL_LENGTH
}

/** 档案可选性：凭证录入只能选「启用中的末级档案」（平铺类别本身就是末级）。 */
export function assistItemSelectable(item: { isLeaf?: boolean; isEnabled?: boolean }, hierarchical: boolean): boolean {
    if (item.isEnabled !== true) return false
    return hierarchical ? item.isLeaf === true : true
}
