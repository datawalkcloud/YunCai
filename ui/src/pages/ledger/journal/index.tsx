import { useState } from "react"
import { Table } from "antd"
import type { TableColumnsType } from "antd"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { LedgersApi, type VoucherJournalRowDto } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { SORT_BY, asDate } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useVoucherLookup } from "@/components/ledger/useVoucherLookup"
import { VoucherDetailSheet } from "@/components/ledger/VoucherDetailSheet"

/** 序时账按「凭证 + 分录」出行，行键用凭证字号与行次组合。 */
type JournalRow = VoucherJournalRowDto & { rowKey: string }

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "summary", "sortBy"]
const SWITCHES = ["showOnlyLeaf"] as const

const columns: TableColumnsType<JournalRow> = [
    { title: "日期", dataIndex: "date" },
    { title: "凭证号", dataIndex: "voucherNo" },
    { title: "摘要", dataIndex: "summary" },
    { title: "科目编码", dataIndex: "accountCode" },
    { title: "科目名称", dataIndex: "accountName" },
    { title: "借方金额", dataIndex: "debit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
    { title: "贷方金额", dataIndex: "credit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
    { title: "制单人", dataIndex: "preparer" },
    { title: "审核人", dataIndex: "reviewer" },
    { title: "附件数", dataIndex: "attachmentCount", render: (v) => (v ? String(v) : "") },
    // 与柠檬云一致：凭证备注列
    { title: "备注", dataIndex: "remark", render: (v) => v ?? "" },
]

export default function VoucherJournalPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

    const query = useLedgerQuery({
        accountSetId,
        token,
        fallbackError: "加载序时账失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersJournalGet({
                accountSetId: id,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                accountFrom: conditions.accountFrom,
                accountTo: conditions.accountTo,
                summaryKeyword: conditions.summaryKeyword,
                sortBy: conditions.sortBy ?? SORT_BY.voucherNumber,
                showOnlyLeaf: conditions.showOnlyLeaf ?? true,
            }),
    })

    const lookup = useVoucherLookup(accountSetId, token)
    const [voucherId, setVoucherId] = useState<string | null>(null)

    const rows: JournalRow[] = (query.data?.rows ?? []).map((row, index) => ({
        ...row,
        rowKey: `${row.voucherNo}-${index}`,
    }))

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
                        <LedgerExportButton
                            kind="journal"
                            accountSetId={accountSetId}
                            token={token}
                            conditions={query.committed}
                        />
                    }
                    disabled={!accountSetId}
                />
            }
            meta={query.meta}
            error={query.error}
        >
            {query.data ? (
                <span className="text-muted-foreground">
                    共 {query.data.voucherCount ?? 0} 张凭证，附件 {query.data.totalAttachments ?? 0} 张。
                </span>
            ) : null}

            <LedgerTable
                columns={columns}
                data={rows}
                loading={query.loading}
                emptyText={
                    accountSetId ? "该期间暂无已过账凭证分录。" : "请先选择账套。"
                }
                rowKey={(row) => row.rowKey}
                onRowClick={(row) => {
                    const id = lookup.resolveId(row.date, row.voucherNo)
                    if (id) setVoucherId(id)
                }}
                footer={
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0} colSpan={5} className="font-medium">
                            合计
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={5} align="right" className="tabular-nums font-medium">
                            {fmtAmount(query.data?.totalDebit, true)}
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={6} align="right" className="tabular-nums font-medium">
                            {fmtAmount(query.data?.totalCredit, true)}
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={7} colSpan={3} />
                    </Table.Summary.Row>
                }
            />

            <VoucherDetailSheet
                accountSetId={accountSetId}
                token={token}
                voucherId={voucherId}
                onOpenChange={(open) => {
                    if (!open) setVoucherId(null)
                }}
            />
        </LedgerPageShell>
    )
}
