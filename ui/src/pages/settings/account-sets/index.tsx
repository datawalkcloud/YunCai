import { useCallback, useEffect, useState, type ReactNode } from "react"
import { App, Alert, Button, Drawer, Empty, Spin, Tabs } from "antd"
import {
    BankOutlined,
    CheckCircleFilled,
    CheckOutlined,
    CopyOutlined,
    EditOutlined,
    PlusOutlined,
    ReloadOutlined,
    DeleteOutlined,
} from "@ant-design/icons"
import dayjs from "dayjs"
import { useNavigate } from "react-router"

import { useAuth } from "@/auth/AuthContext"
import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { AccountSetsApi, type AccountSetDto } from "@/apis-gen"
import { apiConfig, apiConfigWithHeaders, apiErrorMessage } from "@/lib/api-client"
import { useConfirm } from "@/hooks/useConfirm"
import { useStepUp } from "@/lib/step-up"
import { cn } from "@/lib/utils"
import { SectionCard } from "@/components/page/section"
import { parseDate } from "@/components/fields/date-fields"
import { StatusPill } from "@/components/page/status-pill"
import { ICON_STYLE } from "@/components/page/styles"
import {
    BasicInfoFields,
    FunctionParamsFields,
    TaxInfoFields,
    type AccountSetFieldsProps,
} from "./account-set-form"
import {
    FORM_TABS,
    emptyForm,
    formFromDto,
    standardLabel,
    tabOfField,
    taxpayerLabel,
    toCreateRequest,
    validateAccountSet,
    type AccountSetErrors,
    type AccountSetForm,
} from "./account-set-form-data"

/** 卡片内的键值行，替代原嵌套 Row/Col 的两列布局。 */
function KeyValue({ label, children }: { label: string, children: ReactNode }) {
    return (
        <div className="flex items-start justify-between gap-2 text-[12.5px]">
            <span className="shrink-0 text-(--muted-foreground)">{label}</span>
            <span className="min-w-0 text-right text-(--foreground)">{children}</span>
        </div>
    )
}

/** 三页表单共用同一份受控字段集，按页签挑要渲染的那一页。 */
function renderTab(key: string, fields: AccountSetFieldsProps): ReactNode {
    if (key === "function") return <FunctionParamsFields {...fields} />
    if (key === "tax") return <TaxInfoFields {...fields} />
    return <BasicInfoFields {...fields} />
}

export default function AccountSetsPage() {
    const { token } = useAuth()
    const { refresh: refreshAccountSets } = useAccountSets()
    const currentAccountSetId = useCurrentAccountSetId()
    const { confirm, confirmDialog } = useConfirm()
    const { run: withStepUp } = useStepUp()
    const { message } = App.useApp()
    const navigate = useNavigate()

    const [sets, setSets] = useState<AccountSetDto[]>([])
    const [loading, setLoading] = useState(false)
    const [listError, setListError] = useState<string | null>(null)
    const [busyId, setBusyId] = useState<string | null>(null)

    const [submitting, setSubmitting] = useState(false)
    const [open, setOpen] = useState(false)
    const [editing, setEditing] = useState<AccountSetDto | null>(null)
    const [formValues, setFormValues] = useState<AccountSetForm>(emptyForm)
    const [formErrors, setFormErrors] = useState<AccountSetErrors>({})
    const [activeTab, setActiveTab] = useState<string>("basic")

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
        setActiveTab("basic")
    }

    /** 改单个字段，对应原控件的 onChange；已有错误时即时重算。 */
    function setFormValue<K extends keyof AccountSetForm>(key: K, value: AccountSetForm[K]) {
        const next: AccountSetForm = { ...formValues, [key]: value }
        setFormValues(next)
        if (Object.keys(formErrors).length > 0) setFormErrors(validateAccountSet(next, !!editing))
    }

    /** 提交前校验：有错时把抽屉切到出错字段所在的页签，避免改了 A 页看不见 B 页的红字。 */
    function checkForm() {
        const next = validateAccountSet(formValues, !!editing)
        setFormErrors(next)
        const first = Object.keys(next)[0] as keyof AccountSetForm | undefined
        if (first) setActiveTab(tabOfField(first))
        return !first
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

    /** 删除账套（连同其下全部数据）；删的是当前账套时跳到剩下的第一套。 */
    async function removeSet(set: AccountSetDto) {
        if (!set.id || busyId) return
        const id = set.id
        const name = set.name ?? ""
        if (!(await confirm(
            "删除后会一并清除该账套的科目、凭证、账簿、资金、发票、工资、资产、档案等全部数据，且无法恢复。",
            { title: `删除账套「${name}」`, confirmText: "删除", requireText: name },
        ))) return
        setBusyId(set.id)
        setListError(null)
        try {
            await withStepUp((h) =>
                new AccountSetsApi(apiConfigWithHeaders(token, h)).accountSetsIdDelete({ id }),
            )
            message.success(`账套「${name}」已删除`)
            await load()
            refreshAccountSets()
            if (currentAccountSetId === set.id) {
                const next = sets.find((s) => s.id !== set.id)
                navigate(next?.id ? `/${next.id}/settings/account-sets` : "/app", { replace: true })
            }
        } catch (err) {
            message.error(await apiErrorMessage(err, "删除账套失败，请重试。"))
        } finally {
            setBusyId(null)
        }
    }

    function openCreate() {
        setEditing(null)
        fillForm({ ...emptyForm(), startPeriod: dayjs().toDate() })
        setOpen(true)
    }

    function openEdit(set: AccountSetDto) {
        setEditing(set)
        fillForm(formFromDto(set))
        setOpen(true)
    }

    function openCopy(set: AccountSetDto) {
        setEditing(null)
        fillForm({
            ...formFromDto(set),
            name: `${set.name ?? ""}（副本）`,
            startPeriod: set.startPeriod ? parseDate(set.startPeriod) : dayjs().toDate(),
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
                // 编辑：三页字段一次提交，未改动的密码不传即保持原值。
                await api.accountSetsIdPut({
                    id: editing.id!,
                    updateAccountSetRequest: {
                        name: values.name.trim(),
                        companyName: values.company.trim(),
                        standard: values.standard,
                        taxpayerType: values.taxpayer,
                        baseCurrency: values.currency.trim() || "人民币",
                        accountCodingScheme: values.codingScheme?.trim() || undefined,
                        industry: values.industry ?? "",
                        assetsModuleEnabled: values.assetsEnabled,
                        fundsModuleEnabled: values.fundsEnabled,
                        balanceAssistScope: values.balanceAssistScope,
                        unitDecimalPlaces: values.unitDecimals,
                        taxpayerNumber: values.taxpayerNumber?.trim() || undefined,
                        vatRate: values.vatRate ?? undefined,
                        incomeTaxRate: values.incomeTaxRate ?? undefined,
                        cityBuildTaxRate: values.cityBuildTaxRate ?? undefined,
                        educationSurchargeRate: values.educationSurchargeRate ?? undefined,
                        localEducationSurchargeRate: values.localEducationSurchargeRate ?? undefined,
                        isExportEnterprise: values.isExport ?? false,
                        taxRegion: values.taxRegion || undefined,
                        taxLoginType: values.taxLoginType || undefined,
                        taxIdentityType: values.taxIdentityType || undefined,
                        taxLoginAccount: values.taxLoginAccount?.trim() || undefined,
                        // 密码留空表示不修改：已保存的密码不会因为打开过一次编辑抽屉就被清掉。
                        taxLoginPassword: values.taxLoginPassword?.length ? values.taxLoginPassword : undefined,
                        iitChannel: values.iitChannel || undefined,
                        iitLoginType: values.iitLoginType || undefined,
                        iitLoginAccount: values.iitAccount?.trim() || undefined,
                        iitLoginPassword: values.iitPassword?.length ? values.iitPassword : undefined,
                    },
                })
            } else {
                // 新增（含复制）
                await api.accountSetsPost({ createAccountSetRequest: toCreateRequest(values) })
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

    const fields: AccountSetFieldsProps = {
        values: formValues,
        errors: formErrors,
        editing: !!editing,
        // 只有小企业准则账套能切换（升级为企业准则）；企业准则与历史其他准则一律锁定。
        standardLocked: !!editing && editing.standard !== 2,
        onChange: setFormValue,
    }

    return (
        <div className="flex w-full flex-col gap-5">
            <SectionCard
                title={<h3 className="text-[14px] font-semibold text-(--foreground)">账套列表</h3>}
                action={
                    <div className="flex items-center gap-2">
                        <Button
                            icon={loading ? <ReloadOutlined spin /> : <ReloadOutlined />}
                            disabled={loading}
                            onClick={() => void load()}
                        >
                            刷新
                        </Button>
                        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
                            新建账套
                        </Button>
                    </div>
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
                        description="暂无账套，点击“新建账套”创建"
                    />
                ) : (
                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
                        {sets.map((set) => (
                            <div
                                key={set.id}
                                className={cn(
                                    "flex flex-col gap-3 rounded-2xl border bg-(--card) p-4 shadow-[0_1px_2px_rgba(16,24,40,0.04)]",
                                    set.isCurrent ? "border-(--brand-border) ring-1 ring-(--brand-border)" : "border-(--border)",
                                )}
                            >
                                <div className="flex items-start justify-between gap-2">
                                    <div className="flex min-w-0 items-center gap-2">
                                        <div className="flex size-9 shrink-0 items-center justify-center bg-(--brand-soft) text-[20px] text-(--brand)">
                                            <BankOutlined />
                                        </div>
                                        <div className="flex min-w-0 flex-col">
                                            <span className="truncate text-[13px] font-medium text-(--foreground)">{set.name}</span>
                                            <span className="truncate text-[12px] text-(--muted-foreground)">{set.companyName}</span>
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
                                    <KeyValue label="报税地区">{set.taxRegion || "—"}</KeyValue>
                                    <KeyValue label="增值税 / 所得税">{set.vatRate ?? "—"}% / {set.incomeTaxRate ?? "—"}%</KeyValue>
                                    <KeyValue label="附加税（城建 / 教育 / 地方）">
                                        {set.cityBuildTaxRate ?? "—"}% / {set.educationSurchargeRate ?? "—"}% /{" "}
                                        {set.localEducationSurchargeRate ?? "—"}%
                                    </KeyValue>
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
                                            onClick={() => openEdit(set)}
                                            title="编辑账套"
                                            icon={<EditOutlined />}
                                        />
                                        <Button
                                            type="text"
                                            onClick={() => openCopy(set)}
                                            title="复制账套"
                                            icon={<CopyOutlined />}
                                        />
                                        <Button
                                            type="text"
                                            danger
                                            disabled={busyId === set.id}
                                            onClick={() => void removeSet(set)}
                                            title="删除账套"
                                            icon={<DeleteOutlined />}
                                        />
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                )}
            </SectionCard>

            {/* 新增 / 编辑账套：编辑态按「基本信息 / 功能参数 / 税务信息」三页组织 */}
            <Drawer
                open={open}
                size={editing ? 700 : 512}
                title={editing ? "编辑账套" : "新增账套"}
                onClose={() => setOpen(false)}
                styles={{ body: { display: "flex", flexDirection: "column" } }}
                footer={
                    <div className="flex justify-end gap-2">
                        <Button onClick={() => setOpen(false)}>取消</Button>
                        <Button type="primary" loading={submitting} onClick={() => void saveSet()}>
                            {editing ? "保存账套" : "创建账套"}
                        </Button>
                    </div>
                }
            >
                {editing ? (
                    <Tabs
                        activeKey={activeTab}
                        onChange={setActiveTab}
                        items={FORM_TABS.map((tab) => ({
                            key: tab.key,
                            label: tab.label,
                            children: renderTab(tab.key, fields),
                        }))}
                    />
                ) : (
                    <div className="pt-1">
                        <BasicInfoFields {...fields} />
                    </div>
                )}
            </Drawer>

            {confirmDialog}
        </div>
    )
}
