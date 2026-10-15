import { useEffect, useMemo, useRef } from "react"
import { Alert } from "antd"
import type { TableColumnsType } from "antd"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { type AgeingBucketDto, LedgersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { LedgerSection, LedgerTable } from "@/components/ledger/LedgerTable"
import { asDate } from "@/components/ledger/types"
import { useAssistCategories, useAssistItems } from "@/components/ledger/useLedgerOptions"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"

/** 账龄一行对应一个档案，buckets 与表头 bucketLabels 按下标对齐。 */
type AgeingRow = {
    rowKey: string
    code: string
    name: string
    direction: string
    total?: number
    buckets: AgeingBucketDto[]
}

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "assistType", "assistItem"]

/** 账龄档数量由后端给出，列头按 bucketLabels 动态生成。 */
function buildColumns(assistTypeText: string, bucketLabels: string[]): TableColumnsType<AgeingRow> {
    return [
        { title: `${assistTypeText}编码`, dataIndex: "code" },
        { title: `${assistTypeText}名称`, dataIndex: "name" },
        { title: "方向", dataIndex: "direction" },
        { title: "期末余额", dataIndex: "total", render: (v) => fmtAmount(v), ...AMOUNT_COL },
        ...bucketLabels.map((label, index) => ({
            key: `bucket${index}`,
            title: label,
            ...AMOUNT_COL,
            render: (_: unknown, row: AgeingRow) => {
                const amount = fmtAmount(row.buckets[index]?.amount)
                const ratio = row.buckets[index]?.ratio
                if (!amount) return ""
                return (
                    <div className="flex flex-col">
                        <span>{amount}</span>
                        {ratio != null && (
                            <span className="text-xs text-muted-foreground">
                                {(ratio * 100).toFixed(1)}%
                            </span>
                        )}
                    </div>
                )
            },
        })),
    ]
}

export default function AgeingAnalysisPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

    const query = useLedgerQuery({
        accountSetId,
        token,
        canQuery: (conditions) => Boolean(conditions.assistType),
        fallbackError: "加载账龄分析失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersAgeingGet({
                accountSetId: id,
                assistType: conditions.assistType,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                accountFrom: conditions.accountFrom,
                accountTo: conditions.accountTo,
                assistItemId: conditions.assistItemId,
            }),
    })

    const categories = useAssistCategories(accountSetId, token)
    const assistType = query.conditions.assistType
    const { patch } = query

    // 未选类别时默认落在第一个已启用的类别上，避免进页面就报错；只补一次，免得用户清空后被回填。
    const seeded = useRef(false)
    useEffect(() => {
        if (!accountSetId) {
            seeded.current = false
            return
        }
        if (seeded.current || assistType != null) return
        const first = categories.find((c) => c.isEnabled)?.type
        if (first == null) return
        seeded.current = true
        patch({ assistType: first }, true)
    }, [accountSetId, categories, assistType, patch])

    const assistItems = useAssistItems(accountSetId, token, assistType)
    const enabledAssistTypes = useMemo(
        () =>
            categories
                .filter((c) => c.isEnabled)
                .map((c) => c.type)
                .filter((t): t is number => t != null),
        [categories],
    )

    const dto = query.data
    const accounts = dto?.accounts ?? []
    const columns = useMemo(
        () => buildColumns(dto?.assistTypeText ?? "", dto?.bucketLabels ?? []),
        [dto?.assistTypeText, dto?.bucketLabels],
    )

    return (
        <LedgerPageShell
            page="ageing"
            title="账龄分析"
            hint="按「科目 × 档案」列示期末未核销余额，并按账龄区间分档展示金额与占比。"
            panel={
                <LedgerQueryPanel
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
                    onReset={query.reset}
                    fields={FIELDS}
                    enabledAssistTypes={enabledAssistTypes}
                    assistItems={assistItems}
                    loading={query.loading}
                    extra={
                        <LedgerExportButton
                            kind="ageing"
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
                    {!accountSetId
                        ? "请先选择账套。"
                        : assistType
                            ? "该区间暂无未核销余额。"
                            : "请先选择辅助核算类别，再查询账龄分析。"}
                </span>
            ) : (
                <>
                    {dto?.note && <Alert type="info" showIcon title={dto.note} />}

                    {accounts.map((account) => {
                        const rows: AgeingRow[] = (account.items ?? []).map((item, index) => ({
                            rowKey: `${account.code}-${item.code}-${index}`,
                            code: item.code,
                            name: item.name,
                            direction: item.direction,
                            total: item.total,
                            buckets: item.buckets ?? [],
                        }))
                        const total = rows.reduce((sum, row) => sum + (row.total ?? 0), 0)
                        return (
                            <LedgerSection
                                key={account.code}
                                heading={`${account.code} ${account.name}`}
                                extra={<span>合计 {fmtAmount(total, true)}</span>}
                            >
                                <LedgerTable
                                    columns={columns}
                                    data={rows}
                                    loading={query.loading}
                                    emptyText="本科目该区间无未核销余额。"
                                    rowKey={(row) => row.rowKey}
                                />
                            </LedgerSection>
                        )
                    })}

                    {accounts.length > 0 && (
                        <div className="flex items-baseline justify-end gap-3 text-sm font-medium">
                            <span>合计</span>
                            <span className="tabular-nums">
                                {fmtAmount(dto?.total, true)}
                            </span>
                        </div>
                    )}
                </>
            )}
        </LedgerPageShell>
    )
}
