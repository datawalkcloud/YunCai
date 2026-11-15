import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useNavigate, useSearchParams } from "react-router"
import type { Key } from "react"
import {
    Alert,
    App,
    Button,
    Card,
    Checkbox,
    DatePicker,
    Drawer,
    Dropdown,
    Flex,
    Input,
    InputNumber,
    Modal,
    Pagination,
    Popover,
    Radio,
    Segmented,
    Select,
    Space,
    Table,
    Tag,
    Tooltip,
    Typography,
} from "antd"
import type { MenuProps, TableColumnsType } from "antd"
import {
    DeleteOutlined,
    DownOutlined,
    DownloadOutlined,
    FileExcelOutlined,
    FileTextOutlined,
    PlusOutlined,
    PrinterOutlined,
    ReloadOutlined,
    RightOutlined,
    RollbackOutlined,
    SearchOutlined,
    UploadOutlined,
} from "@ant-design/icons"
import dayjs from "dayjs"

import { FilterTriggerButton } from "@/components/page/filter-trigger-button"
import { setPath } from "@/accountSets/url-id"
import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AccountsApi,
    VoucherTemplatesApi,
    VoucherWordsApi,
    VouchersApi,
    type AccountDto,
    type VoucherEntryDto,
    type VoucherImportResult,
    type VoucherListItemDto,
} from "@/apis-gen"
import { apiConfig, apiConfigWithHeaders, apiErrorMessage } from "@/lib/api-client"
import { batchUpdateVouchers, mergeVouchers } from "@/api/voucher-batch"
import { useAccountAssistConfigs } from "@/components/assist/assist-store"
import { useAssistItems } from "@/components/ledger/useLedgerOptions"
import { useStepUp } from "@/lib/step-up"
import { fmtAmount, toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { Tile } from "@/components/page/tile"
import { StatusPill as StatusPillBase } from "@/components/page/status-pill"
import { AMOUNT_COL, ICON_STYLE, SOFT_PANEL, voucherStatusTone } from "@/components/page/styles"
import { PrintVoucherSheet } from "@/pages/closing/print-sheet"
import VoucherSheet from "@/components/voucher/voucher-sheet"

/** 每页条数候选，与原型分页的 showSizeChanger 一致。 */
const PAGE_SIZE_OPTIONS = [20, 50, 200, 500, 1000]

/** 原型默认每页 20 条。 */
const DEFAULT_PAGE_SIZE = 20

/** 三态筛选（全部 / 是 / 否）。 */
type TriState = "all" | "yes" | "no"

/** 时间筛选的维度：会计期间按整月归一，凭证日期精确到所选当日。 */
type DateKind = "period" | "date"

const DATE_KIND_OPTIONS = [
    { value: "period", label: "会计期间" },
    { value: "date", label: "凭证日期" },
] as const

/**
 * 凭证列表的全部筛选条件。字段与柠檬云「查看凭证」筛选面板对齐；
 * 数量核算、外币核算我们没有对应的数据字段，因此没有放进筛选条件（避免给出点了没反应的空条件）。
 */
interface VoucherFilters {
    /** 时间维度。 */
    dateKind: DateKind
    /** 区间起：会计期间为 YYYY-MM，凭证日期为 YYYY-MM-DD。 */
    dateFrom: string | null
    /** 区间止，口径同上。 */
    dateTo: string | null
    word: string | null
    preparer: string | null
    hasAssists: TriState
    summary: string
    remark: string
    accountId: string | null
    /** 借贷方向。 */
    direction: "all" | "debit" | "credit"
    amountFrom: number | null
    amountTo: number | null
    numberFrom: number | null
    numberTo: number | null
    /** 排序依据：按凭证号或按凭证日期。 */
    sortBy: "date" | "number"
    sortDesc: boolean
}

const EMPTY_FILTERS: VoucherFilters = {
    dateKind: "period",
    dateFrom: null,
    dateTo: null,
    word: null,
    preparer: null,
    hasAssists: "all",
    summary: "",
    remark: "",
    accountId: null,
    direction: "all",
    amountFrom: null,
    amountTo: null,
    numberFrom: null,
    numberTo: null,
    sortBy: "date",
    sortDesc: false,
}

/** 首屏默认落在本月：直接看当期凭证，也避免一进页就拉全量历史。 */
function defaultFilters(): VoucherFilters {
    const month = dayjs().format("YYYY-MM")
    return { ...EMPTY_FILTERS, dateFrom: month, dateTo: month }
}

/** 快捷期间：把起止月份压成 本月 / 本季 / 本年 / 全部，用于筛选按钮旁的 Segmented。 */
type PeriodPreset = "month" | "quarter" | "year" | "all"

function periodPreset(filters: VoucherFilters): PeriodPreset | "custom" {
    if (!filters.dateFrom && !filters.dateTo) return "all"
    const presets = (["month", "quarter", "year"] as const).map((key) => ({ key, ...presetRange(key, filters.dateKind) }))
    const hit = presets.find((p) => p.dateFrom === filters.dateFrom && p.dateTo === filters.dateTo)
    return hit?.key ?? "custom"
}

function presetRange(preset: PeriodPreset, kind: DateKind): Pick<VoucherFilters, "dateFrom" | "dateTo"> {
    if (preset === "all") return { dateFrom: null, dateTo: null }
    const fmt = kind === "period" ? "YYYY-MM" : "YYYY-MM-DD"
    const now = dayjs()
    if (preset === "month") {
        return { dateFrom: now.startOf("month").format(fmt), dateTo: now.endOf("month").format(fmt) }
    }
    if (preset === "year") {
        return { dateFrom: now.startOf("year").format(fmt), dateTo: now.endOf("year").format(fmt) }
    }
    // dayjs 核心不认 quarter，自己算：季度首月 = 向下取整到 3 的倍数。
    const firstMonth = Math.floor(now.month() / 3) * 3
    return {
        dateFrom: now.month(firstMonth).startOf("month").format(fmt),
        dateTo: now.month(firstMonth + 2).endOf("month").format(fmt),
    }
}

/** 三态转接口参数：全部返回 undefined，不参与过滤。 */
function triToParam(value: TriState): boolean | undefined {
    return value === "all" ? undefined : value === "yes"
}

/** 月份（YYYY-MM）转当月起止日，供接口按 DateOnly 过滤。 */
function monthRange(period: string | null, edge: "start" | "end"): Date | undefined {
    if (!period) return undefined
    const month = dayjs(`${period}-01`)
    return new Date(month[edge === "start" ? "startOf" : "endOf"]("month").format("YYYY-MM-DD"))
}

/** 区间端点转接口日期：会计期间（YYYY-MM）取整月边界，凭证日期（YYYY-MM-DD）原样使用。 */
function endpointToParam(value: string | null, edge: "start" | "end"): Date | undefined {
    if (!value) return undefined
    return value.length > 7 ? new Date(value) : monthRange(value, edge)
}

/** 端点在两种维度间换算：期间→日期补整月边界，日期→期间取所在月。 */
function endpointToKind(value: string | null, kind: DateKind, edge: "start" | "end"): string | null {
    if (!value) return null
    if (kind === "period") return value.slice(0, 7)
    return value.length > 7 ? value : dayjs(`${value}-01`)[edge === "start" ? "startOf" : "endOf"]("month").format("YYYY-MM-DD")
}

/** 切换时间维度时把已选区间一起换算过去，避免切完条件就空了。 */
function switchDateKind(filters: VoucherFilters, kind: DateKind) {
    return {
        dateKind: kind,
        dateFrom: endpointToKind(filters.dateFrom, kind, "start"),
        dateTo: endpointToKind(filters.dateTo, kind, "end"),
    }
}

/** 区间端点转 RangePicker 的 dayjs 值（月份按月初）。 */
function endpointToDay(value: string | null) {
    return value ? dayjs(value.length > 7 ? value : `${value}-01`) : null
}

/** 整理凭证按期间进行，取区间起所在的月份，与当前是哪种时间维度无关。 */
function periodOf(filters: VoucherFilters): string {
    return filters.dateFrom ? filters.dateFrom.slice(0, 7) : dayjs().format("YYYY-MM")
}

/** 筛选条件转接口入参；回收站开关单独叠加。 */
function toQueryParams(filters: VoucherFilters, deleted: boolean) {
    return {
        startDate: endpointToParam(filters.dateFrom, "start"),
        endDate: endpointToParam(filters.dateTo, "end"),
        word: filters.word ?? undefined,
        preparer: filters.preparer ?? undefined,
        hasAssists: triToParam(filters.hasAssists),
        summary: filters.summary.trim() || undefined,
        remark: filters.remark.trim() || undefined,
        accountId: filters.accountId ?? undefined,
        direction: filters.direction === "all" ? undefined : filters.direction === "debit",
        amountFrom: filters.amountFrom ?? undefined,
        amountTo: filters.amountTo ?? undefined,
        numberFrom: filters.numberFrom ?? undefined,
        numberTo: filters.numberTo ?? undefined,
        sortBy: filters.sortBy,
        sortDesc: filters.sortDesc,
        deleted,
    }
}

/** 区间文本：起止都一样时只显示一个；会计期间显示到月，凭证日期显示到日。 */
function periodText(filters: VoucherFilters): string {
    const fmt = (v: string) => (v.length > 7 ? dayjs(v).format("YYYY年M月D日") : v.replace("-", "年") + "月")
    const from = filters.dateFrom
    const to = filters.dateTo
    if (from && to) return from === to ? fmt(from) : `${fmt(from)} - ${fmt(to)}`
    if (from) return `${fmt(from)} 起`
    if (to) return `截至 ${fmt(to)}`
    return filters.dateKind === "period" ? "全部期间" : "全部日期"
}

/** 统计已生效的筛选条件数，显示在筛选按钮上。 */
function activeFilterCount(filters: VoucherFilters): number {
    let count = 0
    if (filters.dateFrom || filters.dateTo) count++
    if (filters.word) count++
    if (filters.preparer) count++
    if (filters.hasAssists !== "all") count++
    if (filters.summary.trim()) count++
    if (filters.remark.trim()) count++
    if (filters.accountId) count++
    if (filters.direction !== "all") count++
    if (filters.amountFrom != null || filters.amountTo != null) count++
    if (filters.numberFrom != null || filters.numberTo != null) count++
    return count
}

const ACCEPT = ".xls,.xlsx"

/** 借贷金额文本：负数用红色标注，零与空值留白（口径同 fmtAmount）。 */
function AmountText({ value }: { value: number | undefined }) {
    return <span className={(value ?? 0) < 0 ? "text-(--danger)" : undefined}>{fmtAmount(value)}</span>
}

/**
 * 列表的表格行：一张凭证的分录行 + 一行凭证级合计，凭证级字段（日期、字号、状态、操作…）
 * 用 rowSpan 合并到整张凭证的高度，口径与序时账等账页表格一致。
 */
type VoucherRow = {
    key: string
    voucher: VoucherListItemDto
    entry?: VoucherEntryDto
    /** 是否为该凭证的首行（凭证级合并单元格从这里起算）。 */
    isFirst: boolean
    /** 该凭证占用的总行数（分录行 + 合计行）。 */
    groupSize: number
    isTotal: boolean
}

/** 把凭证列表摊平成表格行；无分录的凭证也占一行，保证合计行仍有归属。 */
function toVoucherRows(vouchers: VoucherListItemDto[]): VoucherRow[] {
    const rows: VoucherRow[] = []
    for (const voucher of vouchers) {
        const entries = voucher.entries ?? []
        const groupSize = Math.max(entries.length, 1) + 1
        if (entries.length === 0) {
            rows.push({ key: `${voucher.id}-0`, voucher, isFirst: true, groupSize, isTotal: false })
        } else {
            entries.forEach((entry, i) =>
                rows.push({ key: `${voucher.id}-${i}`, voucher, entry, isFirst: i === 0, groupSize, isTotal: false }),
            )
        }
        rows.push({ key: `${voucher.id}-sum`, voucher, isFirst: false, groupSize, isTotal: true })
    }
    return rows
}

/** 凭证级列的单元格：首行整组合并，其余行（含合计行）让位给合并格。 */
function metaCell(row: VoucherRow) {
    return { rowSpan: row.isFirst ? row.groupSize : 0 }
}

/** 状态胶囊：配色口径见共享的 voucherStatusTone。 */
function StatusPill({ record }: { record: VoucherListItemDto }) {
    return (
        <StatusPillBase tone={voucherStatusTone(record.status, record.isBalanced)}>
            {record.statusText}
        </StatusPillBase>
    )
}

/** 工具栏下拉按钮：功能图标（可选）+ 文案 + 菜单箭头，箭头只表示「有菜单」，不充当功能图标。 */
function toolbarDropdown(
    text: string,
    items: NonNullable<MenuProps["items"]>,
    onClick: MenuProps["onClick"],
    icon?: React.ReactNode,
) {
    return (
        <Dropdown trigger={["click"]} menu={{ items, onClick }}>
            <Button icon={icon}>
                {text}
                <DownOutlined style={{ fontSize: 10 }} />
            </Button>
        </Dropdown>
    )
}

/** 筛选面板里一行：左侧标题、右侧控件，与柠檬云的两列式面板一致。 */
function FilterRow({ label, children }: { label: React.ReactNode; children: React.ReactNode }) {
    return (
        <div className="flex items-center gap-2">
            <span className="w-[72px] shrink-0 text-right text-[13px] text-(--muted-foreground)">{label}</span>
            <div className="flex min-w-0 flex-1 items-center gap-1">{children}</div>
        </div>
    )
}

/**
 * 凭证筛选面板。字段与柠檬云「查看凭证 → 2026年1月」下拉面板对齐；
 * 数量核算、外币核算因为我们没有对应数据字段，未列入。
 */
function FilterPanel({
    value,
    onChange,
    accounts,
    words,
    preparers,
}: {
    value: VoucherFilters
    onChange: (next: VoucherFilters) => void
    accounts: AccountDto[]
    words: string[]
    preparers: string[]
}) {
    const patch = (part: Partial<VoucherFilters>) => onChange({ ...value, ...part })

    return (
        <div className="flex max-h-[440px] w-[420px] flex-col gap-2 overflow-y-auto pr-1 pt-1">
            <FilterRow
                label={
                    <Dropdown
                        trigger={["click"]}
                        menu={{
                            items: DATE_KIND_OPTIONS.map((o) => ({ key: o.value, label: o.label })),
                            selectable: true,
                            selectedKeys: [value.dateKind],
                            onClick: ({ key }) => patch(switchDateKind(value, key as DateKind)),
                        }}
                    >
                        <span className="inline-flex w-full cursor-pointer items-center justify-end gap-1 text-[13px] text-(--muted-foreground)">
                            {DATE_KIND_OPTIONS.find((o) => o.value === value.dateKind)?.label}
                            <DownOutlined style={{ fontSize: 10 }} />
                        </span>
                    </Dropdown>
                }
            >
                <DatePicker.RangePicker
                    style={{ flex: 1 }}
                    picker={value.dateKind === "period" ? "month" : "date"}
                    format={value.dateKind === "period" ? "YYYY年M月" : "YYYY年M月D日"}
                    placeholder={value.dateKind === "period" ? ["开始月份", "结束月份"] : ["开始日期", "结束日期"]}
                    allowEmpty={[true, true]}
                    allowClear
                    value={[endpointToDay(value.dateFrom), endpointToDay(value.dateTo)]}
                    onChange={(dates) => {
                        const [from, to] = dates ?? []
                        const fmt = value.dateKind === "period" ? "YYYY-MM" : "YYYY-MM-DD"
                        patch({ dateFrom: from ? from.format(fmt) : null, dateTo: to ? to.format(fmt) : null })
                    }}
                />
            </FilterRow>
            <FilterRow label="凭证字：">
                <Select
                    style={{ width: "100%" }}
                    value={value.word ?? "__all__"}
                    onChange={(v) => patch({ word: v === "__all__" ? null : v })}
                    options={[{ value: "__all__", label: "全部" }, ...words.map((w) => ({ value: w, label: w }))]}
                />
            </FilterRow>
            <FilterRow label="制单人：">
                <Select
                    style={{ width: "100%" }}
                    showSearch
                    value={value.preparer ?? "__all__"}
                    onChange={(v) => patch({ preparer: v === "__all__" ? null : v })}
                    options={[{ value: "__all__", label: "全部" }, ...preparers.map((p) => ({ value: p, label: p }))]}
                />
            </FilterRow>
            <FilterRow label="辅助核算：">
                <Select
                    style={{ width: "100%" }}
                    value={value.hasAssists}
                    onChange={(v) => patch({ hasAssists: v })}
                    options={[
                        { value: "all", label: "全部" },
                        { value: "yes", label: "有辅助核算" },
                        { value: "no", label: "无辅助核算" },
                    ]}
                />
            </FilterRow>
            <FilterRow label="摘要：">
                <Input
                    allowClear
                    placeholder="包含关键字"
                    value={value.summary}
                    onChange={(e) => patch({ summary: e.target.value })}
                />
            </FilterRow>
            <FilterRow label="备注：">
                <Input
                    allowClear
                    placeholder="包含关键字"
                    value={value.remark}
                    onChange={(e) => patch({ remark: e.target.value })}
                />
            </FilterRow>
            <FilterRow label="科目：">
                <Select
                    style={{ width: "100%" }}
                    showSearch
                    allowClear
                    placeholder="选择科目（含下级）"
                    value={value.accountId ?? undefined}
                    onChange={(v) => patch({ accountId: v ?? null })}
                    optionFilterProp="label"
                    options={accounts.map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` }))}
                />
            </FilterRow>
            <FilterRow label="借贷方向：">
                <Select
                    style={{ width: "100%" }}
                    value={value.direction}
                    onChange={(v) => patch({ direction: v })}
                    options={[
                        { value: "all", label: "全部" },
                        { value: "debit", label: "借方" },
                        { value: "credit", label: "贷方" },
                    ]}
                />
            </FilterRow>
            <FilterRow label="金额：">
                <InputNumber
                    style={{ flex: 1 }}
                    placeholder="从"
                    value={value.amountFrom ?? undefined}
                    onChange={(v) => patch({ amountFrom: v ?? null })}
                />
                <span className="text-[12px] text-(--muted-foreground)">至</span>
                <InputNumber
                    style={{ flex: 1 }}
                    placeholder="到"
                    value={value.amountTo ?? undefined}
                    onChange={(v) => patch({ amountTo: v ?? null })}
                />
            </FilterRow>
            <FilterRow label="凭证号：">
                <InputNumber
                    style={{ flex: 1 }}
                    placeholder="从"
                    value={value.numberFrom ?? undefined}
                    onChange={(v) => patch({ numberFrom: v ?? null })}
                />
                <span className="text-[12px] text-(--muted-foreground)">至</span>
                <InputNumber
                    style={{ flex: 1 }}
                    placeholder="到"
                    value={value.numberTo ?? undefined}
                    onChange={(v) => patch({ numberTo: v ?? null })}
                />
            </FilterRow>
            <FilterRow label="排序依据：">
                <Radio.Group
                    value={value.sortBy}
                    onChange={(e) => patch({ sortBy: e.target.value })}
                    options={[
                        { value: "number", label: "凭证号排序" },
                        { value: "date", label: "凭证日期排序" },
                    ]}
                />
            </FilterRow>
            <FilterRow label="排序方式：">
                <Radio.Group
                    value={value.sortDesc ? "desc" : "asc"}
                    onChange={(e) => patch({ sortDesc: e.target.value === "desc" })}
                    options={[
                        { value: "asc", label: "升序" },
                        { value: "desc", label: "降序" },
                    ]}
                />
            </FilterRow>
        </div>
    )
}

/** 表格行内动作：文字链接（口径同设置页/出纳台账），功能名一眼可读，不靠图标猜。 */
function ActionLink({
    label,
    onClick,
    danger,
    loading,
    tip,
}: {
    label: string
    onClick: () => void
    danger?: boolean
    loading?: boolean
    tip?: string
}) {
    const button = (
        <Button type="link" danger={danger} loading={loading} style={{ paddingInline: 0 }} onClick={onClick}>
            {label}
        </Button>
    )
    return tip ? <Tooltip title={tip}>{button}</Tooltip> : button
}

/** 凭证列表表格的列定义所需的外部能力。 */
interface VoucherColumnActions {
    selectedKeys: Key[]
    deletingId: string | null
    recycleMode: boolean
    selectAll: { checked: boolean; indeterminate: boolean; onChange: (checked: boolean) => void }
    onToggleSelect: (id: string, checked: boolean) => void
    onEdit: (v: VoucherListItemDto) => void
    onDelete: (v: VoucherListItemDto) => void
    onInsert: (v: VoucherListItemDto) => void
    onPrint: (v: VoucherListItemDto) => void
    onCopy: (v: VoucherListItemDto) => void
    onReverse: (v: VoucherListItemDto) => void
    onSaveTemplate: (v: VoucherListItemDto) => void
    onRestore: (v: VoucherListItemDto) => void
    onPurge: (v: VoucherListItemDto) => void
}

/**
 * 凭证列表的列：分录逐行展示，凭证级字段（勾选/日期/字号/附件/制单/审核/状态/操作）
 * 用 rowSpan 合并成一格，每张凭证末尾补一行合计，配色交给 antd 表格主题。
 */
function buildVoucherColumns(x: VoucherColumnActions): TableColumnsType<VoucherRow> {
    return [
        {
            key: "select",
            width: 44,
            align: "center",
            title: (
                <Checkbox
                    checked={x.selectAll.checked}
                    indeterminate={x.selectAll.indeterminate}
                    onChange={(event) => x.selectAll.onChange(event.target.checked)}
                />
            ),
            onCell: metaCell,
            render: (_, row) => (
                <Checkbox
                    checked={x.selectedKeys.includes(row.voucher.id ?? "")}
                    onChange={(event) => x.onToggleSelect(row.voucher.id ?? "", event.target.checked)}
                />
            ),
        },
        { key: "date", title: "日期", width: 110, onCell: metaCell, render: (_, row) => toISODate(row.voucher.date) },
        {
            key: "no",
            title: "凭证字号",
            width: 96,
            onCell: metaCell,
            render: (_, row) => (
                <div className="flex flex-col items-start gap-1">
                    <span className="font-medium">{row.voucher.voucherNo}</span>
                    <StatusPill record={row.voucher} />
                </div>
            ),
        },
        {
            key: "summary",
            title: "摘要",
            ellipsis: true,
            onCell: (row) => (row.isTotal ? { className: "font-medium" } : {}),
            render: (_, row) => (row.isTotal ? "合计" : row.entry?.summary),
        },
        {
            key: "account",
            title: "科目",
            width: 200,
            render: (_, row) => {
                if (!row.isTotal) {
                    return (
                        <span>
                            <span className="text-(--muted-foreground)">{row.entry?.accountCode} </span>
                            {row.entry?.accountName}
                        </span>
                    )
                }
                const diff = Math.abs((row.voucher.totalDebit ?? 0) - (row.voucher.totalCredit ?? 0))
                return row.voucher.isBalanced === false ? (
                    <Typography.Text type="danger">差额 {fmtAmount(diff)}</Typography.Text>
                ) : (
                    <Typography.Text type="success">借贷平衡</Typography.Text>
                )
            },
        },
        {
            key: "debit",
            title: "借方金额",
            width: 118,
            ...AMOUNT_COL,
            render: (_, row) =>
                row.isTotal ? (
                    <span className="font-medium">{fmtAmount(row.voucher.totalDebit, true)}</span>
                ) : (
                    <AmountText value={row.entry?.debit} />
                ),
        },
        {
            key: "credit",
            title: "贷方金额",
            width: 118,
            ...AMOUNT_COL,
            render: (_, row) =>
                row.isTotal ? (
                    <span className="font-medium">{fmtAmount(row.voucher.totalCredit, true)}</span>
                ) : (
                    <AmountText value={row.entry?.credit} />
                ),
        },
        {
            key: "attachment",
            title: "附件",
            width: 64,
            align: "center",
            onCell: metaCell,
            render: (_, row) => (row.voucher.attachmentCount ? row.voucher.attachmentCount : "—"),
        },
        { key: "preparer", title: "制单人", width: 84, onCell: metaCell, render: (_, row) => row.voucher.preparer ?? "—" },
        {
            key: "ops",
            title: "操作",
            width: 200,
            onCell: metaCell,
            render: (_, row) => {
                const v = row.voucher
                if (x.recycleMode) {
                    return (
                        <Space size={[6, 4]} wrap>
                            <ActionLink label="还原" onClick={() => x.onRestore(v)} />
                            <ActionLink label="彻底删除" danger onClick={() => x.onPurge(v)} />
                        </Space>
                    )
                }
                return (
                    <Space size={[6, 4]} wrap>
                        <ActionLink label="修改" onClick={() => x.onEdit(v)} />
                        <ActionLink label="复制" onClick={() => x.onCopy(v)} />
                        <ActionLink
                            label="删除"
                            danger
                            loading={x.deletingId === v.id}
                            onClick={() => x.onDelete(v)}
                        />
                        {/* 低频动作收进「更多」：操作列从 7 个链接缩到 3 个，行高与横向宽度都更省。 */}
                        <Dropdown
                            trigger={["click"]}
                            menu={{
                                items: [
                                    { key: "insert", label: "插入凭证" },
                                    { key: "reverse", label: "红冲" },
                                    { key: "template", label: "存为模板" },
                                    { key: "print", label: "打印" },
                                ],
                                onClick: ({ key }) => {
                                    if (key === "insert") x.onInsert(v)
                                    else if (key === "reverse") x.onReverse(v)
                                    else if (key === "template") x.onSaveTemplate(v)
                                    else if (key === "print") x.onPrint(v)
                                },
                            }}
                        >
                            <Button type="link" style={{ paddingInline: 0 }}>
                                更多
                            </Button>
                        </Dropdown>
                    </Space>
                )
            },
        },
    ]
}

export default function VoucherManagePage() {
    const { token } = useAuth()
    const { run: withStepUp } = useStepUp()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { message, modal } = App.useApp()
    const navigate = useNavigate()
    const [searchParams] = useSearchParams()

    const [vouchers, setVouchers] = useState<VoucherListItemDto[]>([])
    /** 筛选面板的科目下拉：含非末级科目，便于按一级科目过滤全部下级。 */
    const [allAccounts, setAllAccounts] = useState<AccountDto[]>([])
    /** 账套已配置的凭证字，作为筛选下拉的选项来源。 */
    const [voucherWords, setVoucherWords] = useState<string[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [open, setOpen] = useState(false)
    /** 正在编辑的凭证 id；为空表示新增。详情由抽屉里的 VoucherSheet 自行载入。 */
    const [editingId, setEditingId] = useState<string | null>(null)
    const [deletingId, setDeletingId] = useState<string | null>(null)
    /** 插入凭证：新增抽屉预设的日期与凭证字。 */
    const [insertSeed, setInsertSeed] = useState<{ date: string; word: string } | null>(null)
    /** 存为模板弹窗的目标凭证与表单值。 */
    const [templateTarget, setTemplateTarget] = useState<VoucherListItemDto | null>(null)
    const [templateName, setTemplateName] = useState("")
    const [templateType, setTemplateType] = useState(1)
    const [templateSaving, setTemplateSaving] = useState(false)
    /** 单张打印：表格行「打印」动作的目标凭证。 */
    const [printVoucherId, setPrintVoucherId] = useState<string | null>(null)
    /** 连续打印的凭证清单（工具栏「打印」用）。 */
    const [printVoucherIds, setPrintVoucherIds] = useState<string[] | null>(null)
    /** 账簿提示跳进来时带原查询区间，否则落本月。 */
    const initialFilters = (): VoucherFilters => {
        const from = searchParams.get("periodFrom")
        const to = searchParams.get("periodTo")
        if (from || to) return { ...EMPTY_FILTERS, dateFrom: from, dateTo: to }
        return defaultFilters()
    }
    /** 已生效的筛选条件。 */
    const [filters, setFilters] = useState<VoucherFilters>(initialFilters)
    /** 筛选面板里正在编辑、尚未点「确定」的条件。 */
    const [draftFilters, setDraftFilters] = useState<VoucherFilters>(initialFilters)
    const [filterOpen, setFilterOpen] = useState(false)
    /** 表头上方的即时筛选：关键字（凭证号/摘要/金额）与状态，都在已加载结果内过滤，不发请求。 */
    const [quickKeyword, setQuickKeyword] = useState("")
    // 账簿页的未入账提示会带 ?status=draft 跳进来，直接落到暂存视图。
    const [statusFilter, setStatusFilter] = useState<"all" | "draft" | "posted">(() => {
        const status = searchParams.get("status")
        return status === "draft" || status === "posted" ? status : "all"
    })
    /** 回收站视图：只看已删除的凭证。 */
    const [recycleMode, setRecycleMode] = useState(false)
    const [selectedKeys, setSelectedKeys] = useState<Key[]>([])
    /** 整理凭证弹窗。 */
    const [tidyOpen, setTidyOpen] = useState(false)
    const [tidyFrom, setTidyFrom] = useState<string | null>(null)
    const [tidyTo, setTidyTo] = useState<string | null>(null)
    const [tidyWord, setTidyWord] = useState<string | null>(null)
    const [tidyByDate, setTidyByDate] = useState(false)
    const [tidying, setTidying] = useState(false)

    /** 合并暂存凭证：把所选凭证并入凭证号最小的一张。 */
    const [mergeOpen, setMergeOpen] = useState(false)
    const [merging, setMerging] = useState(false)

    /** 批量修改暂存凭证的安全字段（摘要 / 辅助核算）。 */
    const [batchOpen, setBatchOpen] = useState(false)
    const [batchSaving, setBatchSaving] = useState(false)
    const [batchSummary, setBatchSummary] = useState("")
    const [batchAssistAccountId, setBatchAssistAccountId] = useState<string | undefined>()
    const [batchAssistType, setBatchAssistType] = useState<number | undefined>()
    const [batchAssistItemId, setBatchAssistItemId] = useState<string | undefined>()
    /** 批量修改弹窗里的辅助核算下拉：只列所选科目已配置的维度，档案按类别取。 */
    const { configs: batchAssistConfigs } = useAccountAssistConfigs(
        accountSetId,
        [batchAssistAccountId],
        token,
    )
    const batchAssistItems = useAssistItems(accountSetId, token, batchAssistType)

    // 客户端分页：直接对数组切页。
    const [page, setPage] = useState(1)
    const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE)

    // ── 凭证导入相关状态 ──
    const importInputRef = useRef<HTMLInputElement>(null)
    const [importOpen, setImportOpen] = useState(false)
    const [importFile, setImportFile] = useState<File | null>(null)
    const [importDragging, setImportDragging] = useState(false)
    const [importing, setImporting] = useState(false)
    const [importConflict, setImportConflict] = useState<0 | 1>(0)
    const [importResult, setImportResult] = useState<VoucherImportResult | null>(null)
    const [importError, setImportError] = useState<string | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const config = apiConfig(token)
            const [list, everyAccount, words] = await Promise.all([
                new VouchersApi(config).accountsetsAccountSetIdVouchersGet({
                    accountSetId,
                    // DateOnly 口径：先取 yyyy-MM-dd 字符串再构造 Date，避免东八区回退一天。
                    ...toQueryParams(filters, recycleMode),
                }),
                new AccountsApi(config).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: false }),
                new VoucherWordsApi(config).accountsetsAccountSetIdVoucherwordsGet({ accountSetId }),
            ])
            setVouchers(list)
            setAllAccounts(everyAccount)
            setVoucherWords(words.map((w) => w.word).filter((w): w is string => !!w))
            setSelectedKeys([])
        } catch (err) {
            setError(await apiErrorMessage(err, "加载失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, filters, recycleMode])

    useEffect(() => {
        void load()
    }, [load])

    // 当 URL hash 为 #import 时自动打开导入抽屉并清除 hash
    useEffect(() => {
        if (window.location.hash === "#import") {
            setImportOpen(true)
            window.history.replaceState(null, "", window.location.pathname + window.location.search)
        }
    }, [])

    // 首页「导入凭证」带 ?import=1 直达导入抽屉；页面被 KeepAlive 复用时也能响应参数变化。
    useEffect(() => {
        if (searchParams.get("import") === "1") {
            setImportOpen(true)
        }
    }, [searchParams])

    async function handleDelete(v: VoucherListItemDto) {
        if (deletingId || !v.id) return
        setDeletingId(v.id)
        setError(null)
        try {
            const api = new VouchersApi(apiConfig(token))
            // 正常凭证先反过账再删除；所属期间已结账时由后端拦下。
            if (v.status === 3) {
                await api.accountsetsAccountSetIdVouchersIdUnpostPost({ accountSetId, id: v.id })
            }
            await api.accountsetsAccountSetIdVouchersIdDelete({
                accountSetId,
                id: v.id,
            })
            setVouchers((prev) => prev.filter((x) => x.id !== v.id))
            setSelectedKeys((prev) => prev.filter((key) => key !== v.id))
            message.success("凭证已删除")
        } catch (err) {
            setError(await apiErrorMessage(err, "删除失败，请重试。"))
        } finally {
            setDeletingId(null)
        }
    }

    /** 复制凭证：生成同凭证字下的新凭证并直接入账，成功后刷新列表。 */
    async function handleCopy(v: VoucherListItemDto) {
        if (!v.id) return
        try {
            const created = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdCopyPost({
                accountSetId,
                id: v.id,
            })
            message.success(`已复制为 ${created.voucherNo}（已入账）`)
            void load()
        } catch (err) {
            setError(await apiErrorMessage(err, "复制失败，请重试。"))
        }
    }

    /** 红冲凭证：生成金额取反的凭证并直接入账，摘要带「冲销…」前缀。 */
    async function handleReverse(v: VoucherListItemDto) {
        if (!v.id) return
        const ok = await new Promise<boolean>((resolve) =>
            modal.confirm({
                title: `红冲凭证「${v.voucherNo}」？`,
                content: "将生成一张借贷金额取反的凭证并直接入账，原凭证保留不动。",
                okText: "红冲",
                cancelText: "取消",
                onOk: () => resolve(true),
                onCancel: () => resolve(false),
            }),
        )
        if (!ok) return
        try {
            const created = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdReversePost({
                accountSetId,
                id: v.id,
            })
            message.success(`已生成红字凭证 ${created.voucherNo}（已入账）`)
            void load()
        } catch (err) {
            setError(await apiErrorMessage(err, "红冲失败，请重试。"))
        }
    }

    /** 回收站：还原一张凭证。 */
    async function handleRestore(v: VoucherListItemDto) {
        if (!v.id) return
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdRestorePost({
                accountSetId,
                id: v.id,
            })
            setVouchers((prev) => prev.filter((x) => x.id !== v.id))
            message.success(`凭证 ${v.voucherNo} 已还原`)
        } catch (err) {
            setError(await apiErrorMessage(err, "还原失败，请重试。"))
        }
    }

    /** 回收站：彻底删除一张凭证（不可恢复）。 */
    async function handlePurge(v: VoucherListItemDto) {
        if (!v.id) return
        const id = v.id
        const ok = await new Promise<boolean>((resolve) =>
            modal.confirm({
                title: `彻底删除凭证「${v.voucherNo}」？`,
                content: "彻底删除后不可恢复，且不占用凭证号。",
                okText: "彻底删除",
                okButtonProps: { danger: true },
                cancelText: "取消",
                onOk: () => resolve(true),
                onCancel: () => resolve(false),
            }),
        )
        if (!ok) return
        try {
            await withStepUp((h) =>
                new VouchersApi(apiConfigWithHeaders(token, h)).accountsetsAccountSetIdVouchersIdPurgeDelete({
                    accountSetId,
                    id,
                }),
            )
            setVouchers((prev) => prev.filter((x) => x.id !== v.id))
            message.success("已彻底删除")
        } catch (err) {
            setError(await apiErrorMessage(err, "删除失败，请重试。"))
        }
    }

    /** 回收站：清空（含全部彻底删除）。 */
    function clearRecycleBin() {
        modal.confirm({
            title: "清空回收站？",
            content: "回收站中的凭证将被彻底删除，不可恢复。",
            okText: "清空",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: async () => {
                try {
                    await withStepUp((h) =>
                        new VouchersApi(apiConfigWithHeaders(token, h)).accountsetsAccountSetIdVouchersRecycleBinDelete({
                            accountSetId,
                        }),
                    )
                    message.success("回收站已清空")
                    void load()
                } catch (err) {
                    setError(await apiErrorMessage(err, "清空失败，请重试。"))
                }
            },
        })
    }

    /** 整理凭证：按凭证号或日期重新顺次编号。 */
    async function handleTidy() {
        if (tidying) return
        setTidying(true)
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersRenumberPost({
                accountSetId,
                renumberVouchersRequest: {
                    word: tidyWord ?? undefined,
                    periodStart: monthRange(tidyFrom, "start"),
                    periodEnd: monthRange(tidyTo, "end"),
                    byDate: tidyByDate,
                },
            })
            setTidyOpen(false)
            message.success("凭证号已重排")
            void load()
        } catch (err) {
            setError(await apiErrorMessage(err, "整理失败，请重试。"))
        } finally {
            setTidying(false)
        }
    }

    /** 合并暂存凭证：把所选凭证并入凭证号最小的一张，其余删除。 */
    async function handleMerge() {
        if (merging) return
        const ids = selectedKeys.map(String).filter(Boolean)
        if (ids.length < 2) {
            message.warning("请至少勾选两张暂存凭证")
            return
        }
        setMerging(true)
        try {
            // 合并会删除源凭证，属高危操作：由 StepUpProvider 统一做二次确认。
            const merged = await withStepUp((h) => mergeVouchers(accountSetId, ids, token, h))
            message.success(`已合并为 ${merged.voucherNo}`)
            setMergeOpen(false)
            setSelectedKeys([])
            void load()
        } catch (err) {
            setError(await apiErrorMessage(err, "合并失败，请重试。"))
        } finally {
            setMerging(false)
        }
    }

    /** 批量修改暂存凭证：摘要覆盖 + 按科目统一辅助核算。 */
    async function handleBatchUpdate() {
        if (batchSaving) return
        const ids = selectedKeys.map(String).filter(Boolean)
        if (ids.length === 0) {
            message.warning("请先勾选要修改的凭证")
            return
        }
        const summary = batchSummary.trim()
        const hasAssist = Boolean(batchAssistAccountId || batchAssistType)
        if (!summary && !hasAssist) {
            message.warning("请至少填写摘要或选择辅助核算")
            return
        }
        if (hasAssist && !(batchAssistAccountId && batchAssistType)) {
            message.warning("设置辅助核算需要同时选择科目与类别")
            return
        }

        setBatchSaving(true)
        try {
            const result = await batchUpdateVouchers(
                accountSetId,
                {
                    ids,
                    summary: summary || undefined,
                    assist:
                        batchAssistAccountId && batchAssistType
                            ? {
                                  accountId: batchAssistAccountId,
                                  type: batchAssistType,
                                  itemId: batchAssistItemId ?? null,
                              }
                            : undefined,
                },
                token,
            )
            message.success(`已修改 ${result.updated} 张凭证`)
            setBatchOpen(false)
            setBatchSummary("")
            setBatchAssistAccountId(undefined)
            setBatchAssistType(undefined)
            setBatchAssistItemId(undefined)
            setSelectedKeys([])
            void load()
        } catch (err) {
            setError(await apiErrorMessage(err, "批量修改失败，请重试。"))
        } finally {
            setBatchSaving(false)
        }
    }

    /** 批量合并前先做本地预检，把服务端会拒绝的明显组合提前说清楚。 */
    function openMergeDialog() {
        const chosen = vouchers.filter((v) => selectedKeys.includes(v.id ?? ""))
        if (chosen.length < 2) {
            message.warning("请至少勾选两张暂存凭证")
            return
        }
        const posted = chosen.find((v) => v.status !== 1)
        if (posted) {
            message.warning(`${posted.voucherNo} 是正常凭证，请先反过账后再合并`)
            return
        }
        const first = chosen[0]
        const inconsistent = chosen.find((v) => v.word !== first.word || v.date !== first.date)
        if (inconsistent) {
            message.warning("只能合并同一凭证字、同一日期的凭证")
            return
        }
        setMergeOpen(true)
    }

    /** 批量修改弹窗：只允许暂存凭证（与后端口径一致，避免提交后才被拒绝）。 */
    function openBatchDialog() {
        if (selectedKeys.length === 0) {
            message.warning("请先勾选要修改的凭证")
            return
        }
        const posted = vouchers.find((v) => selectedKeys.includes(v.id ?? "") && v.status !== 1)
        if (posted) {
            message.warning(`${posted.voucherNo} 是正常凭证，请先反过账后再批量修改`)
            return
        }
        setBatchOpen(true)
    }

    /**
     * 导出凭证。
     * @param scope ids 只导出选中；query 按当前筛选条件；all 导出全部未删除凭证。
     */
    async function handleExport(scope: "ids" | "query" | "all") {
        if (scope === "ids" && selectedKeys.length === 0) {
            message.warning("请先勾选要导出的凭证")
            return
        }
        try {
            // 导出返回的是文件流，生成的客户端拿不到 Blob，这里直接走 fetch。
            const res = await fetch(`/api/accountsets/${accountSetId}/vouchers/export`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    ...(token ? { Authorization: `Bearer ${token}` } : {}),
                },
                body: JSON.stringify({
                    // 表头上方的关键字/状态筛选只在本地生效，导出「当前查询」时用结果集 ids 对齐，
                    // 否则导出的内容会比屏幕上多。
                    ids:
                        scope === "ids"
                            ? selectedKeys.map(String)
                            : scope === "query" && (quickKeyword.trim() || statusFilter !== "all")
                              ? visibleVouchers.map((v) => v.id ?? "").filter(Boolean)
                              : undefined,
                    query: scope === "all" ? undefined : toQueryParams(filters, recycleMode),
                }),
            })
            if (!res.ok) {
                const text = await res.text()
                throw new Error(text || "导出失败")
            }
            const blob = await res.blob()
            const url = URL.createObjectURL(blob)
            const a = document.createElement("a")
            a.href = url
            a.download = `凭证导出_${dayjs().format("YYYYMMDD_HHmmss")}.xlsx`
            a.click()
            setTimeout(() => URL.revokeObjectURL(url), 30_000)
            message.success("导出已开始下载")
        } catch (err) {
            setError(await apiErrorMessage(err, "导出失败，请重试。"))
        }
    }

    /** 直接打印：按柠檬云的方式打印已勾选的凭证，未勾选时给出提示。 */
    function printCurrentList() {
        const ids = selectedKeys.map(String).filter(Boolean)
        if (ids.length === 0) {
            message.warning("亲，您还没有选择凭证哦！")
            return
        }
        setPrintVoucherIds(ids)
    }

    /** 批量复制选中凭证。 */
    async function batchCopy() {
        const ids = selectedKeys.map(String)
        if (ids.length === 0) {
            message.warning("请先勾选要复制的凭证")
            return
        }
        const api = new VouchersApi(apiConfig(token))
        let done = 0
        const failed: string[] = []
        for (const id of ids) {
            try {
                await api.accountsetsAccountSetIdVouchersIdCopyPost({ accountSetId, id })
                done++
            } catch (err) {
                failed.push(await apiErrorMessage(err, "复制失败"))
            }
        }
        setSelectedKeys([])
        if (failed.length > 0) {
            setError(`有 ${failed.length} 张凭证复制失败：\n${failed.join("\n")}`)
        } else {
            message.success(`已复制 ${done} 张凭证`)
        }
        void load()
    }

    /** 批量删除选中凭证（移入回收站）。 */
    function batchSoftDelete() {
        const ids = selectedKeys.map(String)
        if (ids.length === 0) {
            message.warning("请先勾选要删除的凭证")
            return
        }
        modal.confirm({
            title: `确认删除选中的 ${ids.length} 张凭证？`,
            content: "删除后凭证会移入回收站，可以还原。",
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: async () => {
                let done = 0
                const failed: string[] = []
                for (const id of ids) {
                    try {
                        // 正常凭证先反过账再删除。
                        if (vouchers.find((v) => v.id === id)?.status === 3) {
                            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdUnpostPost({ accountSetId, id })
                        }
                        await withStepUp((h) =>
                            new VouchersApi(apiConfigWithHeaders(token, h)).accountsetsAccountSetIdVouchersIdDelete({
                                accountSetId,
                                id,
                            }),
                        )
                        done++
                    } catch (err) {
                        failed.push(await apiErrorMessage(err, "删除失败"))
                    }
                }
                setSelectedKeys([])
                if (failed.length > 0) {
                    setError(`有 ${failed.length} 张凭证删除失败：\n${failed.join("\n")}`)
                } else {
                    message.success(`已将 ${done} 张凭证移入回收站`)
                }
                void load()
            },
        })
    }

    function handleEdit(v: VoucherListItemDto) {
        if (!v.id) return
        setEditingId(v.id)
        setOpen(true)
    }

    function handleCreate() {
        setEditingId(null)
        setInsertSeed(null)
        setOpen(true)
    }

    /** 插入：以该凭证的日期与凭证字新建一张凭证（凭证号由后端续号）。 */
    function handleInsert(v: VoucherListItemDto) {
        setEditingId(null)
        setInsertSeed({ date: toISODate(v.date), word: v.word ?? "记" })
        setOpen(true)
    }

    /** 存为模板：把该凭证的摘要与科目（不记金额）保存成凭证模板。 */
    function handleSaveTemplate(v: VoucherListItemDto) {
        setTemplateTarget(v)
        setTemplateName(`${v.voucherNo} 模板`)
        setTemplateType(1)
    }

    async function confirmSaveTemplate() {
        if (!templateTarget?.id || templateSaving) return
        if (!templateName.trim()) {
            message.warning("请填写模板名称")
            return
        }
        setTemplateSaving(true)
        try {
            const api = new VouchersApi(apiConfig(token))
            const detail = await api.accountsetsAccountSetIdVouchersIdGet({ accountSetId, id: templateTarget.id })
            const lines = (detail.entries ?? []).map((e) => ({
                summary: e.summary ?? "",
                accountId: e.accountId ?? "",
                isDebit: (e.debit ?? 0) > 0,
            }))
            await new VoucherTemplatesApi(apiConfig(token)).accountsetsAccountSetIdVouchertemplatesPost({
                accountSetId,
                saveVoucherTemplateRequest: { name: templateName.trim(), type: templateType, lines },
            })
            message.success("已保存为凭证模板")
            setTemplateTarget(null)
        } catch (err) {
            setError(await apiErrorMessage(err, "保存模板失败，请重试。"))
        } finally {
            setTemplateSaving(false)
        }
    }

    function handleSaved() {
        setOpen(false)
        setEditingId(null)
        setInsertSeed(null)
        void load()
    }

    // ── 凭证导入处理 ──
    function pickImportFile(next: File | null) {
        setImportFile(next)
        setImportResult(null)
        setImportError(null)
    }

    function onImportDrop(e: React.DragEvent<HTMLDivElement>) {
        e.preventDefault()
        setImportDragging(false)
        const dropped = e.dataTransfer.files?.[0]
        if (dropped) pickImportFile(dropped)
    }

    async function handleImport() {
        if (!importFile || importing) return
        if (!accountSetId) {
            setImportError("请先选择账套。")
            return
        }
        setImporting(true)
        setImportError(null)
        setImportResult(null)
        try {
            const res = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersImportPost({
                accountSetId,
                file: importFile,
                conflict: importConflict,
            })
            setImportResult(res)
            void load()
        } catch (err) {
            setImportError(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    /** 即时筛选后的结果：关键字匹配凭证号、摘要、科目名与金额，状态区分正常/暂存。 */
    const visibleVouchers = useMemo(() => {
        const keyword = quickKeyword.trim().toLowerCase()
        return vouchers.filter((v) => {
            if (statusFilter === "posted" && v.status !== 3) return false
            if (statusFilter === "draft" && v.status === 3) return false
            if (!keyword) return true
            const haystack = [
                v.voucherNo,
                v.word,
                v.preparer,
                v.totalDebit != null ? fmtAmount(v.totalDebit, true) : "",
                v.totalCredit != null ? fmtAmount(v.totalCredit, true) : "",
                ...(v.entries ?? []).flatMap((e) => [e.summary, e.accountCode, e.accountName]),
            ]
                .filter(Boolean)
                .join(" ")
                .toLowerCase()
            return haystack.includes(keyword)
        })
    }, [vouchers, quickKeyword, statusFilter])

    /** 本次筛选结果的借贷合计：页脚一眼可见，不用等导出。 */
    const listTotals = useMemo(
        () =>
            visibleVouchers.reduce(
                (acc, v) => {
                    acc.debit += v.totalDebit ?? 0
                    acc.credit += v.totalCredit ?? 0
                    return acc
                },
                { debit: 0, credit: 0 },
            ),
        [visibleVouchers],
    )

    // 数据变少时把页码夹回有效范围，避免停留在空页。
    const pageCount = Math.max(1, Math.ceil(visibleVouchers.length / pageSize))
    const currentPage = Math.min(page, pageCount)
    const pageRows = useMemo(
        () => visibleVouchers.slice((currentPage - 1) * pageSize, (currentPage - 1) * pageSize + pageSize),
        [visibleVouchers, currentPage, pageSize],
    )
    const from = visibleVouchers.length === 0 ? 0 : (currentPage - 1) * pageSize + 1
    const to = Math.min(currentPage * pageSize, visibleVouchers.length)

    /** 制单人候选：取自当前结果，避免为下拉再单独开接口。 */
    const preparers = useMemo(
        () => [...new Set(vouchers.map((v) => v.preparer).filter((p): p is string => !!p))].sort(),
        [vouchers],
    )

    const allSelected = pageRows.length > 0 && pageRows.every((v) => selectedKeys.includes(v.id ?? ""))
    const someSelected = !allSelected && pageRows.some((v) => selectedKeys.includes(v.id ?? ""))

    /** 合并/批量修改弹窗展示用：当前勾选的凭证与合并目标（凭证号最小的一张）。 */
    const selectedVouchers = useMemo(
        () => vouchers.filter((v) => selectedKeys.includes(v.id ?? "")),
        [vouchers, selectedKeys],
    )
    const mergeTarget = useMemo(
        () => [...selectedVouchers].sort((a, b) => (a.number ?? 0) - (b.number ?? 0))[0],
        [selectedVouchers],
    )
    const batchAssistTypeOptions = useMemo(
        () =>
            (batchAssistAccountId ? (batchAssistConfigs[batchAssistAccountId] ?? []) : []).map((c) => ({
                value: c.type ?? 0,
                label: c.typeText ?? String(c.type ?? ""),
            })),
        [batchAssistAccountId, batchAssistConfigs],
    )
    const batchAssistAccountOptions = useMemo(
        () =>
            allAccounts
                .filter((a) => a.isLeaf !== false && a.isEnabled !== false)
                .map((a) => ({ value: a.id ?? "", label: `${a.code} ${a.name}` })),
        [allAccounts],
    )
    const batchAssistItemOptions = useMemo(
        () =>
            batchAssistItems
                .filter((item) => item.type === batchAssistType && item.isEnabled !== false)
                .map((item) => ({ value: item.id ?? "", label: `${item.code} ${item.name}` })),
        [batchAssistItems, batchAssistType],
    )

    const toggleSelect = (id: string, checked: boolean) =>
        setSelectedKeys((prev) => (checked ? [...prev, id] : prev.filter((key) => key !== id)))

    const confirmDelete = (v: VoucherListItemDto) => {
        modal.confirm({
            title: `确认删除凭证「${v.voucherNo}」？`,
            content: `${toISODate(v.date)} · 删除后移入回收站，可以还原。`,
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: () => handleDelete(v),
        })
    }

    /** 表格行：当前页的凭证摊平成「分录行 + 合计行」。 */
    const voucherRows = toVoucherRows(pageRows)

    const voucherColumns = buildVoucherColumns({
        selectedKeys,
        deletingId,
        recycleMode,
        selectAll: {
            checked: allSelected,
            indeterminate: someSelected,
            onChange: (checked) => setSelectedKeys(checked ? pageRows.map((v) => v.id ?? "") : []),
        },
        onToggleSelect: toggleSelect,
        onEdit: handleEdit,
        onDelete: confirmDelete,
        onInsert: handleInsert,
        onPrint: (r) => setPrintVoucherId(r.id ?? null),
        onCopy: (r) => void handleCopy(r),
        onReverse: (r) => void handleReverse(r),
        onSaveTemplate: handleSaveTemplate,
        onRestore: (r) => void handleRestore(r),
        onPurge: (r) => void handlePurge(r),
    })

    return (
        <div className="flex flex-col gap-3">
            {error && <Alert type="error" showIcon title={error} />}

            <Card>
                {/* 工具行：左侧筛选入口 + 右侧操作按钮，单行排布（与柠檬云一致） */}
                <Flex justify="space-between" align="center" gap={12} wrap style={{ marginBottom: 16 }}>
                    <Space size={8} wrap>
                        <Popover
                            open={filterOpen}
                            trigger="click"
                            placement="bottomLeft"
                            onOpenChange={(next) => {
                                setFilterOpen(next)
                                // 展开时用当前生效条件初始化草稿，避免上次未确认的编辑残留。
                                if (next) setDraftFilters(filters)
                            }}
                            content={
                                <div className="flex w-[420px] flex-col">
                                    <FilterPanel
                                        value={draftFilters}
                                        onChange={setDraftFilters}
                                        accounts={allAccounts}
                                        words={voucherWords}
                                        preparers={preparers}
                                    />
                                    <div className="mt-2 flex items-center justify-between gap-3 border-t border-(--border) pt-2">
                                    <Button
                                        type="link"
                                        style={{ paddingInline: 0 }}
                                        onClick={() => {
                                            setDraftFilters(defaultFilters())
                                            setFilters(defaultFilters())
                                            setFilterOpen(false)
                                            setPage(1)
                                        }}
                                    >
                                        恢复默认排序
                                    </Button>
                                    <Space size={8}>
                                        <Button onClick={() => setFilterOpen(false)}>
                                            取消
                                        </Button>
                                        <Button
                                            onClick={() => {
                                                setDraftFilters(defaultFilters())
                                            }}
                                        >
                                            重置
                                        </Button>
                                        <Button
                                            type="primary"
                                            onClick={() => {
                                                setFilters(draftFilters)
                                                setFilterOpen(false)
                                                setPage(1)
                                            }}
                                        >
                                            确定
                                        </Button>
                                        </Space>
                                    </div>
                                </div>
                            }
                        >
                            <FilterTriggerButton
                                title="高级筛选"
                                label={periodText(filters)}
                                count={activeFilterCount(filters)}
                            />
                        </Popover>
                        {selectedKeys.length > 0 && (
                            <Typography.Text type="secondary">已选 {selectedKeys.length} 张</Typography.Text>
                        )}
                        {recycleMode && (
                            <Tag color="orange">回收站</Tag>
                        )}
                    </Space>
                    <Flex justify="flex-end" align="center" gap={8} wrap flex="1 1 auto" style={{ minWidth: 0 }}>
                        {recycleMode ? (
                            <>
                                <Button onClick={() => setRecycleMode(false)}>返回</Button>
                                <Button danger onClick={clearRecycleBin}>
                                    清空回收站
                                </Button>
                                <Button danger disabled={selectedKeys.length === 0} onClick={batchSoftDelete}>
                                    批量删除
                                </Button>
                            </>
                        ) : (
                            <>
                                <Button type="primary" icon={<PlusOutlined />} disabled={!accountSetId} onClick={handleCreate}>
                                    新增凭证
                                </Button>
                                {/* 版式（纸张、边距、签章栏位）由后端统一渲染，前端不再有「打印设置」入口 */}
                                <Button icon={<PrinterOutlined />} disabled={!accountSetId} onClick={printCurrentList}>
                                    打印
                                </Button>
                                <Tooltip title="跳到明细账，按当前期间查看电子账簿">
                                    <Button onClick={() => navigate(setPath(accountSetId, "/ledger/subsidiary"))}>电子账簿</Button>
                                </Tooltip>
                                {toolbarDropdown(
                                    "导出",
                                    [
                                        { key: "ids", label: "选中凭证", disabled: selectedKeys.length === 0 },
                                        { key: "query", label: "当前查询凭证" },
                                        { key: "all", label: "所有凭证" },
                                    ],
                                    ({ key }) => void handleExport(key as "ids" | "query" | "all"),
                                    <DownloadOutlined />,
                                )}
                                <Button disabled={!accountSetId} onClick={() => setImportOpen(true)}>
                                    导入凭证
                                </Button>
                                <Button
                                    onClick={() => {
                                        setTidyFrom(periodOf(filters))
                                        setTidyTo(periodOf(filters))
                                        setTidyWord(null)
                                        setTidyByDate(false)
                                        setTidyOpen(true)
                                    }}
                                >
                                    整理凭证
                                </Button>
                                {toolbarDropdown(
                                    "批量操作",
                                    [
                                        { key: "copy", label: "批量复制", disabled: selectedKeys.length === 0 },
                                        { key: "delete", label: "批量删除", icon: <DeleteOutlined />, disabled: selectedKeys.length === 0 },
                                        { key: "modify", label: "批量修改", disabled: selectedKeys.length === 0 },
                                        { key: "sort", label: "凭证号排序（可跨期）" },
                                        { key: "adjust", label: "凭证号调整", disabled: true },
                                        { key: "merge", label: "凭证合并", disabled: selectedKeys.length < 2 },
                                    ],
                                    ({ key }) => {
                                        if (key === "copy") void batchCopy()
                                        else if (key === "delete") batchSoftDelete()
                                        else if (key === "modify") openBatchDialog()
                                        else if (key === "merge") openMergeDialog()
                                        else if (key === "sort") {
                                            setTidyFrom(periodOf(filters))
                                            setTidyTo(periodOf(filters))
                                            setTidyWord(null)
                                            setTidyByDate(false)
                                            setTidyOpen(true)
                                        }
                                    },
                                )}
                                <Dropdown
                                    trigger={["click"]}
                                    popupRender={() => (
                                        <div className="min-w-56 rounded-lg border border-(--border) bg-(--card) p-3 shadow-lg">
                                            <div className="flex flex-col gap-2">
                                                <Checkbox disabled>显示数量单价</Checkbox>
                                                <Checkbox disabled>显示原币汇率</Checkbox>
                                            </div>
                                            <div className="mt-2 text-[12px] text-(--muted-foreground)">
                                                需先在「设置 · 科目」启用数量核算 / 外币核算
                                            </div>
                                        </div>
                                    )}
                                >
                                    <Button>
                                        更多
                                        <DownOutlined style={{ fontSize: 10 }} />
                                    </Button>
                                </Dropdown>
                                <Button onClick={() => setRecycleMode(true)}>回收站</Button>
                                <Tooltip title="刷新">
                                    <Button
                                        icon={<ReloadOutlined spin={loading} />}
                                        disabled={!accountSetId || loading}
                                        onClick={() => void load()}
                                    />
                                </Tooltip>
                            </>
                        )}
                    </Flex>
                </Flex>
                {/* 表头上方的即时筛选：快捷期间 / 状态 / 关键字都在本地过滤，改完立即出结果，
                    不必进筛选面板再点「确定」。右侧同步给出本次结果的张数与借贷合计。 */}
                <Flex
                    justify="space-between"
                    align="center"
                    gap={12}
                    wrap
                    style={{ marginBottom: 12, padding: "8px 10px", background: "var(--muted)", borderRadius: 8 }}
                >
                    <Space size={8} wrap>
                        <Segmented
                            size="small"
                            value={periodPreset(filters) === "custom" ? undefined : periodPreset(filters)}
                            options={[
                                { value: "month", label: "本月" },
                                { value: "quarter", label: "本季" },
                                { value: "year", label: "本年" },
                                { value: "all", label: "全部" },
                            ]}
                            onChange={(value) => {
                                const next = { ...filters, ...presetRange(value as PeriodPreset, filters.dateKind) }
                                setFilters(next)
                                setDraftFilters(next)
                                setPage(1)
                            }}
                        />
                        <Segmented
                            size="small"
                            value={statusFilter}
                            options={[
                                { value: "all", label: "全部状态" },
                                { value: "draft", label: "暂存" },
                                { value: "posted", label: "正常" },
                            ]}
                            onChange={(value) => {
                                setStatusFilter(value as typeof statusFilter)
                                setPage(1)
                            }}
                        />
                        <Input
                            allowClear
                            prefix={<SearchOutlined className="text-(--muted-foreground)" />}
                            placeholder="搜索凭证号 / 摘要 / 科目 / 金额"
                            value={quickKeyword}
                            onChange={(event) => {
                                setQuickKeyword(event.target.value)
                                setPage(1)
                            }}
                            style={{ width: 250 }}
                        />
                    </Space>
                    <span className="text-[13px] text-(--muted-foreground)">
                        共 <span className="font-medium text-(--foreground)">{visibleVouchers.length}</span> 张 · 借方{" "}
                        <span className="font-medium tabular-nums text-(--foreground)">{fmtAmount(listTotals.debit, true)}</span>
                        {" "}· 贷方{" "}
                        <span className="font-medium tabular-nums text-(--foreground)">{fmtAmount(listTotals.credit, true)}</span>
                    </span>
                </Flex>
                <Table<VoucherRow>
                    columns={voucherColumns}
                    dataSource={voucherRows}
                    rowKey="key"
                    pagination={false}
                    loading={loading}
                    sticky
                    scroll={{ x: "max-content" }}
                    summary={() => (
                        <Table.Summary fixed>
                            <Table.Summary.Row>
                                <Table.Summary.Cell index={0} colSpan={5} className="font-medium">
                                    合计（{visibleVouchers.length} 张）
                                </Table.Summary.Cell>
                                <Table.Summary.Cell index={5} align="right" className="tabular-nums font-medium">
                                    {fmtAmount(listTotals.debit, true)}
                                </Table.Summary.Cell>
                                <Table.Summary.Cell index={6} align="right" className="tabular-nums font-medium">
                                    {fmtAmount(listTotals.credit, true)}
                                </Table.Summary.Cell>
                                <Table.Summary.Cell index={7} colSpan={4} />
                            </Table.Summary.Row>
                        </Table.Summary>
                    )}
                    locale={{
                        emptyText: recycleMode
                            ? "回收站是空的。"
                            : periodPreset(filters) === "month" && !quickKeyword.trim() && statusFilter === "all"
                              ? "本月暂无凭证：可点上方「本年」或「全部」查看历史凭证，或点「新增凭证」开始记账。"
                              : "没有符合条件的凭证，可调整筛选条件或点击「新增凭证」开始记账。",
                    }}
                />

                <Flex justify="space-between" align="center" gap={16} wrap style={{ marginTop: 16 }}>
                    <Typography.Text type="secondary">
                        显示 {from} 到 {to}，共 {visibleVouchers.length} 条
                    </Typography.Text>
                    <Pagination
                        current={currentPage}
                        pageSize={pageSize}
                        total={visibleVouchers.length}
                        showSizeChanger
                        pageSizeOptions={PAGE_SIZE_OPTIONS}
                        onChange={(nextPage, nextSize) => {
                            if (nextSize !== pageSize) {
                                setPageSize(nextSize)
                                setPage(1)
                            } else {
                                setPage(nextPage)
                            }
                        }}
                    />
                </Flex>
            </Card>

            {/* 新增 / 编辑凭证抽屉：与新增凭证页共用同一套纸样凭证组件 */}
            <Drawer
                open={open}
                size={1152}
                title={
                    editingId
                        ? `编辑凭证 ${vouchers.find((v) => v.id === editingId)?.voucherNo ?? ""}`
                        : "新增凭证"
                }
                onClose={() => {
                    setOpen(false)
                    setEditingId(null)
                    setInsertSeed(null)
                }}
                styles={{ body: { padding: 0, display: "flex", flexDirection: "column" } }}
            >
                {open && (
                    <VoucherSheet
                        key={editingId ?? (insertSeed ? `insert-${insertSeed.date}-${insertSeed.word}` : "new")}
                        voucherId={editingId}
                        initialDate={insertSeed?.date}
                        initialWord={insertSeed?.word ?? voucherWords[0]}
                        embedded
                        onSaved={handleSaved}
                        onCancel={() => {
                            setOpen(false)
                            setEditingId(null)
                            setInsertSeed(null)
                        }}
                    />
                )}
            </Drawer>

            {/* 存为模板 */}
            <Modal
                open={!!templateTarget}
                title={`存为模板${templateTarget ? `（${templateTarget.voucherNo}）` : ""}`}
                width={440}
                confirmLoading={templateSaving}
                onCancel={() => setTemplateTarget(null)}
                onOk={() => void confirmSaveTemplate()}
                okText="保存"
                cancelText="取消"
            >
                <div className="flex flex-col gap-4 py-2">
                    <div className="flex items-center gap-3">
                        <span className="w-[72px] text-right text-[13px] text-(--muted-foreground)">模板名称：</span>
                        <Input value={templateName} onChange={(e) => setTemplateName(e.target.value)} placeholder="请输入模板名称" />
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="w-[72px] text-right text-[13px] text-(--muted-foreground)">模板类型：</span>
                        <Select
                            style={{ width: 160 }}
                            value={templateType}
                            onChange={setTemplateType}
                            options={[
                                { value: 1, label: "日常支出" },
                                { value: 2, label: "采购" },
                                { value: 3, label: "销售" },
                                { value: 4, label: "其他" },
                            ]}
                        />
                    </div>
                    <div className="text-[12px] text-(--muted-foreground)">模板只记录摘要、科目与借贷方向，不记金额。</div>
                </div>
            </Modal>

            {/* 凭证打印 */}
            <PrintVoucherSheet
                accountSetId={accountSetId}
                token={token}
                voucherId={printVoucherId}
                voucherIds={printVoucherIds ?? undefined}
                open={!!printVoucherId || !!printVoucherIds}
                onOpenChange={(o) => {
                    if (!o) {
                        setPrintVoucherId(null)
                        setPrintVoucherIds(null)
                    }
                }}
            />

            {/* 整理凭证：期间 + 凭证字 + 重排依据（与柠檬云弹窗字段一致） */}
            <Modal
                open={tidyOpen}
                title="整理凭证"
                width={480}
                confirmLoading={tidying}
                onCancel={() => setTidyOpen(false)}
                onOk={() => void handleTidy()}
                okText="确认"
                cancelText="取消"
            >
                <div className="flex flex-col gap-4 py-2">
                    <div className="flex items-center gap-3">
                        <span className="w-[72px] text-right text-[13px] text-(--muted-foreground)">整理区间：</span>
                        <DatePicker.RangePicker
                            picker="month"
                            allowClear
                            placeholder={["起始期间", "结束期间"]}
                            value={[
                                tidyFrom ? dayjs(`${tidyFrom}-01`) : null,
                                tidyTo ? dayjs(`${tidyTo}-01`) : null,
                            ]}
                            onChange={(dates) => {
                                setTidyFrom(dates?.[0] ? dates[0].format("YYYY-MM") : null)
                                setTidyTo(dates?.[1] ? dates[1].format("YYYY-MM") : null)
                            }}
                            style={{ width: 300 }}
                        />
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="w-[72px] text-right text-[13px] text-(--muted-foreground)">凭证字：</span>
                        <Select
                            style={{ width: 160 }}
                            value={tidyWord ?? "__all__"}
                            onChange={(v) => setTidyWord(v === "__all__" ? null : v)}
                            options={[{ value: "__all__", label: "全部" }, ...voucherWords.map((w) => ({ value: w, label: w }))]}
                        />
                    </div>
                    <Radio.Group value={tidyByDate ? "date" : "number"} onChange={(e) => setTidyByDate(e.target.value === "date")}>
                        <div className="flex flex-col gap-2">
                            <Radio value="number">按凭证号重新顺次编号</Radio>
                            <Radio value="date">按凭证日期重新顺次编号</Radio>
                        </div>
                    </Radio.Group>
                    <div className="text-[12px] text-(--muted-foreground)">
                        编号从 1 开始重新连续编排，已删除（回收站）的凭证不占号；可按月区间跨期整理，范围内含正常凭证时不允许重排。
                    </div>
                </div>
            </Modal>

            {/* 凭证合并：把多张暂存凭证并入凭证号最小的一张 */}
            <Modal
                open={mergeOpen}
                title="凭证合并"
                width={520}
                confirmLoading={merging}
                onCancel={() => setMergeOpen(false)}
                onOk={() => void handleMerge()}
                okText="合并"
                cancelText="取消"
            >
                <div className="flex flex-col gap-3 py-2 text-[13px]">
                    <div>
                        将选中的 <span className="font-medium">{selectedVouchers.length}</span> 张暂存凭证合并为一张，
                        分录按原顺序追加，合并后其余凭证删除、不可恢复。
                    </div>
                    {mergeTarget && (
                        <div className="rounded-lg border border-(--input) bg-muted px-3 py-2 text-[12.5px] text-(--muted-foreground)">
                            保留凭证：<span className="font-medium text-(--foreground)">{mergeTarget.voucherNo}</span>
                            {" · "}
                            {toISODate(mergeTarget.date)}
                            {selectedVouchers[0] && (
                                <>
                                    {" · "}
                                    合并后共{" "}
                                    {selectedVouchers.reduce((sum, v) => sum + (v.entryCount ?? 0), 0)} 条分录
                                </>
                            )}
                        </div>
                    )}
                    <div className="text-[12px] text-(--muted-foreground)">
                        要求所选凭证均为暂存状态、同一凭证字且同一日期；带附件的凭证请先移除附件。
                    </div>
                </div>
            </Modal>

            {/* 批量修改：摘要与辅助核算等安全字段 */}
            <Modal
                open={batchOpen}
                title="批量修改凭证"
                width={560}
                confirmLoading={batchSaving}
                onCancel={() => setBatchOpen(false)}
                onOk={() => void handleBatchUpdate()}
                okText="确定修改"
                cancelText="取消"
            >
                <div className="flex flex-col gap-4 py-2">
                    <div className="text-[12.5px] text-(--muted-foreground)">
                        将对选中的 <span className="font-medium text-(--foreground)">{selectedKeys.length}</span>{" "}
                        张暂存凭证生效；金额、科目与日期不会被修改，改完仍保持暂存状态。
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="w-[72px] shrink-0 text-right text-[13px] text-(--muted-foreground)">摘要：</span>
                        <Input
                            placeholder="留空表示不修改；填写后替换全部分录摘要"
                            maxLength={200}
                            value={batchSummary}
                            onChange={(e) => setBatchSummary(e.target.value)}
                        />
                    </div>
                    <div className="flex flex-col gap-3 rounded-lg border border-(--input) bg-muted px-3 py-3">
                        <div className="flex items-center gap-3">
                            <span className="w-[72px] shrink-0 text-right text-[13px] text-(--muted-foreground)">辅助科目：</span>
                            <Select
                                allowClear
                                showSearch
                                optionFilterProp="label"
                                placeholder="选择末级科目（留空表示不改辅助核算）"
                                style={{ flex: 1 }}
                                value={batchAssistAccountId}
                                options={batchAssistAccountOptions}
                                onChange={(value) => {
                                    setBatchAssistAccountId(value)
                                    setBatchAssistType(undefined)
                                    setBatchAssistItemId(undefined)
                                }}
                            />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[72px] shrink-0 text-right text-[13px] text-(--muted-foreground)">核算类别：</span>
                            <Select
                                allowClear
                                placeholder={batchAssistAccountId ? "选择该科目已配置的维度" : "请先选择科目"}
                                style={{ flex: 1 }}
                                disabled={!batchAssistAccountId}
                                value={batchAssistType}
                                options={batchAssistTypeOptions}
                                onChange={(value) => {
                                    setBatchAssistType(value)
                                    setBatchAssistItemId(undefined)
                                }}
                                notFoundContent={batchAssistAccountId ? "该科目未配置辅助核算" : undefined}
                            />
                        </div>
                        <div className="flex items-center gap-3">
                            <span className="w-[72px] shrink-0 text-right text-[13px] text-(--muted-foreground)">核算档案：</span>
                            <Select
                                allowClear
                                showSearch
                                optionFilterProp="label"
                                placeholder="留空表示清除该类取值"
                                style={{ flex: 1 }}
                                disabled={!batchAssistType}
                                value={batchAssistItemId}
                                options={batchAssistItemOptions}
                                onChange={setBatchAssistItemId}
                            />
                        </div>
                    </div>
                </div>
            </Modal>

            <Drawer
                open={importOpen}
                size={672}
                title="导入凭证"
                onClose={() => {
                    setImportOpen(false)
                    pickImportFile(null)
                }}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
            >
                <p className="text-[12.5px] text-(--muted-foreground)">
                    支持导入其他平台导出的记账凭证文件（.xls / .xlsx）。系统按凭证号聚合分录、自动补建缺失科目，
                    借贷金额原样保留。
                </p>

                <div className="flex flex-col gap-1">
                    <span className="font-medium">导入到账套</span>
                    <span className="text-[12.5px] text-(--muted-foreground)">
                        {currentSet?.name ?? accountSetId}
                        {currentSet?.isCurrent ? "（当前）" : ""}
                    </span>
                </div>

                {/* 冲突策略 */}
                <div className="flex flex-col gap-2">
                    <span className="text-[13px] font-semibold text-(--foreground)">重复凭证处理</span>
                    <Segmented
                        value={String(importConflict)}
                        options={[
                            { value: "0", label: "跳过" },
                            { value: "1", label: "覆盖" },
                        ]}
                        onChange={(v) => setImportConflict(v === "1" ? 1 : 0)}
                    />
                </div>

                {/* 文件拖放 / 选择区 */}
                <div
                    role="button"
                    tabIndex={0}
                    onClick={() => importInputRef.current?.click()}
                    onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") importInputRef.current?.click()
                    }}
                    onDragOver={(e) => {
                        e.preventDefault()
                        setImportDragging(true)
                    }}
                    onDragLeave={() => setImportDragging(false)}
                    onDrop={onImportDrop}
                    className={cn(
                        "flex cursor-pointer flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-10 text-center transition-colors",
                        importDragging ? "border-(--brand-border) bg-(--brand-soft)" : "border-(--border)",
                    )}
                >
                    <FileExcelOutlined className="text-[40px]" style={ICON_STYLE.placeholder} />
                    {importFile ? (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">{importFile.name}</span>
                            <span className="text-sm leading-normal font-normal text-(--muted-foreground)">
                                {(importFile.size / 1024).toFixed(1)} KB · 点击可重新选择
                            </span>
                        </div>
                    ) : (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">点击选择，或拖拽文件到此处</span>
                            <span className="text-sm leading-normal font-normal text-(--muted-foreground)">支持 .xls / .xlsx，最大 10MB</span>
                        </div>
                    )}
                    <input
                        ref={importInputRef}
                        type="file"
                        accept={ACCEPT}
                        className="hidden"
                        onChange={(e) => pickImportFile(e.target.files?.[0] ?? null)}
                    />
                </div>

                <div className="flex items-center gap-3">
                    <Button
                        type="primary"
                        loading={importing}
                        icon={importing ? undefined : <UploadOutlined />}
                        disabled={!importFile || !accountSetId}
                        onClick={() => void handleImport()}
                    >
                        {importing ? "导入中…" : "开始导入"}
                    </Button>
                    {importFile && !importing && <Button onClick={() => pickImportFile(null)}>清除</Button>}
                </div>

                {importError && <Alert type="error" showIcon title={importError} />}

                {/* 导入结果 */}
                {importResult && (
                    <div className={cn(SOFT_PANEL, "flex flex-col gap-4 p-4")}>
                        <h3 className="text-[13px] font-semibold text-(--foreground)">导入结果</h3>
                        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
                            <Tile label="解析行数" value={importResult.totalRows ?? 0} tone="blue" icon={FileExcelOutlined} />
                            <Tile label="导入凭证" value={importResult.importedVouchers ?? 0} tone="green" icon={FileTextOutlined} />
                            <Tile label="导入分录" value={importResult.importedEntries ?? 0} tone="purple" icon={FileTextOutlined} />
                            <Tile label="新建科目" value={importResult.createdAccounts ?? 0} tone="amber" icon={PlusOutlined} />
                        </div>
                        <div className="grid grid-cols-2 gap-3">
                            <Tile label="覆盖凭证" value={importResult.overwrittenVouchers ?? 0} tone="gray" icon={RollbackOutlined} />
                            <Tile label="跳过凭证" value={importResult.skippedVouchers ?? 0} tone="gray" icon={RightOutlined} />
                        </div>

                        {importResult.warnings && importResult.warnings.length > 0 && (
                            <div className="flex flex-col gap-2">
                                <span className="text-[12.5px] text-(--muted-foreground)">
                                    告警（{importResult.warnings.length}）
                                </span>
                                <ul className="max-h-64 overflow-auto rounded-lg border border-(--border) bg-(--card) p-3 text-[12.5px] text-(--muted-foreground)">
                                    {importResult.warnings.map((w, i) => (
                                        <li key={i} className="py-0.5">
                                            · {w}
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        )}
                    </div>
                )}
            </Drawer>
        </div>
    )
}
