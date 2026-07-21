import { useCallback, useEffect, useState } from "react"
import { Building2, Check, CheckCircle2, Copy, Edit3, Plus, RefreshCw, Trash2 } from "lucide-react"
import {
    Alert,
    Button,
    Card,
    Col,
    DatePicker,
    Drawer,
    Empty,
    Form,
    Input,
    Row,
    Select,
    Space,
    Spin,
    Tag,
    Typography,
    theme,
} from "antd"
import dayjs, { type Dayjs } from "dayjs"

import { useAuth } from "@/auth/AuthContext"
import { useAccountSets } from "@/accountSets/AccountSetsContext"
import { AccountSetsApi, type AccountSetDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

const { Title, Text } = Typography

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

function standardLabel(value?: number): string {
    return STANDARDS.find((s) => s.value === value)?.label ?? "—"
}

function taxpayerLabel(value?: number): string {
    return TAXPAYERS.find((t) => t.value === value)?.label ?? "—"
}

/** 新增表单字段。 */
interface CreateForm {
    name: string
    company: string
    standard: number
    currency: string
    taxpayer: number
    startPeriod: Dayjs
    codingScheme: string
}

export default function AccountSetsPage() {
    const { token } = useAuth()
    const { refresh: refreshAccountSets } = useAccountSets()
    const { token: t } = theme.useToken()

    const [sets, setSets] = useState<AccountSetDto[]>([])
    const [loading, setLoading] = useState(false)
    const [listError, setListError] = useState<string | null>(null)
    const [busyId, setBusyId] = useState<string | null>(null)

    const [submitting, setSubmitting] = useState(false)
    const [open, setOpen] = useState(false)
    const [editing, setEditing] = useState<AccountSetDto | null>(null)
    const [form] = Form.useForm<CreateForm>()

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
        if (!window.confirm(`确定删除账套「${set.name}」吗？此操作不可撤销。`)) return
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

    function openCreate() {
        setEditing(null)
        form.setFieldsValue({
            name: "",
            company: "",
            standard: STANDARDS[0].value,
            currency: "人民币",
            taxpayer: TAXPAYERS[0].value,
            startPeriod: dayjs(),
            codingScheme: "4-2-2-2",
        })
        setOpen(true)
    }

    function openEdit(set: AccountSetDto) {
        setEditing(set)
        form.setFieldsValue({
            name: set.name ?? "",
            company: set.companyName ?? "",
            standard: set.standard ?? STANDARDS[0].value,
            currency: set.baseCurrency ?? "人民币",
            taxpayer: set.taxpayerType ?? TAXPAYERS[0].value,
            startPeriod: set.startPeriod ? dayjs(set.startPeriod) : dayjs(),
            codingScheme: (set as any).accountCodingScheme ?? "4-2-2-2",
        })
        setOpen(true)
    }

    function openCopy(set: AccountSetDto) {
        setEditing(null)
        form.setFieldsValue({
            name: `${set.name ?? ""}（副本）`,
            company: set.companyName ?? "",
            standard: set.standard ?? STANDARDS[0].value,
            currency: set.baseCurrency ?? "人民币",
            taxpayer: set.taxpayerType ?? TAXPAYERS[0].value,
            startPeriod: set.startPeriod ? dayjs(set.startPeriod) : dayjs(),
            codingScheme: (set as any).accountCodingScheme ?? "4-2-2-2",
        })
        setOpen(true)
    }

    async function saveSet() {
        const values = await form.validateFields()
        const period = values.startPeriod
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
                        startYear: period.year(),
                        startMonth: period.month() + 1,
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
        <Space direction="vertical" size="large" style={{ display: "flex" }}>
            <div style={{ display: "flex", flexWrap: "wrap", alignItems: "flex-start", justifyContent: "space-between", gap: 12 }}>
                <div>
                    <Title level={3} style={{ margin: 0 }}>账套</Title>
                    <Text type="secondary">管理各单位的账簿账套，可在多套账之间切换当前账套。</Text>
                </div>
                <Button icon={<RefreshCw size={16} className={loading ? "spin" : undefined} />} onClick={() => void load()} loading={loading}>
                    刷新
                </Button>
            </div>

            <Card
                title="账套列表"
                extra={
                    <Button type="primary" icon={<Plus size={16} />} onClick={openCreate}>
                        新增账套
                    </Button>
                }
            >
                {listError && <Alert type="error" showIcon message={listError} style={{ marginBottom: 16 }} />}
                {loading && sets.length === 0 ? (
                    <div style={{ display: "flex", justifyContent: "center", padding: "40px 0" }}>
                        <Spin />
                    </div>
                ) : sets.length === 0 ? (
                    <Empty description="暂无账套，点击“新增账套”创建" />
                ) : (
                    <Row gutter={[16, 16]}>
                        {sets.map((set) => (
                            <Col key={set.id} xs={24} sm={12} xl={8}>
                                <Card
                                    size="small"
                                    style={{
                                        height: "100%",
                                        borderColor: set.isCurrent ? t.colorPrimary : undefined,
                                        borderLeftWidth: set.isCurrent ? 3 : undefined,
                                    }}
                                    styles={{ body: { display: "flex", flexDirection: "column", gap: 12, height: "100%" } }}
                                >
                                    <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 8 }}>
                                        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                                            <div
                                                style={{
                                                    display: "flex",
                                                    height: 36,
                                                    width: 36,
                                                    alignItems: "center",
                                                    justifyContent: "center",
                                                    borderRadius: t.borderRadius,
                                                    background: t.colorFillSecondary,
                                                    color: t.colorTextSecondary,
                                                }}
                                            >
                                                <Building2 size={20} />
                                            </div>
                                            <div style={{ display: "flex", flexDirection: "column" }}>
                                                <Text strong>{set.name}</Text>
                                                <Text type="secondary" style={{ fontSize: 12 }}>{set.companyName}</Text>
                                            </div>
                                        </div>
                                        {set.isCurrent && (
                                            <Tag color="blue" icon={<CheckCircle2 size={12} style={{ verticalAlign: "-2px" }} />}>
                                                当前
                                            </Tag>
                                        )}
                                    </div>

                                    <Row gutter={[8, 4]} style={{ fontSize: 12 }}>
                                        <Col span={12}><Text type="secondary">会计准则</Text></Col>
                                        <Col span={12} style={{ textAlign: "right" }}>{standardLabel(set.standard)}</Col>
                                        <Col span={12}><Text type="secondary">本位币</Text></Col>
                                        <Col span={12} style={{ textAlign: "right" }}>{set.baseCurrency}</Col>
                                        <Col span={12}><Text type="secondary">纳税人性质</Text></Col>
                                        <Col span={12} style={{ textAlign: "right" }}>{taxpayerLabel(set.taxpayerType)}</Col>
                                        <Col span={12}><Text type="secondary">启用期间</Text></Col>
                                        <Col span={12} style={{ textAlign: "right" }}>{set.startPeriod}</Col>
                                        <Col span={12}><Text type="secondary">编码方案</Text></Col>
                                        <Col span={12} style={{ textAlign: "right" }}>{(set as any).accountCodingScheme ?? "—"}</Col>
                                    </Row>

                                    <div style={{ marginTop: "auto", display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, paddingTop: 4 }}>
                                        <Button
                                            type={set.isCurrent ? "default" : "primary"}
                                            ghost={!set.isCurrent}
                                            size="small"
                                            disabled={set.isCurrent}
                                            loading={busyId === set.id}
                                            icon={<Check size={16} />}
                                            onClick={() => void setCurrent(set.id)}
                                        >
                                            {set.isCurrent ? "当前账套" : "设为当前"}
                                        </Button>
                                        <Space size={0}>
                                            <Button
                                                type="text"
                                                size="small"
                                                icon={<Edit3 size={14} />}
                                                onClick={() => openEdit(set)}
                                                title="编辑账套"
                                            />
                                            <Button
                                                type="text"
                                                size="small"
                                                icon={<Copy size={14} />}
                                                onClick={() => openCopy(set)}
                                                title="复制账套"
                                            />
                                            <Button
                                                type="text"
                                                size="small"
                                                danger
                                                disabled={set.isCurrent || busyId === set.id}
                                                icon={<Trash2 size={16} />}
                                                onClick={() => void removeSet(set)}
                                                title={set.isCurrent ? "当前账套不可删除" : "删除账套"}
                                            />
                                        </Space>
                                    </div>
                                </Card>
                            </Col>
                        ))}
                    </Row>
                )}
            </Card>

            <Drawer
                title={editing ? "编辑账套" : "新增账套"}
                open={open}
                onClose={() => setOpen(false)}
                width={480}
                footer={
                    <Space style={{ display: "flex", justifyContent: "flex-end" }}>
                        <Button onClick={() => setOpen(false)}>取消</Button>
                        <Button type="primary" icon={<Plus size={16} />} loading={submitting} onClick={() => void saveSet()}>
                            {editing ? "保存" : "创建账套"}
                        </Button>
                    </Space>
                }
            >
                <Text type="secondary">
                    {editing ? '修改账套基本信息后点击"保存"。' : '填写单位与核算信息后点击"创建账套"保存。'}
                </Text>
                <Form form={form} layout="vertical" style={{ marginTop: 16 }} requiredMark>
                    <Form.Item label="账套名称" name="name" rules={[{ required: true, message: "请填写账套名称" }]}>
                        <Input placeholder="如 华信科技 2026" />
                    </Form.Item>
                    <Form.Item label="单位名称" name="company" rules={[{ required: true, message: "请填写单位名称" }]}>
                        <Input placeholder="如 华信科技有限公司" />
                    </Form.Item>
                    <Form.Item label="会计准则" name="standard">
                        <Select options={STANDARDS} />
                    </Form.Item>
                    <Form.Item label="记账本位币" name="currency">
                        <Input placeholder="人民币" />
                    </Form.Item>
                    <Form.Item label="纳税人性质" name="taxpayer">
                        <Select options={TAXPAYERS} />
                    </Form.Item>
                    <Form.Item label="启用期间" name="startPeriod" rules={[{ required: true, message: "请选择启用期间" }]}>
                        <DatePicker picker="month" style={{ width: "100%" }} disabled={!!editing} />
                    </Form.Item>
                    <Form.Item
                        label="科目编码方案"
                        name="codingScheme"
                        rules={[
                            { required: true, message: "请填写科目编码方案" },
                            { pattern: /^\d{1,2}(-\d{1,2}){0,9}$/, message: "格式须如 4-2-2-2，每级 1-2 位数字，最多 10 级" },
                        ]}
                        extra={'以 "-" 分隔各级科目编码的位数，如 4-2-2-2 表示一级 4 位、二级 2 位、三级 2 位、四级 2 位。最多支持 10 级。'}
                    >
                        <Input placeholder="4-2-2-2" />
                    </Form.Item>
                </Form>
            </Drawer>
        </Space>
    )
}
