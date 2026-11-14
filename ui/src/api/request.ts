/**
 * 手写 API 模块共用的 fetch 封装：统一拼 /api 前缀、Bearer 令牌，
 * 并把后端错误体（{ message }）转成带 status 的 Error，供 StepUpProvider 等按 428 分支处理。
 */
const API_BASE = "/api"

export async function apiRequest<T>(
  path: string,
  token: string | null | undefined,
  init?: RequestInit,
  extraHeaders?: Record<string, string>,
): Promise<T> {
  const headers = new Headers({ "Content-Type": "application/json", ...extraHeaders })
  if (token) headers.set("Authorization", `Bearer ${token}`)

  const res = await fetch(`${API_BASE}${path}`, { ...init, headers })
  if (!res.ok) {
    let message = `请求失败（${res.status}）`
    try {
      const body = await res.json()
      if (body && typeof body === "object" && typeof body.message === "string" && body.message) {
        message = body.message
      }
    } catch {
      // 非 JSON 响应体，保留状态码提示。
    }
    const error = new Error(message) as Error & { status?: number }
    error.status = res.status
    throw error
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}
