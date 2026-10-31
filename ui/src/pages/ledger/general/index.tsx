import { PrinterOutlined } from "@ant-design/icons"
import { Button } from "antd"
import type { TableColumnsType, TableColumnType } from "antd"
import { useNavigate, useParams } from "react-router"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { type GeneralLedgerRowDto, LedgersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerSection, LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { type LedgerField, LedgerQueryPanel } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { asDate } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"

/** 总账的月份行加上稳定行键。 */
type GeneralRow = GeneralLedgerRowDto & { rowKey: string }

type AmountKey =
    | "openingDebit"
    | "openingCredit"
    | "debit"
    | "credit"
    | "endingDebit"
    | "endingCredit"

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "maxLevel"]
const SWITCHES = ["showOnlyLeaf", "hideZeroBalance", "hideNoActivity", "includeIdleAccounts"] as const

/** 金额叶子列：格式化与科目余额表一致。 */
const amount = (dataIndex: AmountKey, title: string): TableColumnType<GeneralRow> => ({
    title,
    dataIndex,
    render: (v) => fmtAmount(v),
    ...AMOUNT_COL,
})

/** 多级表头：期初 / 本期 / 期末各带借方、贷方两个子列。 */
const columns: TableColumnsType<GeneralRow> = [
    { title: "期间", dataIndex: "periodText" },
    {
        key: "opening",
        title: "期初余额",
        children: [amount("openingDebit", "借方"), amount("openingCredit", "贷方")],
    },
    {
        key: "period",
        title: "本期发生额",
        children: [amount("debit", "借方"), amount("credit", "贷方")],
    },
    {
        key: "ending",
        title: "期末余额",
        children: [amount("endingDebit", "借方"), amount("endingCredit", "贷方")],
    },
    { title: "方向", dataIndex: "direction" },
]

export default function GeneralLedgerPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { accountSetId: routeId } = useParams<{ accountSetId: string }>()
    const navigate = useNavigate()

    const query = useLedgerQuery({
        accountSetId,
        token,
        fallbackError: "加载总账失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersGeneralGet({
                accountSetId: id,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                accountFrom: conditions.accountFrom,
                accountTo: conditions.accountTo,
                maxLevel: conditions.maxLevel,
                showOnlyLeaf: conditions.showOnlyLeaf ?? true,
                hideZeroBalance: conditions.hideZeroBalance,
                hideNoActivity: conditions.hideNoActivity,
                includeIdleAccounts: conditions.includeIdleAccounts,
            }),
    })

    const accounts = query.data?.accounts ?? []

    // 总账点行下钻到该科目的明细账，沿用同一区间。
    function goSubsidiary(code: string) {
        const params = new URLSearchParams()
        params.set("account", code)
        if (query.meta?.startDate) params.set("startDate", query.meta.startDate)
        if (query.meta?.endDate) params.set("endDate", query.meta.endDate)
        navigate(`/${routeId}/ledger/subsidiary?${params.toString()}`)
    }

    return (
        <LedgerPageShell
            page="general"
            title="总账"
            hint="按科目分段、每个自然月一行列示期初、本月借贷、期末与余额方向。点击月份行可下钻该科目明细账。"
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
                            kind="general"
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
                // 空数据也渲染表头与页脚（与柠檬云一致），仅表体提示
                <LedgerTable<GeneralRow>
                    columns={columns}
                    data={[]}
                    emptyText={accountSetId ? "该期间暂无总账数据，请确认凭证已过账。" : "请先选择账套。"}
                    rowKey={(row) => row.rowKey}
                />
            ) : (
                accounts.map((account) => {
                    const rows: GeneralRow[] = (account.rows ?? []).map((row, index) => ({
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
                                rowKey={(row) => row.rowKey}
                                onRowClick={() => goSubsidiary(account.code ?? "")}
                            />
                        </LedgerSection>
                    )
                })
            )}
        </LedgerPageShell>
    )
}
