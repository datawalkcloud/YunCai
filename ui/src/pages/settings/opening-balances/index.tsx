import { useCallback, useEffect, useMemo, useState } from "react"
import { createColumnHelper, tableFeatures, useTable } from "@tanstack/react-table"
import { CircleAlert, CircleCheck, RefreshCw, Save, TriangleAlert, X } from "lucide-react"

import { useAuth } from "@/auth/AuthContext"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { AccountsApi, type OpeningBalanceRowDto, type OpeningBalancesDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { Alert, AlertAction, AlertTitle } from "@/components/ui/alert"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { NumberField } from "@/components/fields/number-field"
import { Spinner } from "@/components/ui/spinner"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"

/** 金额展示：0 显示为空，避免表格满屏 0.00。 */
function fmt(value?: number): string {
    if (!value) return ""
    return value.toLocaleString("zh-CN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

/** 科目层级缩进：每级 1rem，等价原来的 paddingLeft: (level - 1) * 16。 */
const LEVEL_INDENT = ["", "pl-4", "pl-8", "pl-12", "pl-16", "pl-20", "pl-24", "pl-28", "pl-32", "pl-36"]
function indentFor(level?: number): string {
    return LEVEL_INDENT[Math.max(0, Math.min(LEVEL_INDENT.length - 1, (level ?? 1) - 1))]
}

/** 各列对齐方式按列 id 取用，避免写行内样式。 */
const COLUMN_CLASS: Record<string, { head: string, cell: string }> = {
    code: { head: "text-left", cell: "whitespace-nowrap" },
    name: { head: "text-left", cell: "w-full" },
    direction: { head: "text-center", cell: "text-center" },
    openingBalance: { head: "text-right", cell: "text-right" },
    debit: { head: "text-right", cell: "text-right tabular-nums" },
    credit: { head: "text-right", cell: "text-right tabular-nums" },
}

const features = tableFeatures({})
const column = createColumnHelper<typeof features, OpeningBalanceRowDto>()

/**
 * 期初余额录入页：末级科目按余额方向录入期初余额（反方向余额用负数），
 * 非末级科目由下级自动汇总，底部实时试算平衡（借方合计 = 贷方合计）。
 */
export default function OpeningBalancesPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId()

    const [data, setData] = useState<OpeningBalancesDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [savedTip, setSavedTip] = useState(false)
    // 本地未保存的修改：accountId -> 期初余额。
    const [edits, setEdits] = useState<Record<string, number>>({})

    const load = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const dto = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsOpeningBalancesGet({ accountSetId })
            setData(dto)
            setEdits({})
        } catch (err) {
            setError(await apiErrorMessage(err, "加载期初余额失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [token, accountSetId])

    useEffect(() => {
        void load()
    }, [load])

    const dirty = Object.keys(edits).length > 0

    // 本地实时试算：以末级科目的（编辑值优先）期初余额按方向换算净借方后汇总。
    const totals = useMemo(() => {
        let debit = 0
        let credit = 0
        for (const row of data?.rows ?? []) {
            if (!row.isLeaf || !row.accountId) continue
            const value = edits[row.accountId] ?? row.openingBalance ?? 0
            const netDebit = row.direction === 1 ? value : -value
            if (netDebit > 0) debit += netDebit
            else credit += -netDebit
        }
        return { debit, credit, balanced: Math.abs(debit - credit) < 0.005 }
    }, [data, edits])

    async function save() {
        if (!accountSetId || !dirty) return
        setSaving(true)
        setError(null)
        setSavedTip(false)
        try {
            const dto = await new AccountsApi(apiConfig(token)).accountsetsAccountSetIdAccountsOpeningBalancesPut({
                accountSetId,
                saveOpeningBalancesRequest: {
                    items: Object.entries(edits).map(([accountId, amount]) => ({ accountId, amount })),
                },
            })
            setData(dto)
            setEdits({})
            setSavedTip(true)
        } catch (err) {
            setError(await apiErrorMessage(err, "保存期初余额失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    const columns = [
        column.display({
            id: "code",
            header: "科目编码",
            cell: ({ row }) => <span className={indentFor(row.original.level)}>{row.original.code}</span>,
        }),
        column.display({
            id: "name",
            header: "科目名称",
            cell: ({ row }) =>
                row.original.isLeaf
                    ? <div className="truncate">{row.original.name}</div>
                    : <div className="truncate font-medium">{row.original.name}</div>,
        }),
        column.display({
            id: "direction",
            header: "方向",
            cell: ({ row }) => (
                <Badge variant={row.original.direction === 1 ? "default" : "secondary"}>
                    {row.original.direction === 1 ? "借" : "贷"}
                </Badge>
            ),
        }),
        column.display({
            id: "openingBalance",
            header: "期初余额（按方向）",
            cell: ({ row }) => {
                const item = row.original
                if (!item.isLeaf || !item.accountId) {
                    return <span className="text-muted-foreground">{fmt(item.openingBalance)}</span>
                }
                const id = item.accountId
                return (
                    <NumberField
                        className="w-40"
                        precision={2}
                        value={edits[id] ?? item.openingBalance ?? 0}
                        onChange={(value) => {
                            setEdits((prev) => ({ ...prev, [id]: value ?? 0 }))
                            setSavedTip(false)
                        }}
                    />
                )
            },
        }),
        column.display({
            id: "debit",
            header: "借方",
            cell: ({ row }) => fmt(row.original.debit),
        }),
        column.display({
            id: "credit",
            header: "贷方",
            cell: ({ row }) => fmt(row.original.credit),
        }),
    ]

    const table = useTable({
        features,
        columns,
        data: data?.rows ?? [],
        getRowId: (row) => row.accountId ?? row.code ?? "",
    })
    const headerGroups = table.getHeaderGroups()
    const rows = table.getRowModel().rows

    return (
        <div className="flex w-full flex-col gap-6">
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="flex min-w-0 flex-col gap-1">
                    <h1 className="text-lg font-semibold">期初余额</h1>
                    <p className="text-muted-foreground">
                        录入账套启用期间的科目期初余额；末级科目按余额方向录入，反方向余额用负数。
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <Button variant="outline" onClick={() => void load()} disabled={loading}>
                        {loading ? <Spinner data-icon="inline-start" /> : <RefreshCw data-icon="inline-start" />}
                        刷新
                    </Button>
                    <Button disabled={!dirty || saving} onClick={() => void save()}>
                        {saving ? <Spinner data-icon="inline-start" /> : <Save data-icon="inline-start" />}
                        保存
                    </Button>
                </div>
            </div>

            {error && (
                <Alert variant="destructive">
                    <CircleAlert />
                    <AlertTitle>{error}</AlertTitle>
                </Alert>
            )}

            {savedTip && !dirty && (
                <Alert>
                    <CircleCheck />
                    <AlertTitle>期初余额已保存。</AlertTitle>
                    <AlertAction>
                        <Button variant="ghost" size="icon-xs" title="关闭" onClick={() => setSavedTip(false)}>
                            <X />
                        </Button>
                    </AlertAction>
                </Alert>
            )}

            <Alert>
                {totals.balanced ? <CircleCheck /> : <TriangleAlert />}
                <AlertTitle>
                    {totals.balanced
                        ? `试算平衡：借方合计 ${fmt(totals.debit) || "0.00"}，贷方合计 ${fmt(totals.credit) || "0.00"}。`
                        : `试算不平衡：借方合计 ${fmt(totals.debit) || "0.00"}，贷方合计 ${fmt(totals.credit) || "0.00"}，差额 ${fmt(Math.abs(totals.debit - totals.credit))}。`}
                </AlertTitle>
            </Alert>

            <Card>
                <CardContent>
                    {loading && !data ? (
                        <div className="flex justify-center py-10">
                            <Spinner />
                        </div>
                    ) : (
                        <Table>
                            <TableHeader>
                                {headerGroups.map((hg) => (
                                    <TableRow key={hg.id}>
                                        {hg.headers.map((h) => (
                                            <TableHead key={h.id} className={COLUMN_CLASS[h.column.id]?.head}>
                                                <table.FlexRender header={h} />
                                            </TableHead>
                                        ))}
                                    </TableRow>
                                ))}
                            </TableHeader>
                            <TableBody>
                                {rows.map((row) => (
                                    <TableRow key={row.id}>
                                        {row.getAllCells().map((cell) => (
                                            <TableCell key={cell.id} className={COLUMN_CLASS[cell.column.id]?.cell}>
                                                <table.FlexRender cell={cell} />
                                            </TableCell>
                                        ))}
                                    </TableRow>
                                ))}
                                {rows.length === 0 && (
                                    <TableRow>
                                        <TableCell colSpan={columns.length} className="py-8 text-center text-muted-foreground">
                                            暂无数据
                                        </TableCell>
                                    </TableRow>
                                )}
                            </TableBody>
                        </Table>
                    )}
                </CardContent>
            </Card>
        </div>
    )
}
