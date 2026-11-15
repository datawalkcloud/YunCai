import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  changePassword as changePasswordApi,
  fetchMe,
  login as loginApi,
  loginByPhone as loginByPhoneApi,
  register as registerApi,
  setPassword as setPasswordApi,
  type LoginResponse,
  type UserInfo,
} from '../api/auth'
import { setUnauthorizedHandler } from '../lib/api-client'

/** loading：正在校验本地令牌；authenticated：已登录；unauthenticated：未登录。 */
type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'

interface AuthContextValue {
  token: string | null
  user: UserInfo | null
  status: AuthStatus
  isAuthenticated: boolean
  /** 已登录但还没设置密码（手机号首次验证码登录）：需要先补设密码才能进后台。 */
  needsPasswordSetup: boolean
  login: (username: string, password: string) => Promise<UserInfo>
  loginByPhone: (phone: string, code: string) => Promise<UserInfo>
  register: (phone: string, password: string) => Promise<UserInfo>
  setPassword: (password: string) => Promise<UserInfo>
  changePassword: (currentPassword: string | null, newPassword: string) => Promise<UserInfo>
  logout: () => void
}

interface StoredAuth {
  token: string | null
  user: UserInfo | null
}

const STORAGE_KEY = 'yuncai.auth'

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

  const applyLogin = useCallback((res: LoginResponse) => {
    const next: StoredAuth = { token: res.token, user: res.user }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    setToken(res.token)
    setUser(res.user)
    setStatus('authenticated')
    return res.user
  }, [])

  const login = useCallback(
    async (username: string, password: string) => applyLogin(await loginApi(username, password)),
    [applyLogin],
  )

  const loginByPhone = useCallback(
    async (phone: string, code: string) => applyLogin(await loginByPhoneApi(phone, code)),
    [applyLogin],
  )

  const register = useCallback(
    async (phone: string, password: string) => applyLogin(await registerApi(phone, password)),
    [applyLogin],
  )

  const setPassword = useCallback(
    async (password: string) => {
      if (!token) throw new Error('登录状态已失效，请重新登录')
      // 后端换发新令牌并让旧令牌失效，这里必须整体替换本地登录态。
      return applyLogin(await setPasswordApi(token, password))
    },
    [token, applyLogin],
  )

  const changePassword = useCallback(
    async (currentPassword: string | null, newPassword: string) => {
      if (!token) throw new Error('登录状态已失效，请重新登录')
      return applyLogin(await changePasswordApi(token, currentPassword, newPassword))
    },
    [token, applyLogin],
  )

  const needsPasswordSetup = status === 'authenticated' && user?.hasPassword === false

  const value = useMemo<AuthContextValue>(
    () => ({
      token,
      user,
      status,
      isAuthenticated: status === 'authenticated',
      needsPasswordSetup,
      login,
      loginByPhone,
      register,
      setPassword,
      changePassword,
      logout,
    }),
    [token, user, status, needsPasswordSetup, login, loginByPhone, register, setPassword, changePassword, logout],
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
