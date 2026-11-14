import { useEffect, useState } from "react"
import { App, Button, Form, Input, Modal } from "antd"

import { useAuth } from "@/auth/AuthContext"

/** 已登录用户修改密码：成功后后端换发新令牌，本地登录态整体替换。 */
export function ChangePasswordModal({ open, onClose }: { open: boolean; onClose: () => void }) {
    const { user, changePassword } = useAuth()
    const { message } = App.useApp()
    const [form] = Form.useForm<{ current?: string; next: string; confirm: string }>()
    const [submitting, setSubmitting] = useState(false)

    useEffect(() => {
        if (open) form.resetFields()
    }, [open, form])

    async function handleOk() {
        const values = await form.validateFields()
        setSubmitting(true)
        try {
            await changePassword(user?.hasPassword ? (values.current ?? "") : null, values.next)
            message.success("密码已修改，其他设备的登录已失效。")
            onClose()
        } catch (err) {
            message.error(err instanceof Error ? err.message : "修改密码失败，请重试。")
        } finally {
            setSubmitting(false)
        }
    }

    return (
        <Modal
            open={open}
            title="修改密码"
            onCancel={onClose}
            onOk={() => void handleOk()}
            confirmLoading={submitting}
            okText="保存"
            cancelText="取消"
            destroyOnHidden
        >
            <Form form={form} layout="vertical" className="pt-2" onFinish={() => void handleOk()}>
                {user?.hasPassword && (
                    <Form.Item
                        name="current"
                        label="当前密码"
                        rules={[{ required: true, message: "请输入当前密码" }]}
                    >
                        <Input.Password autoComplete="current-password" maxLength={64} placeholder="请输入当前密码" />
                    </Form.Item>
                )}
                <Form.Item
                    name="next"
                    label="新密码"
                    rules={[
                        { required: true, message: "请输入新密码" },
                        { min: 8, max: 64, message: "密码长度需为 8–64 位" },
                    ]}
                >
                    <Input.Password autoComplete="new-password" maxLength={64} placeholder="至少 8 位" />
                </Form.Item>
                <Form.Item
                    name="confirm"
                    label="确认新密码"
                    dependencies={["next"]}
                    rules={[
                        { required: true, message: "请再次输入新密码" },
                        ({ getFieldValue }) => ({
                            validator(_, value) {
                                if (!value || getFieldValue("next") === value) return Promise.resolve()
                                return Promise.reject(new Error("两次输入的密码不一致"))
                            },
                        }),
                    ]}
                >
                    <Input.Password autoComplete="new-password" maxLength={64} placeholder="再次输入新密码" />
                </Form.Item>
                <Button htmlType="submit" className="hidden" />
            </Form>
        </Modal>
    )
}
