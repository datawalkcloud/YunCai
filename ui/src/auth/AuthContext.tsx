import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { fetchMe, login as loginApi, type UserInfo } from '../api/auth'
import { setUnauthorizedHandler } from '../lib/api-client'

/** loading：正在校验本地令牌；authenticated：已登录；unauthenticated：未登录。 */
type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'

interface AuthContextValue {
  token: string | null
  user: UserInfo | null
  status: AuthStatus
  isAuthenticated: boolean
  login: (username: string, password: string) => Promise<void>
  logout: () => void
}

interface StoredAuth {
  token: string | null
  user: UserInfo | null
}

const STORAGE_KEY = 'kuaiji.auth'

const AuthContext = createContext<AuthContextValue | null>(null)

function readStoredAuth(): StoredAuth {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) {
      return JSON.parse(raw) as StoredAuth
    }
  } catch {
    // 忽略损坏的本地存储
  }
  return { token: null, user: null }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(null)
  const [user, setUser] = useState<UserInfo | null>(null)
  const [status, setStatus] = useState<AuthStatus>('loading')

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY)
    setToken(null)
    setUser(null)
    setStatus('unauthenticated')
  }, [])

  // 启动时校验本地令牌是否仍然有效，避免使用过期令牌进入应用。
  useEffect(() => {
    const stored = readStoredAuth()
    if (!stored.token) {
      setStatus('unauthenticated')
      return
    }

    let cancelled = false
    setToken(stored.token)
    setUser(stored.user)

    fetchMe(stored.token)
      .then((me) => {
        if (cancelled) return
        setUser(me)
        setStatus('authenticated')
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ token: stored.token, user: me } satisfies StoredAuth))
      })
      .catch(() => {
        if (cancelled) return
        localStorage.removeItem(STORAGE_KEY)
        setToken(null)
        setUser(null)
        setStatus('unauthenticated')
      })

    return () => {
      cancelled = true
    }
  }, [])

  // 401 的统一出口：apiErrorMessage 识别到令牌失效会调用这里注册的回调。
  // logout() 清掉本地登录态并把状态置为未登录，保护区的 RequireAuth 随即带来源路径跳 /login，
  // 各页面照常显示文案、不必自己处理跳转。
  // 本项目 JWT 不签发刷新令牌（后端只有 Auth/login，响应里也没有 refreshToken），
  // 过期只能重新登录，所以这里刻意不做任何「静默续期」逻辑。
  useEffect(() => {
    if (status !== 'authenticated') return
    setUnauthorizedHandler(() => logout())
    return () => setUnauthorizedHandler(null)
  }, [status, logout])

  const login = useCallback(async (username: string, password: string) => {
    const res = await loginApi(username, password)
    const next: StoredAuth = { token: res.token, user: res.user }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    setToken(res.token)
    setUser(res.user)
    setStatus('authenticated')
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      token,
      user,
      status,
      isAuthenticated: status === 'authenticated',
      login,
      logout,
    }),
    [token, user, status, login, logout],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth 必须在 AuthProvider 内使用')
  }
  return ctx
}
