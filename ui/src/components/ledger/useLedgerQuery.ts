import { useCallback, useEffect, useRef, useState } from "react"

import type { LedgerMetaDto } from "@/apis-gen"
import { apiErrorMessage } from "@/lib/api-client"
import type { LedgerConditions } from "./types"
import { currentMonthPeriod } from "./types"

/** 未指定期间的账页一律默认查当月；下钻带过来的区间原样保留。 */
function withDefaultPeriod(initial?: LedgerConditions): LedgerConditions {
    const month = currentMonthPeriod()
    return { ...initial, startDate: initial?.startDate || month.startDate, endDate: initial?.endDate || month.endDate }
}

/** 所有账页返回体都带 meta，据此约束泛型。 */
export interface LedgerResponse {
    meta: LedgerMetaDto
}

export interface UseLedgerQueryArgs<T extends LedgerResponse> {
    accountSetId: string
    token?: string | null
    /** 进入页面时的条件（含总账下钻带过来的科目区间）。 */
    initial?: LedgerConditions
    /** 账页把条件翻译成具体接口调用。 */
    fetch: (args: {
        accountSetId: string
        conditions: LedgerConditions
        token?: string | null
    }) => Promise<T>
    /** 后端未给出 message 时的兜底文案。 */
    fallbackError?: string
    /**
     * 已提交条件是否足以发起请求；为假时跳过请求并清空数据。
     * 用于「不选就报错」的账页（核算项目账页未选辅助核算类别、多栏账未选科目）。
     */
    canQuery?: (conditions: LedgerConditions) => boolean
}

export interface LedgerQueryState<T> {
    /** 面板编辑中的条件。 */
    conditions: LedgerConditions
    /** 已提交、即当前表体数据对应的条件（导出沿用同一口径）。 */
    committed: LedgerConditions
    /** 局部更新条件；immediate 为真时同时提交并立即重查。 */
    patch: (next: Partial<LedgerConditions>, immediate?: boolean) => void
    /** 把面板条件提交给后端。 */
    query: () => void
    /** 丢弃未提交的编辑，回到已提交条件（不重查）。 */
    cancel: () => void
    /** 回到进入页面时的条件并重查。 */
    reset: () => void
    /** 用当前已提交的条件重查。 */
    reload: () => void
    data: T | null
    meta: LedgerMetaDto | null
    loading: boolean
    error: string | null
}

/**
 * 账页共用的查询状态与取数样板：维护「草稿条件 / 已提交条件」两段状态，
 * 进入页面时未指定期间一律默认当月，并统一处理 loading / error。
 */
export function useLedgerQuery<T extends LedgerResponse>({
    accountSetId,
    token,
    initial,
    fetch,
    fallbackError = "加载账页数据失败，请重试。",
    canQuery,
}: UseLedgerQueryArgs<T>): LedgerQueryState<T> {
    const [draft, setDraft] = useState<LedgerConditions>(() => withDefaultPeriod(initial))
    const [committed, setCommitted] = useState<LedgerConditions>(() => withDefaultPeriod(initial))
    const [data, setData] = useState<T | null>(null)
    const [meta, setMeta] = useState<LedgerMetaDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    // fetch 由账页内联传入，每次渲染都是新函数；放进 ref 避免它触发重查。
    const fetchRef = useRef(fetch)
    fetchRef.current = fetch
    // canQuery 同样是内联闭包，用 ref 持有最新值，不参与依赖数组。
    const canQueryRef = useRef(canQuery)
    canQueryRef.current = canQuery
    // 重置时需要一个稳定的初始条件快照，同时避免账页每次渲染传新对象。
    const seedRef = useRef(withDefaultPeriod(initial))
    const draftRef = useRef(draft)
    draftRef.current = draft

    const run = useCallback(() => setCommitted({ ...draftRef.current }), [])

    const cancel = useCallback(() => {
        draftRef.current = { ...committed }
        setDraft({ ...committed })
    }, [committed])

    const patch = useCallback(
        (next: Partial<LedgerConditions>, immediate = false) => {
            const merged = { ...draftRef.current, ...next }
            draftRef.current = merged
            setDraft(merged)
            if (immediate) setCommitted(merged)
        },
        [],
    )

    const reset = useCallback(() => {
        const seed = { ...seedRef.current }
        draftRef.current = seed
        setDraft(seed)
        setCommitted(seed)
    }, [])

    useEffect(() => {
        if (!accountSetId) {
            setData(null)
            setMeta(null)
            return
        }
        let cancelled = false
        if (canQueryRef.current && !canQueryRef.current(committed)) {
            setData(null)
            setMeta(null)
            setLoading(false)
            setError(null)
            return
        }
        setLoading(true)
        setError(null)
        fetchRef
            .current({ accountSetId, conditions: committed, token })
            .then((res) => {
                if (cancelled) return
                setData(res)
                setMeta(res.meta)
            })
            .catch(async (err) => {
                if (cancelled) return
                setData(null)
                setMeta(null)
                setError(await apiErrorMessage(err, fallbackError))
            })
            .finally(() => {
                if (!cancelled) setLoading(false)
            })
        return () => {
            cancelled = true
        }
    }, [accountSetId, token, committed, fallbackError])

    return { conditions: draft, committed, patch, query: run, cancel, reset, reload: run, data, meta, loading, error }
}
