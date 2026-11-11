import { useRef, useState, type ReactNode } from "react"
import dayjs from "dayjs"
import { ReloadOutlined, SearchOutlined } from "@ant-design/icons"
import { Button, Checkbox, DatePicker, Input, Popover, Radio, Select, Spin } from "antd"

import type { AccountDto, AssistItemDto } from "@/apis-gen"
import { cn } from "@/lib/utils"
import { FilterTriggerButton } from "@/components/page/filter-trigger-button"
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
    /** 丢弃未提交的编辑，回到已提交条件。 */
    onCancel?: () => void
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
    /** 账页自补的额外操作（导出、打印等按钮）。 */
    extra?: ReactNode
    /** 收起条上、期间之后的说明（如「类别：客户」）。 */
    caption?: ReactNode
    className?: string
}

const DEFAULT_FIELDS: readonly LedgerField[] = ["date"]

/**
 * 账簿查询面板：收起态只有一条期间 + 筛选图标，完整表单一律浮层展开、不占首屏。
 * 每个账页用 fields / switches 声明自己需要哪些条件，未声明的不渲染。
 * 「不限」一律用 value=undefined 表示（配合 allowClear 回到未选）。
 */
export function LedgerQueryPanel({
    value,
    onChange,
    onQuery,
    onCancel,
    onReset,
    fields = DEFAULT_FIELDS,
    switches,
    enabledAssistTypes,
    assistItems,
    accounts,
    loading,
    disabled,
    extra,
    caption,
    className,
}: LedgerQueryPanelProps) {
    const [open, setOpen] = useState(false)
    /** 确定 / 重置会提交条件，关闭浮层时不能再按「取消」回滚。 */
    const submitted = useRef(false)
    const show = (field: LedgerField) => fields.includes(field)
    /** 这些条件缺失会让后端直接报错，选定后立即提交重查，不必再点确定。 */
    const apply = (patch: Partial<LedgerConditions>) => onChange(patch, true)
    const assistOptions = (
        enabledAssistTypes ? ASSIST_TYPES.filter((t) => enabledAssistTypes.includes(t.value)) : ASSIST_TYPES
    ).map((t) => ({ value: t.value, label: t.label }))
    const levelCount = accounts?.length ? Math.max(...accounts.map((a) => a.level ?? 1), 1) : 4

    /** 收起态只留一条期间，其余条件都收在浮层里。 */
    const periodText = value.startDate
        ? `${dayjs(value.startDate).format("YYYY年M月")}—${value.endDate ? dayjs(value.endDate).format("YYYY年M月") : "至今"}`
        : "全部期间"

    const row = (label: string, control: ReactNode) => (
        <div className="flex items-center gap-2">
            <span className="w-24 shrink-0 text-right text-[13px] text-(--foreground)">{label}</span>
            <div className="min-w-0 flex-1">{control}</div>
        </div>
    )

    const confirm = () => {
        submitted.current = true
        onQuery()
        setOpen(false)
    }

    const close = () => {
        if (!submitted.current) onCancel?.()
        submitted.current = false
        setOpen(false)
    }

    const form = (
        <form
            className="flex w-[360px] flex-col gap-3"
            onSubmit={(e) => {
                e.preventDefault()
                confirm()
            }}
        >
            {show("date") &&
                row(
                    "会计期间",
                    <DatePicker.RangePicker
                        className="w-full"
                        picker="month"
                        format="YYYY年M月"
                        allowEmpty={[true, true]}
                        disabled={disabled}
                        value={[
                            value.startDate ? dayjs(value.startDate) : null,
                            value.endDate ? dayjs(value.endDate) : null,
                        ]}
                        onChange={(dates) => {
                            const [from, to] = dates ?? []
                            onChange({
                                startDate: from ? from.startOf("month").format("YYYY-MM-DD") : undefined,
                                endDate: to ? to.endOf("month").format("YYYY-MM-DD") : undefined,
                            })
                        }}
                    />,
                )}

            {show("accountRange") &&
                row(
                    "科目区间",
                    <div className="flex items-center gap-1">
                        <Input
                            className="flex-1"
                            placeholder="起始编码"
                            value={value.accountFrom ?? ""}
                            disabled={disabled}
                            onChange={(e) => onChange({ accountFrom: e.target.value || undefined })}
                        />
                        <span className="text-(--muted-foreground)">~</span>
                        <Input
                            className="flex-1"
                            placeholder="终止编码"
                            value={value.accountTo ?? ""}
                            disabled={disabled}
                            onChange={(e) => onChange({ accountTo: e.target.value || undefined })}
                        />
                    </div>,
                )}

            {show("maxLevel") &&
                row(
                    "科目级次",
                    <Select
                        className="w-full"
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
                row(
                    "摘要",
                    <Input
                        className="w-full"
                        placeholder="按摘要筛选"
                        value={value.summaryKeyword ?? ""}
                        disabled={disabled}
                        onChange={(e) => onChange({ summaryKeyword: e.target.value || undefined })}
                    />,
                )}

            {show("sortBy") &&
                row(
                    "排序方式",
                    <Radio.Group
                        disabled={disabled}
                        value={value.sortBy ?? SORT_BY_OPTIONS[0].value}
                        onChange={(e) => onChange({ sortBy: e.target.value })}
                        options={SORT_BY_OPTIONS.map((o) => ({ value: o.value, label: o.label }))}
                    />,
                )}

            {show("assistType") &&
                row(
                    "辅助核算类别",
                    <Select
                        className="w-full"
                        placeholder="请选择"
                        disabled={disabled}
                        value={value.assistType}
                        onChange={(next) => apply({ assistType: next, assistItemId: undefined })}
                        options={assistOptions}
                    />,
                )}

            {show("secondaryAssistType") &&
                row(
                    "交叉类别",
                    <Select
                        className="w-full"
                        placeholder="请选择"
                        disabled={disabled}
                        value={value.secondaryAssistType}
                        onChange={(next) => apply({ secondaryAssistType: next })}
                        options={assistOptions.filter((o) => o.value !== value.assistType)}
                    />,
                )}

            {show("assistItem") &&
                row(
                    "核算项目",
                    <Select
                        className="w-full"
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
                row(
                    "科目",
                    <Select
                        className="w-full"
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

            {switches?.length ? (
                <div className="flex flex-col gap-1.5 border-t border-(--border) pt-2">
                    {switches.map((key) => (
                        <Checkbox
                            key={key}
                            className="text-[13px] text-(--foreground)"
                            checked={Boolean(value[key])}
                            disabled={disabled}
                            onChange={(e) =>
                                onChange({ [key]: e.target.checked } as Partial<LedgerConditions>)
                            }
                        >
                            {SWITCH_LABELS[key]}
                        </Checkbox>
                    ))}
                </div>
            ) : null}

            <div className="flex items-center gap-2 border-t border-(--border) pt-3">
                <Button type="primary" htmlType="submit" disabled={disabled} icon={<SearchOutlined />}>
                    确定
                </Button>
                <Button type="default" disabled={disabled} onClick={close}>
                    取消
                </Button>
                <Button
                    type="default"
                    disabled={disabled}
                    icon={<ReloadOutlined />}
                    onClick={() => {
                        submitted.current = true
                        onReset()
                        setOpen(false)
                    }}
                >
                    重置
                </Button>
            </div>
        </form>
    )

    return (
        <div className={cn("py-1", className)}>
            <div className="flex flex-wrap items-center gap-2">
                <Popover
                    open={open}
                    onOpenChange={(next) => (next ? (submitted.current = false, setOpen(true)) : close())}
                    trigger="click"
                    placement="bottomLeft"
                    arrow={false}
                    content={form}
                >
                    <FilterTriggerButton
                        title="点击编辑查询条件"
                        label={periodText}
                        disabled={disabled}
                    />
                </Popover>

                {caption ? <span className="truncate text-[12.5px] text-(--muted-foreground)">{caption}</span> : null}

                <div className="ml-auto flex items-center gap-2">
                    {loading && <Spin size="small" />}
                    {extra}
                </div>
            </div>
        </div>
    )
}
