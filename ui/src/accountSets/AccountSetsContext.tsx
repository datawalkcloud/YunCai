import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react"
import { useParams } from "react-router"

import { fetchAccountSets, type AccountSetDto } from "@/api/accountSets"
import { useAuth } from "@/auth/AuthContext"

interface AccountSetsContextValue {
    /** 全部账套。 */
    accountSets: AccountSetDto[]
    /** 是否正在加载账套列表。 */
    loading: boolean
    /** 加载失败时的错误信息。 */
    error: string | null
    /** 默认账套 id：优先取当前账套，其次取第一个；无账套时为空串。 */
    defaultId: string
}

const AccountSetsContext = createContext<AccountSetsContextValue | null>(null)

/**
 * 登录后加载一次账套列表，供路由重定向、侧边栏切换器与各页面共享，
 * 避免每个页面各自拉取。
 */
export function AccountSetsProvider({ children }: { children: ReactNode }) {
    const { token } = useAuth()
    const [accountSets, setAccountSets] = useState<AccountSetDto[]>([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        let cancelled = false
        setLoading(true)
        setError(null)
        fetchAccountSets(token ?? undefined)
            .then((sets) => {
                if (!cancelled) setAccountSets(sets)
            })
            .catch((err) => {
                if (!cancelled) setError(err instanceof Error ? err.message : "加载账套失败。")
            })
            .finally(() => {
                if (!cancelled) setLoading(false)
            })
        return () => {
            cancelled = true
        }
    }, [token])

    const value = useMemo<AccountSetsContextValue>(() => {
        const preferred = accountSets.find((s) => s.isCurrent) ?? accountSets[0]
        return {
            accountSets,
            loading,
            error,
            defaultId: preferred?.id ?? "",
        }
    }, [accountSets, loading, error])

    return <AccountSetsContext value={value}>{children}</AccountSetsContext>
}

export function useAccountSets(): AccountSetsContextValue {
    const ctx = useContext(AccountSetsContext)
    if (!ctx) {
        throw new Error("useAccountSets 必须在 AccountSetsProvider 内使用")
    }
    return ctx
}

/** 从路由第一段读取当前账套 id（可能为 undefined，例如尚未进入具体账套时）。 */
export function useCurrentAccountSetId(): string | undefined {
    return useParams().accountSetId
}
