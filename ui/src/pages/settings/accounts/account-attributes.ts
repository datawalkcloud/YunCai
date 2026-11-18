import type { AccountAssistTypeDto, CurrencyDto } from "@/apis-gen"
import { ASSIST_TYPES } from "@/components/assist/constants"

/**
 * 科目核算属性（科目类别 / 现金科目 / 辅助核算 / 数量核算 / 外币核算）的取值与转换，
 * 供新增与编辑两个弹窗共用；渲染见同目录 account-attribute-fields.tsx。
 */

/** 科目大类（数值对应后端 AccountCategory）。 */
export const CATEGORY_OPTIONS: { value: number; label: string }[] = [
    { value: 1, label: "资产" },
    { value: 2, label: "负债" },
    { value: 3, label: "权益" },
    { value: 4, label: "成本" },
    { value: 5, label: "损益" },
]

/** 余额方向（数值对应后端 BalanceDirection）。 */
export const DIRECTION_OPTIONS: { value: number; label: string }[] = [
    { value: 1, label: "借" },
    { value: 2, label: "贷" },
]

export const CATEGORY_LABELS: Record<number, string> = Object.fromEntries(
    CATEGORY_OPTIONS.map((c) => [c.value, c.label]),
)
export const DIRECTION_LABELS: Record<number, string> = Object.fromEntries(
    DIRECTION_OPTIONS.map((d) => [d.value, d.label]),
)

/**
 * 科目二级类别（数值对应后端 AccountSubCategory），候选项按大类给出。
 * 只作维护与展示口径，报表取数仍按大类与编码归集，故此处与后端 AccountSubCategoryCatalog 一一对应。
 */
export const SUB_CATEGORY_OPTIONS_BY_CATEGORY: Record<number, { value: number; label: string }[]> = {
    1: [
        { value: 1, label: "流动资产" },
        { value: 2, label: "非流动资产" },
    ],
    2: [
        { value: 3, label: "流动负债" },
        { value: 4, label: "非流动负债" },
    ],
    3: [{ value: 5, label: "所有者权益" }],
    4: [{ value: 6, label: "成本" }],
    5: [
        { value: 7, label: "收入" },
        { value: 8, label: "费用" },
    ],
}

/** 计量单位长度上限（与后端 Account.MaxUnitLength 一致）。 */
export const UNIT_MAX_LENGTH = 20

/** 辅助核算面板的一行：七个固定类别各一行，勾选即挂到该科目上。 */
export interface AssistRow {
    type: number
    checked: boolean
    isRequired: boolean
}

/** 把科目已有的维度配置映射为七个固定类别的展示行（未配置的默认必录）。 */
export function toAssistRows(configured: AccountAssistTypeDto[]): AssistRow[] {
    const byType = new Map(configured.map((c) => [c.type as number, c]))
    return ASSIST_TYPES.map((t) => ({
        type: t.value,
        checked: byType.has(t.value),
        isRequired: byType.get(t.value)?.isRequired ?? true,
    }))
}

/** 勾选中的维度转成后端请求体（未勾选不提交）。 */
export function assistRowsToRequest(rows: AssistRow[]): { type: number; isRequired: boolean }[] {
    return rows.filter((r) => r.checked).map((r) => ({ type: r.type, isRequired: r.isRequired }))
}

/** 科目上可编辑的核算属性。 */
export interface AccountAttributes {
    /** 二级类别，未指定为 undefined。 */
    subCategory?: number
    isCashAccount: boolean
    assistRows: AssistRow[]
    isQuantityAccounted: boolean
    unit: string
    isForeignCurrencyAccounted: boolean
    currencyIds: string[]
}

export const emptyAccountAttributes: AccountAttributes = {
    subCategory: undefined,
    isCashAccount: false,
    assistRows: toAssistRows([]),
    isQuantityAccounted: false,
    unit: "",
    isForeignCurrencyAccounted: false,
    currencyIds: [],
}

export interface AccountAttributeErrors {
    subCategory?: string
    unit?: string
    currencyIds?: string
}

/** 账套币别档案里可挂到科目上的外币（本位币不参与勾选：外币核算只是让分录能选这些币别）。 */
export function foreignCurrenciesOf(currencies: CurrencyDto[]): CurrencyDto[] {
    return currencies.filter((c) => c.isBase !== true)
}
