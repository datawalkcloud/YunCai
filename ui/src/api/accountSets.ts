/** 账套响应，字段与后端 AccountSetDto 对应（camelCase）。 */
export interface AccountSetDto {
  id: string
  name: string
  companyName: string
  baseCurrency: string
  startPeriod: string
  isCurrent: boolean
  /** 科目编码方案，形如 "4-2-2-2"，表示各级科目的编码位数。 */
  accountCodingScheme?: string
  /** 会计准则与纳税人性质（枚举值）：更新账套的接口要求整包提交，缺了会被后端当成 0。 */
  standard?: number
  taxpayerType?: number
  /** 纳税人识别号（统一社会信用代码），未填写为 null；一键登录税局靠它定位企业。 */
  taxpayerNumber?: string | null
  /** 报税地区（省级税务局名称，如「浙江省」）；助手据此决定打开哪个省的电子税务局。 */
  taxRegion?: string | null
  /** 税局登录方式（如「企业业务登录」）。 */
  taxLoginType?: string | null
  /** 税局身份类型（如「办税员」）。 */
  taxIdentityType?: string | null
  /** 税局实名账号；密码本身不下发，只告知是否已保存。 */
  taxLoginAccount?: string | null
  /** 是否已保存税局实名密码。 */
  taxLoginPasswordSet?: boolean
  /** 凭证字列表（逗号分隔，如 "记,收,付,转"），制单时只能从中选择。 */
  voucherWords?: string
  /** 启用期间（记账日期不得早于该月）。 */
  startYear?: number
  startMonth?: number
  /** 已结账至期间（"2026-06"），尚未结账为 null。 */
  closedPeriod?: string | null
  /** 已结转损益至期间（"2026-06"），尚未结转为 null。 */
  carriedPeriod?: string | null
  /** 下一个待结账期间。 */
  nextCloseYear?: number
  nextCloseMonth?: number
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
