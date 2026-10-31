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
import { useDefaultAccountingPeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet"

/** 凭证状态：草稿 1 / 已审核 2 / 已过账 3（与后端 VoucherStatus 对齐）。 */
export const V_STATUS = {
    draft: 1,
    reviewed: 2,
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
    let reviewed = 0
    let posted = 0
    for (const v of vouchers) {
        if (v.status === V_STATUS.draft) draft++
        else if (v.status === V_STATUS.reviewed) reviewed++
        else if (v.status === V_STATUS.posted) posted++
    }
    return { draft, reviewed, posted, unposted: draft + reviewed, total: vouchers.length }
}
