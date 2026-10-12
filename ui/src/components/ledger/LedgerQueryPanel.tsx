import type { ReactNode } from "react"
import { RotateCcw, Search } from "lucide-react"
import dayjs from "dayjs"

import type { AccountDto, AssistItemDto } from "@/apis-gen"
import { cn } from "@/lib/utils"
import { CONTROL, DATE_FIELD, PAGE_BTN_GHOST, PAGE_BTN_PRIMARY, SOFT_PANEL } from "@/components/page/styles"
import { DateRangeField, parseDate } from "@/components/fields/date-fields"
import { Button } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Spinner } from "@/components/ui/spinner"
import {
    ASSIST_TYPES,
    type LedgerConditions,
    type LedgerSwitch,
    SORT_BY_OPTIONS,
    SWITCH_LABELS,
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

/** Radix Select 不接受空串值，用固定哨兵表示「不限」。 */
const ALL = "__all__"

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
    const assistOptions = (enabledAssistTypes
        ? ASSIST_TYPES.filter((t) => enabledAssistTypes.includes(t.value))
        : ASSIST_TYPES
    ).map((t) => ({ ...t }))
    const levelCount = accounts?.length
        ? Math.max(...accounts.map((a) => a.level ?? 1), 1)
        : 4

    const control = (label: string, node: ReactNode) => (
        <div key={label} className="flex items-center gap-1.5">
            <span className="text-[12.5px] text-slate-500">{label}：</span>
            {node}
        </div>
    )

    return (
        <form
            className={cn(
                SOFT_PANEL,
                "flex flex-wrap items-center gap-x-3 gap-y-2.5 px-4 py-3",
                className,
            )}
            onSubmit={(e) => {
                e.preventDefault()
                onQuery()
            }}
        >
            {show("date") &&
                control(
                    "日期范围",
                    <DateRangeField
                        className={cn(DATE_FIELD, "w-56")}
                        value={
                            value.startDate
                                ? {
                                      from: parseDate(value.startDate),
                                      to: parseDate(value.endDate),
                                  }
                                : undefined
                        }
                        onChange={(range) =>
                            onChange(
                                range?.from
                                    ? {
                                          startDate: dayjs(range.from).format("YYYY-MM-DD"),
                                          endDate: range.to
                                              ? dayjs(range.to).format("YYYY-MM-DD")
                                              : undefined,
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
                            className={cn(CONTROL, "w-28")}
                            placeholder="起始编码"
                            value={value.accountFrom ?? ""}
                            disabled={disabled}
                            onChange={(e) => onChange({ accountFrom: e.target.value || undefined })}
                        />
                        <span className="text-slate-400">~</span>
                        <Input
                            className={cn(CONTROL, "w-28")}
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
                        value={value.maxLevel == null ? ALL : String(value.maxLevel)}
                        disabled={disabled}
                        onValueChange={(next) =>
                            onChange({ maxLevel: next === ALL ? undefined : Number(next) })
                        }
                    >
                        <SelectTrigger className={cn(CONTROL, "w-24")}>
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            <SelectItem value={ALL}>全部</SelectItem>
                            {Array.from({ length: levelCount }, (_, i) => i + 1).map((level) => (
                                <SelectItem key={level} value={String(level)}>
                                    {level} 级以内
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>,
                )}

            {show("summary") &&
                control(
                    "摘要关键字",
                    <Input
                        className={cn(CONTROL, "w-40")}
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
                        value={String(value.sortBy ?? SORT_BY_OPTIONS[0].value)}
                        disabled={disabled}
                        onValueChange={(next) => onChange({ sortBy: Number(next) })}
                    >
                        <SelectTrigger className={cn(CONTROL, "w-32")}>
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            {SORT_BY_OPTIONS.map((option) => (
                                <SelectItem key={option.value} value={String(option.value)}>
                                    {option.label}
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>,
                )}

            {show("assistType") &&
                control(
                    "辅助核算类别",
                    <Select
                        value={value.assistType == null ? ALL : String(value.assistType)}
                        disabled={disabled}
                        onValueChange={(next) =>
                            apply({
                                assistType: next === ALL ? undefined : Number(next),
                                assistItemId: undefined,
                            })
                        }
                    >
                        <SelectTrigger className={cn(CONTROL, "w-28")}>
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            {assistOptions.map((option) => (
                                <SelectItem key={option.value} value={String(option.value)}>
                                    {option.label}
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>,
                )}

            {show("secondaryAssistType") &&
                control(
                    "交叉类别",
                    <Select
                        value={value.secondaryAssistType == null ? ALL : String(value.secondaryAssistType)}
                        disabled={disabled}
                        onValueChange={(next) =>
                            apply({ secondaryAssistType: next === ALL ? undefined : Number(next) })
                        }
                    >
                        <SelectTrigger className={cn(CONTROL, "w-28")}>
                            <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                            {assistOptions
                                .filter((option) => option.value !== value.assistType)
                                .map((option) => (
                                    <SelectItem key={option.value} value={String(option.value)}>
                                        {option.label}
                                    </SelectItem>
                                ))}
                        </SelectContent>
                    </Select>,
                )}

            {show("assistItem") &&
                control(
                    "核算项目",
                    <Select
                        value={value.assistItemId ?? ALL}
                        disabled={disabled || !value.assistType}
                        onValueChange={(next) =>
                            onChange({ assistItemId: next === ALL ? undefined : next })
                        }
                    >
                        <SelectTrigger className={cn(CONTROL, "w-44")}>
                            <SelectValue placeholder="全部档案" />
                        </SelectTrigger>
                        <SelectContent>
                            <SelectItem value={ALL}>全部档案</SelectItem>
                            {(assistItems ?? []).map((item) => (
                                <SelectItem key={item.id} value={item.id ?? ""}>
                                    {item.code} {item.name}
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>,
                )}

            {show("account") &&
                control(
                    "科目",
                    <Select
                        value={value.accountId ?? ALL}
                        disabled={disabled}
                        onValueChange={(next) => apply({ accountId: next === ALL ? undefined : next })}
                    >
                        <SelectTrigger className={cn(CONTROL, "w-52")}>
                            <SelectValue placeholder="请选择科目" />
                        </SelectTrigger>
                        <SelectContent>
                            {(accounts ?? []).map((account) => (
                                <SelectItem key={account.id} value={account.id ?? ""}>
                                    {account.code} {account.name}
                                </SelectItem>
                            ))}
                        </SelectContent>
                    </Select>,
                )}

            {switches?.length
                ? control(
                      "选项",
                      <div className="flex flex-wrap items-center gap-3">
                          {switches.map((key) => (
                              <Label
                                  key={key}
                                  className="flex items-center gap-1.5 font-normal text-slate-500"
                              >
                                  <Checkbox
                                      checked={Boolean(value[key])}
                                      disabled={disabled}
                                      onCheckedChange={(next) =>
                                          onChange({ [key]: next === true } as Partial<LedgerConditions>)
                                      }
                                  />
                                  {SWITCH_LABELS[key]}
                              </Label>
                          ))}
                      </div>,
                  )
                : null}

            {extra}

            <div className="ml-auto flex items-center gap-2">
                {loading && <Spinner className="size-4 text-slate-300" />}
                <Button type="submit" disabled={disabled} className={PAGE_BTN_PRIMARY}>
                    <Search data-icon="inline-start" />
                    查询
                </Button>
                <Button
                    type="button"
                    variant="outline"
                    disabled={disabled}
                    onClick={onReset}
                    className={PAGE_BTN_GHOST}
                >
                    <RotateCcw data-icon="inline-start" />
                    重置
                </Button>
            </div>
        </form>
    )
}
