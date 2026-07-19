import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { login as loginApi, type UserInfo } from '../api/auth'

interface AuthState {
  token: string | null
  user: UserInfo | null
}

interface AuthContextValue {
  token: string | null
  user: UserInfo | null
  isAuthenticated: boolean
  login: (username: string, password: string) => Promise<void>
  logout: () => void
}

const STORAGE_KEY = 'kuaiji.auth'

const AuthContext = createContext<AuthContextValue | null>(null)

function readStoredAuth(): AuthState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) {
      return JSON.parse(raw) as AuthState
    }
  } catch {
    // 忽略损坏的本地存储
  }
  return { token: null, user: null }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>(readStoredAuth)

  const login = useCallback(async (username: string, password: string) => {
    const res = await loginApi(username, password)
    const next: AuthState = { token: res.token, user: res.user }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    setState(next)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY)
    setState({ token: null, user: null })
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      token: state.token,
      user: state.user,
      isAuthenticated: Boolean(state.token),
      login,
      logout,
    }),
    [state, login, logout],
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
