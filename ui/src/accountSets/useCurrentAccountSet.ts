import { useMemo } from "react"

import type { AccountSetDto } from "@/api/accountSets"
import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"

/** 路由第一段对应的账套完整信息（含启用期间与结账状态）；列表里找不到时返回 undefined。 */
export function useCurrentAccountSet(): AccountSetDto | undefined {
    const { accountSets } = useAccountSets()
    const id = useCurrentAccountSetId()
    if (!accountSets.length) return undefined
    if (id) return accountSets.find((s) => s.id === id)
    return accountSets.find((s) => s.isCurrent) ?? accountSets[0]
}

export interface AccountingPeriod {
    year: number
    month: number
    /** 「2026-10」 */
    label: string
    /** 「202610」，后端账簿与报表使用的期间标识 */
    period: number
    /** 当月首日 yyyy-MM-dd */
    startDate: string
    /** 当月末日 yyyy-MM-dd */
    endDate: string
}

const pad = (n: number) => String(n).padStart(2, "0")

/** 构造会计期间，month 允许越界（0 表示上一年 12 月，13 表示下一年 1 月）。 */
export function makePeriod(year: number, month: number): AccountingPeriod {
    const normalizedYear = year + Math.floor((month - 1) / 12)
    const normalizedMonth = ((month - 1 + 1200) % 12) + 1
    const lastDay = new Date(normalizedYear, normalizedMonth, 0).getDate()
    return {
        year: normalizedYear,
        month: normalizedMonth,
        label: `${normalizedYear}-${pad(normalizedMonth)}`,
        period: normalizedYear * 100 + normalizedMonth,
        startDate: `${normalizedYear}-${pad(normalizedMonth)}-01`,
        endDate: `${normalizedYear}-${pad(normalizedMonth)}-${pad(lastDay)}`,
    }
}

/** 相对当前期间偏移 delta 个月（期间步进器用）。 */
export function shiftPeriod(period: AccountingPeriod, delta: number): AccountingPeriod {
    return makePeriod(period.year, period.month + delta)
}

/**
 * 账套当前工作期间：优先取「下一个待结账期间」（即尚未结账的当期），
 * 其次取当前自然月，并保证不早于账套启用期间。页面不再硬编码日期。
 */
export function useDefaultAccountingPeriod(): AccountingPeriod {
    const accountSet = useCurrentAccountSet()

    return useMemo(() => {
        if (accountSet?.nextCloseYear && accountSet?.nextCloseMonth) {
            return makePeriod(accountSet.nextCloseYear, accountSet.nextCloseMonth)
        }

        const now = new Date()
        const current = makePeriod(now.getFullYear(), now.getMonth() + 1)
        const start = accountSet?.startYear && accountSet?.startMonth
            ? accountSet.startYear * 100 + accountSet.startMonth
            : 0

        return current.period < start ? makePeriod(Math.floor(start / 100), start % 100) : current
    }, [accountSet])
}

/**
 * 可选会计期间范围：账套启用期间 ~ 当前自然月。
 * 启用期间缺失时只收上界；月份尚未到时不可选。
 */
export function useAccountingPeriodBounds(): { min?: Date; max: Date } {
    const accountSet = useCurrentAccountSet()

    return useMemo(
        () => ({
            min:
                accountSet?.startYear && accountSet?.startMonth
                    ? new Date(accountSet.startYear, accountSet.startMonth - 1, 1)
                    : undefined,
            max: new Date(),
        }),
        [accountSet],
    )
}
