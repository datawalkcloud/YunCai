import { useCallback, useEffect, useState } from "react"
import { Pencil, Plus, RefreshCw, Tags, Trash2 } from "lucide-react"
import { Alert, Button, Card, Form, Input, Modal, Radio, Space, Table, Tag, Typography } from "antd"
import type { ColumnsType } from "antd/es/table"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { EntryCategoriesApi, type EntryCategoryDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"

const { Title, Text } = Typography

/** 新建/编辑类别表单值。 */
interface CategoryFormValues {
    type: 1 | 2
    name: string
}

export default function EntryCategoriesPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    const [categories, setCategories] = useState<EntryCategoryDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    // ── 类别新建/编辑 ──
    const [modalOpen, setModalOpen] = useState(false)
    const [editing, setEditing] = useState<EntryCategoryDto | null>(null)
    const [saving, setSaving] = useState(false)
    const [form] = Form.useForm<CategoryFormValues>()

    /** 加载当前账套的全部收支类别。 */
    const loadCategories = useCallback(async () => {
        if (!accountSetId) return
        setLoading(true)
        setError(null)
        try {
            const list = await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesGet({ accountSetId })
            setCategories(list)
        } catch (err) {
            setError(await apiErrorMessage(err, "加载收支类别失败，请重试。"))
            setCategories([])
        } finally {
            setLoading(false)
        }
    }, [accountSetId, token])

    useEffect(() => {
        void loadCategories()
    }, [loadCategories])

    function openCreate() {
        setEditing(null)
        form.setFieldsValue({ type: 1, name: "" })
        setModalOpen(true)
    }

    function openEdit(c: EntryCategoryDto) {
        setEditing(c)
        form.setFieldsValue({ type: (c.type as 1 | 2) ?? 1, name: c.name ?? "" })
        setModalOpen(true)
    }

    async function handleSubmit() {
        const values = await form.validateFields()
        setSaving(true)
        setError(null)
        try {
            const api = new EntryCategoriesApi(apiConfig(token))
            if (editing?.id) {
                await api.accountsetsAccountSetIdEntrycategoriesIdPut({
                    accountSetId,
                    id: editing.id,
                    updateEntryCategoryRequest: { name: values.name },
                })
            } else {
                await api.accountsetsAccountSetIdEntrycategoriesPost({
                    accountSetId,
                    createEntryCategoryRequest: { type: values.type, name: values.name },
                })
            }
            setModalOpen(false)
            await loadCategories()
        } catch (err) {
            setError(await apiErrorMessage(err, editing ? "保存类别失败，请重试。" : "新建类别失败，请重试。"))
        } finally {
            setSaving(false)
        }
    }

    async function handleDelete(c: EntryCategoryDto) {
        if (!c.id) return
        if (!window.confirm(`确定删除${c.typeText}类别「${c.name}」吗？既有流水中已登记的类别不受影响。`)) return
        setError(null)
        try {
            await new EntryCategoriesApi(apiConfig(token)).accountsetsAccountSetIdEntrycategoriesIdDelete({ accountSetId, id: c.id })
            await loadCategories()
        } catch (err) {
            setError(await apiErrorMessage(err, "删除类别失败，请重试。"))
        }
    }

    const columns: ColumnsType<EntryCategoryDto> = [
        {
            title: "类型",
            dataIndex: "type",
            width: 100,
            filters: [
                { text: "收入", value: 1 },
                { text: "支出", value: 2 },
            ],
            onFilter: (value, r) => r.type === value,
            render: (v: number) => <Tag color={v === 1 ? "green" : "volcano"}>{v === 1 ? "收入" : "支出"}</Tag>,
        },
        { title: "类别名称", dataIndex: "name", ellipsis: true },
        {
            title: "操作",
            key: "actions",
            width: 100,
            render: (_: unknown, r: EntryCategoryDto) => (
                <Space size={0}>
                    <Button type="text" size="small" icon={<Pencil size={16} />} onClick={() => openEdit(r)} title="编辑类别" />
                    <Button type="text" size="small" danger icon={<Trash2 size={16} />} onClick={() => void handleDelete(r)} title="删除类别" />
                </Space>
            ),
        },
    ]

    return (
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
            <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                    <Title level={3} style={{ marginBottom: 4 }}>收支类别</Title>
                    <Text type="secondary">
                        账套：{currentSet?.name ?? (accountSetId || "未指定")}
                        。维护记账时可选择的收入 / 支出类别，日记账登记流水时按类别归类统计。
                    </Text>
                </div>
                <Space>
                    <Button icon={<RefreshCw size={16} />} onClick={() => void loadCategories()} loading={loading} disabled={!accountSetId}>
                        刷新
                    </Button>
                    <Button type="primary" icon={<Plus size={16} />} onClick={openCreate} disabled={!accountSetId}>
                        新建类别
                    </Button>
                </Space>
            </div>

            {error && <Alert type="error" message={error} showIcon closable onClose={() => setError(null)} />}

            {categories.length === 0 && !loading ? (
                <Card>
                    <div className="flex flex-col items-center gap-3 py-10">
                        <Tags size={40} strokeWidth={1.2} />
                        <Text type="secondary">
                            {accountSetId ? "还没有收支类别。点击「新建类别」添加，如「销售收入」「办公费」。" : "请先选择账套。"}
                        </Text>
                        {accountSetId && (
                            <Button type="primary" icon={<Plus size={16} />} onClick={openCreate}>
                                新建类别
                            </Button>
                        )}
                    </div>
                </Card>
            ) : (
                <Card>
                    <Table<EntryCategoryDto>
                        rowKey={(r) => r.id ?? r.name ?? ""}
                        columns={columns}
                        dataSource={categories}
                        loading={loading}
                        pagination={false}
                        size="small"
                    />
                </Card>
            )}

            {/* 新建/编辑类别 */}
            <Modal
                title={editing ? "编辑类别" : "新建类别"}
                open={modalOpen}
                onOk={() => void handleSubmit()}
                confirmLoading={saving}
                onCancel={() => setModalOpen(false)}
                okText={editing ? "保存" : "创建"}
                destroyOnHidden
            >
                <Form form={form} layout="vertical" requiredMark={false}>
                    <Form.Item name="type" label="类型" rules={[{ required: true }]}>
                        <Radio.Group
                            disabled={!!editing}
                            options={[
                                { label: "收入", value: 1 },
                                { label: "支出", value: 2 },
                            ]}
                        />
                    </Form.Item>
                    <Form.Item
                        name="name"
                        label="类别名称"
                        rules={[{ required: true, whitespace: true, message: "请填写类别名称，如「销售收入」「办公费」。" }]}
                    >
                        <Input placeholder="如：销售收入、办公费、差旅费" maxLength={50} />
                    </Form.Item>
                    {editing && (
                        <Text type="secondary" style={{ fontSize: 12 }}>
                            改名不影响既有流水中已登记的类别文本。
                        </Text>
                    )}
                </Form>
            </Modal>
        </Space>
    )
}
