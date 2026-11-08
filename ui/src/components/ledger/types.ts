/** 账簿页共用的条件模型、枚举文案与路由清单。 */
import { ASSIST_TYPES as ASSIST_TYPE_META, assistLabel } from "@/components/assist/constants"

/**
 * 账簿查询条件，字段与后端 LedgerQueryOptions 一一对应。
 * undefined / 空串一律表示「不做该维度的裁剪」。
 */
export interface LedgerConditions {
    /** 起始日期 yyyy-MM-dd（后端会归一到月初）。 */
    startDate?: string
    /** 截止日期 yyyy-MM-dd（后端会归一到月末）。 */
    endDate?: string
    /** 科目编码区间起始。 */
    accountFrom?: string
    /** 科目编码区间终止。 */
    accountTo?: string
    /** 科目级次上限。 */
    maxLevel?: number
    /** 科目级次下限。 */
    minLevel?: number
    /** 摘要关键字。 */
    summaryKeyword?: string
    /** 排序方式，取值见 SORT_BY。 */
    sortBy?: number
    /** 显示辅助核算。 */
    showAssist?: boolean
    /** 显示对方科目。 */
    showCounterAccount?: boolean
    /** 只显示明细科目。 */
    showOnlyLeaf?: boolean
    /** 余额为零不显示。 */
    hideZeroBalance?: boolean
    /** 无发生额且余额为零不显示。 */
    hideNoActivity?: boolean
    /** 无发生额不显示本期合计、本年累计。 */
    hideTotalsNoActivity?: boolean
    /** 列示区间内无发生额且余额为零的科目（这些科目默认被过滤掉）。 */
    includeIdleAccounts?: boolean
    /** 辅助核算类别，取值见 ASSIST_TYPES。 */
    assistType?: number
    /** 组合表的次类别。 */
    secondaryAssistType?: number
    /** 辅助核算档案 id。 */
    assistItemId?: string
    /** 多栏账的主科目 id。 */
    accountId?: string
}

/** 排序方式，与后端 LedgerSortBy 一致。 */
export const SORT_BY = {
    voucherNumber: 0,
    voucherDate: 1,
} as const

export const SORT_BY_OPTIONS: ReadonlyArray<{ value: number; label: string }> = [
    { value: SORT_BY.voucherNumber, label: "凭证号排序" },
    { value: SORT_BY.voucherDate, label: "凭证日期排序" },
]

/** 辅助核算类别，与后端 AssistType 一致（1 客户 … 7 现金流）；文案与档案维护页共用一份常量。 */
export const ASSIST_TYPES: ReadonlyArray<{ value: number; label: string }> = ASSIST_TYPE_META

export const assistTypeLabel = assistLabel

/** 明细账行种类，与后端 SubsidiaryRowKind 一致。 */
export const ROW_KIND = {
    entry: 0,
    opening: 1,
    monthTotal: 2,
    yearTotal: 3,
} as const

/** 账页开关的取值范围。 */
export type LedgerSwitch =
    | "showAssist"
    | "showCounterAccount"
    | "showOnlyLeaf"
    | "hideZeroBalance"
    | "hideNoActivity"
    | "hideTotalsNoActivity"
    | "includeIdleAccounts"

export const SWITCH_LABELS: Record<LedgerSwitch, string> = {
    showAssist: "显示辅助核算",
    showCounterAccount: "显示对方科目",
    showOnlyLeaf: "只显示明细科目",
    hideZeroBalance: "余额为0不显示",
    hideNoActivity: "无发生额且余额为0不显示",
    hideTotalsNoActivity: "无发生额不显示本期合计、本年累计",
    includeIdleAccounts: "包含无发生额科目",
}

/** 科目余额的默认开关（各页可按需覆盖）。 */
export const DEFAULT_SWITCHES: readonly LedgerSwitch[] = [
    "showOnlyLeaf",
    "hideZeroBalance",
    "hideNoActivity",
    "includeIdleAccounts",
]

/** 把条件里的日期字符串转成生成客户端要求的 Date。 */
export function asDate(value?: string): Date | undefined {
    return value ? new Date(value) : undefined
}
