import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useNavigate } from "react-router"
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
    Empty,
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
    Spin,
    Tag,
    Tooltip,
    Typography,
} from "antd"
import type { MenuProps } from "antd"
import {
    DeleteOutlined,
    DownOutlined,
    FileExcelOutlined,
    FileTextOutlined,
    FilterOutlined,
    PaperClipOutlined,
    PlusOutlined,
    PrinterOutlined,
    ReloadOutlined,
    RightOutlined,
    RollbackOutlined,
    UploadOutlined,
} from "@ant-design/icons"
import dayjs from "dayjs"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    AccountsApi,
    VoucherWordsApi,
    VouchersApi,
    type AccountDto,
    type VoucherDetailDto,
    type VoucherEntryDto,
    type VoucherImportResult,
    type VoucherListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount, toISODate } from "@/lib/format"
import { cn } from "@/lib/utils"
import { Tile } from "@/components/page/tile"
import { StatusPill as StatusPillBase } from "@/components/page/status-pill"
import { ICON_STYLE, SOFT_PANEL, voucherStatusTone } from "@/components/page/styles"
import { PrintVoucherSheet } from "@/pages/closing/print-sheet"
import VoucherForm from "./voucher-form"

/** 每页条数候选，与原型分页的 showSizeChanger 一致。 */
const PAGE_SIZE_OPTIONS = [20, 50, 200, 500, 1000]

/** 原型默认每页 20 条。 */
const DEFAULT_PAGE_SIZE = 20

/** 三态筛选（全部 / 是 / 否），与柠檬云下拉的「全部 / 已审核 / 未审核」一一对应。 */
type TriState = "all" | "yes" | "no"

/**
 * 凭证列表的全部筛选条件。字段与柠檬云「查看凭证」筛选面板对齐；
 * 数量核算、外币核算我们没有对应的数据字段，因此没有放进筛选条件（避免给出点了没反应的空条件）。
 */
interface VoucherFilters {
    /** 会计期间起（YYYY-MM）。 */
    periodFrom: string | null
    /** 会计期间止（YYYY-MM）。 */
    periodTo: string | null
    word: string | null
    preparer: string | null
    reviewed: TriState
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
    periodFrom: null,
    periodTo: null,
    word: null,
    preparer: null,
    reviewed: "all",
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

/** 筛选条件转接口入参；回收站开关单独叠加。 */
function toQueryParams(filters: VoucherFilters, deleted: boolean) {
    return {
        startDate: monthRange(filters.periodFrom, "start"),
        endDate: monthRange(filters.periodTo, "end"),
        word: filters.word ?? undefined,
        preparer: filters.preparer ?? undefined,
        reviewed: triToParam(filters.reviewed),
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

/** 期间文本：起止都一样时只显示一个月份。 */
function periodText(filters: VoucherFilters): string {
    const from = filters.periodFrom
    const to = filters.periodTo
    const fmt = (v: string) => v.replace("-", "年") + "月"
    if (from && to) return from === to ? fmt(from) : `${fmt(from)} - ${fmt(to)}`
    if (from) return `${fmt(from)} 起`
    if (to) return `截至 ${fmt(to)}`
    return "全部期间"
}

/** 统计已生效的筛选条件数，显示在筛选按钮上。 */
function activeFilterCount(filters: VoucherFilters): number {
    let count = 0
    if (filters.periodFrom || filters.periodTo) count++
    if (filters.word) count++
    if (filters.preparer) count++
    if (filters.reviewed !== "all") count++
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

/** 勾选 / 摘要 / 科目 / 借方 / 贷方 / 操作，表头、分录行与合计行共用同一份栅格。 */
const LINE_GRID = "grid grid-cols-[40px_minmax(0,1fr)_240px_150px_150px_minmax(0,1fr)] items-center"

/** 金额列右对齐、等宽数字。 */
const NUM_CELL = "pr-2 text-right tabular-nums"

/** 借贷金额文本：负数用红色标注，零与空值留白（口径同 fmtAmount）。 */
function AmountCell({ value }: { value: number | undefined }) {
    return (
        <span className={cn(NUM_CELL, (value ?? 0) < 0 ? "text-[#ef4444]" : "text-slate-700")}>
            {fmtAmount(value)}
        </span>
    )
}

/** 状态胶囊：配色口径见共享的 voucherStatusTone。 */
function StatusPill({ record }: { record: VoucherListItemDto }) {
    return (
        <StatusPillBase tone={voucherStatusTone(record.status, record.isBalanced)}>
            {record.statusText}
        </StatusPillBase>
    )
}

/** 工具栏下拉按钮：文案 + 菜单项，沿用原型的 Button + DownOutlined 样式。 */
function toolbarDropdown(
    text: string,
    items: NonNullable<MenuProps["items"]>,
    onClick: MenuProps["onClick"],
) {
    return (
        <Dropdown trigger={["click"]} menu={{ items, onClick }}>
            <Button>
                {text}
                <DownOutlined style={{ fontSize: 10 }} />
            </Button>
        </Dropdown>
    )
}

/** 筛选面板里一行：左侧标题、右侧控件，与柠檬云的两列式面板一致。 */
function FilterRow({ label, children }: { label: string; children: React.ReactNode }) {
    return (
        <div className="flex items-center gap-2">
            <span className="w-[72px] shrink-0 text-right text-[13px] text-slate-500">{label}</span>
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
            <FilterRow label="会计期间：">
                <DatePicker
                    picker="month"
                    size="small"
                    allowClear
                    placeholder="开始"
                    value={value.periodFrom ? dayjs(`${value.periodFrom}-01`) : null}
                    onChange={(d) => patch({ periodFrom: d ? d.format("YYYY-MM") : null })}
                    style={{ flex: 1 }}
                />
                <span className="text-[12px] text-slate-400">至</span>
                <DatePicker
                    picker="month"
                    size="small"
                    allowClear
                    placeholder="结束"
                    value={value.periodTo ? dayjs(`${value.periodTo}-01`) : null}
                    onChange={(d) => patch({ periodTo: d ? d.format("YYYY-MM") : null })}
                    style={{ flex: 1 }}
                />
            </FilterRow>
            <FilterRow label="凭证字：">
                <Select
                    size="small"
                    style={{ width: "100%" }}
                    value={value.word ?? "__all__"}
                    onChange={(v) => patch({ word: v === "__all__" ? null : v })}
                    options={[{ value: "__all__", label: "全部" }, ...words.map((w) => ({ value: w, label: w }))]}
                />
            </FilterRow>
            <FilterRow label="制单人：">
                <Select
                    size="small"
                    style={{ width: "100%" }}
                    showSearch
                    value={value.preparer ?? "__all__"}
                    onChange={(v) => patch({ preparer: v === "__all__" ? null : v })}
                    options={[{ value: "__all__", label: "全部" }, ...preparers.map((p) => ({ value: p, label: p }))]}
                />
            </FilterRow>
            <FilterRow label="是否审核：">
                <Select
                    size="small"
                    style={{ width: "100%" }}
                    value={value.reviewed}
                    onChange={(v) => patch({ reviewed: v })}
                    options={[
                        { value: "all", label: "全部" },
                        { value: "yes", label: "已审核" },
                        { value: "no", label: "未审核" },
                    ]}
                />
            </FilterRow>
            <FilterRow label="辅助核算：">
                <Select
                    size="small"
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
                    size="small"
                    allowClear
                    placeholder="包含关键字"
                    value={value.summary}
                    onChange={(e) => patch({ summary: e.target.value })}
                />
            </FilterRow>
            <FilterRow label="备注：">
                <Input
                    size="small"
                    allowClear
                    placeholder="包含关键字"
                    value={value.remark}
                    onChange={(e) => patch({ remark: e.target.value })}
                />
            </FilterRow>
            <FilterRow label="科目：">
                <Select
                    size="small"
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
                    size="small"
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
                    size="small"
                    style={{ flex: 1 }}
                    placeholder="从"
                    value={value.amountFrom ?? undefined}
                    onChange={(v) => patch({ amountFrom: v ?? null })}
                />
                <span className="text-[12px] text-slate-400">至</span>
                <InputNumber
                    size="small"
                    style={{ flex: 1 }}
                    placeholder="到"
                    value={value.amountTo ?? undefined}
                    onChange={(v) => patch({ amountTo: v ?? null })}
                />
            </FilterRow>
            <FilterRow label="凭证号：">
                <InputNumber
                    size="small"
                    style={{ flex: 1 }}
                    placeholder="从"
                    value={value.numberFrom ?? undefined}
                    onChange={(v) => patch({ numberFrom: v ?? null })}
                />
                <span className="text-[12px] text-slate-400">至</span>
                <InputNumber
                    size="small"
                    style={{ flex: 1 }}
                    placeholder="到"
                    value={value.numberTo ?? undefined}
                    onChange={(v) => patch({ numberTo: v ?? null })}
                />
            </FilterRow>
            <FilterRow label="排序依据：">
                <Radio.Group
                    size="small"
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
                    size="small"
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

/** 单张凭证卡片：元信息 + 行内操作 + 分录行 + 合计条。 */
function VoucherCard({
    record,
    checked,
    openingId,
    deletingId,
    workflowId,
    onCheck,
    onEdit,
    onDelete,
    onReview,
    onUnreview,
    onPost,
    onUnpost,
    onPrint,
    onCopy,
    onReverse,
    recycleMode,
    onRestore,
    onPurge,
}: {
    record: VoucherListItemDto
    checked: boolean
    openingId: string | null
    deletingId: string | null
    workflowId: string | null
    onCheck: (checked: boolean) => void
    onEdit: (v: VoucherListItemDto) => void
    onDelete: (v: VoucherListItemDto) => void
    onReview: (v: VoucherListItemDto) => void
    onUnreview: (v: VoucherListItemDto) => void
    onPost: (v: VoucherListItemDto) => void
    onUnpost: (v: VoucherListItemDto) => void
    onPrint: (v: VoucherListItemDto) => void
    onCopy: (v: VoucherListItemDto) => void
    onReverse: (v: VoucherListItemDto) => void
    /** 回收站视图：行内操作换成「还原 / 彻底删除」。 */
    recycleMode: boolean
    onRestore: (v: VoucherListItemDto) => void
    onPurge: (v: VoucherListItemDto) => void
}) {
    return (
        <div className="overflow-hidden border border-[#f0f0f0] bg-white transition-colors hover:border-[#d9d9d9]">
            <div className="voucher-meta flex flex-wrap items-center border-b border-[#f0f0f0] bg-[#f5f5f5] py-1.5 pr-4 pl-3">
                <div className="w-10 shrink-0">
                    <Checkbox checked={checked} onChange={(event) => onCheck(event.target.checked)} />
                </div>
                <Flex align="center" gap={24} wrap style={{ flex: 1, minWidth: 0 }}>
                    <span>
                        <Typography.Text type="secondary">日期：</Typography.Text>
                        <Typography.Text strong>{toISODate(record.date)}</Typography.Text>
                    </span>
                    <span>
                        <Typography.Text type="secondary">凭证字号：</Typography.Text>
                        <Tag color="blue" style={{ marginInlineEnd: 0 }}>
                            {record.voucherNo}
                        </Tag>
                    </span>
                    <StatusPill record={record} />
                    <Typography.Text type="secondary">
                        <PaperClipOutlined /> 附件 {record.attachmentCount ?? 0}
                    </Typography.Text>
                    <Typography.Text type="secondary">
                        制单：{record.preparer ?? "—"}
                        {record.reviewer ? ` · 审核：${record.reviewer}` : ""}
                    </Typography.Text>
                </Flex>
                <Space size={0}>
                    {recycleMode ? (
                        <>
                            <Button type="link" size="small" onClick={() => onRestore(record)}>
                                还原
                            </Button>
                            <Button type="link" size="small" danger onClick={() => onPurge(record)}>
                                彻底删除
                            </Button>
                        </>
                    ) : (
                        <>
                            {record.status === 1 && (
                                <Button type="link" size="small" loading={openingId === record.id} onClick={() => onEdit(record)}>
                                    修改
                                </Button>
                            )}
                            {record.status === 1 && (
                                <Button type="link" size="small" loading={workflowId === record.id} onClick={() => onReview(record)}>
                                    审核
                                </Button>
                            )}
                            {record.status === 2 && (
                                <Button type="link" size="small" loading={workflowId === record.id} onClick={() => onUnreview(record)}>
                                    反审核
                                </Button>
                            )}
                            {record.status === 2 && (
                                <Tooltip title="过账后凭证登记入账并锁定，不可再修改或删除">
                                    <Button type="link" size="small" loading={workflowId === record.id} onClick={() => onPost(record)}>
                                        过账
                                    </Button>
                                </Tooltip>
                            )}
                            {record.status === 3 && (
                                <Tooltip title="反过账后凭证退回已审核状态，可再次修改或删除">
                                    <Button type="link" size="small" loading={workflowId === record.id} onClick={() => onUnpost(record)}>
                                        反过账
                                    </Button>
                                </Tooltip>
                            )}
                            <Button type="link" size="small" onClick={() => onCopy(record)}>
                                复制
                            </Button>
                            <Button type="link" size="small" onClick={() => onReverse(record)}>
                                红冲
                            </Button>
                            <Button type="link" size="small" onClick={() => onPrint(record)}>
                                打印
                            </Button>
                            {record.status !== 3 && (
                                <Button type="link" size="small" danger loading={deletingId === record.id} onClick={() => onDelete(record)}>
                                    删除
                                </Button>
                            )}
                        </>
                    )}
                </Space>
            </div>

            <div>
                {(record.entries ?? []).map((e: VoucherEntryDto) => (
                    <div key={e.id} className={cn(LINE_GRID, "min-h-10 border-b border-[#f5f5f5] py-0.5 pr-4 pl-3 text-[14px]")}>
                        <span />
                        <span className="truncate pr-4 text-slate-600">{e.summary}</span>
                        <span className="text-slate-600">
                            <span className="text-slate-400">{e.accountCode} </span>
                            {e.accountName}
                        </span>
                        <AmountCell value={e.debit} />
                        <AmountCell value={e.credit} />
                        <span />
                    </div>
                ))}
            </div>

            <div className={cn(LINE_GRID, "min-h-10 bg-[#f6ffed] px-3")}>
                <span />
                <Typography.Text strong>合计</Typography.Text>
                <span>
                    {record.isBalanced === false ? (
                        <Typography.Text type="danger">
                            差额 {fmtAmount(Math.abs((record.totalDebit ?? 0) - (record.totalCredit ?? 0)))}
                        </Typography.Text>
                    ) : (
                        <Typography.Text type="success">借贷平衡</Typography.Text>
                    )}
                </span>
                <Typography.Text strong className={NUM_CELL}>
                    {fmtAmount(record.totalDebit, true)}
                </Typography.Text>
                <Typography.Text strong className={NUM_CELL}>
                    {fmtAmount(record.totalCredit, true)}
                </Typography.Text>
                <span />
            </div>
        </div>
    )
}

export default function VoucherManagePage() {
    const { token, user } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)
    const { message, modal } = App.useApp()
    const navigate = useNavigate()

    const [vouchers, setVouchers] = useState<VoucherListItemDto[]>([])
    const [accounts, setAccounts] = useState<AccountDto[]>([])
    /** 筛选面板的科目下拉：含非末级科目，便于按一级科目过滤全部下级。 */
    const [allAccounts, setAllAccounts] = useState<AccountDto[]>([])
    /** 账套已配置的凭证字，作为筛选下拉的选项来源。 */
    const [voucherWords, setVoucherWords] = useState<string[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [open, setOpen] = useState(false)
    const [editing, setEditing] = useState<VoucherDetailDto | null>(null)
    const [openingId, setOpeningId] = useState<string | null>(null)
    const [deletingId, setDeletingId] = useState<string | null>(null)
    const [workflowId, setWorkflowId] = useState<string | null>(null)
    const [printVoucherId, setPrintVoucherId] = useState<string | null>(null)
    /** 连续打印的凭证清单（打印「直接打印」用），为空表示只打印单张。 */
    const [printVoucherIds, setPrintVoucherIds] = useState<string[] | null>(null)
    /** 已生效的筛选条件。 */
    const [filters, setFilters] = useState<VoucherFilters>(EMPTY_FILTERS)
    /** 筛选面板里正在编辑、尚未点「确定」的条件。 */
    const [draftFilters, setDraftFilters] = useState<VoucherFilters>(EMPTY_FILTERS)
    const [filterOpen, setFilterOpen] = useState(false)
    /** 回收站视图：只看已删除的凭证。 */
    const [recycleMode, setRecycleMode] = useState(false)
    const [selectedKeys, setSelectedKeys] = useState<Key[]>([])
    /** 整理凭证弹窗。 */
    const [tidyOpen, setTidyOpen] = useState(false)
    const [tidyPeriod, setTidyPeriod] = useState<string | null>(null)
    const [tidyWord, setTidyWord] = useState<string | null>(null)
    const [tidyByDate, setTidyByDate] = useState(false)
    const [tidying, setTidying] = useState(false)

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
            const [list, accs, everyAccount, words] = await Promise.all([
                new VouchersApi(config).accountsetsAccountSetIdVouchersGet({
                    accountSetId,
                    // DateOnly 口径：先取 yyyy-MM-dd 字符串再构造 Date，避免东八区回退一天。
                    ...toQueryParams(filters, recycleMode),
                }),
                new AccountsApi(config).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: true }),
                new AccountsApi(config).accountsetsAccountSetIdAccountsGet({ accountSetId, leafOnly: false }),
                new VoucherWordsApi(config).accountsetsAccountSetIdVoucherwordsGet({ accountSetId }),
            ])
            setVouchers(list)
            setAccounts(accs)
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

    async function handleDelete(v: VoucherListItemDto) {
        if (deletingId || !v.id) return
        setDeletingId(v.id)
        setError(null)
        try {
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdDelete({
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

    /** 复制凭证：生成同凭证字下的新草稿，成功后刷新列表。 */
    async function handleCopy(v: VoucherListItemDto) {
        if (!v.id) return
        try {
            const created = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdCopyPost({
                accountSetId,
                id: v.id,
            })
            message.success(`已复制为 ${created.voucherNo}（草稿）`)
            void load()
        } catch (err) {
            setError(await apiErrorMessage(err, "复制失败，请重试。"))
        }
    }

    /** 红冲凭证：生成金额取反的草稿，摘要带「冲销…」前缀。 */
    async function handleReverse(v: VoucherListItemDto) {
        if (!v.id) return
        const ok = await new Promise<boolean>((resolve) =>
            modal.confirm({
                title: `红冲凭证「${v.voucherNo}」？`,
                content: "将生成一张借贷金额取反的草稿凭证，原凭证保留不动。",
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
            message.success(`已生成红字凭证 ${created.voucherNo}`)
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
            await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdPurgeDelete({
                accountSetId,
                id: v.id,
            })
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
                    await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersRecycleBinDelete({ accountSetId })
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
                    periodStart: monthRange(tidyPeriod, "start"),
                    periodEnd: monthRange(tidyPeriod, "end"),
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
                    ids: scope === "ids" ? selectedKeys.map(String) : undefined,
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
                const api = new VouchersApi(apiConfig(token))
                let done = 0
                const failed: string[] = []
                for (const id of ids) {
                    try {
                        await api.accountsetsAccountSetIdVouchersIdDelete({ accountSetId, id })
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

    async function handleEdit(v: VoucherListItemDto) {
        if (openingId || !v.id) return
        setOpeningId(v.id)
        setError(null)
        try {
            const detail = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersIdGet({
                accountSetId,
                id: v.id,
            })
            setEditing(detail)
            setOpen(true)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载凭证详情失败，请重试。"))
        } finally {
            setOpeningId(null)
        }
    }

    function handleCreate() {
        setEditing(null)
        setOpen(true)
    }

    /** 凭证状态流转（审核/反审核/过账/反过账）：成功后就地更新该张凭证的状态与审核人。 */
    async function handleWorkflow(v: VoucherListItemDto, action: "review" | "unreview" | "post" | "unpost") {
        if (workflowId || !v.id) return
        if (action === "post" || action === "unpost") {
            const isPost = action === "post"
            const ok = await new Promise<boolean>((resolve) =>
                modal.confirm({
                    title: isPost ? `确定过账凭证「${v.voucherNo}」吗？` : `确定反过账凭证「${v.voucherNo}」吗？`,
                    content: isPost
                        ? "过账后凭证将登记入账并锁定，不可再修改或删除。"
                        : "反过账后凭证退回已审核状态，可再次修改或删除。",
                    okText: isPost ? "过账" : "反过账",
                    cancelText: "取消",
                    onOk: () => resolve(true),
                    onCancel: () => resolve(false),
                }),
            )
            if (!ok) return
        }
        setWorkflowId(v.id)
        setError(null)
        try {
            const api = new VouchersApi(apiConfig(token))
            const params = { accountSetId, id: v.id }
            const updated =
                action === "review"
                    ? await api.accountsetsAccountSetIdVouchersIdReviewPost(params)
                    : action === "unreview"
                        ? await api.accountsetsAccountSetIdVouchersIdUnreviewPost(params)
                        : action === "post"
                            ? await api.accountsetsAccountSetIdVouchersIdPostPost(params)
                            : await api.accountsetsAccountSetIdVouchersIdUnpostPost(params)
            setVouchers((prev) =>
                prev.map((x) =>
                    x.id === v.id
                        ? { ...x, status: updated.status, statusText: updated.statusText, reviewer: updated.reviewer }
                        : x,
                ),
            )
            message.success(
                action === "review"
                    ? "凭证已审核"
                    : action === "unreview"
                        ? "凭证已退回草稿"
                        : action === "post"
                            ? "凭证已过账"
                            : "凭证已反过账",
            )
        } catch (err) {
            const fallback =
                action === "review"
                    ? "审核失败，请重试。"
                    : action === "unreview"
                        ? "反审核失败，请重试。"
                        : action === "post"
                            ? "过账失败，请重试。"
                            : "反过账失败，请重试。"
            setError(await apiErrorMessage(err, fallback))
        } finally {
            setWorkflowId(null)
        }
    }

    function handleSaved() {
        setOpen(false)
        setEditing(null)
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

    // 数据变少时把页码夹回有效范围，避免停留在空页。
    const pageCount = Math.max(1, Math.ceil(vouchers.length / pageSize))
    const currentPage = Math.min(page, pageCount)
    const pageRows = useMemo(
        () => vouchers.slice((currentPage - 1) * pageSize, (currentPage - 1) * pageSize + pageSize),
        [vouchers, currentPage, pageSize],
    )
    const from = vouchers.length === 0 ? 0 : (currentPage - 1) * pageSize + 1
    const to = Math.min(currentPage * pageSize, vouchers.length)

    /** 制单人候选：取自当前结果，避免为下拉再单独开接口。 */
    const preparers = useMemo(
        () => [...new Set(vouchers.map((v) => v.preparer).filter((p): p is string => !!p))].sort(),
        [vouchers],
    )

    const allSelected = pageRows.length > 0 && pageRows.every((v) => selectedKeys.includes(v.id ?? ""))
    const someSelected = !allSelected && pageRows.some((v) => selectedKeys.includes(v.id ?? ""))

    const toggleSelect = (id: string, checked: boolean) =>
        setSelectedKeys((prev) => (checked ? [...prev, id] : prev.filter((key) => key !== id)))

    const confirmDelete = (v: VoucherListItemDto) => {
        modal.confirm({
            title: `确认删除凭证「${v.voucherNo}」？`,
            content: `${toISODate(v.date)} · 删除后不可恢复。`,
            okText: "删除",
            okButtonProps: { danger: true },
            cancelText: "取消",
            onOk: () => handleDelete(v),
        })
    }

    return (
        <div className="flex flex-col gap-3">
            {error && <Alert type="error" showIcon title={error} />}

            <Card
                styles={{
                    root: {
                        border: "1px solid #f1f5f9",
                        boxShadow: "0 1px 2px rgba(16,24,40,0.04)",
                    },
                    body: { padding: 16 },
                }}
            >
                {/* 第一行：筛选条件入口（点开与柠檬云一致的下拉筛选面板） */}
                <Flex justify="space-between" align="center" gap={12} wrap style={{ marginBottom: 10 }}>
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
                                <div className="mt-2 flex items-center justify-between gap-3 border-t border-slate-100 pt-2">
                                <Button
                                    type="link"
                                    size="small"
                                    style={{ paddingInline: 0 }}
                                    onClick={() => {
                                        setDraftFilters(EMPTY_FILTERS)
                                        setFilters(EMPTY_FILTERS)
                                        setFilterOpen(false)
                                        setPage(1)
                                    }}
                                >
                                    恢复默认排序
                                </Button>
                                <Space size={8}>
                                    <Button size="small" onClick={() => setFilterOpen(false)}>
                                        取消
                                    </Button>
                                    <Button
                                        size="small"
                                        onClick={() => {
                                            setDraftFilters(EMPTY_FILTERS)
                                        }}
                                    >
                                        重置
                                    </Button>
                                    <Button
                                        size="small"
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
                        <Button type={activeFilterCount(filters) > 0 ? "primary" : "default"} ghost={activeFilterCount(filters) > 0}>
                            <FilterOutlined />
                            {periodText(filters)}
                            {activeFilterCount(filters) > 0 && (
                                <span className="ml-1 rounded-full bg-white/25 px-1.5 text-[11px]">{activeFilterCount(filters)}</span>
                            )}
                        </Button>
                    </Popover>
                    <Space size={8}>
                        {selectedKeys.length > 0 && (
                            <Typography.Text type="secondary">已选 {selectedKeys.length} 张</Typography.Text>
                        )}
                        {recycleMode && (
                            <Tag color="orange">回收站</Tag>
                        )}
                    </Space>
                </Flex>

                {/* 第二行：工具栏（与柠檬云一致的按钮顺序） */}
                <Flex justify="flex-end" align="center" gap={8} wrap style={{ marginBottom: 12 }}>
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
                            {toolbarDropdown(
                                "打印",
                                [
                                    { key: "direct", label: "直接打印", icon: <PrinterOutlined /> },
                                    { key: "settings", label: "打印设置" },
                                ],
                                ({ key }) => {
                                    if (key === "direct") printCurrentList()
                                    // 打印设置与单张打印共用同一个面板，打开任意一张凭证即可调整版式。
                                    else if (vouchers[0]?.id) setPrintVoucherId(vouchers[0].id)
                                    else message.warning("当前没有可打印的凭证")
                                },
                            )}
                            <Tooltip title="跳到明细账，按当前期间查看电子账簿">
                                <Button onClick={() => navigate(`/${accountSetId}/ledger/subsidiary`)}>电子账簿</Button>
                            </Tooltip>
                            {toolbarDropdown(
                                "导出",
                                [
                                    { key: "ids", label: "选中凭证", disabled: selectedKeys.length === 0 },
                                    { key: "query", label: "当前查询凭证" },
                                    { key: "all", label: "所有凭证" },
                                ],
                                ({ key }) => void handleExport(key as "ids" | "query" | "all"),
                            )}
                            <Button disabled={!accountSetId} onClick={() => setImportOpen(true)}>
                                导入凭证
                            </Button>
                            <Button
                                onClick={() => {
                                    setTidyPeriod(filters.periodFrom ?? dayjs().format("YYYY-MM"))
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
                                    { key: "modify", label: "批量修改", disabled: true },
                                    { key: "sort", label: "凭证号排序" },
                                    { key: "adjust", label: "凭证号调整", disabled: true },
                                    { key: "merge", label: "凭证合并", disabled: true },
                                ],
                                ({ key }) => {
                                    if (key === "copy") void batchCopy()
                                    else if (key === "delete") batchSoftDelete()
                                    else if (key === "sort") {
                                        setTidyPeriod(filters.periodFrom ?? dayjs().format("YYYY-MM"))
                                        setTidyWord(null)
                                        setTidyByDate(false)
                                        setTidyOpen(true)
                                    }
                                },
                            )}
                            <Dropdown
                                trigger={["click"]}
                                popupRender={() => (
                                    <div className="min-w-56 rounded-lg border border-slate-200 bg-white p-3 shadow-lg">
                                        <div className="flex flex-col gap-2">
                                            <Checkbox disabled>显示数量单价</Checkbox>
                                            <Checkbox disabled>显示原币汇率</Checkbox>
                                        </div>
                                        <div className="mt-2 text-[12px] text-slate-400">
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
                <Spin spinning={loading && vouchers.length > 0}>
                    {vouchers.length === 0 ? (
                        <Empty
                            image={Empty.PRESENTED_IMAGE_SIMPLE}
                            description={
                                loading
                                    ? "加载中…"
                                        : recycleMode
                                        ? "回收站是空的。"
                                        : "没有符合条件的凭证，可调整筛选条件或点击「新增凭证」开始记账。"
                            }
                            style={{ padding: "56px 0" }}
                        />
                    ) : (
                        <>
                            <div className={cn(LINE_GRID, "mb-2 h-10 border border-[#f0f0f0] bg-[#fafafa] py-0 pr-4 pl-3 text-[13px] text-[#595959]")}>
                                <Checkbox
                                    checked={allSelected}
                                    indeterminate={someSelected}
                                    onChange={(event) =>
                                        setSelectedKeys(
                                            event.target.checked ? pageRows.map((v) => v.id ?? "") : [],
                                        )
                                    }
                                />
                                <span>摘要</span>
                                <span>科目</span>
                                <span className="pr-2 text-right">借方金额</span>
                                <span className="pr-2 text-right">贷方金额</span>
                                <span />
                            </div>

                            <div className="flex flex-col gap-2">
                                {pageRows.map((v) => (
                                    <VoucherCard
                                        key={v.id}
                                        record={v}
                                        checked={selectedKeys.includes(v.id ?? "")}
                                        openingId={openingId}
                                        deletingId={deletingId}
                                        workflowId={workflowId}
                                        onCheck={(checked) => toggleSelect(v.id ?? "", checked)}
                                        onEdit={handleEdit}
                                        onDelete={confirmDelete}
                                        onReview={(r) => void handleWorkflow(r, "review")}
                                        onUnreview={(r) => void handleWorkflow(r, "unreview")}
                                        onPost={(r) => void handleWorkflow(r, "post")}
                                        onUnpost={(r) => void handleWorkflow(r, "unpost")}
                                        onPrint={(r) => setPrintVoucherId(r.id ?? null)}
                                        onCopy={(r) => void handleCopy(r)}
                                        onReverse={(r) => void handleReverse(r)}
                                        recycleMode={recycleMode}
                                        onRestore={(r) => void handleRestore(r)}
                                        onPurge={(r) => void handlePurge(r)}
                                    />
                                ))}
                            </div>
                        </>
                    )}
                </Spin>

                <Flex justify="space-between" align="center" gap={16} wrap style={{ marginTop: 12 }}>
                    <Typography.Text type="secondary">
                        显示 {from} 到 {to}，共 {vouchers.length} 条
                    </Typography.Text>
                    <Pagination
                        current={currentPage}
                        pageSize={pageSize}
                        total={vouchers.length}
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

            {/* 新增 / 编辑凭证抽屉 */}
            <Drawer
                open={open}
                size={1152}
                title={editing ? `编辑凭证 ${editing.voucherNo}` : "新增凭证"}
                onClose={() => {
                    setOpen(false)
                    setEditing(null)
                }}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
            >
                {open && (
                    <VoucherForm
                        key={editing?.id ?? "new"}
                        accountSetId={accountSetId}
                        accounts={accounts}
                        preparer={user?.displayName}
                        token={token ?? undefined}
                        initial={editing ?? undefined}
                        onSaved={handleSaved}
                        onCancel={() => {
                            setOpen(false)
                            setEditing(null)
                        }}
                    />
                )}
            </Drawer>

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
                        <span className="w-[72px] text-right text-[13px] text-slate-500">整理期间：</span>
                        <DatePicker
                            picker="month"
                            allowClear
                            placeholder="不限"
                            value={tidyPeriod ? dayjs(`${tidyPeriod}-01`) : null}
                            onChange={(d) => setTidyPeriod(d ? d.format("YYYY-MM") : null)}
                            style={{ width: 160 }}
                        />
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="w-[72px] text-right text-[13px] text-slate-500">凭证字：</span>
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
                    <div className="text-[12px] text-slate-400">
                        编号从 1 开始重新连续编排，已删除（回收站）的凭证不占号；范围内含已过账凭证时不允许重排。
                    </div>
                </div>
            </Modal>

            {/* 导入凭证抽屉 */}
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
                <p className="text-[12.5px] text-slate-400">
                    支持导入其他平台导出的记账凭证文件（.xls / .xlsx）。系统按凭证号聚合分录、自动补建缺失科目，
                    借贷金额原样保留。
                </p>

                <div className="flex flex-col gap-1">
                    <span className="font-medium">导入到账套</span>
                    <span className="text-[12.5px] text-slate-400">
                        {currentSet?.name ?? accountSetId}
                        {currentSet?.isCurrent ? "（当前）" : ""}
                    </span>
                </div>

                {/* 冲突策略 */}
                <div className="flex flex-col gap-2">
                    <span className="text-[13px] font-semibold text-slate-700">重复凭证处理</span>
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
                        importDragging ? "border-[#bfdbfe] bg-[#eef4ff]" : "border-slate-200",
                    )}
                >
                    <FileExcelOutlined className="text-[40px]" style={ICON_STYLE.placeholder} />
                    {importFile ? (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">{importFile.name}</span>
                            <span className="text-sm leading-normal font-normal text-slate-400">
                                {(importFile.size / 1024).toFixed(1)} KB · 点击可重新选择
                            </span>
                        </div>
                    ) : (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">点击选择，或拖拽文件到此处</span>
                            <span className="text-sm leading-normal font-normal text-slate-400">支持 .xls / .xlsx，最大 10MB</span>
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
                        <h3 className="text-[13px] font-semibold text-slate-800">导入结果</h3>
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
                                <span className="text-[12.5px] text-slate-400">
                                    告警（{importResult.warnings.length}）
                                </span>
                                <ul className="max-h-64 overflow-auto rounded-lg border border-slate-100 bg-white p-3 text-[12.5px] text-slate-500">
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
