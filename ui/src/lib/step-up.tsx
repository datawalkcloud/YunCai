import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from "react"
import { App, Button, Form, Input, Modal, Segmented } from "antd"

import { sendSmsCode } from "@/api/auth"
import { requestStepUpToken } from "@/api/security"
import { useAuth } from "@/auth/AuthContext"

/* ------------------------------------------------------------------ */
/*  高危操作二次确认                                                     */
/*                                                                      */
/*  后端对结账、反结账、删除凭证、重新初始化、恢复备份等端点要求请求头    */
/*  X-Step-Up-Token；未携带或已过期时返回 428 + code=step_up_required。   */
/*  这里统一弹窗换取令牌（密码或短信验证码），5 分钟内复用，             */
/*  并在首次 428 后自动重试原请求一次。                                   */
/* ------------------------------------------------------------------ */

/** 判断错误是否为「需要二次确认」（生成客户端 ResponseError 与手写 fetch 均覆盖）。 */
export function isStepUpRequired(err: unknown): boolean {
    if (!err || typeof err !== "object") return false
    const e = err as { status?: number; response?: { status?: number } }
    return e.status === 428 || e.response?.status === 428
}

type ExtraHeaders = Record<string, string>

interface StepUpContextValue {
    /** 执行高危请求；首次 428 时弹窗确认，拿到令牌后自动重试一次。 */
    run: <T>(action: (headers: ExtraHeaders) => Promise<T>) => Promise<T>
}

const StepUpContext = createContext<StepUpContextValue | null>(null)

/** 令牌复用窗口略短于后端 5 分钟，避免每次点按钮都重新验证。 */
const TOKEN_REUSE_MS = 4 * 60 * 1000

export function StepUpProvider({ children }: { children: ReactNode }) {
    const { token, user } = useAuth()
    const cached = useRef<{ value: string; expiresAt: number } | null>(null)
    const [open, setOpen] = useState(false)
    const pending = useRef<{ resolve: (value: string) => void; reject: (reason: unknown) => void } | null>(null)

    const requestToken = useCallback(
        () =>
            new Promise<string>((resolve, reject) => {
                pending.current = { resolve, reject }
                setOpen(true)
            }),
        [],
    )

    const run = useCallback(
        async <T,>(action: (headers: ExtraHeaders) => Promise<T>): Promise<T> => {
            const headers: ExtraHeaders = {}
            if (cached.current && cached.current.expiresAt > Date.now()) {
                headers["X-Step-Up-Token"] = cached.current.value
            }

            try {
                return await action(headers)
            } catch (err) {
                if (!isStepUpRequired(err)) throw err
                const value = await requestToken()
                cached.current = { value, expiresAt: Date.now() + TOKEN_REUSE_MS }
                return await action({ "X-Step-Up-Token": value })
            }
        },
        [requestToken],
    )

    const value = useMemo(() => ({ run }), [run])

    function settle(value?: string, error?: unknown) {
        const entry = pending.current
        pending.current = null
        setOpen(false)
        if (!entry) return
        if (value) entry.resolve(value)
        else entry.reject(error ?? new Error("已取消二次确认。"))
    }

    return (
        <StepUpContext value={value}>
            {children}
            <StepUpModal
                open={open}
                token={token}
                hasPassword={user?.hasPassword ?? false}
                phone={user?.phone ?? null}
                onCancel={() => settle(undefined, new Error("已取消二次确认。"))}
                onConfirmed={(stepUpToken) => settle(stepUpToken)}
            />
        </StepUpContext>
    )
}

export function useStepUp(): StepUpContextValue {
    const ctx = useContext(StepUpContext)
    if (!ctx) throw new Error("useStepUp 必须在 StepUpProvider 内使用")
    return ctx
}

function StepUpModal({
    open,
    token,
    hasPassword,
    phone,
    onCancel,
    onConfirmed,
}: {
    open: boolean
    token: string | null
    hasPassword: boolean
    phone: string | null
    onCancel: () => void
    onConfirmed: (stepUpToken: string) => void
}) {
    const { message } = App.useApp()
    // 有密码默认密码验证；纯验证码账号只能走短信。
    const [mode, setMode] = useState<"password" | "sms">(hasPassword ? "password" : "sms")
    const [password, setPassword] = useState("")
    const [code, setCode] = useState("")
    const [hint, setHint] = useState<string | null>(null)
    const [countdown, setCountdown] = useState(0)
    const [submitting, setSubmitting] = useState(false)

    const canSubmit = mode === "password" ? password.length > 0 : code.trim().length === 6

    async function handleSendCode() {
        if (!phone) {
            setHint("当前账号未绑定手机号，无法使用短信验证。")
            return
        }
        try {
            const res = await sendSmsCode(phone)
            setCountdown(res.retrySeconds > 0 ? res.retrySeconds : 60)
            setHint(res.debugCode ? `验证码已发送（本地模拟：${res.debugCode}）` : res.message || "验证码已发送")
            const timer = window.setInterval(() => {
                setCountdown((value) => {
                    if (value <= 1) {
                        window.clearInterval(timer)
                        return 0
                    }
                    return value - 1
                })
            }, 1000)
        } catch (err) {
            setHint(err instanceof Error ? err.message : "验证码发送失败，请重试。")
        }
    }

    async function handleSubmit() {
        if (!token || !canSubmit) return
        setSubmitting(true)
        setHint(null)
        try {
            const result = await requestStepUpToken(
                token,
                mode === "password" ? { password } : { smsCode: code.trim() },
            )
            setPassword("")
            setCode("")
            message.success("身份确认成功。")
            onConfirmed(result.token)
        } catch (err) {
            setHint(err instanceof Error ? err.message : "身份确认失败，请重试。")
        } finally {
            setSubmitting(false)
        }
    }

    return (
        <Modal
            open={open}
            title="高危操作身份确认"
            okText="确认"
            cancelText="取消"
            confirmLoading={submitting}
            okButtonProps={{ disabled: !canSubmit }}
            onOk={() => void handleSubmit()}
            onCancel={onCancel}
            destroyOnHidden
        >
            <p className="pb-3 text-[13px] text-(--muted-foreground)">
                该操作会修改已入账数据或账套状态，需要再次验证身份。确认后 5 分钟内执行同类操作无需重复输入。
            </p>
            {hasPassword && (
                <Segmented
                    block
                    className="mb-3"
                    value={mode}
                    options={[
                        { label: "登录密码", value: "password" },
                        { label: "短信验证码", value: "sms" },
                    ]}
                    onChange={(value) => setMode(value as "password" | "sms")}
                />
            )}
            <Form layout="vertical" onFinish={() => void handleSubmit()}>
                {mode === "password" ? (
                    <Form.Item label="登录密码">
                        <Input.Password
                            autoFocus
                            autoComplete="current-password"
                            maxLength={64}
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            placeholder="请输入当前登录密码"
                        />
                    </Form.Item>
                ) : (
                    <Form.Item label={`短信验证码${phone ? `（${phone}）` : ""}`}>
                        <div className="flex gap-2">
                            <Input
                                autoFocus={!hasPassword}
                                inputMode="numeric"
                                maxLength={6}
                                value={code}
                                onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))}
                                placeholder="6 位验证码"
                            />
                            <Button onClick={() => void handleSendCode()} disabled={countdown > 0}>
                                {countdown > 0 ? `${countdown}s` : "获取验证码"}
                            </Button>
                        </div>
                    </Form.Item>
                )}
                {hint && <p className="text-[12.5px] text-(--danger)">{hint}</p>}
                <button type="submit" hidden />
            </Form>
        </Modal>
    )
}
