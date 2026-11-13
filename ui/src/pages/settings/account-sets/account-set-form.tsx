import type { ReactNode } from "react"
import { Alert, Input, Radio, Select } from "antd"
import { HelpTip } from "@/components/page/help-tip"
import { PeriodField } from "@/components/fields/date-fields"
import { NumberField } from "@/components/fields/number-field"
import { FORM_CONTROL_W } from "@/components/page/styles"
import {
    TAX_IDENTITY_TYPES,
    TAX_LOGIN_TYPES,
    TAX_REGIONS,
    toOptions,
} from "@/lib/tax-regions"
import {
    BALANCE_ASSIST_SCOPES,
    INDUSTRIES,
    IIT_CHANNELS,
    IIT_LOGIN_TYPES,
    STANDARDS,
    TAXPAYERS,
    UNIT_DECIMALS,
    type AccountSetErrors,
    type AccountSetForm,
} from "./account-set-form-data"

/** 右侧说明图标：悬停显示字段口径。 */
function Tip({ text }: { text: string }) {
    return <HelpTip kind="info" title={text} />
}

/** 一行字段：右对齐标签 + 控件，与柠檬云的账套表单同排版。 */
function Row({
    label,
    htmlFor,
    required,
    tip,
    error,
    children,
}: {
    label: string
    htmlFor?: string
    required?: boolean
    tip?: string
    error?: string
    children: ReactNode
}) {
    return (
        <div className="flex items-start gap-3">
            <label
                htmlFor={htmlFor}
                className="w-[150px] shrink-0 pt-1 text-right text-[13px] leading-6 text-(--foreground)"
            >
                {required && <span className="mr-0.5 text-(--danger)">*</span>}
                {label}：
            </label>
            <div className="flex min-w-0 flex-1 flex-col gap-1">
                <div className="flex items-center gap-2">
                    {children}
                    {tip && <Tip text={tip} />}
                </div>
                {error && (
                    <p role="alert" className="text-[12px] leading-normal text-(--danger)">
                        {error}
                    </p>
                )}
            </div>
        </div>
    )
}

/** 分组标题，如「模块设置」「系统参数」。 */
function Group({ title, hint, children }: { title: string; hint?: string; children: ReactNode }) {
    return (
        <section className="flex flex-col gap-4">
            <div className="flex items-baseline gap-2">
                <h4 className="text-[13.5px] font-semibold text-(--foreground)">{title}</h4>
                {hint && <span className="text-[12px] text-(--muted-foreground)">{hint}</span>}
            </div>
            {children}
        </section>
    )
}

/** 二选一单选组：布尔值与 antd 的 number value 之间在此转换。 */
function BoolRadio({
    value,
    onChange,
    yes,
    no,
}: {
    value: boolean
    onChange: (value: boolean) => void
    yes: ReactNode
    no: ReactNode
}) {
    return (
        <Radio.Group
            value={value ? 1 : 0}
            onChange={(e) => onChange(e.target.value === 1)}
            options={[
                { value: 1, label: yes },
                { value: 0, label: no },
            ]}
        />
    )
}

export interface AccountSetFieldsProps {
    values: AccountSetForm
    errors: AccountSetErrors
    /** 编辑态：启用年月锁定，凭证字可改。 */
    editing: boolean
    /** 企业会计准则账套不可再切回小企业准则；小企业准则可单向升级。 */
    standardLocked?: boolean
    onChange: <K extends keyof AccountSetForm>(key: K, value: AccountSetForm[K]) => void
}

/** 基本信息页。 */
export function BasicInfoFields({ values, errors, editing, standardLocked, onChange }: AccountSetFieldsProps) {
    return (
        <div className="flex flex-col gap-5">
            <Row label="账套名称" htmlFor="account-set-name" required error={errors.name}>
                <Input
                    id="account-set-name"
                    className={FORM_CONTROL_W}
                    value={values.name}
                    onChange={(e) => onChange("name", e.target.value)}
                    placeholder="如 杭州数从科技有限公司"
                />
            </Row>

            <Row
                label="账套启用年月"
                required
                tip="启用年月决定第一个会计期间；建账后不可修改，需要换期请重新初始化或新建账套。"
                error={errors.startPeriod}
            >
                <PeriodField
                    value={values.startPeriod}
                    onChange={(v) => onChange("startPeriod", v)}
                    disabled={editing}
                    allowClear={false}
                    className={FORM_CONTROL_W}
                />
            </Row>

            <Row
                label="会计准则"
                htmlFor="account-set-standard"
                required
                tip={
                    standardLocked
                        ? "会计准则已锁定：企业会计准则不能退回小企业会计准则。"
                        : editing
                          ? "小企业会计准则可升级为企业会计准则，升级后不可退回；切换后需重新核对科目与报表。"
                          : "决定科目体系与报表格式。"
                }
            >
                <Select
                    id="account-set-standard"
                    className={FORM_CONTROL_W}
                    value={values.standard}
                    options={STANDARDS}
                    disabled={standardLocked}
                    onChange={(v) => onChange("standard", v)}
                />
            </Row>

            <Row label="行业" htmlFor="account-set-industry">
                <Select
                    id="account-set-industry"
                    className={FORM_CONTROL_W}
                    allowClear
                    showSearch
                    placeholder="请选择"
                    value={values.industry}
                    options={toOptions(INDUSTRIES)}
                    onChange={(v) => onChange("industry", v)}
                />
            </Row>

            <Row label="增值税种类" required>
                <Radio.Group
                    value={values.taxpayer}
                    onChange={(e) => onChange("taxpayer", e.target.value)}
                    options={TAXPAYERS}
                />
            </Row>

            <Row label="记账本位币" htmlFor="account-set-currency">
                <Input
                    id="account-set-currency"
                    className={FORM_CONTROL_W}
                    value={values.currency}
                    onChange={(e) => onChange("currency", e.target.value)}
                    placeholder="人民币"
                />
            </Row>

            <Row
                label="科目编码方案"
                htmlFor="account-set-coding-scheme"
                required
                error={errors.codingScheme}
                tip='以 "-" 分隔各级科目编码的位数，如 4-3-2-2；最多 10 级。改动会按新方案重建全部科目编码。'
            >
                <Input
                    id="account-set-coding-scheme"
                    className={FORM_CONTROL_W}
                    value={values.codingScheme}
                    onChange={(e) => onChange("codingScheme", e.target.value)}
                    placeholder="4-3-2-2"
                />
            </Row>

            {editing && (
                <Row
                    label="凭证字"
                    tip="在「设置 → 凭证字」中维护，新增、删除与默认设置会同步到制单下拉。"
                >
                    <span className="text-[13px] text-(--foreground)">{values.voucherWords || "—"}</span>
                </Row>
            )}
        </div>
    )
}

/** 功能参数页。 */
export function FunctionParamsFields({ values, editing, onChange }: AccountSetFieldsProps) {
    // 资产模块启用后按账套启用年月起算，与柠檬云「启用(2025-10)」的写法一致。
    const startPeriod = values.startPeriod
        ? `${values.startPeriod.getFullYear()}-${String(values.startPeriod.getMonth() + 1).padStart(2, "0")}`
        : ""
    return (
        <div className="flex flex-col gap-7">
            <Group title="模块设置">
                <Row label="资产模块" required>
                    <BoolRadio
                        value={values.assetsEnabled}
                        onChange={(v) => onChange("assetsEnabled", v)}
                        yes={editing && startPeriod ? `启用(${startPeriod})` : "启用"}
                        no="不启用"
                    />
                </Row>
                <Row label="资金模块" required>
                    <BoolRadio
                        value={values.fundsEnabled}
                        onChange={(v) => onChange("fundsEnabled", v)}
                        yes="启用"
                        no="不启用"
                    />
                </Row>
            </Group>

            <Group title="系统参数">
                <Row label="资产负债表辅助核算余额取值方式">
                    <Radio.Group
                        value={values.balanceAssistScope}
                        onChange={(e) => onChange("balanceAssistScope", e.target.value)}
                        options={BALANCE_ASSIST_SCOPES}
                    />
                </Row>
                <Row label="单价/数量/汇率小数" htmlFor="account-set-unit-decimals">
                    <Select
                        id="account-set-unit-decimals"
                        className={FORM_CONTROL_W}
                        value={values.unitDecimals}
                        options={UNIT_DECIMALS.map((n) => ({ value: n, label: String(n) }))}
                        onChange={(v) => onChange("unitDecimals", v)}
                    />
                </Row>
            </Group>
        </div>
    )
}

/** 税务信息页。 */
export function TaxInfoFields({ values, errors, editing, onChange }: AccountSetFieldsProps) {
    const incomplete = !values.taxpayerNumber || !values.taxRegion
    return (
        <div className="flex flex-col gap-7">
            {incomplete && (
                <Alert
                    type="warning"
                    showIcon
                    title="为了能正常使用一键取票、一键报税、一键登录电子税局等功能，请及时补充税务信息"
                />
            )}

            <Group title="纳税人信息">
                <Row label="公司名称" htmlFor="account-set-company" required error={errors.company}>
                    <Input
                        id="account-set-company"
                        className={FORM_CONTROL_W}
                        value={values.company}
                        onChange={(e) => onChange("company", e.target.value)}
                        placeholder="如 杭州数从科技有限公司"
                    />
                </Row>
                <Row label="统一社会信用代码" htmlFor="account-set-taxpayer-number">
                    <Input
                        id="account-set-taxpayer-number"
                        className={FORM_CONTROL_W}
                        value={values.taxpayerNumber ?? ""}
                        onChange={(e) => onChange("taxpayerNumber", e.target.value)}
                        placeholder="如 91330105MA0R8L09G"
                        maxLength={50}
                    />
                </Row>
                <Row label="报税地区" htmlFor="account-set-tax-region" tip="电子税务局按省分域部署，一键登录税局靠它决定打开哪个入口。">
                    <Select
                        id="account-set-tax-region"
                        className={FORM_CONTROL_W}
                        showSearch
                        placeholder="请选择"
                        value={values.taxRegion}
                        options={toOptions(TAX_REGIONS)}
                        onChange={(v) => onChange("taxRegion", v)}
                    />
                </Row>
                <Row label="是否出口企业">
                    <Select
                        className={FORM_CONTROL_W}
                        value={values.isExport ? "1" : "0"}
                        options={[
                            { value: "0", label: "否" },
                            { value: "1", label: "是" },
                        ]}
                        onChange={(v) => onChange("isExport", v === "1")}
                    />
                </Row>
                <div className="flex items-start gap-3">
                    <span className="w-[150px] shrink-0 pt-1 text-right text-[13px] leading-6 text-(--foreground)">
                        税率：
                    </span>
                    <div className="flex flex-wrap items-center gap-2">
                        <span className="flex items-center gap-1">
                            <NumberField
                                id="account-set-vat-rate"
                                className="w-[90px]"
                                value={values.vatRate}
                                onChange={(v) => onChange("vatRate", v)}
                                min={0}
                                max={100}
                                precision={2}
                            />
                            <span className="text-[12.5px] text-(--muted-foreground)">% 增值税</span>
                        </span>
                        <span className="flex items-center gap-1">
                            <NumberField
                                id="account-set-income-tax-rate"
                                className="w-[90px]"
                                value={values.incomeTaxRate}
                                onChange={(v) => onChange("incomeTaxRate", v)}
                                min={0}
                                max={100}
                                precision={2}
                            />
                            <span className="text-[12.5px] text-(--muted-foreground)">% 企业所得税</span>
                        </span>
                        <span className="flex items-center gap-1">
                            <NumberField
                                id="account-set-city-build-tax-rate"
                                className="w-[90px]"
                                value={values.cityBuildTaxRate}
                                onChange={(v) => onChange("cityBuildTaxRate", v)}
                                min={0}
                                max={100}
                                precision={2}
                                disabled={!editing}
                            />
                            <span className="text-[12.5px] text-(--muted-foreground)">% 城建税</span>
                        </span>
                        <span className="flex items-center gap-1">
                            <NumberField
                                id="account-set-education-surcharge-rate"
                                className="w-[90px]"
                                value={values.educationSurchargeRate}
                                onChange={(v) => onChange("educationSurchargeRate", v)}
                                min={0}
                                max={100}
                                precision={2}
                                disabled={!editing}
                            />
                            <span className="text-[12.5px] text-(--muted-foreground)">% 教育费附加</span>
                        </span>
                        <span className="flex items-center gap-1">
                            <NumberField
                                id="account-set-local-education-surcharge-rate"
                                className="w-[90px]"
                                value={values.localEducationSurchargeRate}
                                onChange={(v) => onChange("localEducationSurchargeRate", v)}
                                min={0}
                                max={100}
                                precision={2}
                                disabled={!editing}
                            />
                            <span className="text-[12.5px] text-(--muted-foreground)">% 地方教育附加</span>
                        </span>
                    </div>
                </div>
                {!editing && (
                    <div className="pl-[163px] text-[12px] text-(--muted-foreground)">
                        附加税费率决定期末检查「计提税金」的计提比例，新建账套按法定 7 / 3 / 2 登记，创建后可修改。
                    </div>
                )}
            </Group>

            <Group title="企税申报登录" hint="用于一键取票、增值税/企业所得税等税种申报、快速登录税局办理业务">
                <Row label="登录方式" htmlFor="account-set-tax-login-type" tip="决定助手打开税局后停在哪个登录页签。">
                    <Select
                        id="account-set-tax-login-type"
                        className={FORM_CONTROL_W}
                        value={values.taxLoginType}
                        options={toOptions(TAX_LOGIN_TYPES)}
                        onChange={(v) => onChange("taxLoginType", v)}
                    />
                </Row>
                <Row label="身份类型" htmlFor="account-set-tax-identity-type">
                    <Select
                        id="account-set-tax-identity-type"
                        className={FORM_CONTROL_W}
                        value={values.taxIdentityType}
                        options={toOptions(TAX_IDENTITY_TYPES)}
                        onChange={(v) => onChange("taxIdentityType", v)}
                    />
                </Row>
                <Row label="税局实名账号" htmlFor="account-set-tax-login-account">
                    <Input
                        id="account-set-tax-login-account"
                        className={FORM_CONTROL_W}
                        value={values.taxLoginAccount ?? ""}
                        onChange={(e) => onChange("taxLoginAccount", e.target.value)}
                        placeholder="税局登录用的手机号或身份证号"
                        maxLength={100}
                    />
                </Row>
                <Row
                    label="税局实名密码"
                    htmlFor="account-set-tax-login-password"
                    tip="加密后存库，接口只回「是否已填写」、不再下发；更换 Jwt 签名密钥会让已存密码解不开，届时重填一次即可。"
                >
                    <Input.Password
                        id="account-set-tax-login-password"
                        className={FORM_CONTROL_W}
                        value={values.taxLoginPassword ?? ""}
                        onChange={(e) => onChange("taxLoginPassword", e.target.value)}
                        placeholder={values.taxPasswordSaved ? "已保存，留空表示不修改" : "请输入税局实名密码"}
                        autoComplete="new-password"
                    />
                </Row>
            </Group>

            <Group title="个税申报登录" hint="用于个税申报、作废、更正">
                <Row label="申报渠道" htmlFor="account-set-iit-channel">
                    <Select
                        id="account-set-iit-channel"
                        className={FORM_CONTROL_W}
                        value={values.iitChannel}
                        options={toOptions(IIT_CHANNELS)}
                        onChange={(v) => onChange("iitChannel", v)}
                    />
                </Row>
                <Row label="登录方式">
                    <Radio.Group
                        value={values.iitLoginType}
                        onChange={(e) => onChange("iitLoginType", e.target.value)}
                        options={toOptions(IIT_LOGIN_TYPES)}
                    />
                </Row>
                <Row label="实名登录账号" htmlFor="account-set-iit-account">
                    <Input
                        id="account-set-iit-account"
                        className={FORM_CONTROL_W}
                        value={values.iitAccount ?? ""}
                        onChange={(e) => onChange("iitAccount", e.target.value)}
                        placeholder="自然人电子税务局的手机号或身份证号"
                        maxLength={100}
                    />
                </Row>
                <Row
                    label="实名登录密码"
                    htmlFor="account-set-iit-password"
                    tip="留空表示不修改；未单独填写时，个税登录沿用上面的企税账号密码。"
                >
                    <Input.Password
                        id="account-set-iit-password"
                        className={FORM_CONTROL_W}
                        value={values.iitPassword ?? ""}
                        onChange={(e) => onChange("iitPassword", e.target.value)}
                        placeholder={values.iitPasswordSaved ? "已保存，留空表示不修改" : "请输入个税实名密码"}
                        autoComplete="new-password"
                    />
                </Row>
            </Group>
        </div>
    )
}
