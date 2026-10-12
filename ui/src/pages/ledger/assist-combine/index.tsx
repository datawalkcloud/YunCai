import { useEffect, useMemo, useRef } from "react"
import { createColumnHelper } from "@tanstack/react-table"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { type AssistCombineCellDto, LedgersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/components/ledger/format"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { asDate, assistTypeLabel, ledgerFeatures } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useAssistCategories } from "@/components/ledger/useLedgerOptions"
import { TableCell, TableRow } from "@/components/ui/table"

/** 交叉表一行对应一个主档案：次维度发生额按列下标摊平进 cells。 */
type CombineRow = {
    rowKey: string
    code: string
    name: string
    cells: Record<string, number | undefined>
    totalDebit?: number
    totalCredit?: number
}

const helper = createColumnHelper<typeof ledgerFeatures, CombineRow>()

const AMOUNT_ID = /(Debit|Credit)$/
const isAmount = (id: string) => AMOUNT_ID.test(id)

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "assistType", "secondaryAssistType"]

/** 列随次维度档案变化：按返回体的 columns 重建借/贷成组的交叉列。 */
function buildColumns(assistTypeText: string, cells: AssistCombineCellDto[]) {
    return helper.columns([
        helper.accessor("code", { header: `主${assistTypeText}编码` }),
        helper.accessor("name", { header: `主${assistTypeText}名称` }),
        ...cells.map(
            (cell, index) =>
                helper.group({
                    id: `secondary${index}`,
                    header: `${cell.code} ${cell.name}`,
                    columns: helper.columns([
                        helper.accessor((row) => row.cells[`${index}Debit`], {
                            id: `secondary${index}Debit`,
                            header: "借方",
                            cell: (info) => fmtAmount(info.getValue()),
                        }),
                        helper.accessor((row) => row.cells[`${index}Credit`], {
                            id: `secondary${index}Credit`,
                            header: "贷方",
                            cell: (info) => fmtAmount(info.getValue()),
                        }),
                    ]),
                }),
        ),
        helper.accessor("totalDebit", {
            header: "合计-借方",
            cell: (info) => fmtAmount(info.getValue()),
        }),
        helper.accessor("totalCredit", {
            header: "合计-贷方",
            cell: (info) => fmtAmount(info.getValue()),
        }),
    ])
}

export default function AssistCombinePage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const categories = useAssistCategories(accountSetId, token)
    const enabledTypes = useMemo(
        () => categories.filter((c) => c.isEnabled === true).map((c) => c.type as number),
        [categories],
    )
    // 默认值优先挑已有档案的类别，否则交叉出来是空表。
    const typesWithItems = useMemo(
        () =>
            categories
                .filter((c) => c.isEnabled === true && (c.itemCount ?? 0) > 0)
                .map((c) => c.type as number),
        [categories],
    )

    const query = useLedgerQuery({
        accountSetId,
        token,
        canQuery: (conditions) =>
            Boolean(conditions.assistType && conditions.secondaryAssistType),
        fallbackError: "加载核算项目交叉表失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersAssistCombineGet({
                accountSetId: id,
                assistType: conditions.assistType,
                secondaryAssistType: conditions.secondaryAssistType,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                accountFrom: conditions.accountFrom,
                accountTo: conditions.accountTo,
            }),
    })

    const seeded = useRef(false)
    // 交叉表要主、次两个类别都有值才能取数；默认取前两个「已启用且已有档案」的类别，免得打开就是一张空表。
    useEffect(() => {
        if (seeded.current || query.conditions.assistType || typesWithItems.length < 2) return
        seeded.current = true
        query.patch({ assistType: typesWithItems[0], secondaryAssistType: typesWithItems[1] }, true)
    }, [typesWithItems, query])

    const dto = query.data
    const cells = dto?.columns ?? []
    const canQuery = Boolean(
        query.conditions.assistType && query.conditions.secondaryAssistType,
    )

    const columns = useMemo(
        () => buildColumns(dto?.assistTypeText ?? "", dto?.columns ?? []),
        [dto?.assistTypeText, dto?.columns],
    )
    const rows = useMemo<CombineRow[]>(
        () =>
            (dto?.groups ?? []).map((group, index) => {
                const flat: Record<string, number | undefined> = {}
                ;(group.cells ?? []).forEach((cell, cellIndex) => {
                    flat[`${cellIndex}Debit`] = cell.debit
                    flat[`${cellIndex}Credit`] = cell.credit
                })
                return {
                    rowKey: `${group.code}-${index}`,
                    code: group.code,
                    name: group.name,
                    cells: flat,
                    totalDebit: group.totalDebit,
                    totalCredit: group.totalCredit,
                }
            }),
        [dto?.groups],
    )

    return (
        <LedgerPageShell
            page="assist-combine"
            title="核算项目交叉表"
            hint="按主、次两个辅助核算类别交叉列示区间内借贷发生额，每个主档案一组。注意：一条分录在同一类别下挂多个档案时，各档案分别计全额，所以主、次两维度的合计不必然相等。"
            panel={
                <LedgerQueryPanel
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
                    onReset={query.reset}
                    fields={FIELDS}
                    enabledAssistTypes={enabledTypes}
                    loading={query.loading}
                    extra={
                        <LedgerExportButton
                            kind="assist-combine"
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
            {cells.length === 0 && !query.loading ? (
                <span className="py-4 text-muted-foreground">
                    {!accountSetId
                        ? "请先选择账套。"
                        : canQuery
                            ? `${assistTypeLabel(query.committed.assistType)} × ${assistTypeLabel(
                                  query.committed.secondaryAssistType,
                              )} 在本区间没有交叉发生额，请更换辅助核算类别或交叉类别后重新查询。`
                            : "请先同时选择辅助核算类别与交叉类别，再查询交叉表。"}
                </span>
            ) : (
                <LedgerTable
                    columns={columns}
                    data={rows}
                    loading={query.loading}
                    emptyText="该区间内无已过账的分录数据。"
                    getRowId={(row) => row.rowKey}
                    isAmountColumn={isAmount}
                    footer={
                        <TableRow>
                            <TableCell colSpan={2} className="font-medium">
                                合计
                            </TableCell>
                            <TableCell colSpan={cells.length * 2} />
                            <TableCell className="text-right tabular-nums font-medium">
                                {fmtAmount(dto?.totalDebit, true)}
                            </TableCell>
                            <TableCell className="text-right tabular-nums font-medium">
                                {fmtAmount(dto?.totalCredit, true)}
                            </TableCell>
                        </TableRow>
                    }
                />
            )}
        </LedgerPageShell>
    )
}
