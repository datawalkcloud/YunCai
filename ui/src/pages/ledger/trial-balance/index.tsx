import { PrinterOutlined } from "@ant-design/icons"
import { useCallback, useEffect, useMemo, useState } from "react"
import { useNavigate, useParams } from "react-router"
import { Button, Alert, Checkbox, Spin, Table } from "antd";
import type { TableColumnsType, TableColumnType } from "antd"
import dayjs from "dayjs"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type TrialBalanceRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { DateRangeField, parseDate } from "@/components/fields/date-fields"
import { AMOUNT_COL, SOFT_PANEL } from "@/components/page/styles"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerTabs } from "@/components/ledger/LedgerTabs"
import { SectionCard } from "@/components/page/section"

/** 选定的日期范围。 */
interface DateRange {
    startDate: string
    endDate: string
}

const AMOUNT_KEYS = [
    "openingDebit",
    "openingCredit",
    "periodDebit",
    "periodCredit",
    "endingDebit",
    "endingCredit",
] as const

type AmountKey = (typeof AMOUNT_KEYS)[number]

/** 树形行：后端给出 level / parentCode，这里按 parentCode 把平表挂成 children。 */
type TreeRow = TrialBalanceRowDto & { children?: TreeRow[] }

function fmt(value: number, force = false): string {
    if (value === 0 && !force) return ""
    return value.toLocaleString("zh-CN", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    })
}

/** 金额列（借贷方向）右对齐并使用等宽数字，表头与单元格一致。 */
const amount = (dataIndex: AmountKey, title: string): TableColumnType<TreeRow> => ({
    title,
    dataIndex,
    render: (v) => fmt(v ?? 0),
    ...AMOUNT_COL,
})

/** 多级表头：编码、名称为单层列，三组余额各带借方/贷方两个子列。 */
const columns: TableColumnsType<TreeRow> = [
    { title: "科目编码", dataIndex: "code" },
    { title: "科目名称", dataIndex: "name" },
    {
        key: "opening",
        title: "期初余额",
        children: [amount("openingDebit", "借方"), amount("openingCredit", "贷方")],
    },
    {
        key: "period",
        title: "本期发生额",
        children: [amount("periodDebit", "借方"), amount("periodCredit", "贷方")],
    },
    {
        key: "ending",
        title: "期末余额",
        children: [amount("endingDebit", "借方"), amount("endingCredit", "贷方")],
    },
]

export default function TrialBalancePage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { accountSetId: routeId } = useParams<{ accountSetId: string }>()
    const navigate = useNavigate()

    const [dateRange, setDateRange] = useState<DateRange | null>(null)
    const [rows, setRows] = useState<TrialBalanceRowDto[]>([])
    const [periodText, setPeriodText] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [expandAll, setExpandAll] = useState(true)
    const [expandedKeys, setExpandedKeys] = useState<string[]>([])

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const res = await new ReportsApi(apiConfig(token)).accountsetsAccountSetIdReportsTrialBalanceGet({
                accountSetId,
                startDate: dateRange?.startDate ? new Date(dateRange.startDate) : undefined,
                endDate: dateRange?.endDate ? new Date(dateRange.endDate) : undefined,
            })
            setRows(res.rows ?? [])
            setPeriodText(res.periodText ?? "")
            // 首次未指定日期范围时，采用后端解析出的日期范围。
            if (dateRange === null && res.startDate && res.endDate) {
                setDateRange({ startDate: res.startDate, endDate: res.endDate })
            }
        } catch (err) {
            setError(await apiErrorMessage(err, "加载科目余额表失败，请重试。"))
            setRows([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, dateRange])

    useEffect(() => {
        void load()
    }, [load])

    // 平表按 parentCode 挂成树；后端已按编码排序，子行天然有序。
    const treeRows = useMemo(() => {
        const nodes = new Map<string, TreeRow>()
        for (const row of rows) nodes.set(row.code ?? "", { ...row })

        const roots: TreeRow[] = []
        for (const node of nodes.values()) {
            const parent = node.parentCode ? nodes.get(node.parentCode) : undefined
            if (parent) {
                ;(parent.children ??= []).push(node)
            } else {
                roots.push(node)
            }
        }
        return roots
    }, [rows])

    const allParentKeys = useMemo(() => {
        const keys: string[] = []
        const walk = (nodes: TreeRow[]) => {
            for (const node of nodes) {
                if (node.children?.length) {
                    keys.push(node.code ?? "")
                    walk(node.children)
                }
            }
        }
        walk(treeRows)
        return keys
    }, [treeRows])

    useEffect(() => {
        if (expandAll) setExpandedKeys(allParentKeys)
    }, [allParentKeys, expandAll])

    // 手动展开/收起后同步勾选态：全部展开才算勾上「展开所有级次」。
    function setExpanded(code: string, expanded: boolean) {
        const next = expanded
            ? expandedKeys.includes(code)
                ? expandedKeys
                : [...expandedKeys, code]
            : expandedKeys.filter((key) => key !== code)
        setExpandedKeys(next)
        setExpandAll(next.length === allParentKeys.length)
    }

    const { totals, balanced } = useMemo(() => {
        const t = {
            openingDebit: 0,
            openingCredit: 0,
            periodDebit: 0,
            periodCredit: 0,
            endingDebit: 0,
            endingCredit: 0,
        }
        for (const row of rows) {
            // 上级行已含下级合计，只累加一级科目，避免重复计入。
            if (row.parentCode) continue
            for (const key of AMOUNT_KEYS) t[key] += row[key] ?? 0
        }
        // 金额以分为单位比较，规避浮点误差。
        const eq = (a: number, b: number) => Math.round(a * 100) === Math.round(b * 100)
        return {
            totals: t,
            balanced:
                eq(t.openingDebit, t.openingCredit) &&
                eq(t.periodDebit, t.periodCredit) &&
                eq(t.endingDebit, t.endingCredit),
        }
    }, [rows])

    // 点行下钻到该科目明细账，沿用同一日期区间。
    function goSubsidiary(code: string | undefined) {
        if (!code || !routeId) return
        const params = new URLSearchParams({ account: code })
        if (dateRange?.startDate) params.set("startDate", dateRange.startDate)
        if (dateRange?.endDate) params.set("endDate", dateRange.endDate)
        navigate(`/${routeId}/ledger/subsidiary?${params.toString()}`)
    }

    return (
        <div className="flex flex-col gap-4">
            <LedgerTabs current="trial-balance" />

            <SectionCard bodyClassName="flex flex-col gap-4">
                <div className={cn(SOFT_PANEL, "flex flex-wrap items-center justify-between gap-2 px-4 py-3")}>
                    <div className="flex flex-wrap items-center gap-1.5">
                        <span className="text-[12.5px] text-slate-500">日期范围：</span>
                        <DateRangeField
                            className="w-56"
                            value={
                                dateRange
                                    ? {
                                          from: parseDate(dateRange.startDate),
                                          to: parseDate(dateRange.endDate),
                                      }
                                    : undefined
                            }
                            onChange={(range) => {
                                if (range?.from && range.to) {
                                    setDateRange({
                                        startDate: dayjs(range.from).format("YYYY-MM-DD"),
                                        endDate: dayjs(range.to).format("YYYY-MM-DD"),
                                    })
                                } else if (!range?.from) {
                                    // 清除后回到后端解析出的默认区间。
                                    setDateRange(null)
                                }
                            }}
                            disabled={!accountSetId}
                            allowClear
                        />
                        <Checkbox
                            className="ml-2 text-[12.5px]"
                            checked={expandAll}
                            disabled={allParentKeys.length === 0}
                            onChange={(e) => {
                                const on = e.target.checked
                                setExpandAll(on)
                                setExpandedKeys(on ? allParentKeys : [])
                            }}
                        >
                            展开所有级次
                        </Checkbox>
                    </div>
                    <div className="flex items-center gap-2">
                        {loading && <Spin />}
                        <span className="text-[12.5px] text-slate-400">单位：元</span>
                        <Button icon={<PrinterOutlined />} onClick={() => window.print()}>
                            打印
                        </Button>
                    </div>
                </div>

                {error && <Alert type="error" showIcon title={error} />}

                <LedgerTable
                    columns={columns}
                    data={treeRows}
                    loading={loading}
                    emptyText={accountSetId ? "该期间暂无科目余额数据。" : "请先选择账套。"}
                    rowKey={(row) => row.code ?? ""}
                    onRowClick={(row) => {
                        // 上级行点行即展开/收起；明细行点行下钻该科目明细账。
                        if (row.children?.length) {
                            setExpanded(row.code ?? "", !expandedKeys.includes(row.code ?? ""))
                        } else {
                            goSubsidiary(row.code)
                        }
                    }}
                    expandable={{
                        expandedRowKeys: expandedKeys,
                        indentSize: 18,
                        onExpand: (expanded, row) => setExpanded(row.code ?? "", expanded),
                    }}
                    footer={
                        <Table.Summary.Row>
                            <Table.Summary.Cell index={0} colSpan={2}>
                                合计
                            </Table.Summary.Cell>
                            {AMOUNT_KEYS.map((key, i) => (
                                <Table.Summary.Cell key={key} index={2 + i} {...AMOUNT_COL}>
                                    {fmt(totals[key], true)}
                                </Table.Summary.Cell>
                            ))}
                        </Table.Summary.Row>
                    }
                />

                {rows.length > 0 && (
                    <span className={cn("text-[12.5px] text-slate-400", !balanced && "text-[#ef4444]")}>
                        {balanced
                            ? `试算平衡：借方合计 = 贷方合计（${periodText}）`
                            : "试算不平衡，请核对数据"}
                    </span>
                )}
            </SectionCard>
        </div>
    )
}
