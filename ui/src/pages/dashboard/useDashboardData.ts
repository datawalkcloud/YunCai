import { useCallback, useEffect, useState } from "react"

import {
    ClosingApi,
    ReportsApi,
    VouchersApi,
    type ClosingStatusDto,
    type FinancialSummaryDto,
    type VoucherListItemDto,
} from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { useAuth } from "@/auth/AuthContext"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import {
    makePeriod,
    useDefaultAccountingPeriod,
    type AccountingPeriod,
} from "@/accountSets/useCurrentAccountSet"

/** 凭证状态：暂存 1 / 正常 3（与后端 VoucherStatus 对齐；保存即入账，没有审核态）。 */
export const V_STATUS = {
    draft: 1,
    posted: 3,
} as const

/** 首页所需的全部真实数据 + 加载态。 */
export interface DashboardData {
    loading: boolean
    /** 加载失败时的中文错误文案；成功为 null。 */
    error: string | null
    /** 重新拉取（错误重试）。 */
    reload: () => void
    /** 财务概要信息（含盈利/费用/应收应付/资金/税金各行）。 */
    summary: FinancialSummaryDto | null
    /** 账套内全部凭证（用于待办计数）。 */
    vouchers: VoucherListItemDto[]
    /** 结账/结转状态。 */
    closing: ClosingStatusDto | null
}

/**
 * 首页数据源：并发拉取财务概要、凭证列表与结账状态。
 * 任一失败都整屏报错并给出重试，绝不静默 catch。
 *
 * period 可选：首页的期间步进器需要回看历史月份，此时的期间由页面传入；
 * 不传则跟随账套「下一个待结账期间」。传入值需由调用方 useMemo 保持引用稳定。
 */
export function useDashboardData(period?: AccountingPeriod): DashboardData {
    const { token } = useAuth()
    const accountSetId = useCurrentAccountSetId() ?? ""
    // 表头显示的期间必须就是取数期间，故显式传给概要接口（后端省略时会回退到「最近有凭证期间」）。
    const fallbackPeriod = useDefaultAccountingPeriod()
    const activePeriod = period ?? fallbackPeriod

    const [loading, setLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [summary, setSummary] = useState<FinancialSummaryDto | null>(null)
    const [vouchers, setVouchers] = useState<VoucherListItemDto[]>([])
    const [closing, setClosing] = useState<ClosingStatusDto | null>(null)

    const load = useCallback(async () => {
        if (!accountSetId) {
            setError("未选择账套，请先在左上角创建一个账套。")
            setLoading(false)
            return
        }
        setLoading(true)
        setError(null)
        try {
            const config = apiConfig(token)
            const [sum, vs, cl] = await Promise.all([
                new ReportsApi(config).accountsetsAccountSetIdReportsFinancialSummaryGet({ accountSetId, year: activePeriod.year, month: activePeriod.month }),
                new VouchersApi(config).accountsetsAccountSetIdVouchersGet({ accountSetId }),
                new ClosingApi(config).accountsetsAccountSetIdClosingGet({ accountSetId }),
            ])
            setSummary(sum)
            setVouchers(vs ?? [])
            setClosing(cl)
        } catch (err) {
            setError(await apiErrorMessage(err, "首页数据加载失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token, activePeriod])

    useEffect(() => {
        void load()
    }, [load])

    return { loading, error, reload: () => void load(), summary, vouchers, closing }
}

/** 按指标名从财务概要中取行。 */
export function findRow(summary: FinancialSummaryDto | null, item: string) {
    return summary?.rows?.find((r) => r.item === item) ?? null
}

/** 凭证状态计数。 */
export function countByStatus(vouchers: VoucherListItemDto[]) {
    let draft = 0
    let posted = 0
    for (const v of vouchers) {
        if (v.status === V_STATUS.draft) draft++
        else if (v.status === V_STATUS.posted) posted++
    }
    return { draft, posted, unposted: draft, total: vouchers.length }
}

/* ------------------------------------------------------------------ */
/*  首页趋势：现金类科目（1001/1002/1012）按月汇总收付发生额              */
/* ------------------------------------------------------------------ */

/** 现金及现金等价物一级科目编码，与后端资金收入/支付口径一致。 */
const CASH_CODE_PREFIXES = ["1001", "1002", "1012"]

export interface CashTrendPoint {
    year: number
    month: number
    /** 展示用月份，如「8月」。 */
    label: string
    /** 现金类科目借方发生额（资金收入）。 */
    inflow: number
    /** 现金类科目贷方发生额（资金支付）。 */
    outflow: number
}

/**
 * 生成截至 end 的近 months 个月资金收付趋势。
 * 只统计已过账凭证；口径与后端「资金收入/资金支付」一致——先按现金科目净额结算，
 * 再按方向分别计入收入/支付。日期按后端 DateOnly 的 UTC 口径取年月，避免时区偏移。
 */
export function buildCashTrend(
    vouchers: VoucherListItemDto[],
    end: AccountingPeriod,
    months = 6,
): CashTrendPoint[] {
    const points: CashTrendPoint[] = []
    const byPeriod = new Map<number, CashTrendPoint>()
    for (let i = months - 1; i >= 0; i--) {
        const p = makePeriod(end.year, end.month - i)
        const point: CashTrendPoint = { year: p.year, month: p.month, label: `${p.month}月`, inflow: 0, outflow: 0 }
        points.push(point)
        byPeriod.set(p.period, point)
    }

    // 期间 → 现金科目 → 当月净额（借 - 贷）
    const nets = new Map<number, Map<string, number>>()
    for (const voucher of vouchers) {
        if (voucher.status !== V_STATUS.posted || !voucher.date) continue
        const date = new Date(voucher.date)
        const period = date.getUTCFullYear() * 100 + date.getUTCMonth() + 1
        if (!byPeriod.has(period)) continue
        let bucket = nets.get(period)
        if (!bucket) {
            bucket = new Map()
            nets.set(period, bucket)
        }
        for (const entry of voucher.entries ?? []) {
            if (!CASH_CODE_PREFIXES.some((prefix) => entry.accountCode?.startsWith(prefix))) continue
            const account = entry.accountId ?? entry.accountCode ?? ""
            bucket.set(account, (bucket.get(account) ?? 0) + (entry.debit ?? 0) - (entry.credit ?? 0))
        }
    }

    for (const [period, bucket] of nets) {
        const point = byPeriod.get(period)
        if (!point) continue
        for (const net of bucket.values()) {
            if (net > 0) point.inflow += net
            else point.outflow += -net
        }
    }

    return points
}

/** 最近的凭证：按日期、凭证号倒序截取。 */
export function latestVouchers(vouchers: VoucherListItemDto[], count: number): VoucherListItemDto[] {
    return [...vouchers]
        .sort((a, b) => {
            const dateDiff = (b.date ? new Date(b.date).getTime() : 0) - (a.date ? new Date(a.date).getTime() : 0)
            return dateDiff !== 0 ? dateDiff : (b.number ?? 0) - (a.number ?? 0)
        })
        .slice(0, count)
}

/** 凭证日期是否落在指定会计期间（按 DateOnly 的 UTC 口径取年月）。 */
export function isInPeriod(date: Date | undefined, period: AccountingPeriod): boolean {
    if (!date) return false
    const d = new Date(date)
    return d.getUTCFullYear() === period.year && d.getUTCMonth() + 1 === period.month
}
