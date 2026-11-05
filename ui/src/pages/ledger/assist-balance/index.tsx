import { PrinterOutlined } from "@ant-design/icons"
import { useEffect, useMemo, useRef } from "react"
import { Button } from "antd"
import type { TableColumnsType, TableColumnType } from "antd"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { type AssistBalanceRowDto, LedgersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerSection, LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import {
    asDate,
    assistTypeLabel,
    type LedgerConditions,
} from "@/components/ledger/types"
import { useAssistCategories, useAssistItems } from "@/components/ledger/useLedgerOptions"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"

/** 档案余额行加上稳定行键。 */
type BalanceRow = AssistBalanceRowDto & { rowKey: string }

type AmountKey =
    | "openingDebit"
    | "openingCredit"
    | "periodDebit"
    | "periodCredit"
    | "endingDebit"
    | "endingCredit"

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "assistType", "assistItem"]
const SWITCHES = ["hideZeroBalance", "hideNoActivity", "includeIdleAccounts"] as const

/** 后端把 assistType 作为必填参数，未选类别时不发请求。 */
const canQuery = (conditions: LedgerConditions) => Boolean(conditions.assistType)

/** 金额叶子列：0 显示为空，其余千分位保留两位。 */
const amount = (dataIndex: AmountKey, title: string): TableColumnType<BalanceRow> => ({
    title,
    dataIndex,
    render: (v) => fmtAmount(v),
    ...AMOUNT_COL,
})

/** 档案两列的表头跟着类别中文名走，例如「客户编码」「客户名称」。 */
function buildColumns(assistTypeText: string): TableColumnsType<BalanceRow> {
    return [
        { title: `${assistTypeText}编码`, dataIndex: "code" },
        { title: `${assistTypeText}名称`, dataIndex: "name" },
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

    // 未选类别时按「客户」展示表头，与柠檬云默认页签一致
    const assistTypeText = query.data?.assistTypeText || assistTypeLabel(assistType) || "客户"
    const columns = useMemo(() => buildColumns(assistTypeText), [assistTypeText])
    const accounts = query.data?.accounts ?? []

    return (
        <LedgerPageShell
            page="assist-balance"
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
                        <>
                            <LedgerExportButton
                                kind="assist-balance"
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
            {!accountSetId ? (
                <span className="py-4 text-muted-foreground">请先选择账套。</span>
            ) : !assistType ? (
                // 未选类别也渲染表头与页脚（与柠檬云一致）
                <LedgerTable<BalanceRow>
                    columns={columns}
                    data={[]}
                    emptyText="请先选择辅助核算类别。"
                    rowKey={(row) => row.rowKey}
                />
            ) : accounts.length === 0 && !query.loading ? (
                // 空数据也渲染表头与页脚（与柠檬云一致），仅表体提示
                <LedgerTable<BalanceRow>
                    columns={columns}
                    data={[]}
                    emptyText="该期间暂无核算项目余额，请确认凭证已过账。"
                    rowKey={(row) => row.rowKey}
                />
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
                                rowKey={(row) => row.rowKey}
                            />
                        </LedgerSection>
                    )
                })
            )}
        </LedgerPageShell>
    )
}
