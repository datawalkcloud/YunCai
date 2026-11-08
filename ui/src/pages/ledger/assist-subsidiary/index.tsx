import { PrinterOutlined } from "@ant-design/icons"
import { useEffect, useMemo, useRef, useState } from "react"
import { Button } from "antd"
import type { TableColumnsType } from "antd"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { type SubsidiaryLedgerRowDto, LedgersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { kindRowClass } from "@/components/ledger/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerSection, LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import {
    ROW_KIND,
    asDate,
    assistTypeLabel,
    type LedgerConditions,
} from "@/components/ledger/types"
import { useAssistCategories, useAssistItems } from "@/components/ledger/useLedgerOptions"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useVoucherLookup } from "@/components/ledger/useVoucherLookup"
import { VoucherDetailSheet } from "@/components/ledger/VoucherDetailSheet"

/** 档案段内的明细账行加上稳定行键。 */
type AssistRow = SubsidiaryLedgerRowDto & { rowKey: string }

const FIELDS: readonly LedgerField[] = [
    "date",
    "accountRange",
    "summary",
    "assistType",
    "assistItem",
]
const SWITCHES = ["showCounterAccount", "hideZeroBalance", "hideNoActivity"] as const

/** 后端把 assistType 作为必填参数，未选类别时不发请求。 */
const canQuery = (conditions: LedgerConditions) => Boolean(conditions.assistType)

function buildColumns(showCounterAccount: boolean, accountLabel = ""): TableColumnsType<AssistRow> {
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

export default function AssistSubsidiaryPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

    const query = useLedgerQuery({
        accountSetId,
        token,
        canQuery,
        fallbackError: "加载核算项目明细账失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersAssistSubsidiaryGet({
                accountSetId: id,
                assistType: conditions.assistType,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                accountFrom: conditions.accountFrom,
                accountTo: conditions.accountTo,
                assistItemId: conditions.assistItemId,
                summaryKeyword: conditions.summaryKeyword,
                showCounterAccount: conditions.showCounterAccount ?? true,
                hideZeroBalance: conditions.hideZeroBalance,
                hideNoActivity: conditions.hideNoActivity,
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

    const lookup = useVoucherLookup(accountSetId, token)
    const [voucherId, setVoucherId] = useState<string | null>(null)

    const showCounterAccount = query.conditions.showCounterAccount ?? false
    const columns = useMemo(() => buildColumns(showCounterAccount), [showCounterAccount])
    const accounts = query.data?.accounts ?? []

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
                    caption={`类别：${assistType ? assistTypeLabel(assistType) : "未选择"}`}
                    switches={SWITCHES}
                    enabledAssistTypes={enabledTypes}
                    assistItems={assistItems}
                    loading={query.loading}
                    extra={
                        <>
                            <LedgerExportButton
                                kind="assist-subsidiary"
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
                <LedgerTable<AssistRow>
                    columns={columns}
                    data={[]}
                    emptyText="请先选择辅助核算类别。"
                    rowKey={(row) => row.rowKey}
                />
            ) : accounts.length === 0 && !query.loading ? (
                // 空数据也渲染表头与页脚（与柠檬云一致），仅表体提示
                <LedgerTable<AssistRow>
                    columns={columns}
                    data={[]}
                    emptyText="该期间暂无核算项目明细，请确认凭证已过账。"
                    rowKey={(row) => row.rowKey}
                />
            ) : (
                accounts.map((account) => (
                    <LedgerSection
                        key={account.code}
                        heading={`${account.code} ${account.name}`}
                    >
                        <div className="flex flex-col gap-4">
                            {(account.items ?? []).map((item, itemIndex) => {
                                const rows: AssistRow[] = (item.rows ?? []).map((row, index) => ({
                                    ...row,
                                    rowKey: `${account.code}-${item.code}-${index}`,
                                }))
                                // 柠檬云的「科目」列按分录行重复显示所属科目
                                const accountLabel = `${account.code} ${account.name}`
                                return (
                                    <LedgerSection
                                        key={`${account.code}-${item.code}-${itemIndex}`}
                                        heading={`${item.code} ${item.name}`}
                                        extra={
                                            <span>
                                                借方 {fmtAmount(item.totalDebit, true)} ／ 贷方{" "}
                                                {fmtAmount(item.totalCredit, true)}
                                            </span>
                                        }
                                    >
                                        <LedgerTable
                                            columns={buildColumns(showCounterAccount, accountLabel)}
                                            data={rows}
                                            loading={query.loading}
                                            emptyText="本档案该期间无发生额。"
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
                            })}
                        </div>
                    </LedgerSection>
                ))
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
