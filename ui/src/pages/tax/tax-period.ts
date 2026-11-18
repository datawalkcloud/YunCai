/**
 * 税款所属期：报税各页共用的当前所属期，与记账期间相互独立。
 *
 * 默认按税费种认定里主税种的申报期限推算（顶栏选择器可改），改过一次就一直沿用，
 * 页面之间跳转、刷新前都在同一份内存值上取数，避免每页各自推算出不同月份。
 */
import { useSyncExternalStore } from "react";

import { makePeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { defaultFilingPeriod } from "@/lib/tax-format";
import { getCachedTaxKinds, type TaxKindRow } from "@/lib/tax-portal";

let chosen: AccountingPeriod | null = null;
let cachedKinds: readonly TaxKindRow[] | null = null;
let cachedDefault: AccountingPeriod | null = null;
const listeners = new Set<() => void>();

function currentPeriod(): AccountingPeriod {
    if (chosen) return chosen;
    const kinds = getCachedTaxKinds();
    // 认定是异步从税局取回的，引用变了才重算默认所属期；引用相同则复用，保证快照稳定。
    if (kinds !== cachedKinds || !cachedDefault) {
        const { year, month } = defaultFilingPeriod(kinds);
        cachedDefault = makePeriod(year, month);
        cachedKinds = kinds;
    }
    return cachedDefault;
}

/** 顶栏选择器改所属期，报税各页同时收到新值。 */
export function setTaxPeriod(period: AccountingPeriod): void {
    chosen = period;
    for (const notify of listeners) notify();
}

export function useTaxPeriod(): AccountingPeriod {
    return useSyncExternalStore(subscribe, currentPeriod);
}

function subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
}
