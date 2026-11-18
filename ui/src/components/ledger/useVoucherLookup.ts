import { useCallback, useEffect, useState } from "react"

import { type VoucherListItemDto, VouchersApi } from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"
import { toISODate } from "@/lib/format"

/** 把后端返回的日期归一为 yyyy-MM-dd，与账页行上的日期串一致。 */
export function dayKey(value?: Date | string | null): string {
    return toISODate(value)
}

/**
 * 账页行只带「日期 + 凭证字号」，没有凭证 id，
 * 因此按账套拉一次凭证列表建立索引，点击时再解析 id。
 */
export function useVoucherLookup(accountSetId: string, token?: string | null) {
    const [items, setItems] = useState<VoucherListItemDto[]>([])

    useEffect(() => {
        if (!accountSetId) {
            setItems([])
            return
        }
        let cancelled = false
        new VouchersApi(apiConfig(token))
            .accountsetsAccountSetIdVouchersGet({ accountSetId })
            .then((res) => {
                if (!cancelled) setItems(res ?? [])
            })
            .catch(() => {
                // 索引取不到时账页仍可浏览，只是点行不跳转。
                if (!cancelled) setItems([])
            })
        return () => {
            cancelled = true
        }
    }, [accountSetId, token])

    const resolveId = useCallback(
        (date?: string | null, voucherNo?: string | null): string | null => {
            if (!date || !voucherNo) return null
            const hit = items.find(
                (item) => item.voucherNo === voucherNo && dayKey(item.date) === date,
            )
            return hit?.id ?? null
        },
        [items],
    )

    return { resolveId, ready: items.length > 0 }
}
