import { useCallback, useEffect, useRef, useState } from "react"

import type { LedgerMetaDto } from "@/apis-gen"
import { apiErrorMessage } from "@/lib/api-client"
import type { LedgerConditions } from "./types"

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
 * 首次进入时用后端解析出的区间回填日期，并统一处理 loading / error。
 */
export function useLedgerQuery<T extends LedgerResponse>({
    accountSetId,
    token,
    initial,
    fetch,
    fallbackError = "加载账页数据失败，请重试。",
    canQuery,
}: UseLedgerQueryArgs<T>): LedgerQueryState<T> {
    const [draft, setDraft] = useState<LedgerConditions>(() => ({ ...initial }))
    const [committed, setCommitted] = useState<LedgerConditions>(() => ({ ...initial }))
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
    const seedRef = useRef(initial)
    const draftRef = useRef(draft)
    draftRef.current = draft
    const backfilled = useRef(false)

    const run = useCallback(() => setCommitted({ ...draftRef.current }), [])

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
        backfilled.current = false
        setDraft(seed)
        setCommitted(seed)
    }, [])

    // 切换账套后允许重新回填一次日期。
    useEffect(() => {
        backfilled.current = false
    }, [accountSetId])

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
                // 未指定区间时采用后端解析出的区间，避免面板一直显示空。
                if (!backfilled.current && res.meta?.startDate && res.meta?.endDate) {
                    backfilled.current = true
                    if (!draftRef.current.startDate) {
                        const merged = { ...draftRef.current, startDate: res.meta.startDate, endDate: res.meta.endDate }
                        draftRef.current = merged
                        setDraft(merged)
                        setCommitted(merged)
                    }
                }
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

    return { conditions: draft, committed, patch, query: run, reset, reload: run, data, meta, loading, error }
}
