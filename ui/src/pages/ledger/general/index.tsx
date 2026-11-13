import { DownOutlined, RightOutlined } from "@ant-design/icons"
import { useEffect, useMemo, useRef, useState } from "react"
import { Button } from "antd"
import type { TableColumnsType } from "antd"
import { useNavigate, useParams } from "react-router"

import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { type GeneralLedgerAccountDto, LedgersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"
import { LedgerTable } from "@/components/ledger/LedgerTable"
import { LedgerPageShell } from "@/components/ledger/LedgerPageShell"
import { GeneralLedgerFilter } from "@/components/ledger/GeneralLedgerFilter"
import { LedgerPrintButton, LedgerExportButton } from "@/components/ledger/LedgerExportButton"
import { useAccounts } from "@/components/ledger/useLedgerOptions"
import { asDate, type LedgerConditions } from "@/components/ledger/types"
import { useLedgerQuery } from "@/components/ledger/useLedgerQuery"

/** 总账表格的一行：科目分组头，或组内的期初 / 本期合计 / 本年累计。 */
type Line =
    | { rowKey: string; kind: "group"; code: string; name: string }
    | {
          rowKey: string
          kind: "data"
          code: string
          name: string
          period: string
          label: string
          debit?: number
          credit?: number
          direction: string
          balance?: number
      }

/** 组内三行合计的金额口径。 */
interface Totals {
    debit: number
    credit: number
}

/** 2026-10 → 2026年10月，与柠檬云「期间」栏一致。 */
function formatPeriod(periodText?: string): string {
    const [year, month] = (periodText ?? "").split("-")
    return year && month ? `${year}年${Number(month)}月` : ""
}

/** 组装一个科目的分组头与三行汇总。 */
function buildLines(
    account: GeneralLedgerAccountDto,
    totals: Totals | undefined,
    collapsed: boolean,
    hideTotalsNoActivity: boolean,
): Line[] {
    const code = account.code ?? ""
    const name = account.name ?? ""
    const group: Line = { rowKey: `group-${code}`, kind: "group", code, name }
    const rows = account.rows ?? []
    const first = rows[0]
    const last = rows[rows.length - 1]
    if (!first || !last || collapsed) return [group]

    const openingDebit = first.openingDebit ?? 0
    const openingCredit = first.openingCredit ?? 0
    const endingDebit = last.endingDebit ?? 0
    const endingCredit = last.endingCredit ?? 0
    const period = formatPeriod(last.periodText)
    const ending = { direction: last.direction ?? "", balance: endingDebit || endingCredit }
    const data = (key: string, label: string, debit?: number, credit?: number, direction = "", balance?: number): Line => ({
        rowKey: `${code}-${key}`,
        kind: "data",
        code,
        name,
        period,
        label,
        debit,
        credit,
        direction,
        balance,
    })

    const opening = data(
        "opening",
        "期初余额",
        openingDebit,
        openingCredit,
        openingDebit > 0 ? "借" : openingCredit > 0 ? "贷" : "平",
        openingDebit || openingCredit,
    )

    // 无发生额时不输出「本期合计 / 本年累计」两行，只保留期初余额。
    const noActivity = (account.totalDebit ?? 0) === 0 && (account.totalCredit ?? 0) === 0
    if (hideTotalsNoActivity && noActivity) return [group, opening]

    return [
        group,
        opening,
        data("period", "本期合计", account.totalDebit, account.totalCredit, ending.direction, ending.balance),
        data("year", "本年累计", totals?.debit, totals?.credit, ending.direction, ending.balance),
    ]
}

/** 总账取数：主查询与「本年累计」的年窗口查询共用一套参数。 */
function fetchGeneral(token: string | null | undefined, id: string, conditions: LedgerConditions) {
    return new LedgersApi(apiConfig(token)).accountsetsAccountSetIdLedgersGeneralGet({
        accountSetId: id,
        startDate: asDate(conditions.startDate),
        endDate: asDate(conditions.endDate),
        accountFrom: conditions.accountFrom,
        accountTo: conditions.accountTo,
        minLevel: conditions.minLevel,
        maxLevel: conditions.maxLevel,
        showAssist: conditions.showAssist,
        // 总账按科目层级汇总，默认展示全部级次（含上级科目）。
        showOnlyLeaf: conditions.showOnlyLeaf ?? false,
        hideZeroBalance: conditions.hideZeroBalance,
        hideNoActivity: conditions.hideNoActivity,
        hideTotalsNoActivity: conditions.hideTotalsNoActivity,
        includeIdleAccounts: conditions.includeIdleAccounts,
    })
}

export default function GeneralLedgerPage() {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { accountSetId: routeId } = useParams<{ accountSetId: string }>()
    const navigate = useNavigate()
    const [collapsed, setCollapsed] = useState<Set<string>>(() => new Set())

    const query = useLedgerQuery({
        accountSetId,
        token,
        fallbackError: "加载总账失败，请重试。",
        fetch: ({ accountSetId: id, conditions }) => fetchGeneral(token, id, conditions),
    })

    const allAccounts = useAccounts(accountSetId, token)

    const accounts = useMemo(() => query.data?.accounts ?? [], [query.data])
    const { committed } = query
    const endYear = committed.endDate?.slice(0, 4)
    const yearStart = endYear ? `${endYear}-01-01` : undefined
    // 主查询区间已覆盖本年 1 月起时，本年累计可直接由月行汇总，无需二次取数。
    const covered = Boolean(committed.endDate && (!committed.startDate || (yearStart && committed.startDate <= yearStart)))

    const coveredTotals = useMemo(() => {
        if (!covered || !query.data || !endYear) return null
        const map = new Map<string, Totals>()
        for (const account of query.data.accounts ?? []) {
            let debit = 0
            let credit = 0
            for (const row of account.rows ?? []) {
                if ((row.periodText ?? "").slice(0, 4) !== endYear) continue
                debit += row.debit ?? 0
                credit += row.credit ?? 0
            }
            map.set(account.code ?? "", { debit, credit })
        }
        return map
    }, [covered, query.data, endYear])

    // 只查了某个月时，本年累计还需本年 1 月至期末的数据，补一次年窗口查询。
    const [fetchedTotals, setFetchedTotals] = useState<Map<string, Totals> | null>(null)
    const fetchedKey = useRef("")
    useEffect(() => {
        if (!accountSetId || !yearStart || !committed.endDate || covered) {
            fetchedKey.current = ""
            setFetchedTotals(null)
            return
        }
        const key = JSON.stringify([
            committed.accountFrom,
            committed.accountTo,
            committed.minLevel,
            committed.maxLevel,
            committed.showAssist,
            committed.showOnlyLeaf,
            committed.hideZeroBalance,
            committed.hideNoActivity,
            committed.hideTotalsNoActivity,
            committed.includeIdleAccounts,
            yearStart,
            committed.endDate,
        ])
        if (fetchedKey.current === key) return
        fetchedKey.current = key
        let cancelled = false
        fetchGeneral(token, accountSetId, { ...committed, startDate: yearStart, endDate: committed.endDate })
            .then((res) => {
                if (cancelled) return
                const map = new Map<string, Totals>()
                for (const account of res.accounts ?? []) {
                    map.set(account.code ?? "", {
                        debit: account.totalDebit ?? 0,
                        credit: account.totalCredit ?? 0,
                    })
                }
                setFetchedTotals(map)
            })
            .catch(() => {
                if (!cancelled) setFetchedTotals(null)
            })
        return () => {
            cancelled = true
        }
    }, [accountSetId, token, committed, covered, yearStart])

    const yearTotals = covered ? coveredTotals : fetchedTotals

    const lines = useMemo(
        () =>
            accounts.flatMap((account) =>
                buildLines(
                    account,
                    yearTotals?.get(account.code ?? ""),
                    collapsed.has(account.code ?? ""),
                    committed.hideTotalsNoActivity ?? false,
                ),
            ),
        [accounts, yearTotals, collapsed, committed.hideTotalsNoActivity],
    )

    // 总账点科目下钻到该科目的明细账，沿用同一区间。
    function goSubsidiary(code: string) {
        const params = new URLSearchParams()
        params.set("account", code)
        if (query.meta?.startDate) params.set("startDate", query.meta.startDate)
        if (query.meta?.endDate) params.set("endDate", query.meta.endDate)
        navigate(`/${routeId}/ledger/subsidiary?${params.toString()}`)
    }

    function toggleGroup(code: string) {
        setCollapsed((prev) => {
            const next = new Set(prev)
            if (next.has(code)) next.delete(code)
            else next.add(code)
            return next
        })
    }

    const selectedCode =
        committed.accountFrom && committed.accountFrom === committed.accountTo ? committed.accountFrom : undefined
    const selectedName = selectedCode ? accounts.find((account) => account.code === selectedCode)?.name : undefined

    const link = (line: Extract<Line, { kind: "data" }>, text: string) => (
        <Button
            type="link"
            size="small"
            className="h-auto p-0 text-[13px]"
            onClick={(e) => {
                e.stopPropagation()
                goSubsidiary(line.code)
            }}
        >
            {text}
        </Button>
    )

    /** 非首列在分组头行让位给跨列标题。 */
    const groupCell = (line: Line) => (line.kind === "group" ? { colSpan: 0 } : {})

    const columns: TableColumnsType<Line> = [
        {
            title: "科目编码",
            dataIndex: "code",
            width: 116,
            onCell: (line) => (line.kind === "group" ? { colSpan: 8 } : {}),
            render: (_, line) =>
                line.kind === "group" ? (
                    <span className="inline-flex items-center gap-1.5 font-medium">
                        {collapsed.has(line.code) ? (
                            <RightOutlined className="text-[10px] text-(--muted-foreground)" />
                        ) : (
                            <DownOutlined className="text-[10px] text-(--muted-foreground)" />
                        )}
                        {line.name}
                    </span>
                ) : (
                    link(line, line.code)
                ),
        },
        {
            title: "科目名称",
            dataIndex: "name",
            width: 160,
            onCell: groupCell,
            render: (_, line) => (line.kind === "group" ? null : link(line, line.name)),
        },
        { title: "期间", dataIndex: "period", width: 108, onCell: groupCell },
        { title: "摘要", dataIndex: "label", onCell: groupCell },
        {
            title: "借方金额",
            dataIndex: "debit",
            render: (v) => fmtAmount(v),
            onCell: groupCell,
            ...AMOUNT_COL,
        },
        {
            title: "贷方金额",
            dataIndex: "credit",
            render: (v) => fmtAmount(v),
            onCell: groupCell,
            ...AMOUNT_COL,
        },
        { title: "方向", dataIndex: "direction", width: 64, align: "center", onCell: groupCell },
        {
            title: "余额",
            dataIndex: "balance",
            render: (v) => fmtAmount(v),
            onCell: groupCell,
            ...AMOUNT_COL,
        },
    ]

    return (
        <LedgerPageShell
            panel={
                <GeneralLedgerFilter
                    value={query.conditions}
                    onChange={query.patch}
                    onQuery={query.query}
                    onCancel={query.cancel}
                    onReset={query.reset}
                    accounts={allAccounts}
                    loading={query.loading}
                    extra={
                        <>
                            <LedgerExportButton
                                kind="general"
                                accountSetId={accountSetId}
                                token={token}
                                conditions={query.committed}
                            />
                            <LedgerPrintButton
                                kind="general"
                                accountSetId={accountSetId}
                                token={token}
                                conditions={query.committed}
                            />
                        </>
                    }
                    disabled={!accountSetId}
                    caption={`科目：${selectedCode ? `${selectedCode} ${selectedName ?? ""}`.trim() : "所有"}`}
                />
            }
            meta={query.meta}
            error={query.error}
        >
            <LedgerTable<Line>
                columns={columns}
                data={lines}
                loading={query.loading}
                emptyText={accountSetId ? "该期间暂无总账数据，请确认凭证为正常状态。" : "请先选择账套。"}
                rowKey={(line) => line.rowKey}
                rowClassName={(line) => (line.kind === "group" ? "cursor-pointer bg-muted/60 font-medium" : "")}
                onRow={(line) => (line.kind === "group" ? { onClick: () => toggleGroup(line.code) } : {})}
                pagination={{
                    pageSize: 20,
                    showSizeChanger: true,
                    pageSizeOptions: [20, 50, 100],
                    showTotal: (total, range) => `显示 ${range[0]} 到 ${range[1]}，共 ${total} 条记录`,
                }}
            />
        </LedgerPageShell>
    )
}
