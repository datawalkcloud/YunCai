import { createColumnHelper } from "@tanstack/react-table"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { LedgersApi, type VoucherSummaryRowDto } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/components/ledger/format"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { asDate, ledgerFeatures } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { TableCell, TableRow } from "@/components/ui/table"

/** 汇总行以科目编码为键。 */
type SummaryRow = VoucherSummaryRowDto & { rowKey: string }

const helper = createColumnHelper<typeof ledgerFeatures, SummaryRow>()

const AMOUNT_IDS = ["voucherCount", "debit", "credit"]
const isAmount = (id: string) => AMOUNT_IDS.includes(id)

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "summary"]
const SWITCHES = ["showOnlyLeaf"] as const

const columns = helper.columns([
    helper.accessor("code", { header: "科目编码" }),
    helper.accessor("name", { header: "科目名称" }),
    helper.accessor("voucherCount", {
        header: "凭证张数",
        cell: (info) => (info.getValue() ? String(info.getValue()) : ""),
    }),
    helper.accessor("debit", { header: "借方发生额", cell: (info) => fmtAmount(info.getValue()) }),
    helper.accessor("credit", { header: "贷方发生额", cell: (info) => fmtAmount(info.getValue()) }),
])

export default function VoucherSummaryPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

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
                showOnlyLeaf: conditions.showOnlyLeaf ?? true,
            }),
    })

    const rows: SummaryRow[] = (query.data?.rows ?? []).map((row) => ({ ...row, rowKey: row.code }))

    return (
        <LedgerPageShell
            page="voucher-summary"
            title="凭证汇总表"
            hint="按科目汇总区间内已过账凭证的借贷发生额，凭证张数与附件张数均按凭证去重统计。"
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
                            kind="voucher-summary"
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
                    涉及 {query.data.voucherCount ?? 0} 张凭证，附件 {query.data.attachmentCount ?? 0} 张。
                </span>
            ) : null}

            <LedgerTable
                columns={columns}
                data={rows}
                loading={query.loading}
                emptyText={accountSetId ? "该期间暂无已过账凭证。" : "请先选择账套。"}
                getRowId={(row) => row.rowKey ?? ""}
                isAmountColumn={isAmount}
                footer={
                    <TableRow>
                        <TableCell colSpan={2} className="font-medium">
                            合计
                        </TableCell>
                        <TableCell />
                        <TableCell className="text-right tabular-nums font-medium">
                            {fmtAmount(query.data?.totalDebit, true)}
                        </TableCell>
                        <TableCell className="text-right tabular-nums font-medium">
                            {fmtAmount(query.data?.totalCredit, true)}
                        </TableCell>
                    </TableRow>
                }
            />
        </LedgerPageShell>
    )
}
