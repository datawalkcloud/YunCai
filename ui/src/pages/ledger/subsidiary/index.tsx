import { useMemo, useState } from "react"
import { createColumnHelper } from "@tanstack/react-table"
import { useSearchParams } from "react-router"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { LedgersApi, type SubsidiaryLedgerRowDto } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { kindRowClass } from "@/components/ledger/format"
import { LedgerSection, LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { ROW_KIND, SORT_BY, asDate, type LedgerConditions, ledgerFeatures } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useVoucherLookup } from "@/components/ledger/useVoucherLookup"
import { VoucherDetailSheet } from "@/components/ledger/VoucherDetailSheet"

/** 明细账的一行了加上稳定行键。 */
type SubRow = SubsidiaryLedgerRowDto & { rowKey: string }

const helper = createColumnHelper<typeof ledgerFeatures, SubRow>()

const AMOUNT_IDS = ["debit", "credit", "balance"]
const isAmount = (id: string) => AMOUNT_IDS.includes(id)

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

function buildColumns(showCounterAccount: boolean) {
    return helper.columns([
        helper.accessor("date", { header: "日期" }),
        helper.accessor("voucherNo", { header: "凭证字号" }),
        helper.accessor("summary", { header: "摘要" }),
        ...(showCounterAccount
            ? [helper.accessor("counterAccount", { header: "对方科目" })]
            : []),
        helper.accessor("debit", { header: "借方", cell: (info) => fmtAmount(info.getValue()) }),
        helper.accessor("credit", { header: "贷方", cell: (info) => fmtAmount(info.getValue()) }),
        helper.accessor("direction", { header: "方向" }),
        helper.accessor("balance", { header: "余额", cell: (info) => fmtAmount(info.getValue()) }),
    ])
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

    const showCounterAccount = query.conditions.showCounterAccount ?? true
    const columns = useMemo(() => buildColumns(showCounterAccount), [showCounterAccount])
    const accounts = query.data?.accounts ?? []

    return (
        <LedgerPageShell
            page="subsidiary"
            title="明细账"
            hint="按科目分段逐笔列示发生额与滚动余额，每月末结出本月合计，末尾结出本年累计。"
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
                            kind="subsidiary"
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
            {accounts.length === 0 && !query.loading ? (
                <span className="py-4 text-muted-foreground">
                    {accountSetId ? "该期间暂无明细账数据，请确认凭证已过账。" : "请先选择账套。"}
                </span>
            ) : (
                accounts.map((account) => {
                    const rows: SubRow[] = (account.rows ?? []).map((row, index) => ({
                        ...row,
                        rowKey: `${account.code}-${index}`,
                    }))
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
                                columns={columns}
                                data={rows}
                                loading={query.loading}
                                emptyText="本科目该期间无发生额。"
                                getRowId={(row) => row.rowKey}
                                isAmountColumn={isAmount}
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
