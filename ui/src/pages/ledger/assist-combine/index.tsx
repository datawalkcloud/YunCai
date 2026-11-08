import { useEffect, useMemo, useRef } from "react"
import { Table } from "antd"
import type { TableColumnsType } from "antd"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { type AssistCombineCellDto, LedgersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { asDate, assistTypeLabel } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useAssistCategories } from "@/components/ledger/useLedgerOptions"

/** 交叉表一行对应一个主档案：次维度发生额按列下标摊平进 cells。 */
type CombineRow = {
    rowKey: string
    code: string
    name: string
    cells: Record<string, number | undefined>
    totalDebit?: number
    totalCredit?: number
}

const FIELDS: readonly LedgerField[] = ["date", "accountRange", "assistType", "secondaryAssistType"]

/** 列随次维度档案变化：按返回体的 columns 重建借/贷成组的交叉列。 */
function buildColumns(
    assistTypeText: string,
    cells: AssistCombineCellDto[],
): TableColumnsType<CombineRow> {
    const cell = (key: string) => (_: unknown, row: CombineRow) => fmtAmount(row.cells[key])
    return [
        { title: `主${assistTypeText}编码`, dataIndex: "code" },
        { title: `主${assistTypeText}名称`, dataIndex: "name" },
        ...cells.map((item, index) => ({
            key: `secondary${index}`,
            title: `${item.code} ${item.name}`,
            children: [
                {
                    key: `secondary${index}Debit`,
                    title: "借方",
                    render: cell(`${index}Debit`),
                    ...AMOUNT_COL,
                },
                {
                    key: `secondary${index}Credit`,
                    title: "贷方",
                    render: cell(`${index}Credit`),
                    ...AMOUNT_COL,
                },
            ],
        })),
        { title: "合计-借方", dataIndex: "totalDebit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
        { title: "合计-贷方", dataIndex: "totalCredit", render: (v) => fmtAmount(v), ...AMOUNT_COL },
    ]
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
            panel={
                <LedgerQueryPanel
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
                    onCancel={query.cancel}
                    onReset={query.reset}
                    fields={FIELDS}
                    caption={
                        query.conditions.assistType && query.conditions.secondaryAssistType
                            ? `类别：${assistTypeLabel(query.conditions.assistType)} × ${assistTypeLabel(
                                  query.conditions.secondaryAssistType,
                              )}`
                            : "类别：未选择"
                    }
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
                    rowKey={(row) => row.rowKey}
                    footer={
                        <Table.Summary.Row>
                            <Table.Summary.Cell index={0} colSpan={2} className="font-medium">
                                合计
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={2} colSpan={cells.length * 2} />
                            <Table.Summary.Cell index={3} align="right" className="tabular-nums font-medium">
                                {fmtAmount(dto?.totalDebit, true)}
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={4} align="right" className="tabular-nums font-medium">
                                {fmtAmount(dto?.totalCredit, true)}
                            </Table.Summary.Cell>
                        </Table.Summary.Row>
                    }
                />
            )}
        </LedgerPageShell>
    )
}
