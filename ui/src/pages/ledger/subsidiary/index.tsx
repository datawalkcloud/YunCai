import { useEffect, useMemo, useState } from "react"
import { Table } from "antd"
import type { TableColumnsType } from "antd"
import { useSearchParams } from "react-router"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { LedgersApi, type SubsidiaryLedgerRowDto } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { kindRowClass } from "@/components/ledger/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { SubsidiaryLedgerFilter } from "@/components/ledger/SubsidiaryLedgerFilter"
import { LedgerPrintButton, LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { QuickAccountSwitch } from "@/components/ledger/QuickAccountSwitch"
import { useAccounts } from "@/components/ledger/useLedgerOptions"
import { ROW_KIND, SORT_BY, asDate, type LedgerConditions } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useVoucherLookup } from "@/components/ledger/useVoucherLookup"
import { VoucherDetailSheet } from "@/components/ledger/VoucherDetailSheet"

/** 明细账的一行了加上稳定行键与所属科目。 */
type SubRow = SubsidiaryLedgerRowDto & { rowKey: string; accountLabel: string }

function buildColumns(showCounterAccount: boolean): TableColumnsType<SubRow> {
    const nowrap = { style: { whiteSpace: "nowrap" as const } }
    return [
        { title: "日期", dataIndex: "date", width: 108, onCell: () => nowrap },
        {
            title: "凭证字号",
            dataIndex: "voucherNo",
            width: 96,
            onCell: () => nowrap,
            render: (value) => (value ? <span className="text-(--brand)">{value}</span> : null),
        },
        // 科目列按分录行重复显示所属科目编码与名称
        { title: "科目", dataIndex: "accountLabel", width: 180 },
        { title: "摘要", dataIndex: "summary" },
        ...(showCounterAccount ? [{ title: "对方科目", dataIndex: "counterAccount" }] : []),
        { title: "借方", dataIndex: "debit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
        { title: "贷方", dataIndex: "credit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
        { title: "方向", dataIndex: "direction", width: 64, align: "center" as const, onCell: () => nowrap },
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
                minLevel: conditions.minLevel,
                summaryKeyword: conditions.summaryKeyword,
                sortBy: conditions.sortBy ?? SORT_BY.voucherNumber,
                showAssist: conditions.showAssist,
                showCounterAccount: conditions.showCounterAccount ?? true,
                showOnlyLeaf: conditions.showOnlyLeaf ?? true,
                hideZeroBalance: conditions.hideZeroBalance,
                hideNoActivity: conditions.hideNoActivity,
                hideTotalsNoActivity: conditions.hideTotalsNoActivity,
                includeIdleAccounts: conditions.includeIdleAccounts,
            }),
    })

    const lookup = useVoucherLookup(accountSetId, token)
    const [voucherId, setVoucherId] = useState<string | null>(null)
    const allAccounts = useAccounts(accountSetId, token)

    const showCounterAccount = query.conditions.showCounterAccount ?? false
    const columns = useMemo(() => buildColumns(showCounterAccount), [showCounterAccount])
    const accounts = useMemo(() => query.data?.accounts ?? [], [query.data])

    // 右侧快速切换只列示本期有数据的科目；带科目区间或级次的结果被裁剪，沿用上一次全量口径。
    const accountScoped = Boolean(
        query.committed.accountFrom || query.committed.accountTo || query.committed.minLevel || query.committed.maxLevel,
    )
    const [dataCodes, setDataCodes] = useState<Set<string>>(() => new Set())
    useEffect(() => {
        if (accountScoped) return
        setDataCodes(new Set(accounts.map((account) => account.code).filter((code): code is string => Boolean(code))))
    }, [accounts, accountScoped])

    // 快速切换只在「起止科目同为一条编码」时高亮，手工输入区间则不选中任何节点。
    const selectedAccount =
        query.conditions.accountFrom && query.conditions.accountFrom === query.conditions.accountTo
            ? query.conditions.accountFrom
            : undefined

    // 信息行「科目：1001 库存现金」，与表头上方一致。
    const caption = useMemo(() => {
        const { accountFrom: from, accountTo: to } = query.committed
        if (from && to && from === to) {
            const name = (allAccounts.find((account) => account.code === from) ?? accounts.find((account) => account.code === from))?.name
            return `${from} ${name ?? ""}`.trim()
        }
        if (from || to) return `${from ?? ""} 至 ${to ?? ""}`.trim()
        return "所有"
    }, [query.committed, allAccounts, accounts])

    // 明细账是一张连续表：科目按行重复，横向滚动与分页都在同一张表上。
    const rows = useMemo<SubRow[]>(
        () =>
            accounts.flatMap((account) =>
                (account.rows ?? []).map((row, index) => ({
                    ...row,
                    accountLabel: `${account.code} ${account.name}`,
                    rowKey: `${account.code}-${index}`,
                })),
            ),
        [accounts],
    )

    // 合计只累加发生额行：期初/本月合计/本年累计行本身是汇总，重复计入会把金额翻倍。
    const totals = useMemo(
        () =>
            rows.reduce(
                (acc, row) => {
                    if (row.kind === ROW_KIND.entry) {
                        acc.debit += row.debit ?? 0
                        acc.credit += row.credit ?? 0
                    }
                    return acc
                },
                { debit: 0, credit: 0 },
            ),
        [rows],
    )

    return (
        <LedgerPageShell
            panel={
                <SubsidiaryLedgerFilter
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
                    onCancel={query.cancel}
                    onReset={query.reset}
                    accounts={allAccounts}
                    loading={query.loading}
                    disabled={!accountSetId}
                    caption={`科目：${caption}`}
                    extra={
                        <>
                            <LedgerExportButton
                                kind="subsidiary"
                                accountSetId={accountSetId}
                                token={token}
                                conditions={query.committed}
                            />
                            <LedgerPrintButton
                                kind="subsidiary"
                                accountSetId={accountSetId}
                                token={token}
                                conditions={query.committed}
                            />
                        </>
                    }
                />
            }
            meta={query.meta}
            error={query.error}
            side={
                <QuickAccountSwitch
                    accounts={allAccounts}
                    hasDataCodes={dataCodes}
                    value={selectedAccount}
                    disabled={!accountSetId}
                    onChange={(code) => query.patch({ accountFrom: code, accountTo: code }, true)}
                />
            }
        >
            <LedgerTable<SubRow>
                columns={columns}
                data={rows}
                loading={query.loading}
                emptyText={accountSetId ? "该期间暂无明细账数据，请确认凭证为正常状态。" : "请先选择账套。"}
                rowKey={(row) => row.rowKey}
                rowClassName={(row) => kindRowClass(row.kind)}
                onRowClick={(row) => {
                    if (row.kind !== ROW_KIND.entry || !row.voucherNo) return
                    const id = lookup.resolveId(row.date, row.voucherNo)
                    if (id) setVoucherId(id)
                }}
                pagination={{
                    pageSize: 20,
                    showSizeChanger: true,
                    pageSizeOptions: [20, 50, 100],
                    showTotal: (total, range) => `显示 ${range[0]} 到 ${range[1]}，共 ${total} 条记录`,
                }}
                footer={
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0} colSpan={showCounterAccount ? 5 : 4} className="font-medium">
                            合计
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={showCounterAccount ? 5 : 4} {...AMOUNT_COL} className="font-medium tabular-nums">
                            {fmtAmount(totals.debit, true)}
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={showCounterAccount ? 6 : 5} {...AMOUNT_COL} className="font-medium tabular-nums">
                            {fmtAmount(totals.credit, true)}
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={showCounterAccount ? 7 : 6} colSpan={2} />
                    </Table.Summary.Row>
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

export default function SubsidiaryLedgerPage() {
    const [params] = useSearchParams()
    const accountSetId = useCurrentAccountSetId() ?? ""
    // 总账行点击下钻时带上科目编码与区间，这里据此重建初始条件；未带区间时由 useLedgerQuery 默认当月。
    const account = params.get("account")
    const initial: LedgerConditions = {
        ...(account ? { accountFrom: account, accountTo: account } : {}),
        ...(params.get("startDate") ? { startDate: params.get("startDate") ?? undefined } : {}),
        ...(params.get("endDate") ? { endDate: params.get("endDate") ?? undefined } : {}),
    }

    return <SubsidiaryLedger key={`${accountSetId}|${account ?? ""}`} initial={initial} />
}
