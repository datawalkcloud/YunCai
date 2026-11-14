export interface UserInfo {
  id: string
  username: string
  displayName: string
  phone?: string | null
  /** 是否已设置密码；手机验证码首次登录自动建号后为 false，需要先补设密码。 */
  hasPassword: boolean
  /** 系统管理员可管理登录账号（开户 / 停用 / 重置密码）。 */
  isSystemAdmin?: boolean
  isActive?: boolean
  lastLoginAt?: string | null
  createdAt?: string | null
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: UserInfo
}

export interface SmsCodeResponse {
  success: boolean
  retrySeconds: number
  message: string
  /** 本地模拟短信时回显的验证码；真实通道下为空。 */
  debugCode?: string | null
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

export function sendSmsCode(phone: string): Promise<SmsCodeResponse> {
  return request<SmsCodeResponse>('/auth/sms-code', {
    method: 'POST',
    body: JSON.stringify({ phone }),
  })
}

export function loginByPhone(phone: string, code: string): Promise<LoginResponse> {
  return request<LoginResponse>('/auth/login-by-phone', {
    method: 'POST',
    body: JSON.stringify({ phone, code }),
  })
}

export function fetchMe(token: string): Promise<UserInfo> {
  return request<UserInfo>('/auth/me', { method: 'GET' }, token)
}

/** 首次登录补设密码；后端会换发新令牌（旧令牌随 TokenVersion 递增失效）。 */
export function setPassword(token: string, password: string): Promise<LoginResponse> {
  return request<LoginResponse>(
    '/auth/set-password',
    { method: 'POST', body: JSON.stringify({ password }) },
    token,
  )
}

/** 已登录用户修改密码；成功后旧令牌全部失效，返回新令牌。 */
export function changePassword(
  token: string,
  currentPassword: string | null,
  newPassword: string,
): Promise<LoginResponse> {
  return request<LoginResponse>(
    '/auth/change-password',
    { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) },
    token,
  )
}

/** 忘记密码：手机验证码 + 新密码（未注册手机号同样返回成功，避免账号探测）。 */
export function resetPassword(phone: string, code: string, newPassword: string): Promise<{ message: string }> {
  return request<{ message: string }>('/auth/reset-password', {
    method: 'POST',
    body: JSON.stringify({ phone, code, newPassword }),
  })
}
