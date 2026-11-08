import { useEffect, useMemo, useState } from "react"
import { useNavigate, useParams } from "react-router"
import { PrinterOutlined } from "@ant-design/icons"
import { Button, Alert, Table } from "antd";
import type { TableColumnsType, TableColumnType } from "antd"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { ReportsApi, type TrialBalanceRowDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { SectionCard } from "@/components/page/section"
import { TrialBalanceFilter } from "@/components/ledger/TrialBalanceFilter"
import { useAccounts } from "@/components/ledger/useLedgerOptions"
import type { LedgerConditions } from "@/components/ledger/types"

/** 科目余额表进入页面时的默认查询条件（不做任何裁剪）。 */
const DEFAULT_CONDITIONS: LedgerConditions = {
    startDate: undefined,
    endDate: undefined,
    accountFrom: undefined,
    accountTo: undefined,
    minLevel: 1,
    maxLevel: undefined,
    showAssist: false,
    hideZeroBalance: true,
    hideNoActivity: false,
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

/** 金额列（借贷方向）右对齐并使用等宽数字，表头与单元格一致。 */
const amount = (dataIndex: AmountKey, title: string): TableColumnType<TreeRow> => ({
    title,
    dataIndex,
    render: (v) => fmtAmount(v ?? 0),
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

    const allAccounts = useAccounts(accountSetId, token)

    const [committed, setCommitted] = useState<LedgerConditions>(DEFAULT_CONDITIONS)
    const [draft, setDraft] = useState<LedgerConditions>(DEFAULT_CONDITIONS)
    const [rows, setRows] = useState<TrialBalanceRowDto[]>([])
    const [periodText, setPeriodText] = useState("")
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [expandAll, setExpandAll] = useState(true)
    const [expandedKeys, setExpandedKeys] = useState<string[]>([])

    // 提交条件变化时重查；draft 只用于面板内编辑，不影响取数。
    useEffect(() => {
        if (!accountSetId) return
        let cancelled = false
        setLoading(true)
        setError(null)
        new ReportsApi(apiConfig(token))
            .accountsetsAccountSetIdReportsTrialBalanceGet({
                accountSetId,
                startDate: committed.startDate ? new Date(committed.startDate) : undefined,
                endDate: committed.endDate ? new Date(committed.endDate) : undefined,
                accountFrom: committed.accountFrom || undefined,
                accountTo: committed.accountTo || undefined,
                minLevel: committed.minLevel,
                maxLevel: committed.maxLevel,
                hideZeroBalance: committed.hideZeroBalance,
                hideNoActivity: committed.hideNoActivity,
            })
            .then((res) => {
                if (cancelled) return
                setRows(res.rows ?? [])
                const text = res.periodText ?? ""
                setPeriodText(text)
                // 未指定期间时把后端解析出的区间回填到面板，浮层里不会一片空白。
                const range = text.match(/(\d{4}-\d{2}-\d{2})\s*至\s*(\d{4}-\d{2}-\d{2})/)
                if (range) {
                    setDraft((d) =>
                        d.startDate || d.endDate ? d : { ...d, startDate: range[1], endDate: range[2] },
                    )
                }
            })
            .catch(async (err) => {
                if (cancelled) return
                setError(await apiErrorMessage(err, "加载科目余额表失败，请重试。"))
                setRows([])
            })
            .finally(() => {
                if (!cancelled) setLoading(false)
            })
        return () => {
            cancelled = true
        }
    }, [accountSetId, token, committed])

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
        if (committed.startDate) params.set("startDate", committed.startDate)
        if (committed.endDate) params.set("endDate", committed.endDate)
        navigate(`/${routeId}/ledger/subsidiary?${params.toString()}`)
    }

    return (
        <div className="flex flex-col gap-4">
            <SectionCard bodyClassName="flex flex-col gap-4">
                <TrialBalanceFilter
                    value={draft}
                    onChange={(patch) => setDraft((d) => ({ ...d, ...patch }))}
                    onQuery={() => setCommitted(draft)}
                    onCancel={() => setDraft(committed)}
                    onReset={() => {
                        setDraft(DEFAULT_CONDITIONS)
                        setCommitted(DEFAULT_CONDITIONS)
                    }}
                    accounts={allAccounts}
                    periodText={periodText}
                    expandAll={expandAll}
                    onExpandAllChange={(checked) => {
                        setExpandAll(checked)
                        setExpandedKeys(checked ? allParentKeys : [])
                    }}
                    loading={loading}
                    disabled={!accountSetId}
                    extra={
                        <Button icon={<PrinterOutlined />} onClick={() => window.print()}>
                            打印
                        </Button>
                    }
                />

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
                                    {fmtAmount(totals[key], true)}
                                </Table.Summary.Cell>
                            ))}
                        </Table.Summary.Row>
                    }
                />

                {rows.length > 0 && (
                    <Alert
                        type={balanced ? "success" : "error"}
                        showIcon
                        title={
                            balanced
                                ? `试算平衡：借方合计 = 贷方合计（${periodText}）`
                                : "试算不平衡，请核对数据"
                        }
                    />
                )}
            </SectionCard>
        </div>
    )
}
