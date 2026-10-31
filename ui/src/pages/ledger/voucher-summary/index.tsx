import { PrinterOutlined } from "@ant-design/icons"
import { Button, Table  } from "antd"
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
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { asDate } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"

/** 汇总行以科目编码为键。 */
type SummaryRow = VoucherSummaryRowDto & { rowKey: string }

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "summary"]
const SWITCHES = ["showOnlyLeaf"] as const

const columns: TableColumnsType<SummaryRow> = [
    { title: "科目编码", dataIndex: "code" },
    { title: "科目名称", dataIndex: "name" },
    // 与柠檬云一致：金额合计为分组表头，下辖借方/贷方两列
    {
        title: "金额合计",
        children: [
            { title: "借方", dataIndex: "debit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
            { title: "贷方", dataIndex: "credit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
        ],
    },
]

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
            actions={

                <Button icon={<PrinterOutlined />} onClick={() => window.print()}>

                    打印

                </Button>

            }

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
                rowKey={(row) => row.rowKey ?? ""}
                footer={
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0} colSpan={2} className="font-medium">
                            合计
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={2} />
                        <Table.Summary.Cell index={3} align="right" className="tabular-nums font-medium">
                            {fmtAmount(query.data?.totalDebit, true)}
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={4} align="right" className="tabular-nums font-medium">
                            {fmtAmount(query.data?.totalCredit, true)}
                        </Table.Summary.Cell>
                    </Table.Summary.Row>
                }
            />
        </LedgerPageShell>
    )
}
