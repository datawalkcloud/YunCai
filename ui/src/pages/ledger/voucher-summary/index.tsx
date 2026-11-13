import { useMemo, useState } from "react"
import { DownOutlined } from "@ant-design/icons"
import { Table } from "antd"
import type { TableColumnsType } from "antd"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { LedgersApi, type VoucherSummaryRowDto } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerPrintButton, LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { asDate } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"

/** 树形汇总行：后端按 parentCode 给平表，这里挂成 children 交给表格展开。 */
type SummaryRow = VoucherSummaryRowDto & { children?: SummaryRow[] }

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "summary"]
const SWITCHES = ["showOnlyLeaf"] as const

/** 科目编码、名称与柠檬云一致用蓝色；合计行分列挂在科目名称下，避免分组表头下的错位。 */
const columns: TableColumnsType<SummaryRow> = [
    {
        title: "科目编码",
        dataIndex: "code",
        width: 240,
        render: (code: string) => <span className="text-(--brand)">{code}</span>,
    },
    {
        title: "科目名称",
        dataIndex: "name",
        width: 280,
        render: (name: string) => <span className="text-(--brand)">{name}</span>,
    },
    {
        title: "金额合计",
        align: "center",
        children: [
            { title: "借方", dataIndex: "debit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
            { title: "贷方", dataIndex: "credit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
        ],
    },
]

export default function VoucherSummaryPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    /** 收起的科目编码；默认全部展开，刷新数据后仍保持用户手动收起的状态。 */
    const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set())

    const query = useLedgerQuery({
        accountSetId,
        token,
        fallbackError: "加载凭证汇总表失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersVoucherSummaryGet({
                accountSetId: id,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                accountFrom: conditions.accountFrom,
                accountTo: conditions.accountTo,
                summaryKeyword: conditions.summaryKeyword,
                // 与总账一致：默认列示全部级次，上级科目的合计即其下级之和。
                showOnlyLeaf: conditions.showOnlyLeaf ?? false,
            }),
    })

    const treeRows = useMemo(() => {
        const nodes = new Map<string, SummaryRow>()
        for (const row of query.data?.rows ?? []) nodes.set(row.code, { ...row })

        const roots: SummaryRow[] = []
        for (const node of nodes.values()) {
            const parent = node.parentCode ? nodes.get(node.parentCode) : undefined
            if (parent) {
                ;(parent.children ??= []).push(node)
            } else {
                roots.push(node)
            }
        }
        return roots
    }, [query.data])

    const parentKeys = useMemo(() => {
        const keys: string[] = []
        const walk = (nodes: SummaryRow[]) => {
            for (const node of nodes) {
                if (node.children?.length) {
                    keys.push(node.code)
                    walk(node.children)
                }
            }
        }
        walk(treeRows)
        return keys
    }, [treeRows])

    const accountCaption =
        query.committed.accountFrom || query.committed.accountTo
            ? `${query.committed.accountFrom ?? ""} ~ ${query.committed.accountTo ?? ""}`.trim()
            : "所有"

    return (
        <LedgerPageShell
            panel={
                <LedgerQueryPanel
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
                    onCancel={query.cancel}
                    onReset={query.reset}
                    fields={FIELDS}
                    switches={SWITCHES}
                    loading={query.loading}
                    extra={
                        <>
                            <LedgerExportButton
                                kind="voucher-summary"
                                accountSetId={accountSetId}
                                token={token}
                                conditions={query.committed}
                            />
                            <LedgerPrintButton
                                kind="voucher-summary"
                                accountSetId={accountSetId}
                                token={token}
                                conditions={query.committed}
                            />
                        </>
                    }
                    disabled={!accountSetId}
                />
            }
            meta={query.meta}
            error={query.error}
        >
            <div className="voucher-summary-sheet border border-(--border) bg-(--card)">
                <div className="flex items-center justify-between gap-3 border-b border-(--border) px-4 py-2.5 text-[13px] text-(--foreground)">
                    <span>科目：{accountCaption}</span>
                    <span className="text-(--foreground)">
                        【凭证数：{query.data?.voucherCount ?? 0}张 附件数：{query.data?.attachmentCount ?? 0}张】
                    </span>
                </div>

                <LedgerTable
                    columns={columns}
                    data={treeRows}
                    loading={query.loading}
                    emptyText={accountSetId ? "该期间暂无正常凭证。" : "请先选择账套。"}
                    rowKey={(row) => row.code}
                    rowClassName={(_, index) => (index % 2 === 1 ? "voucher-summary-row-alt" : "")}
                    expandable={{
                        expandedRowKeys: parentKeys.filter((key) => !collapsed.has(key)),
                        indentSize: 18,
                        expandIcon: ({ expanded, onExpand, record }) =>
                            record.children?.length ? (
                                <span
                                    className="inline-flex size-4 cursor-pointer items-center justify-center text-(--muted-foreground)"
                                    onClick={(e) => {
                                        e.stopPropagation()
                                        onExpand(record, e)
                                    }}
                                >
                                    <DownOutlined rotate={expanded ? 0 : -90} className="text-[10px]" />
                                </span>
                            ) : null,
                        onExpand: (expanded, row) =>
                            setCollapsed((prev) => {
                                const next = new Set(prev)
                                if (expanded) next.delete(row.code)
                                else next.add(row.code)
                                return next
                            }),
                    }}
                    showCount={false}
                    footer={
                        <Table.Summary.Row>
                            <Table.Summary.Cell index={0} />
                            <Table.Summary.Cell index={1} className="font-medium">
                                合计
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={2} align="right" className="tabular-nums font-medium">
                                {fmtAmount(query.data?.totalDebit, true)}
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={3} align="right" className="tabular-nums font-medium">
                                {fmtAmount(query.data?.totalCredit, true)}
                            </Table.Summary.Cell>
                        </Table.Summary.Row>
                    }
                />
            </div>
        </LedgerPageShell>
    )
}
