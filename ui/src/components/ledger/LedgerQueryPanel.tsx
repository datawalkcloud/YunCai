import type { ReactNode } from "react"
import { ReloadOutlined, SearchOutlined } from "@ant-design/icons"
import { Button, Checkbox, Input, Select, Spin } from "antd"
import dayjs from "dayjs"

import type { AccountDto, AssistItemDto } from "@/apis-gen"
import { cn } from "@/lib/utils"
import { SOFT_PANEL } from "@/components/page/styles"
import { DateRangeField, parseDate } from "@/components/fields/date-fields"
import {
    ASSIST_TYPES,
    SORT_BY_OPTIONS,
    SWITCH_LABELS,
    type LedgerConditions,
    type LedgerSwitch,
} from "./types"

/** 面板可显示的条件项，由各账页按需声明。 */
export type LedgerField =
    | "date"
    | "accountRange"
    | "maxLevel"
    | "summary"
    | "sortBy"
    | "assistType"
    | "secondaryAssistType"
    | "assistItem"
    | "account"

export interface LedgerQueryPanelProps {
    value: LedgerConditions
    /**
     * 局部更新条件；immediate 为真时同时提交并立即重查，
     * 用于「不选就报错」的条件（辅助核算类别、多栏账科目）。
     */
    onChange: (patch: Partial<LedgerConditions>, immediate?: boolean) => void
    onQuery: () => void
    onReset: () => void
    /** 本账页需要显示的条件项。 */
    fields?: readonly LedgerField[]
    /** 本账页需要显示的开关项。 */
    switches?: readonly LedgerSwitch[]
    /** 辅助核算类别下拉只显示账套已启用的类别。 */
    enabledAssistTypes?: readonly number[]
    /** assistItem 条件的档案候选。 */
    assistItems?: AssistItemDto[]
    /** account 条件的科目候选。 */
    accounts?: AccountDto[]
    loading?: boolean
    disabled?: boolean
    /** 账页自补的额外条件（如导出、打印等按钮）。 */
    extra?: ReactNode
    className?: string
}

const DEFAULT_FIELDS: readonly LedgerField[] = ["date"]

/**
 * 账簿查询面板：一组受控条件 + 查询 / 重置。
 * 每个账页用 fields / switches 声明自己需要哪些条件，未声明的不渲染。
 * 「不限」一律用 value=undefined 表示（配合 allowClear 回到未选）。
 */
export function LedgerQueryPanel({
    value,
    onChange,
    onQuery,
    onReset,
    fields = DEFAULT_FIELDS,
    switches,
    enabledAssistTypes,
    assistItems,
    accounts,
    loading,
    disabled,
    extra,
    className,
}: LedgerQueryPanelProps) {
    const show = (field: LedgerField) => fields.includes(field)
    /** 这些条件缺失会让后端直接报错，选定后立即提交重查，不必再点查询。 */
    const apply = (patch: Partial<LedgerConditions>) => onChange(patch, true)
    const assistOptions = (
        enabledAssistTypes ? ASSIST_TYPES.filter((t) => enabledAssistTypes.includes(t.value)) : ASSIST_TYPES
    ).map((t) => ({ value: t.value, label: t.label }))
    const levelCount = accounts?.length ? Math.max(...accounts.map((a) => a.level ?? 1), 1) : 4

    const control = (label: string, node: ReactNode) => (
        <div key={label} className="flex items-center gap-1.5">
            <span className="text-[12.5px] text-slate-500">{label}：</span>
            {node}
        </div>
    )

    return (
        <form
            className={cn(SOFT_PANEL, "flex flex-wrap items-center gap-x-3 gap-y-2.5 px-4 py-3", className)}
            onSubmit={(e) => {
                e.preventDefault()
                onQuery()
            }}
        >
            {show("date") &&
                control(
                    "日期范围",
                    <DateRangeField
                        className="w-56"
                        value={
                            value.startDate
                                ? { from: parseDate(value.startDate), to: parseDate(value.endDate) }
                                : undefined
                        }
                        onChange={(range) =>
                            onChange(
                                range?.from
                                    ? {
                                          startDate: dayjs(range.from).format("YYYY-MM-DD"),
                                          endDate: range.to ? dayjs(range.to).format("YYYY-MM-DD") : undefined,
                                      }
                                    : { startDate: undefined, endDate: undefined },
                            )
                        }
                        disabled={disabled}
                        allowClear
                    />,
                )}

            {show("accountRange") &&
                control(
                    "科目区间",
                    <div className="flex items-center gap-1">
                        <Input
                            className="w-28"
                            placeholder="起始编码"
                            value={value.accountFrom ?? ""}
                            disabled={disabled}
                            onChange={(e) => onChange({ accountFrom: e.target.value || undefined })}
                        />
                        <span className="text-slate-400">~</span>
                        <Input
                            className="w-28"
                            placeholder="终止编码"
                            value={value.accountTo ?? ""}
                            disabled={disabled}
                            onChange={(e) => onChange({ accountTo: e.target.value || undefined })}
                        />
                    </div>,
                )}

            {show("maxLevel") &&
                control(
                    "级次",
                    <Select
                        className="w-24"
                        placeholder="全部"
                        allowClear
                        disabled={disabled}
                        value={value.maxLevel}
                        onChange={(next) => onChange({ maxLevel: next })}
                        options={Array.from({ length: levelCount }, (_, i) => ({
                            value: i + 1,
                            label: `${i + 1} 级以内`,
                        }))}
                    />,
                )}

            {show("summary") &&
                control(
                    "摘要关键字",
                    <Input
                        className="w-40"
                        placeholder="按摘要筛选"
                        value={value.summaryKeyword ?? ""}
                        disabled={disabled}
                        onChange={(e) => onChange({ summaryKeyword: e.target.value || undefined })}
                    />,
                )}

            {show("sortBy") &&
                control(
                    "排序",
                    <Select
                        className="w-32"
                        disabled={disabled}
                        value={value.sortBy ?? SORT_BY_OPTIONS[0].value}
                        onChange={(next) => onChange({ sortBy: next })}
                        options={SORT_BY_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                    />,
                )}

            {show("assistType") &&
                control(
                    "辅助核算类别",
                    <Select
                        className="w-28"
                        placeholder="请选择"
                        disabled={disabled}
                        value={value.assistType}
                        onChange={(next) => apply({ assistType: next, assistItemId: undefined })}
                        options={assistOptions}
                    />,
                )}

            {show("secondaryAssistType") &&
                control(
                    "交叉类别",
                    <Select
                        className="w-28"
                        placeholder="请选择"
                        disabled={disabled}
                        value={value.secondaryAssistType}
                        onChange={(next) => apply({ secondaryAssistType: next })}
                        options={assistOptions.filter((o) => o.value !== value.assistType)}
                    />,
                )}

            {show("assistItem") &&
                control(
                    "核算项目",
                    <Select
                        className="w-44"
                        placeholder="全部档案"
                        allowClear
                        showSearch
                        optionFilterProp="label"
                        disabled={disabled || !value.assistType}
                        value={value.assistItemId}
                        onChange={(next) => onChange({ assistItemId: next })}
                        options={(assistItems ?? []).map((item) => ({
                            value: item.id ?? "",
                            label: `${item.code} ${item.name}`,
                        }))}
                    />,
                )}

            {show("account") &&
                control(
                    "科目",
                    <Select
                        className="w-52"
                        placeholder="请选择科目"
                        showSearch
                        optionFilterProp="label"
                        disabled={disabled}
                        value={value.accountId}
                        onChange={(next) => apply({ accountId: next })}
                        options={(accounts ?? []).map((account) => ({
                            value: account.id ?? "",
                            label: `${account.code} ${account.name}`,
                        }))}
                    />,
                )}

            {switches?.length
                ? control(
                      "选项",
                      <div className="flex flex-wrap items-center gap-3">
                          {switches.map((key) => (
                              <Checkbox
                                  key={key}
                                  className="text-slate-500"
                                  checked={Boolean(value[key])}
                                  disabled={disabled}
                                  onChange={(e) =>
                                      onChange({ [key]: e.target.checked } as Partial<LedgerConditions>)
                                  }
                              >
                                  {SWITCH_LABELS[key]}
                              </Checkbox>
                          ))}
                      </div>,
                  )
                : null}

            {extra}

            <div className="ml-auto flex items-center gap-2">
                {loading && <Spin />}
                <Button type="primary" htmlType="submit" disabled={disabled} icon={<SearchOutlined />}>
                    查询
                </Button>
                <Button type="default" disabled={disabled} onClick={onReset} icon={<ReloadOutlined />}>
                    重置
                </Button>
            </div>
        </form>
    )
}
