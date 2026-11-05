import type { AccountSetDto } from "@/apis-gen"
import { parseDate } from "@/components/fields/date-fields"
import {
    DEFAULT_TAX_IDENTITY_TYPE,
    DEFAULT_TAX_LOGIN_TYPE,
} from "@/lib/tax-regions"

/** 会计准则枚举值（与后端 AccountingStandard 对应）及其显示名。 */
export const STANDARDS: { value: number; label: string }[] = [
    { value: 1, label: "企业会计准则" },
    { value: 2, label: "小企业会计准则" },
    { value: 3, label: "民间非营利组织会计制度" },
]

/** 纳税人性质枚举值（与后端 TaxpayerType 对应）及其显示名。 */
export const TAXPAYERS: { value: number; label: string }[] = [
    { value: 2, label: "小规模纳税人" },
    { value: 1, label: "一般纳税人" },
]

/** 行业：建账候选清单，后端按文案原样存储。 */
export const INDUSTRIES = [
    "商业",
    "工业",
    "建筑业",
    "房地产业",
    "交通运输业",
    "餐饮业",
    "服务业",
    "旅游业",
    "金融保险",
    "农业",
    "外贸企业",
    "其他",
]

/** 单价 / 数量 / 汇率小数位的可选档。 */
export const UNIT_DECIMALS = [2, 4, 6, 8]

/** 资产负债表辅助核算余额取值方式（与后端 BalanceAssistScope 对应）。 */
export const BALANCE_ASSIST_SCOPES: { value: number; label: string }[] = [
    { value: 1, label: "仅供应商/客户辅助核算余额参与计算" },
    { value: 2, label: "所有辅助核算余额参与计算" },
]

/** 枚举值 → 显示名，列表卡片与表单共用一份。 */
export function standardLabel(value?: number): string {
    return STANDARDS.find((s) => s.value === value)?.label ?? "—"
}

export function taxpayerLabel(value?: number): string {
    return TAXPAYERS.find((t) => t.value === value)?.label ?? "—"
}

/** 个税申报渠道：目前服务端只走自然人电子税务局网页端。 */
export const IIT_CHANNELS = ["个税网页端"]

/** 个税登录方式，与后端 iit/login 的两种模式一一对应。 */
export const IIT_LOGIN_TYPES = ["实名账号登录", "扫码登录"]

export const DEFAULT_IIT_CHANNEL = IIT_CHANNELS[0]
export const DEFAULT_IIT_LOGIN_TYPE = IIT_LOGIN_TYPES[0]

/** 科目编码方案格式：每级 1-2 位数字、以 "-" 分隔，最多 10 级。 */
export const CODING_SCHEME_PATTERN = /^\d{1,2}(-\d{1,2}){0,9}$/

/** 账套表单字段，覆盖「基本信息 / 功能参数 / 税务信息」三页。 */
export interface AccountSetForm {
    name: string
    company: string
    standard: number
    currency: string
    taxpayer: number
    startPeriod?: Date
    codingScheme: string
    industry?: string
    // 功能参数
    assetsEnabled: boolean
    fundsEnabled: boolean
    balanceAssistScope: number
    unitDecimals: number
    // 涉税资料与税率
    taxpayerNumber?: string
    vatRate?: number
    incomeTaxRate?: number
    isExport?: boolean
    // 企税申报登录。密码只在用户主动改写时提交，留空表示保持原值。
    taxRegion?: string
    taxLoginType?: string
    taxIdentityType?: string
    taxLoginAccount?: string
    taxLoginPassword?: string
    /** 后端已存有企税密码（密码本身不下发，仅这个布尔值下发）。 */
    taxPasswordSaved?: boolean
    // 个税申报登录
    iitChannel?: string
    iitLoginType?: string
    iitAccount?: string
    iitPassword?: string
    iitPasswordSaved?: boolean
    // 凭证字
    voucherWords?: string
}

/** 表单错误集合。 */
export type AccountSetErrors = Partial<Record<keyof AccountSetForm, string>>

/** 新建账套的初始表单。 */
export function emptyForm(): AccountSetForm {
    return {
        name: "",
        company: "",
        standard: STANDARDS[0].value,
        currency: "人民币",
        taxpayer: TAXPAYERS[1].value,
        codingScheme: "4-3-2-2",
        assetsEnabled: true,
        fundsEnabled: true,
        balanceAssistScope: BALANCE_ASSIST_SCOPES[1].value,
        unitDecimals: 8,
        isExport: false,
    }
}

/** 用后端账套预填表单。 */
export function formFromDto(set: AccountSetDto): AccountSetForm {
    return {
        ...emptyForm(),
        name: set.name ?? "",
        company: set.companyName ?? "",
        standard: set.standard ?? STANDARDS[0].value,
        currency: set.baseCurrency ?? "人民币",
        taxpayer: set.taxpayerType ?? TAXPAYERS[1].value,
        startPeriod: set.startPeriod ? parseDate(set.startPeriod) : undefined,
        codingScheme: set.accountCodingScheme ?? "4-3-2-2",
        industry: set.industry ?? undefined,
        assetsEnabled: set.assetsModuleEnabled ?? true,
        fundsEnabled: set.fundsModuleEnabled ?? true,
        balanceAssistScope: set.balanceAssistScope ?? BALANCE_ASSIST_SCOPES[1].value,
        unitDecimals: set.unitDecimalPlaces ?? 8,
        taxpayerNumber: set.taxpayerNumber ?? "",
        vatRate: set.vatRate,
        incomeTaxRate: set.incomeTaxRate,
        isExport: set.isExportEnterprise ?? false,
        taxRegion: set.taxRegion ?? undefined,
        taxLoginType: set.taxLoginType ?? DEFAULT_TAX_LOGIN_TYPE,
        taxIdentityType: set.taxIdentityType ?? DEFAULT_TAX_IDENTITY_TYPE,
        taxLoginAccount: set.taxLoginAccount ?? "",
        taxLoginPassword: "",
        taxPasswordSaved: set.taxLoginPasswordSet ?? false,
        iitChannel: set.iitChannel ?? DEFAULT_IIT_CHANNEL,
        iitLoginType: set.iitLoginType ?? DEFAULT_IIT_LOGIN_TYPE,
        iitAccount: set.iitLoginAccount ?? "",
        iitPassword: "",
        iitPasswordSaved: set.iitLoginPasswordSet ?? false,
        voucherWords: set.voucherWords ?? "记,收,付,转",
    }
}

/**
 * 表单校验：只有决定账套结构、填错就得重建的字段才拦提交，
 * 税务与登录资料允许留空（一键登录税局时另有提示）。
 */
export function validateAccountSet(values: AccountSetForm, isEditing: boolean): AccountSetErrors {
    const errors: AccountSetErrors = {}
    if (!values.name) errors.name = "请填写账套名称"
    if (!values.company) errors.company = "请填写公司名称"
    if (!values.startPeriod) errors.startPeriod = "请选择启用年月"
    if (!values.codingScheme) {
        errors.codingScheme = "请填写科目编码方案"
    } else if (!CODING_SCHEME_PATTERN.test(values.codingScheme)) {
        errors.codingScheme = "格式须如 4-2-2-2，每级 1-2 位数字，最多 10 级"
    }
    if (isEditing && !values.voucherWords) errors.voucherWords = "请填写凭证字"
    return errors
}

/** 三个 Tab 的 key。 */
export const FORM_TABS = [
    { key: "basic", label: "基本信息" },
    { key: "function", label: "功能参数" },
    { key: "tax", label: "税务信息" },
] as const

/** 每个 Tab 需要展示哪些错误，切到别的 Tab 时不显示其它 Tab 的红字。 */
const TAB_FIELDS: Record<string, (keyof AccountSetForm)[]> = {
    basic: ["name", "startPeriod", "codingScheme", "voucherWords"],
    function: [],
    tax: ["company"],
}

export function tabOfField(field: keyof AccountSetForm): string {
    for (const [tab, fields] of Object.entries(TAB_FIELDS)) {
        if (fields.includes(field)) return tab
    }
    return "basic"
}
