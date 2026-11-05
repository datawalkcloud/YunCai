import { PrinterOutlined } from "@ant-design/icons"
import { useMemo, useState } from "react"
import { Button } from "antd"
import type { TableColumnsType } from "antd"
import { useSearchParams } from "react-router"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { LedgersApi, type SubsidiaryLedgerRowDto } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { kindRowClass } from "@/components/ledger/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerSection, LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { ROW_KIND, SORT_BY, asDate, type LedgerConditions } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useVoucherLookup } from "@/components/ledger/useVoucherLookup"
import { VoucherDetailSheet } from "@/components/ledger/VoucherDetailSheet"

/** 明细账的一行了加上稳定行键。 */
type SubRow = SubsidiaryLedgerRowDto & { rowKey: string }

const FIELDS: readonly LedgerField[] = [
    "date",
    "accountRange",
    "maxLevel",
    "summary",
    "sortBy",
]

const SWITCHES = [
    "showCounterAccount",
    "showOnlyLeaf",
    "hideZeroBalance",
    "hideNoActivity",
    "includeIdleAccounts",
] as const

function buildColumns(showCounterAccount: boolean, accountLabel = ""): TableColumnsType<SubRow> {
    return [
        { title: "日期", dataIndex: "date" },
        { title: "凭证字号", dataIndex: "voucherNo" },
        // 与柠檬云一致：科目列按分录行重复显示所属科目编码与名称
        { title: "科目", width: "180px", render: () => accountLabel },
        { title: "摘要", dataIndex: "summary" },
        ...(showCounterAccount ? [{ title: "对方科目", dataIndex: "counterAccount" }] : []),
        { title: "借方", dataIndex: "debit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
        { title: "贷方", dataIndex: "credit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
        { title: "方向", dataIndex: "direction" },
        { title: "余额", dataIndex: "balance", render: (v) => fmtAmount(v), ...AMOUNT_COL },
    ]
}

function SubsidiaryLedger({ initial }: { initial: LedgerConditions }) {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

    const query = useLedgerQuery({
        accountSetId,
        token,
        initial,
        fallbackError: "加载明细账失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersSubsidiaryGet({
                accountSetId: id,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                accountFrom: conditions.accountFrom,
                accountTo: conditions.accountTo,
                maxLevel: conditions.maxLevel,
                summaryKeyword: conditions.summaryKeyword,
                sortBy: conditions.sortBy ?? SORT_BY.voucherNumber,
                showCounterAccount: conditions.showCounterAccount ?? true,
                showOnlyLeaf: conditions.showOnlyLeaf ?? true,
                hideZeroBalance: conditions.hideZeroBalance,
                hideNoActivity: conditions.hideNoActivity,
                includeIdleAccounts: conditions.includeIdleAccounts,
            }),
    })

    const lookup = useVoucherLookup(accountSetId, token)
    const [voucherId, setVoucherId] = useState<string | null>(null)

    const showCounterAccount = query.conditions.showCounterAccount ?? false
    const columns = useMemo(() => buildColumns(showCounterAccount), [showCounterAccount])
    const accounts = query.data?.accounts ?? []

    return (
        <LedgerPageShell
            page="subsidiary"
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
                        <>
                            <LedgerExportButton
                                kind="subsidiary"
                                accountSetId={accountSetId}
                                token={token}
                                conditions={query.committed}
                            />
                            <Button icon={<PrinterOutlined />} onClick={() => window.print()}>
                                打印
                            </Button>
                        </>
                    }
                    disabled={!accountSetId}
                />
            }
            meta={query.meta}
            error={query.error}
        >
            {accounts.length === 0 && !query.loading ? (
                // 空数据也渲染表头与页脚（与柠檬云一致），仅表体提示
                <LedgerTable<SubRow>
                    columns={columns}
                    data={[]}
                    emptyText={accountSetId ? "该期间暂无明细账数据，请确认凭证已过账。" : "请先选择账套。"}
                    rowKey={(row) => row.rowKey}
                />
            ) : (
                accounts.map((account) => {
                    const rows: SubRow[] = (account.rows ?? []).map((row, index) => ({
                        ...row,
                        rowKey: `${account.code}-${index}`,
                    }))
                    // 柠檬云的「科目」列按分录行重复显示所属科目
                    const accountLabel = `${account.code} ${account.name}`
                    return (
                        <LedgerSection
                            key={account.code}
                            heading={`${account.code} ${account.name}`}
                            extra={
                                <span>
                                    借方 {fmtAmount(account.totalDebit, true)} ／ 贷方{" "}
                                    {fmtAmount(account.totalCredit, true)}
                                </span>
                            }
                        >
                            <LedgerTable
                                columns={buildColumns(showCounterAccount, accountLabel)}
                                data={rows}
                                loading={query.loading}
                                emptyText="本科目该期间无发生额。"
                                rowKey={(row) => row.rowKey}
                                rowClassName={(row) => kindRowClass(row.kind)}
                                onRowClick={(row) => {
                                    if (row.kind !== ROW_KIND.entry || !row.voucherNo) return
                                    const id = lookup.resolveId(row.date, row.voucherNo)
                                    if (id) setVoucherId(id)
                                }}
                            />
                        </LedgerSection>
                    )
                })
            )}

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

export default function SubsidiaryLedgerPage() {
    const [params] = useSearchParams()
    const accountSetId = useCurrentAccountSetId() ?? ""
    // 总账行点击下钻时带上科目编码与区间，这里据此重建初始条件。
    const account = params.get("account")
    const initial: LedgerConditions = {
        ...(account ? { accountFrom: account, accountTo: account } : {}),
        ...(params.get("startDate") ? { startDate: params.get("startDate") ?? undefined } : {}),
        ...(params.get("endDate") ? { endDate: params.get("endDate") ?? undefined } : {}),
    }

    return <SubsidiaryLedger key={`${accountSetId}|${account ?? ""}`} initial={initial} />
}
