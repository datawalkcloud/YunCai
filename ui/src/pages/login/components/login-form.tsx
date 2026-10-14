import { useState } from "react"
import { useLocation, useNavigate } from "react-router"
import { CircleAlert } from "lucide-react"

import { useAuth } from "@/auth/AuthContext"
import { Alert, AlertTitle } from "@/components/ui/alert"
import { Button } from "@/components/ui/button"
import { Field, FieldError, FieldLabel } from "@/components/ui/field"
import { Input } from "@/components/ui/input"
import { Spinner } from "@/components/ui/spinner"

interface LoginFormValues {
  username: string
  password: string
}

type FieldErrors = Partial<Record<keyof LoginFormValues, string>>

export function LoginForm() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const [values, setValues] = useState<LoginFormValues>({ username: "", password: "" })
  const [errors, setErrors] = useState<FieldErrors>({})
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  // 登录成功后回到来源页；无来源时进入应用控制台。
  const from = (location.state as { from?: string } | null)?.from ?? "/app"

  function setValue(key: keyof LoginFormValues, value: string) {
    setValues((prev) => ({ ...prev, [key]: value }))
    setErrors((prev) => ({ ...prev, [key]: undefined }))
  }

  // 必填校验，提示文案与原表单的 required 规则逐条一致。
  function validate(): FieldErrors {
    const next: FieldErrors = {}
    if (!values.username) next.username = "请输入用户名"
    if (!values.password) next.password = "请输入密码"
    return next
  }

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (submitting) return

    const nextErrors = validate()
    setErrors(nextErrors)
    if (Object.values(nextErrors).some(Boolean)) return

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
    <form onSubmit={handleSubmit} className="flex flex-col gap-5 text-sm">
      <div className="flex flex-col items-center gap-1 text-center">
        <h1 className="text-lg font-semibold">登录你的账户</h1>
        <span className="text-muted-foreground">输入用户名和密码登录云财</span>
      </div>

      <Field data-invalid={Boolean(errors.username)} data-disabled={submitting}>
        <FieldLabel htmlFor="username">用户名</FieldLabel>
        <Input
          id="username"
          autoComplete="username"
          placeholder="请输入用户名"
          value={values.username}
          disabled={submitting}
          aria-invalid={Boolean(errors.username)}
          onChange={(e) => setValue("username", e.target.value)}
        />
        <FieldError>{errors.username}</FieldError>
      </Field>

      <Field data-invalid={Boolean(errors.password)} data-disabled={submitting}>
        <FieldLabel htmlFor="password">密码</FieldLabel>
        <Input
          id="password"
          type="password"
          autoComplete="current-password"
          placeholder="请输入密码"
          value={values.password}
          disabled={submitting}
          aria-invalid={Boolean(errors.password)}
          onChange={(e) => setValue("password", e.target.value)}
        />
        <FieldError>{errors.password}</FieldError>
      </Field>

      {error && (
        <Alert variant="destructive">
          <CircleAlert />
          <AlertTitle>{error}</AlertTitle>
        </Alert>
      )}

      <Button type="submit" size="lg" className="w-full" disabled={submitting}>
        {submitting && <Spinner data-icon="inline-start" />}
        {submitting ? "登录中…" : "登录"}
      </Button>
    </form>
  )
}
