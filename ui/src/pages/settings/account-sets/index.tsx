import { useCallback, useEffect, useState, type ReactNode } from "react"
import { Alert, Button, Drawer, Empty, Input, Select, Spin } from "antd"
import {
    BankOutlined,
    CheckCircleFilled,
    CheckOutlined,
    CopyOutlined,
    EditOutlined,
    LockOutlined,
    PlusOutlined,
    ReloadOutlined,
    DeleteOutlined,
    UnlockOutlined,
} from "@ant-design/icons"
import dayjs from "dayjs"

import { useAuth } from "@/auth/AuthContext"
import { useAccountSets } from "@/accountSets/AccountSetsContext"
import { AccountSetsApi, type AccountSetDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { useConfirm } from "@/hooks/useConfirm"
import { cn } from "@/lib/utils"
import { PageHeader, SectionCard } from "@/components/page/section"
import { FormField } from "@/components/page/form-field"
import { StatusPill } from "@/components/page/status-pill"
import { ICON_STYLE } from "@/components/page/styles"
import { PeriodField, parseDate } from "@/components/fields/date-fields"
import { NumberField } from "@/components/fields/number-field"

/** 会计准则枚举值（与后端 AccountingStandard 对应）及其显示名。 */
const STANDARDS: { value: number; label: string }[] = [
    { value: 1, label: "企业会计准则" },
    { value: 2, label: "小企业会计准则" },
    { value: 3, label: "民间非营利组织会计制度" },
]
/** 纳税人性质枚举值（与后端 TaxpayerType 对应）及其显示名。 */
const TAXPAYERS: { value: number; label: string }[] = [
    { value: 1, label: "一般纳税人" },
    { value: 2, label: "小规模纳税人" },
]

/** 科目编码方案格式：每级 1-2 位数字、以 "-" 分隔，最多 10 级。 */
const CODING_SCHEME_PATTERN = /^\d{1,2}(-\d{1,2}){0,9}$/

function standardLabel(value?: number): string {
    return STANDARDS.find((s) => s.value === value)?.label ?? "—"
}

function taxpayerLabel(value?: number): string {
    return TAXPAYERS.find((t) => t.value === value)?.label ?? "—"
}

/** 账套表单字段。 */
interface AccountSetForm {
    name: string
    company: string
    standard: number
    currency: string
    taxpayer: number
    startPeriod?: Date
    codingScheme: string
    // 以下为编辑时才提交的涉税与凭证字信息（新增账套时由后端按纳税人性质预置）。
    taxpayerNumber?: string
    vatRate?: number
    incomeTaxRate?: number
    voucherWords?: string
}

/** 表单错误集合。 */
type AccountSetErrors = Partial<Record<keyof AccountSetForm, string>>

/**
 * 表单校验，提示文案与原 antd rules 逐条一致。
 * 凭证字只在编辑态渲染（等价原条件挂载的 Form.Item），故也只在校验编辑态。
 */
function validateAccountSet(values: AccountSetForm, isEditing: boolean): AccountSetErrors {
    const errors: AccountSetErrors = {}
    if (!values.name) errors.name = "请填写账套名称"
    if (!values.company) errors.company = "请填写单位名称"
    if (!values.startPeriod) errors.startPeriod = "请选择启用期间"
    if (!values.codingScheme) {
        errors.codingScheme = "请填写科目编码方案"
    } else if (!CODING_SCHEME_PATTERN.test(values.codingScheme)) {
        errors.codingScheme = "格式须如 4-2-2-2，每级 1-2 位数字，最多 10 级"
    }
    if (isEditing && !values.voucherWords) errors.voucherWords = "请填写凭证字"
    return errors
}

/** 卡片内的键值行，替代原嵌套 Row/Col 的两列布局。 */
function KeyValue({ label, children }: { label: string, children: ReactNode }) {
    return (
        <div className="flex items-start justify-between gap-2 text-[12.5px]">
            <span className="shrink-0 text-slate-400">{label}</span>
            <span className="min-w-0 text-right text-slate-700">{children}</span>
        </div>
    )
}

export default function AccountSetsPage() {
    const { token } = useAuth()
    const { refresh: refreshAccountSets } = useAccountSets()
    const { confirm, confirmDialog } = useConfirm()

    const [sets, setSets] = useState<AccountSetDto[]>([])
    const [loading, setLoading] = useState(false)
    const [listError, setListError] = useState<string | null>(null)
    const [busyId, setBusyId] = useState<string | null>(null)

    const [submitting, setSubmitting] = useState(false)
    const [open, setOpen] = useState(false)
    const [editing, setEditing] = useState<AccountSetDto | null>(null)
    const [formValues, setFormValues] = useState<AccountSetForm>({
        name: "",
        company: "",
        standard: STANDARDS[0].value,
        currency: "人民币",
        taxpayer: TAXPAYERS[0].value,
        codingScheme: "4-3-2-2",
    })
    const [formErrors, setFormErrors] = useState<AccountSetErrors>({})

    const load = useCallback(async () => {
        setLoading(true)
        setListError(null)
        try {
            const list = await new AccountSetsApi(apiConfig(token)).accountSetsGet()
            setSets(list)
        } catch (err) {
            setListError(await apiErrorMessage(err, "加载账套失败，请重试。"))
        } finally {
            setLoading(false)
        }
    }, [token])

    useEffect(() => {
        void load()
    }, [load])

    /** 整体预填表单，对应原 setFieldsValue。 */
    function fillForm(next: AccountSetForm) {
        setFormValues(next)
        setFormErrors({})
    }

    /** 改单个字段，对应原控件的 onChange；已有错误时即时重算。 */
    function setFormValue<K extends keyof AccountSetForm>(key: K, value: AccountSetForm[K]) {
        const next: AccountSetForm = { ...formValues, [key]: value }
        setFormValues(next)
        if (Object.keys(formErrors).length > 0) setFormErrors(validateAccountSet(next, !!editing))
    }

    /** 提交前校验，对应原 validateFields。 */
    function checkForm() {
        const next = validateAccountSet(formValues, !!editing)
        setFormErrors(next)
        return Object.keys(next).length === 0
    }

    async function setCurrent(id?: string) {
        if (!id || busyId) return
        setBusyId(id)
        setListError(null)
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdCurrentPost({ id })
            await load()
            refreshAccountSets()
        } catch (err) {
            setListError(await apiErrorMessage(err, "设为当前账套失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    async function removeSet(set: AccountSetDto) {
        if (!set.id || busyId) return
        if (!(await confirm(`确定删除账套「${set.name}」吗？此操作不可撤销。`))) return
        setBusyId(set.id)
        setListError(null)
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdDelete({ id: set.id })
            await load()
        } catch (err) {
            setListError(await apiErrorMessage(err, "删除账套失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    /** 月结账：按后端给出的下一待结期间顺序推进，结账前需期间内凭证全部已过账。 */
    async function closePeriod(set: AccountSetDto) {
        if (!set.id || busyId || !set.nextCloseYear || !set.nextCloseMonth) return
        if (!(await confirm(`确定对账套「${set.name}」执行 ${set.nextCloseYear} 年 ${set.nextCloseMonth} 月的结账吗？结账后该期间凭证将不可增删改。`))) return
        setBusyId(set.id)
        setListError(null)
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdClosePeriodPost({
                id: set.id,
                closePeriodRequest: { year: set.nextCloseYear, month: set.nextCloseMonth },
            })
            await load()
        } catch (err) {
            setListError(await apiErrorMessage(err, "结账失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    /** 反结账：退回最近一个已结期间。 */
    async function reopenPeriod(set: AccountSetDto) {
        if (!set.id || busyId || !set.closedPeriod) return
        if (!(await confirm(`确定对账套「${set.name}」反结账吗？将退回最近一个已结期间（${set.closedPeriod}）。`))) return
        setBusyId(set.id)
        setListError(null)
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdReopenPeriodPost({ id: set.id })
            await load()
        } catch (err) {
            setListError(await apiErrorMessage(err, "反结账失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    function openCreate() {
        setEditing(null)
        fillForm({
            name: "",
            company: "",
            standard: STANDARDS[0].value,
            currency: "人民币",
            taxpayer: TAXPAYERS[0].value,
            startPeriod: dayjs().toDate(),
            codingScheme: "4-3-2-2",
        })
        setOpen(true)
    }

    function openEdit(set: AccountSetDto) {
        setEditing(set)
        fillForm({
            name: set.name ?? "",
            company: set.companyName ?? "",
            standard: set.standard ?? STANDARDS[0].value,
            currency: set.baseCurrency ?? "人民币",
            taxpayer: set.taxpayerType ?? TAXPAYERS[0].value,
            startPeriod: set.startPeriod ? parseDate(set.startPeriod) : dayjs().toDate(),
            codingScheme: set.accountCodingScheme ?? "4-3-2-2",
            taxpayerNumber: set.taxpayerNumber ?? "",
            vatRate: set.vatRate,
            incomeTaxRate: set.incomeTaxRate,
            voucherWords: set.voucherWords ?? "记,收,付,转",
        })
        setOpen(true)
    }

    function openCopy(set: AccountSetDto) {
        setEditing(null)
        fillForm({
            name: `${set.name ?? ""}（副本）`,
            company: set.companyName ?? "",
            standard: set.standard ?? STANDARDS[0].value,
            currency: set.baseCurrency ?? "人民币",
            taxpayer: set.taxpayerType ?? TAXPAYERS[0].value,
            startPeriod: set.startPeriod ? parseDate(set.startPeriod) : dayjs().toDate(),
            codingScheme: set.accountCodingScheme ?? "4-3-2-2",
        })
        setOpen(true)
    }

    async function saveSet() {
        if (!checkForm()) return
        const values = formValues
        const period = values.startPeriod
        if (!period) return
        setSubmitting(true)
        setListError(null)
        try {
            const api = new AccountSetsApi(apiConfig(token))
            if (editing) {
                // 编辑
                await api.accountSetsIdPut({
                    id: editing.id!,
                    updateAccountSetRequest: {
                        name: values.name.trim(),
                        companyName: values.company.trim(),
                        standard: values.standard,
                        taxpayerType: values.taxpayer,
                        baseCurrency: values.currency.trim() || "人民币",
                        accountCodingScheme: values.codingScheme?.trim() || undefined,
                        taxpayerNumber: values.taxpayerNumber?.trim() || undefined,
                        vatRate: values.vatRate ?? undefined,
                        incomeTaxRate: values.incomeTaxRate ?? undefined,
                        voucherWords: values.voucherWords?.trim() || undefined,
                    },
                })
            } else {
                // 新增（含复制）
                await api.accountSetsPost({
                    createAccountSetRequest: {
                        name: values.name.trim(),
                        companyName: values.company.trim(),
                        standard: values.standard,
                        taxpayerType: values.taxpayer,
                        baseCurrency: values.currency.trim() || "人民币",
                        startYear: dayjs(period).year(),
                        startMonth: dayjs(period).month() + 1,
                        accountCodingScheme: values.codingScheme?.trim() || undefined,
                    },
                })
            }
            setOpen(false)
            await load()
            refreshAccountSets()
        } catch (err) {
            setListError(await apiErrorMessage(err, editing ? "更新账套失败，请重试。" : "创建账套失败，请重试。"))
        } finally {
            setSubmitting(false)
        }
    }

    return (
        <div className="flex w-full flex-col gap-5">
            <PageHeader
                icon={BankOutlined}
                title="账套"
                subtitle="管理各单位的账簿账套，可在多套账之间切换当前账套。"
                actions={
                    <Button
                        icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                        disabled={loading}
                        onClick={() => void load()}
                    >
                        刷新
                    </Button>
                }
            />

            <SectionCard
                title={<h3 className="text-[14px] font-semibold text-slate-800">账套列表</h3>}
                action={
                    <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                        新增账套
                    </Button>
                }
                bodyClassName="flex flex-col gap-4"
            >
                {listError && <Alert type="error" showIcon title={listError} />}

                {loading && sets.length === 0 ? (
                    <div className="flex justify-center py-10">
                        <Spin />
                    </div>
                ) : sets.length === 0 ? (
                    <Empty
                        image={<BankOutlined className="text-[40px]" style={ICON_STYLE.placeholder} />}
                        description="暂无账套，点击“新增账套”创建"
                    />
                ) : (
                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
                        {sets.map((set) => (
                            <div
                                key={set.id}
                                className={cn(
                                    "flex flex-col gap-3 rounded-2xl border bg-white p-4 shadow-[0_1px_2px_rgba(16,24,40,0.04)]",
                                    set.isCurrent ? "border-[#bfdbfe] ring-1 ring-[#dbeafe]" : "border-slate-100",
                                )}
                            >
                                <div className="flex items-start justify-between gap-2">
                                    <div className="flex min-w-0 items-center gap-2">
                                        <div className="flex size-9 shrink-0 items-center justify-center bg-[#eef4ff] text-[20px] text-[#2f6bff]">
                                            <BankOutlined />
                                        </div>
                                        <div className="flex min-w-0 flex-col">
                                            <span className="truncate text-[13px] font-medium text-slate-800">{set.name}</span>
                                            <span className="truncate text-[12px] text-slate-400">{set.companyName}</span>
                                        </div>
                                    </div>
                                    {set.isCurrent && (
                                        <StatusPill tone="green">
                                            <CheckCircleFilled className="text-[12px]" />
                                            当前
                                        </StatusPill>
                                    )}
                                </div>

                                <div className="flex flex-col gap-1">
                                    <KeyValue label="会计准则">{standardLabel(set.standard)}</KeyValue>
                                    <KeyValue label="本位币">{set.baseCurrency}</KeyValue>
                                    <KeyValue label="纳税人性质">{taxpayerLabel(set.taxpayerType)}</KeyValue>
                                    <KeyValue label="纳税人识别号">{set.taxpayerNumber || "—"}</KeyValue>
                                    <KeyValue label="增值税 / 所得税">{set.vatRate ?? "—"}% / {set.incomeTaxRate ?? "—"}%</KeyValue>
                                    <KeyValue label="凭证字">{set.voucherWords || "—"}</KeyValue>
                                    <KeyValue label="启用期间">{set.startPeriod}</KeyValue>
                                    <KeyValue label="已结至">{set.closedPeriod || "未结账"}</KeyValue>
                                    <KeyValue label="编码方案">{set.accountCodingScheme ?? "—"}</KeyValue>
                                </div>

                                <div className="mt-auto flex items-center justify-between gap-2 pt-1">
                                    <Button
                                        icon={
                                            busyId === set.id ? (
                                                <ReloadOutlined spin />
                                            ) : (
                                                <CheckOutlined />
                                            )
                                        }
                                        disabled={set.isCurrent || busyId === set.id}
                                        onClick={() => void setCurrent(set.id)}
                                    >
                                        {set.isCurrent ? "当前账套" : "设为当前"}
                                    </Button>
                                    <div className="flex items-center gap-0">
                                        <Button
                                            type="text"
                                            size="small"
                                            disabled={busyId === set.id}
                                            onClick={() => void closePeriod(set)}
                                            title={set.nextCloseYear && set.nextCloseMonth ? `结账（${set.nextCloseYear} 年 ${set.nextCloseMonth} 月）` : "结账"}
                                            icon={<LockOutlined />}
                                        />
                                        <Button
                                            type="text"
                                            size="small"
                                            disabled={!set.closedPeriod || busyId === set.id}
                                            onClick={() => void reopenPeriod(set)}
                                            title={set.closedPeriod ? `反结账（退回 ${set.closedPeriod}）` : "尚未结账"}
                                            icon={<UnlockOutlined />}
                                        />
                                        <Button
                                            type="text"
                                            size="small"
                                            onClick={() => openEdit(set)}
                                            title="编辑账套"
                                            icon={<EditOutlined />}
                                        />
                                        <Button
                                            type="text"
                                            size="small"
                                            onClick={() => openCopy(set)}
                                            title="复制账套"
                                            icon={<CopyOutlined />}
                                        />
                                        <Button
                                            type="text"
                                            size="small"
                                            danger
                                            disabled={set.isCurrent || busyId === set.id}
                                            onClick={() => void removeSet(set)}
                                            title={set.isCurrent ? "当前账套不可删除" : "删除账套"}
                                            icon={<DeleteOutlined />}
                                        />
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </SectionCard>

            {/* 新增 / 编辑账套 */}
            <Drawer
                open={open}
                size={512}
                title={editing ? "编辑账套" : "新增账套"}
                onClose={() => setOpen(false)}
                styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
                footer={
                    <div className="flex justify-end gap-2">
                        <Button onClick={() => setOpen(false)}>取消</Button>
                        <Button type="primary" loading={submitting} onClick={() => void saveSet()}>
                            {editing ? "保存" : "创建账套"}
                        </Button>
                    </div>
                }
            >
                <p className="text-[12.5px] text-slate-400">
                    {editing ? '修改账套基本信息后点击"保存"。' : '填写单位与核算信息后点击"创建账套"保存。'}
                </p>

                <FormField label="账套名称" htmlFor="account-set-name" required error={formErrors.name}>
                    <Input
                        id="account-set-name"
                        value={formValues.name}
                        onChange={(e) => setFormValue("name", e.target.value)}
                        placeholder="如 华信科技 2026"
                    />
                </FormField>

                <FormField label="单位名称" htmlFor="account-set-company" required error={formErrors.company}>
                    <Input
                        id="account-set-company"
                        value={formValues.company}
                        onChange={(e) => setFormValue("company", e.target.value)}
                        placeholder="如 华信科技有限公司"
                    />
                </FormField>

                <FormField label="会计准则" htmlFor="account-set-standard">
                    <Select
                        id="account-set-standard"
                        className="w-full"
                        value={formValues.standard}
                        options={STANDARDS}
                        onChange={(v) => setFormValue("standard", v)}
                    />
                </FormField>

                <FormField label="记账本位币" htmlFor="account-set-currency">
                    <Input
                        id="account-set-currency"
                        value={formValues.currency}
                        onChange={(e) => setFormValue("currency", e.target.value)}
                        placeholder="人民币"
                    />
                </FormField>

                <FormField label="纳税人性质" htmlFor="account-set-taxpayer">
                    <Select
                        id="account-set-taxpayer"
                        className="w-full"
                        value={formValues.taxpayer}
                        options={TAXPAYERS}
                        onChange={(v) => setFormValue("taxpayer", v)}
                    />
                </FormField>

                <FormField label="启用期间" required error={formErrors.startPeriod}>
                    <PeriodField
                        value={formValues.startPeriod}
                        onChange={(v) => setFormValue("startPeriod", v)}
                        disabled={!!editing}
                        allowClear={false}
                        className="w-full"
                    />
                </FormField>

                <FormField
                    label="科目编码方案"
                    htmlFor="account-set-coding-scheme"
                    required
                    error={formErrors.codingScheme}
                    description={
                        '以 "-" 分隔各级科目编码的位数，如 4-3-2-2 表示一级 4 位、二级 3 位、三级 2 位、四级 2 位。最多支持 10 级。'
                    }
                >
                    <Input
                        id="account-set-coding-scheme"
                        value={formValues.codingScheme}
                        onChange={(e) => setFormValue("codingScheme", e.target.value)}
                        placeholder="4-3-2-2"
                    />
                </FormField>

                {editing && (
                    <>
                        <FormField label="纳税人识别号" htmlFor="account-set-taxpayer-number">
                            <Input
                                id="account-set-taxpayer-number"
                                value={formValues.taxpayerNumber ?? ""}
                                onChange={(e) => setFormValue("taxpayerNumber", e.target.value)}
                                placeholder="如 91330100MA27XXXXXX"
                                maxLength={50}
                            />
                        </FormField>

                        <div className="grid grid-cols-2 gap-3">
                            <FormField label="增值税率（%）" htmlFor="account-set-vat-rate">
                                <NumberField
                                    id="account-set-vat-rate"
                                    value={formValues.vatRate}
                                    onChange={(v) => setFormValue("vatRate", v)}
                                    min={0}
                                    max={100}
                                    precision={2}
                                />
                            </FormField>
                            <FormField label="所得税率（%）" htmlFor="account-set-income-tax-rate">
                                <NumberField
                                    id="account-set-income-tax-rate"
                                    value={formValues.incomeTaxRate}
                                    onChange={(v) => setFormValue("incomeTaxRate", v)}
                                    min={0}
                                    max={100}
                                    precision={2}
                                />
                            </FormField>
                        </div>

                        <FormField
                            label="凭证字"
                            htmlFor="account-set-voucher-words"
                            required
                            error={formErrors.voucherWords}
                            description="以逗号分隔多个凭证字，制单时只能从中选择，如：记,收,付,转。"
                        >
                            <Input
                                id="account-set-voucher-words"
                                value={formValues.voucherWords ?? ""}
                                onChange={(e) => setFormValue("voucherWords", e.target.value)}
                                placeholder="记,收,付,转"
                                maxLength={100}
                            />
                        </FormField>
                    </>
                )}
            </Drawer>

            {confirmDialog}
        </div>
    )
}
