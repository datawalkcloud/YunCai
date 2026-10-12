import { useState } from "react"
import { createColumnHelper } from "@tanstack/react-table"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { LedgersApi, type VoucherJournalRowDto } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/components/ledger/format"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { SORT_BY, asDate, ledgerFeatures } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useVoucherLookup } from "@/components/ledger/useVoucherLookup"
import { VoucherDetailSheet } from "@/components/ledger/VoucherDetailSheet"
import { TableCell, TableRow } from "@/components/ui/table"

/** 序时账按「凭证 + 分录」出行，行键用凭证字号与行次组合。 */
type JournalRow = VoucherJournalRowDto & { rowKey: string }

const helper = createColumnHelper<typeof ledgerFeatures, JournalRow>()

const AMOUNT_IDS = ["debit", "credit"]
const isAmount = (id: string) => AMOUNT_IDS.includes(id)

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "summary", "sortBy"]
const SWITCHES = ["showOnlyLeaf"] as const

const columns = helper.columns([
    helper.accessor("date", { header: "日期" }),
    helper.accessor("voucherNo", { header: "凭证字号" }),
    helper.accessor("summary", { header: "摘要" }),
    helper.accessor("accountCode", { header: "科目编码" }),
    helper.accessor("accountName", { header: "科目名称" }),
    helper.accessor("debit", { header: "借方", cell: (info) => fmtAmount(info.getValue()) }),
    helper.accessor("credit", { header: "贷方", cell: (info) => fmtAmount(info.getValue()) }),
    helper.accessor("preparer", { header: "制单人" }),
    helper.accessor("reviewer", { header: "审核人" }),
    helper.accessor("attachmentCount", {
        header: "附件",
        cell: (info) => (info.getValue() ? String(info.getValue()) : ""),
    }),
])

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
            page="journal"
            title="序时账"
            hint="按期间顺序逐笔列示全部已过账凭证分录，点击任一分录行可查看该凭证全文。"
            panel={
                <LedgerQueryPanel
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
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
                getRowId={(row) => row.rowKey}
                isAmountColumn={isAmount}
                onRowClick={(row) => {
                    const id = lookup.resolveId(row.date, row.voucherNo)
                    if (id) setVoucherId(id)
                }}
                footer={
                    <TableRow>
                        <TableCell colSpan={5} className="font-medium">
                            合计
                        </TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                            {fmtAmount(query.data?.totalDebit, true)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                            {fmtAmount(query.data?.totalCredit, true)}
                        </TableCell>
                        <TableCell colSpan={3} />
                    </TableRow>
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
