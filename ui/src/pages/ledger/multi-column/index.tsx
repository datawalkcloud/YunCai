import { useMemo, useState } from "react"
import type { TableColumnsType } from "antd"
import { useSearchParams } from "react-router"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import {
    type AccountDto,
    LedgersApi,
    type MultiColumnHeaderDto,
} from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { kindRowClass } from "@/components/ledger/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerSection, LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { LedgerQueryPanel, type LedgerField } from "@/components/ledger/LedgerQueryPanel"
import { LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { ROW_KIND, asDate, type LedgerConditions } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"
import { useAccounts } from "@/components/ledger/useLedgerOptions"
import { useVoucherLookup } from "@/components/ledger/useVoucherLookup"
import { VoucherDetailSheet } from "@/components/ledger/VoucherDetailSheet"

/** 无发生额数据时的列骨架占位，避免每次渲染都重建列定义。 */
const EMPTY_COLUMNS: MultiColumnHeaderDto[] = []

/** 多栏账行：把与 columns 同序的发生额数组摊平成固定键（d{i} / c{i}）。 */
type MultiRow = {
    rowKey: string
    kind?: number
    date?: string | null
    voucherNo?: string | null
    summary: string
    direction?: string | null
    balance?: number | null
    cells: Record<string, number>
}

const FIELDS: readonly LedgerField[] = ["account", "date", "summary"]

/** 借方 / 贷方栏（d{i} / c{i}）与余额列右对齐等宽数字。 */
function buildColumns(columns: MultiColumnHeaderDto[]): TableColumnsType<MultiRow> {
    const cell = (key: string) => (_: unknown, row: MultiRow) => fmtAmount(row.cells[key])
    return [
        { title: "日期", dataIndex: "date" },
        { title: "凭证字号", dataIndex: "voucherNo" },
        { title: "摘要", dataIndex: "summary" },
        ...columns.map((col, index) => ({
            key: `col${index}`,
            title: `${col.code} ${col.name}`,
            children: [
                { key: `d${index}`, title: "借方", render: cell(`d${index}`), ...AMOUNT_COL },
                { key: `c${index}`, title: "贷方", render: cell(`c${index}`), ...AMOUNT_COL },
            ],
        })),
        { title: "方向", dataIndex: "direction" },
        { title: "余额", dataIndex: "balance", render: (v) => fmtAmount(v), ...AMOUNT_COL },
    ]
}

function MultiColumnLedger({
    initial,
    accounts,
}: {
    initial: LedgerConditions
    accounts: AccountDto[]
}) {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""

    const query = useLedgerQuery({
        accountSetId,
        token,
        initial,
        // 后端 accountId 是必填 Guid，未选科目就请求会 400。
        canQuery: (c) => Boolean(c.accountId),
        fallbackError: "加载多栏账失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) =>
            new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersMultiColumnGet({
                accountSetId: id,
                accountId: conditions.accountId,
                startDate: asDate(conditions.startDate),
                endDate: asDate(conditions.endDate),
                summaryKeyword: conditions.summaryKeyword,
            }),
    })

    const lookup = useVoucherLookup(accountSetId, token)
    const [voucherId, setVoucherId] = useState<string | null>(null)

    const dto = query.data
    // 空数组取模块级常量，保证列定义的 memo 依赖在数据未就绪时也是稳定引用。
    const columns = dto?.columns ?? EMPTY_COLUMNS
    const tableColumns = useMemo(() => buildColumns(columns), [columns])

    const rows: MultiRow[] = (dto?.rows ?? []).map((row, index) => {
        const cells: Record<string, number> = {}
        columns.forEach((_, i) => {
            // 越界当 0，与后端导出保持一致。
            cells[`d${i}`] = row.debits?.[i] ?? 0
            cells[`c${i}`] = row.credits?.[i] ?? 0
        })
        return {
            rowKey: `${index}`,
            kind: row.kind,
            date: row.date,
            voucherNo: row.voucherNo,
            summary: row.summary,
            direction: row.direction,
            balance: row.balance,
            cells,
        }
    })

    return (
        <LedgerPageShell
            page="multi-column"
            title="多栏账"
            hint="选定科目后其下级科目逐栏列示发生额，并结出逐笔滚动余额。"
            panel={
                <LedgerQueryPanel
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
                    onReset={query.reset}
                    fields={FIELDS}
                    accounts={accounts}
                    loading={query.loading}
                    extra={
                        <LedgerExportButton
                            kind="multi-column"
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
            ) : !dto && !query.loading ? (
                <span className="py-4 text-muted-foreground">请先选择科目。</span>
            ) : dto ? (
                <LedgerSection
                    heading={
                        <span>
                            {dto.code} {dto.name}
                            {dto.path && dto.path !== dto.name && (
                                <span className="ml-2 text-muted-foreground">（{dto.path}）</span>
                            )}
                        </span>
                    }
                    extra={
                        <span>
                            借方 {fmtAmount(dto.totalDebit, true)} ／ 贷方{" "}
                            {fmtAmount(dto.totalCredit, true)}
                        </span>
                    }
                >
                    <LedgerTable
                        columns={tableColumns}
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
            ) : null}

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

export default function MultiColumnPage() {
    const [params] = useSearchParams()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { token } = useAuth()
    const accounts = useAccounts(accountSetId, token)

    // 从 URL 预置科目：按编码在科目表里解析出 id 作为初始条件。
    const code = params.get("account")
    const accountId = code ? accounts.find((a) => a.code === code)?.id : undefined
    const initial: LedgerConditions = {
        ...(accountId ? { accountId } : {}),
        ...(params.get("startDate") ? { startDate: params.get("startDate") ?? undefined } : {}),
        ...(params.get("endDate") ? { endDate: params.get("endDate") ?? undefined } : {}),
    }

    return (
        <MultiColumnLedger
            key={`${accountSetId}|${accountId ?? ""}`}
            initial={initial}
            accounts={accounts}
        />
    )
}
