export interface UserInfo {
  id: string
  username: string
  displayName: string
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: UserInfo
}

const API_BASE = '/api'

/**
 * 统一的 JSON 请求封装，自动携带令牌并处理错误响应。
 */
async function request<T>(path: string, options: RequestInit = {}, token?: string): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Content-Type', 'application/json')
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers })

  if (!res.ok) {
    let message = `请求失败（${res.status}）`
    try {
      const body = await res.json()
      if (body?.message) message = body.message
    } catch {
      // 忽略非 JSON 响应体
    }
    throw new Error(message)
  }

  return res.json() as Promise<T>
}

export function login(username: string, password: string): Promise<LoginResponse> {
  return request<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  })
}

export function fetchMe(token: string): Promise<UserInfo> {
  return request<UserInfo>('/auth/me', { method: 'GET' }, token)
}
