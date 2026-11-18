import { useCallback, useEffect, useMemo, useRef, useState } from "react"

import {
    AccountsApi,
    AssistItemsApi,
    type AccountAssistTypeDto,
    type AssistCategoryStatDto,
    type AssistItemDto,
} from "@/apis-gen"
import { apiConfig } from "@/lib/api-client"

/**
 * 辅助核算取数的共享层：档案列表与科目维度配置在「档案页 / 科目页 / 凭证页」三处共用，
 * 用模块级缓存避免同一账套同类别重复请求；档案维护后调用 invalidate 使缓存失效。
 */
const itemsCache = new Map<string, AssistItemDto[]>()
const itemsPending = new Map<string, Promise<AssistItemDto[]>>()
const configsCache = new Map<string, AccountAssistTypeDto[]>()
const configsPending = new Map<string, Promise<AccountAssistTypeDto[]>>()

const itemsKey = (accountSetId: string, type: number) => `${accountSetId}:${type}`
const configsKey = (accountSetId: string, accountId: string) => `${accountSetId}:${accountId}`

/** 使某账套某类别（不传则全部类别）的档案缓存失效。 */
export function invalidateAssistItems(accountSetId: string, type?: number) {
    if (type == null) {
        for (const key of [...itemsCache.keys()]) if (key.startsWith(`${accountSetId}:`)) itemsCache.delete(key)
        for (const key of [...itemsPending.keys()]) if (key.startsWith(`${accountSetId}:`)) itemsPending.delete(key)
        return
    }
    itemsCache.delete(itemsKey(accountSetId, type))
    itemsPending.delete(itemsKey(accountSetId, type))
}

/** 拉取某类别的全部档案（含非末级与已停用，前端自行组树）。force 为真时忽略缓存。 */
export function fetchAssistItems(
    accountSetId: string,
    type: number,
    token?: string | null,
    force = false,
): Promise<AssistItemDto[]> {
    if (!accountSetId) return Promise.resolve([])
    const key = itemsKey(accountSetId, type)
    if (!force) {
        const cached = itemsCache.get(key)
        if (cached) return Promise.resolve(cached)
        const pending = itemsPending.get(key)
        if (pending) return pending
    }
    const request = new AssistItemsApi(apiConfig(token))
        .accountsetsAccountSetIdAssistItemsGet({ accountSetId, type })
        .then((list) => {
            itemsCache.set(key, list)
            itemsPending.delete(key)
            return list
        })
        .catch((err) => {
            itemsPending.delete(key)
            throw err
        })
    itemsPending.set(key, request)
    return request
}

/** 使某科目（不传则该账套全部科目）的辅助核算维度缓存失效。 */
export function invalidateAccountAssistTypes(accountSetId: string, accountId?: string) {
    if (accountId == null) {
        for (const key of [...configsCache.keys()]) if (key.startsWith(`${accountSetId}:`)) configsCache.delete(key)
        for (const key of [...configsPending.keys()]) if (key.startsWith(`${accountSetId}:`)) configsPending.delete(key)
        return
    }
    configsCache.delete(configsKey(accountSetId, accountId))
    configsPending.delete(configsKey(accountSetId, accountId))
}

/** 拉取某科目已设置的辅助核算维度（空数组即该科目未挂辅助核算）。 */
export function fetchAccountAssistTypes(
    accountSetId: string,
    accountId: string,
    token?: string | null,
    force = false,
): Promise<AccountAssistTypeDto[]> {
    if (!accountSetId || !accountId) return Promise.resolve([])
    const key = configsKey(accountSetId, accountId)
    if (!force) {
        const cached = configsCache.get(key)
        if (cached) return Promise.resolve(cached)
        const pending = configsPending.get(key)
        if (pending) return pending
    }
    const request = new AccountsApi(apiConfig(token))
        .accountsetsAccountSetIdAccountsIdAssistTypesGet({ accountSetId, id: accountId })
        .then((list) => {
            configsCache.set(key, list)
            configsPending.delete(key)
            return list
        })
        .catch((err) => {
            configsPending.delete(key)
            throw err
        })
    configsPending.set(key, request)
    return request
}

/** 记入缓存（科目弹窗保存后可直接写入，省一次回读）。 */
export function setAccountAssistTypesCache(
    accountSetId: string,
    accountId: string,
    list: AccountAssistTypeDto[],
) {
    configsCache.set(configsKey(accountSetId, accountId), list)
}

/**
 * 按科目 id 批量取辅助核算维度配置，返回 accountId → 维度数组。
 * 只在实际被使用（凭证分录选中、档案页标记）的科目上发请求，已缓存的直接命中。
 */
export function useAccountAssistConfigs(
    accountSetId: string,
    accountIds: readonly (string | undefined)[],
    token?: string | null,
) {
    const wanted = useMemo(() => accountIds.filter((id): id is string => Boolean(id)), [accountIds])
    const signature = wanted.join(",")
    const [configs, setConfigs] = useState<Record<string, AccountAssistTypeDto[]>>({})
    const [loading, setLoading] = useState(false)
    const loadedRef = useRef(new Set<string>())

    useEffect(() => {
        if (!accountSetId || signature === "") return
        const ids = signature.split(",")
        const missing = ids.filter((id) => !loadedRef.current.has(id))
        if (missing.length === 0) return
        setLoading(true)
        let cancelled = false
        void Promise.all(
            missing.map((id) =>
                fetchAccountAssistTypes(accountSetId, id, token)
                    .then((list) => ({ id, list }))
                    // 单个科目取数失败不影响其余行（凭证录入仍可用，只是不显示维度选择器）。
                    .catch(() => ({ id, list: [] as AccountAssistTypeDto[] })),
            ),
        ).then((results) => {
            if (cancelled) return
            setConfigs((prev) => {
                const next = { ...prev }
                for (const r of results) next[r.id] = r.list
                return next
            })
            for (const r of results) loadedRef.current.add(r.id)
            setLoading(false)
        })
        return () => {
            cancelled = true
        }
    }, [accountSetId, signature, token])

    /** 保存科目维度后刷新本地映射，避免再发请求。 */
    const applyConfig = useCallback((accountId: string, list: AccountAssistTypeDto[]) => {
        loadedRef.current.add(accountId)
        setConfigs((prev) => ({ ...prev, [accountId]: list }))
    }, [])

    return { configs, loading, applyConfig }
}

/**
 * 多类别档案列表，汇总成「类别 → 档案」。凭证录入要把已选档案回显成「编码+名称」，
 * 而分录里只存档案 id，集中取一次命中模块级缓存。
 */
export function useAssistItemsMany(accountSetId: string, types: readonly number[], token?: string | null) {
    const signature = useMemo(() => [...types].sort((a, b) => a - b).join(","), [types])
    const [byType, setByType] = useState<Record<number, AssistItemDto[]>>({})

    useEffect(() => {
        if (!accountSetId || signature === "") return
        let cancelled = false
        void Promise.all(
            signature.split(",").map((raw) => {
                const type = Number(raw)
                return fetchAssistItems(accountSetId, type, token)
                    .then((items) => ({ type, items }))
                    .catch(() => ({ type, items: [] as AssistItemDto[] }))
            }),
        ).then((results) => {
            if (cancelled) return
            setByType((prev) => {
                const next = { ...prev }
                for (const r of results) next[r.type] = r.items
                return next
            })
        })
        return () => {
            cancelled = true
        }
    }, [accountSetId, signature, token])

    return byType
}

/**
 * 某类别档案列表（走共享缓存）。reload(true) 用于档案维护后立即取最新数据。
 */
export function useAssistItems(accountSetId: string, type: number | null, token?: string | null) {
    const [items, setItems] = useState<AssistItemDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const reload = useCallback(
        async (force = true) => {
            if (!accountSetId || type == null) {
                setItems([])
                return
            }
            setLoading(true)
            setError(null)
            try {
                setItems(await fetchAssistItems(accountSetId, type, token, force))
            } catch (err) {
                setError(err instanceof Error ? err.message : "加载核算项目档案失败，请重试。")
                setItems([])
            } finally {
                setLoading(false)
            }
        },
        [accountSetId, type, token],
    )

    useEffect(() => {
        void reload(false)
    }, [reload])

    return { items, loading, error, reload }
}

/** 账套下七个类别的概览（启用状态 + 档案数），供档案页与科目设置弹窗共用。 */
export function useAssistCategories(accountSetId: string, token?: string | null) {
    const [categories, setCategories] = useState<AssistCategoryStatDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    const reload = useCallback(async () => {
        if (!accountSetId) {
            setCategories([])
            return
        }
        setLoading(true)
        setError(null)
        try {
            setCategories(
                await new AssistItemsApi(apiConfig(token)).accountsetsAccountSetIdAssistItemsCategoriesGet({
                    accountSetId,
                }),
            )
        } catch (err) {
            setError(err instanceof Error ? err.message : "加载辅助核算类别失败，请重试。")
            setCategories([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void reload()
    }, [reload])

    /** 该类别是否已在账套启用；类别数据未就绪时按未启用处理。 */
    const isEnabled = useCallback(
        (type?: number) => categories.find((c) => c.type === type)?.isEnabled === true,
        [categories],
    )

    return { categories, loading, error, reload, isEnabled }
}
