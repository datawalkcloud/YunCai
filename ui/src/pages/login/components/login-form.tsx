import { useState } from "react"
import { useLocation, useNavigate } from "react-router"
import { LockOutlined, UserOutlined } from "@ant-design/icons"
import { Alert, Button, Form, Input, Typography } from "antd"

import { useAuth } from "@/auth/AuthContext"

interface LoginFormValues {
  username: string
  password: string
}

export function LoginForm() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // 登录成功后回到来源页；无来源时进入应用控制台。
  const from = (location.state as { from?: string } | null)?.from ?? "/app"

  async function handleFinish(values: LoginFormValues) {
    if (submitting) return

    setError(null)
    setSubmitting(true)
    try {
      await login(values.username.trim(), values.password)
      navigate(from, { replace: true })
    } catch (err) {
      setError(err instanceof Error ? err.message : "登录失败，请重试")
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Form<LoginFormValues>
      layout="vertical"
      requiredMark={false}
      onFinish={handleFinish}
      disabled={submitting}
      size="large"
    >
      <div style={{ textAlign: "center", marginBottom: 24 }}>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          登录你的账户
        </Typography.Title>
        <Typography.Text type="secondary">输入用户名和密码登录好会计</Typography.Text>
      </div>

      <Form.Item
        name="username"
        label="用户名"
        rules={[{ required: true, message: "请输入用户名" }]}
      >
        <Input
          prefix={<UserOutlined />}
          autoComplete="username"
          placeholder="admin"
        />
      </Form.Item>

      <Form.Item
        name="password"
        label="密码"
        rules={[{ required: true, message: "请输入密码" }]}
      >
        <Input.Password
          prefix={<LockOutlined />}
          autoComplete="current-password"
          placeholder="请输入密码"
        />
      </Form.Item>

      {error && (
        <Form.Item>
          <Alert type="error" message={error} showIcon />
        </Form.Item>
      )}

      <Form.Item style={{ marginBottom: 12 }}>
        <Button type="primary" htmlType="submit" block loading={submitting}>
          {submitting ? "登录中…" : "登录"}
        </Button>
      </Form.Item>

      <Typography.Paragraph
        type="secondary"
        style={{ textAlign: "center", fontSize: 12, marginBottom: 0 }}
      >
        演示账号：admin / admin123，user / user123
      </Typography.Paragraph>
    </Form>
  )
}
