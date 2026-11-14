/** 账套内当前用户的角色与权限（对应后端 MyPermissionsDto）。 */
export interface MyPermissions {
  roleId: string
  roleName: string
  isAdmin: boolean
  permissions: string[]
  permissionText: string
}

/** 登录账号（系统管理员可见的完整资料）。 */
export interface AdminUserDto {
  id: string
  username: string
  displayName: string
  phone?: string | null
  hasPassword: boolean
  isSystemAdmin: boolean
  isActive: boolean
  lastLoginAt?: string | null
  createdAt?: string | null
}

export interface CreateUserPayload {
  displayName: string
  phone: string
  password?: string
}

const API_BASE = '/api'

/** 带令牌的 JSON 请求；支持 204 无响应体。 */
async function request<T>(path: string, options: RequestInit, token?: string): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers })
  if (!res.ok) {
    let message = `请求失败（${res.status}）`
    try {
      const body = await res.json()
      if (body?.message) message = body.message
    } catch {
      // 忽略非 JSON 响应体
    }
    const error = new Error(message) as Error & { status?: number }
    error.status = res.status
    throw error
  }

  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export function fetchMyPermissions(accountSetId: string, token?: string): Promise<MyPermissions> {
  return request<MyPermissions>(`/accountsets/${accountSetId}/my-permissions`, { method: 'GET' }, token)
}

export function fetchUsers(token: string, keyword?: string): Promise<AdminUserDto[]> {
  const query = keyword?.trim() ? `?keyword=${encodeURIComponent(keyword.trim())}` : ''
  return request<AdminUserDto[]>(`/users${query}`, { method: 'GET' }, token)
}

export function createUser(token: string, payload: CreateUserPayload): Promise<AdminUserDto> {
  return request<AdminUserDto>('/users', { method: 'POST', body: JSON.stringify(payload) }, token)
}

export function updateUser(
  token: string,
  id: string,
  payload: { displayName: string; phone?: string },
): Promise<AdminUserDto> {
  return request<AdminUserDto>(`/users/${id}`, { method: 'PUT', body: JSON.stringify(payload) }, token)
}

export function setUserEnabled(token: string, id: string, enabled: boolean): Promise<AdminUserDto> {
  return request<AdminUserDto>(`/users/${id}/status`, { method: 'PUT', body: JSON.stringify({ enabled }) }, token)
}

export function adminResetPassword(token: string, id: string, newPassword: string): Promise<void> {
  return request<void>(`/users/${id}/reset-password`, { method: 'POST', body: JSON.stringify({ newPassword }) }, token)
}

/** 换取高危操作令牌（5 分钟有效），请求头 X-Step-Up-Token 携带。 */
export function requestStepUpToken(
  token: string,
  payload: { password?: string; smsCode?: string },
): Promise<{ token: string; expiresInSeconds: number }> {
  return request<{ token: string; expiresInSeconds: number }>(
    '/auth/step-up',
    { method: 'POST', body: JSON.stringify(payload) },
    token,
  )
}
