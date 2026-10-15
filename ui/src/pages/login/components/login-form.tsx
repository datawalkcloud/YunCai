import { useState } from "react"
import { useLocation, useNavigate } from "react-router"
import { Alert, Button, Form, Input } from "antd"

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

  async function handleSubmit(values: LoginFormValues) {
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
      onFinish={(values) => void handleSubmit(values)}
      className="flex flex-col gap-1 text-sm"
    >
      <div className="mb-4 flex flex-col items-center gap-1 text-center">
        <h1 className="text-lg font-semibold text-slate-800">登录你的账户</h1>
        <span className="text-slate-500">输入用户名和密码登录云财</span>
      </div>

      <Form.Item
        name="username"
        label="用户名"
        rules={[{ required: true, message: "请输入用户名" }]}
      >
        <Input id="username" autoComplete="username" placeholder="请输入用户名" size="large" />
      </Form.Item>

      <Form.Item
        name="password"
        label="密码"
        rules={[{ required: true, message: "请输入密码" }]}
      >
        <Input.Password
          id="password"
          autoComplete="current-password"
          placeholder="请输入密码"
          size="large"
        />
      </Form.Item>

      {error && <Alert className="mb-4" type="error" showIcon message={error} />}

      <Form.Item className="mb-0">
        <Button type="primary" htmlType="submit" size="large" block loading={submitting}>
          登录
        </Button>
      </Form.Item>
    </Form>
  )
}
