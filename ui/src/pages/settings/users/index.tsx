import { useCallback, useEffect, useMemo, useState } from "react"
import { Alert, App, Button, Card, Form, Input, Modal, Popconfirm, Space, Table, Tag } from "antd"
import type { TableColumnsType } from "antd"
import { PlusOutlined, ReloadOutlined, SearchOutlined } from "@ant-design/icons"
import dayjs from "dayjs"

import { useAuth } from "@/auth/AuthContext"
import {
    adminResetPassword,
    createUser,
    fetchUsers,
    setUserEnabled,
    updateUser,
    type AdminUserDto,
} from "@/api/security"
import { StatusPill } from "@/components/page/status-pill"

/** 系统管理员专用：登录账号的开户、停用 / 启用、重置密码。 */
export default function UsersPage() {
    const { token, user } = useAuth()
    const { message } = App.useApp()
    const [rows, setRows] = useState<AdminUserDto[]>([])
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [keyword, setKeyword] = useState("")
    const [busyId, setBusyId] = useState<string | null>(null)

    const [createOpen, setCreateOpen] = useState(false)
    const [editing, setEditing] = useState<AdminUserDto | null>(null)
    const [resetting, setResetting] = useState<AdminUserDto | null>(null)
    const [submitting, setSubmitting] = useState(false)
    const [createForm] = Form.useForm<{ displayName: string; phone: string; password?: string }>()
    const [editForm] = Form.useForm<{ displayName: string; phone?: string }>()
    const [resetForm] = Form.useForm<{ password: string; confirm: string }>()

    const load = useCallback(async () => {
        if (!token) return
        setLoading(true)
        setError(null)
        try {
            setRows(await fetchUsers(token, keyword))
        } catch (err) {
            setRows([])
            setError(err instanceof Error ? err.message : "加载用户列表失败，请重试。")
        } finally {
            setLoading(false)
        }
    }, [token, keyword])

    useEffect(() => {
        void load()
    }, [load])

    const toggleStatus = useCallback(
        async (row: AdminUserDto) => {
            if (!token) return
            setBusyId(row.id)
            try {
                await setUserEnabled(token, row.id, !row.isActive)
                message.success(row.isActive ? "账号已停用" : "账号已启用")
                await load()
            } catch (err) {
                message.error(err instanceof Error ? err.message : "操作失败，请重试。")
            } finally {
                setBusyId(null)
            }
        },
        [token, message, load],
    )

    const columns: TableColumnsType<AdminUserDto> = useMemo(
        () => [
            {
                title: "姓名",
                dataIndex: "displayName",
                width: 160,
                render: (_, row) => (
                    <span className="flex items-center gap-2">
                        <span className="truncate font-medium text-(--foreground)">{row.displayName || "—"}</span>
                        {row.isSystemAdmin && <Tag color="blue">系统管理员</Tag>}
                    </span>
                ),
            },
            {
                title: "手机号",
                dataIndex: "phone",
                width: 150,
                render: (_, row) => (
                    <span className="tabular-nums text-(--foreground)">{row.phone || row.username}</span>
                ),
            },
            {
                title: "状态",
                dataIndex: "isActive",
                width: 100,
                render: (_, row) =>
                    row.isActive ? <StatusPill tone="green">启用</StatusPill> : <StatusPill tone="gray">已停用</StatusPill>,
            },
            {
                title: "密码",
                dataIndex: "hasPassword",
                width: 100,
                render: (_, row) =>
                    row.hasPassword ? (
                        <span className="text-(--muted-foreground)">已设置</span>
                    ) : (
                        <span className="text-(--danger)">未设置</span>
                    ),
            },
            {
                title: "最后登录",
                dataIndex: "lastLoginAt",
                width: 170,
                render: (_, row) => (
                    <span className="text-(--muted-foreground)">
                        {row.lastLoginAt ? dayjs(row.lastLoginAt).format("YYYY-MM-DD HH:mm") : "从未登录"}
                    </span>
                ),
            },
            {
                title: "创建时间",
                dataIndex: "createdAt",
                width: 120,
                render: (_, row) => (
                    <span className="text-(--muted-foreground)">
                        {row.createdAt ? dayjs(row.createdAt).format("YYYY-MM-DD") : "—"}
                    </span>
                ),
            },
            {
                title: "操作",
                key: "actions",
                width: 210,
                render: (_, row) => {
                    const isSelf = row.id === user?.id
                    return (
                        <Space size={4}>
                            <Button
                                type="link"
                                size="small"
                                onClick={() => {
                                    setEditing(row)
                                    editForm.setFieldsValue({ displayName: row.displayName, phone: row.phone ?? "" })
                                }}
                            >
                                编辑
                            </Button>
                            <Button type="link" size="small" onClick={() => setResetting(row)}>
                                重置密码
                            </Button>
                            <Popconfirm
                                title={row.isActive ? "停用该账号？" : "启用该账号？"}
                                description={row.isActive ? "停用后该账号已登录的会话将立即失效。" : "启用后可用手机号验证码或密码登录。"}
                                okText="确定"
                                cancelText="取消"
                                disabled={isSelf}
                                onConfirm={() => void toggleStatus(row)}
                            >
                                <Button type="link" size="small" danger={row.isActive} disabled={isSelf || busyId === row.id}>
                                    {row.isActive ? "停用" : "启用"}
                                </Button>
                            </Popconfirm>
                        </Space>
                    )
                },
            },
        ],
        [busyId, editForm, user?.id, toggleStatus],
    )

    async function submitCreate() {
        const values = await createForm.validateFields()
        if (!token) return
        setSubmitting(true)
        try {
            await createUser(token, values)
            message.success("账号已创建")
            setCreateOpen(false)
            createForm.resetFields()
            await load()
        } catch (err) {
            message.error(err instanceof Error ? err.message : "创建账号失败，请重试。")
        } finally {
            setSubmitting(false)
        }
    }

    async function submitEdit() {
        if (!token || !editing) return
        const values = await editForm.validateFields()
        setSubmitting(true)
        try {
            await updateUser(token, editing.id, values)
            message.success("资料已保存")
            setEditing(null)
            await load()
        } catch (err) {
            message.error(err instanceof Error ? err.message : "保存失败，请重试。")
        } finally {
            setSubmitting(false)
        }
    }

    async function submitReset() {
        if (!token || !resetting) return
        const values = await resetForm.validateFields()
        setSubmitting(true)
        try {
            await adminResetPassword(token, resetting.id, values.password)
            message.success("密码已重置，该账号已登录的会话已失效")
            setResetting(null)
            resetForm.resetFields()
        } catch (err) {
            message.error(err instanceof Error ? err.message : "重置密码失败，请重试。")
        } finally {
            setSubmitting(false)
        }
    }

    if (!user?.isSystemAdmin) {
        return (
            <Card>
                <Alert type="warning" showIcon title="仅系统管理员可以管理登录账号。" />
            </Card>
        )
    }

    return (
        <Card>
            <div className="flex flex-wrap items-start justify-between gap-3 px-4 pt-4">
                <div className="min-w-0">
                    <h2 className="text-[15px] font-semibold text-(--foreground)">用户管理</h2>
                    <p className="mt-1 text-[12.5px] leading-5 text-(--muted-foreground)">
                        管理可登录系统的手机号账号。停用或重置密码会立即吊销该账号已签发的令牌；进入哪个账套仍由【权限设置】里的账套成员决定。
                    </p>
                </div>
                <Space>
                    <Input
                        allowClear
                        style={{ width: 220 }}
                        prefix={<SearchOutlined style={{ color: "var(--icon-muted)" }} />}
                        placeholder="搜索姓名 / 手机号"
                        value={keyword}
                        onChange={(e) => setKeyword(e.target.value)}
                    />
                    <Button icon={<ReloadOutlined spin={loading} />} onClick={() => void load()}>
                        刷新
                    </Button>
                    <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>
                        新增用户
                    </Button>
                </Space>
            </div>

            {error && (
                <div className="px-4 pb-3">
                    <Alert type="error" showIcon title={error} closable onClose={() => setError(null)} />
                </div>
            )}

            <div className="px-3 pt-3">
                <Table
                    rowKey="id"
                    size="middle"
                    columns={columns}
                    dataSource={rows}
                    loading={loading}
                    scroll={{ x: 1000 }}
                    pagination={{ pageSize: 20, showTotal: (total) => `共 ${total} 个账号` }}
                />
            </div>

            <Modal
                open={createOpen}
                title="新增用户"
                okText="创建"
                cancelText="取消"
                confirmLoading={submitting}
                onOk={() => void submitCreate()}
                onCancel={() => setCreateOpen(false)}
                destroyOnHidden
            >
                <Form form={createForm} layout="vertical" className="pt-2">
                    <Form.Item name="displayName" label="姓名" rules={[{ required: true, message: "请填写姓名" }]}>
                        <Input maxLength={50} placeholder="用于界面显示与操作日志" />
                    </Form.Item>
                    <Form.Item
                        name="phone"
                        label="手机号"
                        rules={[
                            { required: true, message: "请填写手机号" },
                            { pattern: /^1\d{10}$/, message: "请输入 11 位手机号" },
                        ]}
                    >
                        <Input maxLength={11} placeholder="手机号即登录账号" />
                    </Form.Item>
                    <Form.Item
                        name="password"
                        label="初始密码（可留空）"
                        rules={[{ min: 8, max: 64, message: "密码长度需为 8–64 位" }]}
                    >
                        <Input.Password maxLength={64} placeholder="留空则用户首次用验证码登录后自行设置" />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                open={editing !== null}
                title="编辑用户"
                okText="保存"
                cancelText="取消"
                confirmLoading={submitting}
                onOk={() => void submitEdit()}
                onCancel={() => setEditing(null)}
                destroyOnHidden
            >
                <Form form={editForm} layout="vertical" className="pt-2">
                    <Form.Item name="displayName" label="姓名" rules={[{ required: true, message: "请填写姓名" }]}>
                        <Input maxLength={50} />
                    </Form.Item>
                    <Form.Item
                        name="phone"
                        label="手机号"
                        rules={[{ pattern: /^1\d{10}$/, message: "请输入 11 位手机号" }]}
                    >
                        <Input maxLength={11} />
                    </Form.Item>
                </Form>
            </Modal>

            <Modal
                open={resetting !== null}
                title={`重置密码${resetting?.displayName ? `：${resetting.displayName}` : ""}`}
                okText="重置"
                cancelText="取消"
                confirmLoading={submitting}
                onOk={() => void submitReset()}
                onCancel={() => setResetting(null)}
                destroyOnHidden
            >
                <Form form={resetForm} layout="vertical" className="pt-2">
                    <Form.Item
                        name="password"
                        label="新密码"
                        rules={[
                            { required: true, message: "请输入新密码" },
                            { min: 8, max: 64, message: "密码长度需为 8–64 位" },
                        ]}
                    >
                        <Input.Password maxLength={64} autoComplete="new-password" />
                    </Form.Item>
                    <Form.Item
                        name="confirm"
                        label="确认新密码"
                        dependencies={["password"]}
                        rules={[
                            { required: true, message: "请再次输入新密码" },
                            ({ getFieldValue }) => ({
                                validator(_, value) {
                                    if (!value || getFieldValue("password") === value) return Promise.resolve()
                                    return Promise.reject(new Error("两次输入的密码不一致"))
                                },
                            }),
                        ]}
                    >
                        <Input.Password maxLength={64} autoComplete="new-password" />
                    </Form.Item>
                </Form>
            </Modal>
        </Card>
    )
}
