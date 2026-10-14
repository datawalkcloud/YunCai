import { useEffect, useMemo, useRef } from "react"
import { createColumnHelper } from "@tanstack/react-table"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { type AssistBalanceRowDto, LedgersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { LedgerSection, LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import {
    asDate,
    assistTypeLabel,
    ledgerFeatures,
    type LedgerConditions,
} from "@/components/ledger/types"
import { useAssistCategories, useAssistItems } from "@/components/ledger/useLedgerOptions"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"

/** 档案余额行加上稳定行键。 */
type BalanceRow = AssistBalanceRowDto & { rowKey: string }

const helper = createColumnHelper<typeof ledgerFeatures, BalanceRow>()

const AMOUNT_IDS = [
    "openingDebit",
    "openingCredit",
    "periodDebit",
    "periodCredit",
    "endingDebit",
    "endingCredit",
] as const
const isAmount = (id: string) => (AMOUNT_IDS as readonly string[]).includes(id)

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "assistType", "assistItem"]
const SWITCHES = ["hideZeroBalance", "hideNoActivity", "includeIdleAccounts"] as const

/** 后端把 assistType 作为必填参数，未选类别时不发请求。 */
const canQuery = (conditions: LedgerConditions) => Boolean(conditions.assistType)

/** 金额叶子列：0 显示为空，其余千分位保留两位。 */
const amount = (accessor: (typeof AMOUNT_IDS)[number], title: string) =>
    helper.accessor(accessor, { header: title, cell: (info) => fmtAmount(info.getValue()) })

/** 档案两列的表头跟着类别中文名走，例如「客户编码」「客户名称」。 */
function buildColumns(assistTypeText: string) {
    return helper.columns([
        helper.accessor("code", { header: `${assistTypeText}编码` }),
        helper.accessor("name", { header: `${assistTypeText}名称` }),
        helper.group({
            id: "opening",
            header: "期初余额",
            columns: helper.columns([amount("openingDebit", "借方"), amount("openingCredit", "贷方")]),
        }),
        helper.group({
            id: "period",
            header: "本期发生额",
            columns: helper.columns([amount("periodDebit", "借方"), amount("periodCredit", "贷方")]),
        }),
        helper.group({
            id: "ending",
            header: "期末余额",
            columns: helper.columns([amount("endingDebit", "借方"), amount("endingCredit", "贷方")]),
        }),
        helper.accessor("direction", { header: "方向" }),
    ])
}

export default function AssistBalancePage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

    const query = useLedgerQuery({
        accountSetId,
        token,
        canQuery,
        fallbackError: "加载核算项目余额表失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersAssistBalanceGet({
                accountSetId: id,
                assistType: conditions.assistType,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                accountFrom: conditions.accountFrom,
                accountTo: conditions.accountTo,
                assistItemId: conditions.assistItemId,
                hideZeroBalance: conditions.hideZeroBalance,
                hideNoActivity: conditions.hideNoActivity,
                includeIdleAccounts: conditions.includeIdleAccounts,
            }),
    })

    const categories = useAssistCategories(accountSetId, token)
    const enabledTypes = useMemo(
        () => categories.filter((item) => item.isEnabled).map((item) => item.type ?? 0).filter((type) => type > 0),
        [categories],
    )

    const assistType = query.conditions.assistType
    const assistItems = useAssistItems(accountSetId, token, assistType)
    const patch = query.patch

    const seeded = useRef(false)
    // 类别必填：进入页面后自动选中第一个已启用的类别并立即查询，只补一次。
    useEffect(() => {
        if (!accountSetId) {
            seeded.current = false
            return
        }
        if (seeded.current || assistType != null) return
        const first = enabledTypes[0]
        if (first === undefined) return
        seeded.current = true
        patch({ assistType: first }, true)
    }, [accountSetId, enabledTypes, assistType, patch])

    const assistTypeText = query.data?.assistTypeText || assistTypeLabel(assistType)
    const columns = useMemo(() => buildColumns(assistTypeText), [assistTypeText])
    const accounts = query.data?.accounts ?? []

    return (
        <LedgerPageShell
            page="assist-balance"
            title="核算项目余额表"
            hint="按科目分段列示各档案的期初余额、本期发生额与期末余额；一条分录在同一类别下挂多个档案时，各档案分别计全额。"
            panel={
                <LedgerQueryPanel
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
                    onReset={query.reset}
                    fields={FIELDS}
                    switches={SWITCHES}
                    enabledAssistTypes={enabledTypes}
                    assistItems={assistItems}
                    loading={query.loading}
                    extra={
                        <LedgerExportButton
                            kind="assist-balance"
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
            {!accountSetId ? (
                <span className="py-4 text-muted-foreground">请先选择账套。</span>
            ) : !assistType ? (
                <span className="py-4 text-muted-foreground">请先选择辅助核算类别。</span>
            ) : accounts.length === 0 && !query.loading ? (
                <span className="py-4 text-muted-foreground">
                    该期间暂无核算项目余额，请确认凭证已过账。
                </span>
            ) : (
                accounts.map((account) => {
                    const rows: BalanceRow[] = (account.rows ?? []).map((row, index) => ({
                        ...row,
                        rowKey: `${account.code}-${row.code}-${index}`,
                    }))
                    return (
                        <LedgerSection
                            key={account.code}
                            heading={`${account.code} ${account.name}`}
                        >
                            <LedgerTable
                                columns={columns}
                                data={rows}
                                loading={query.loading}
                                emptyText="本科目该期间无辅助核算档案余额。"
                                getRowId={(row) => row.rowKey}
                                isAmountColumn={isAmount}
                            />
                        </LedgerSection>
                    )
                })
            )}
        </LedgerPageShell>
    )
}
