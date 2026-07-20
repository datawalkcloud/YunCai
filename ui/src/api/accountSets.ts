/** 账套响应，字段与后端 AccountSetDto 对应（camelCase）。 */
export interface AccountSetDto {
  id: string
  name: string
  companyName: string
  baseCurrency: string
  startPeriod: string
  isCurrent: boolean
}

const API_BASE = '/api'

/** 获取全部账套（用于选择导入目标账套等场景）。 */
export async function fetchAccountSets(token?: string): Promise<AccountSetDto[]> {
  const headers = new Headers()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const res = await fetch(`${API_BASE}/accountsets`, { headers })

  if (!res.ok) {
    let message = `加载账套失败（${res.status}）`
    try {
      const body = await res.json()
      if (body?.message) message = body.message
    } catch {
      // 忽略非 JSON 响应体
    }
    throw new Error(message)
  }

  return res.json() as Promise<AccountSetDto[]>
}
