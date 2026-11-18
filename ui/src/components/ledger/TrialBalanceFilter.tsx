import { useMemo, useRef, useState, type ReactNode } from "react"
import dayjs from "dayjs"
import { BookOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons"
import { Button, Checkbox, DatePicker, Input, InputNumber, Popover, Spin } from "antd"

import type { AccountDto } from "@/apis-gen"
import { cn } from "@/lib/utils"
import { FilterTriggerButton } from "@/components/page/filter-trigger-button"
import { SWITCH_LABELS, type LedgerConditions, type LedgerSwitch } from "./types"
import { AccountPicker } from "./SubsidiaryLedgerFilter"

/** 科目余额表筛选面板专用的复选框顺序（无「无发生额不显示本期合计本年累计」）。 */
const SWITCH_ORDER: readonly LedgerSwitch[] = ["showAssist", "hideZeroBalance", "hideNoActivity"]

interface TrialBalanceFilterProps {
    value: LedgerConditions
    /** 局部更新条件。 */
    onChange: (patch: Partial<LedgerConditions>) => void
    /** 提交当前草稿条件并重查。 */
    onQuery: () => void
    /** 丢弃未提交的编辑，回到已提交条件。 */
    onCancel: () => void
    /** 回到初始条件并重查。 */
    onReset: () => void
    /** 科目候选（起始 / 结束科目选择用）。 */
    accounts: AccountDto[]
    /** 接口返回的当前有效期间文本（折叠条上展示）；不传则按草稿日期推算。 */
    periodText?: string
    /** 树形展开 / 收起全部级次（科目余额表特有）。 */
    expandAll: boolean
    onExpandAllChange: (checked: boolean) => void
    loading?: boolean
    disabled?: boolean
    /** 账页自补的额外操作（打印）。 */
    extra?: ReactNode
    className?: string
}

/**
 * 科目余额表筛选面板（竖向单列，）。
 * 顶部常驻折叠条：有效期间 + 筛选图标（点击展开 / 收起）+ 「展开所有级次」复选框；
 * 展开后字段为 会计期间、起始科目、结束科目（带科目选择图标）、科目级别（起止双步进器），
 * 再加三个复选框与 确定 / 取消 / 重置 三枚按钮。
 */
export function TrialBalanceFilter({
    value,
    onChange,
    onQuery,
    onCancel,
    onReset,
    accounts,
    periodText,
    expandAll,
    onExpandAllChange,
    loading,
    disabled,
    extra,
    className,
}: TrialBalanceFilterProps) {
    const [picker, setPicker] = useState<"accountFrom" | "accountTo" | null>(null)
    const [open, setOpen] = useState(false)
    /** 确定 / 重置会提交条件，关闭浮层时不能再按「取消」回滚。 */
    const submitted = useRef(false)
    const maxLevel = useMemo(() => Math.max(...accounts.map((a) => a.level ?? 1), 1), [accounts])
    const minLevel = value.minLevel ?? 1
    const levelEnd = value.maxLevel ?? maxLevel

    // 收起条统一按「YYYY年M月—YYYY年M月」展示；接口的 periodText 是 yyyy-MM-dd 区间，折成月份。
    const fallbackPeriod = (() => {
        const m = periodText?.match(/(\d{4})-(\d{2})-\d{2}\s*至\s*(\d{4})-(\d{2})-\d{2}/)
        return m ? `${Number(m[1])}年${Number(m[2])}月—${Number(m[3])}年${Number(m[4])}月` : periodText
    })()
    const headerPeriod = value.startDate
        ? `${dayjs(value.startDate).format("YYYY年M月")}—${value.endDate ? dayjs(value.endDate).format("YYYY年M月") : "至今"}`
        : (fallbackPeriod || "全部期间")

    const row = (label: string, control: ReactNode, labelWidth = "w-24") => (
        <div className="flex items-center gap-2">
            <span className={cn("shrink-0 text-right text-[13px] text-(--foreground)", labelWidth)}>{label}</span>
            <div className="min-w-0 flex-1">{control}</div>
        </div>
    )

    const accountInput = (field: "accountFrom" | "accountTo", placeholder: string) => (
        <div className="flex items-center gap-1">
            <Input
                className="flex-1"
                placeholder={placeholder}
                value={value[field] ?? ""}
                disabled={disabled}
                onChange={(e) => onChange({ [field]: e.target.value || undefined })}
            />
            <Button
                type="text"
                icon={<BookOutlined />}
                disabled={disabled}
                onClick={() => setPicker(field)}
                title="选择科目"
            />
        </div>
    )

    const levelStepper = (
        <div className="flex items-center gap-1.5">
            <InputNumber
                className="w-16"
                min={1}
                max={maxLevel}
                value={minLevel}
                disabled={disabled}
                onChange={(v) => onChange({ minLevel: typeof v === "number" ? v : 1, maxLevel: levelEnd })}
            />
            <span className="text-(--muted-foreground)">至</span>
            <InputNumber
                className="w-16"
                min={minLevel}
                max={maxLevel}
                value={levelEnd}
                disabled={disabled}
                onChange={(v) => onChange({ minLevel, maxLevel: typeof v === "number" ? v : maxLevel })}
            />
        </div>
    )

    const confirm = () => {
        submitted.current = true
        onQuery()
        setOpen(false)
    }

    const close = () => {
        if (!submitted.current) onCancel()
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
            {row(
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

            {row("起始科目", accountInput("accountFrom", "科目编码"))}
            {row("结束科目", accountInput("accountTo", "科目编码"))}
            {row("科目级别", levelStepper)}

            <div className="flex flex-col gap-1.5 border-t border-(--border) pt-2">
                {SWITCH_ORDER.map((key) => (
                    <Checkbox
                        key={key}
                        className="text-[13px] text-(--foreground)"
                        checked={Boolean(value[key])}
                        disabled={disabled}
                        onChange={(e) => onChange({ [key]: e.target.checked } as Partial<LedgerConditions>)}
                    >
                        {SWITCH_LABELS[key]}
                    </Checkbox>
                ))}
            </div>

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
                        label={headerPeriod}
                        disabled={disabled}
                    />
                </Popover>

                <div className="ml-auto flex items-center gap-2">
                    <Checkbox
                        className="text-[13px] text-(--foreground)"
                        checked={expandAll}
                        onChange={(e) => onExpandAllChange(e.target.checked)}
                    >
                        展开所有级次
                    </Checkbox>
                    {loading && <Spin size="small" />}
                    {extra}
                </div>
            </div>

            <AccountPicker
                open={picker !== null}
                accounts={accounts}
                onPick={(code) => picker && onChange({ [picker]: code || undefined })}
                onClose={() => setPicker(null)}
            />
        </div>
    )
}
