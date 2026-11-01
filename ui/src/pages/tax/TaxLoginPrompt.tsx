/**
 * 税局登录续期弹窗。
 *
 * 智能报税各页面取数时，如果服务器上的税局登录态已过期（后端返回 needLogin），
 * 由本弹窗用账套里保存的税局资料重新发起登录：验证码由服务器自动识别，
 * 用户只需把税局发来的短信验证码填回来，成功后页面继续取数，全程不打开税局页面。
 * 服务器若还存着可用的登录态，会先自动恢复，这种情况下连短信都不需要。
 */
import { useCallback, useEffect, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";
import { Alert, App, Button, Input, Modal, Space, Spin } from "antd";
import { useNavigate } from "react-router";

import { useAuth } from "@/auth/AuthContext";
import {
    getTaxPortalLoginStatus,
    startTaxPortalLogin,
    submitTaxPortalCaptcha,
    verifyTaxPortalLogin,
    type TaxPortalCaptcha,
    type TaxPortalCaptchaClick,
    type TaxPortalLoginResponse,
    type TaxPortalLoginState,
} from "@/lib/tax-portal";

type Phase = "checking" | TaxPortalLoginState;

interface TaxLoginPromptProps {
    open: boolean;
    accountSetId: string;
    /** 打开弹窗的原因，例如「税局登录状态已过期」。 */
    reason?: string;
    onClose: () => void;
    onSuccess: () => void;
}

const DEFAULT_REASON = "税局登录状态已过期：服务器需要用账套里保存的税局资料重新登录，你只需要填一次短信验证码。";

export function TaxLoginPrompt({ open, accountSetId, reason, onClose, onSuccess }: TaxLoginPromptProps) {
    const { token } = useAuth();
    const { message } = App.useApp();
    const navigate = useNavigate();

    const [phase, setPhase] = useState<Phase>("checking");
    const [flowMessage, setFlowMessage] = useState("");
    const [sessionId, setSessionId] = useState<string | null>(null);
    const [smsCode, setSmsCode] = useState("");
    const [countdown, setCountdown] = useState(0);
    const [busy, setBusy] = useState(false);
    const [captcha, setCaptcha] = useState<TaxPortalCaptcha | null>(null);
    const [captchaClicks, setCaptchaClicks] = useState<TaxPortalCaptchaClick[]>([]);

    const startedRef = useRef(false);
    const attemptRef = useRef(0);

    /** 把服务器返回的状态落到界面：等点选、等短信、成功或失败。 */
    const applyResponse = useCallback(
        (result: TaxPortalLoginResponse) => {
            setSessionId(result.sessionId ?? null);
            setPhase(result.state);
            setFlowMessage(result.message || "");
            setCaptcha(result.state === "captcha_required" ? (result.captcha ?? null) : null);
            if (result.state === "sms_required") {
                setCountdown((n) => (n > 0 ? n : 120));
            }
            if (result.state === "success") {
                message.success("已登录电子税务局");
                onSuccess();
                onClose();
            }
        },
        [message, onClose, onSuccess],
    );

    /**
     * 打开弹窗：先看服务器上有没有还在进行的登录会话，有就接着等；
     * 没有就发起一次登录——服务器会优先用保存的登录态恢复，恢复不了才走短信。
     */
    useEffect(() => {
        if (!open) {
            startedRef.current = false;
            return;
        }
        if (startedRef.current) return;
        startedRef.current = true;

        const attempt = ++attemptRef.current;
        const alive = () => attemptRef.current === attempt;

        setPhase("checking");
        setFlowMessage("");
        setSessionId(null);
        setSmsCode("");
        setCountdown(0);
        setCaptcha(null);
        setCaptchaClicks([]);
        setBusy(false);

        void (async () => {
            try {
                const status = await getTaxPortalLoginStatus(accountSetId, token);
                if (!alive()) return;
                if (status.state && status.state !== "failed") {
                    applyResponse(status);
                    return;
                }

                const started = await startTaxPortalLogin(accountSetId, token);
                if (alive()) applyResponse(started);
            } catch (err) {
                if (!alive()) return;
                setPhase("failed");
                setFlowMessage(err instanceof Error && err.message ? err.message : "登录电子税务局失败，请稍后重试。");
            }
        })();
    }, [accountSetId, applyResponse, open, token]);

    /** 登录在服务器后台推进，这里轮询状态，等到短信后自动开始倒计时。 */
    useEffect(() => {
        const pending = phase === "starting" || phase === "captcha_required" || phase === "sms_required";
        if (!open || !sessionId || !pending) return;
        const timer = window.setInterval(() => {
            void getTaxPortalLoginStatus(accountSetId, token)
                .then((result) => applyResponse(result))
                .catch(() => {
                    // 后端重启或会话被回收时轮询会失败，保持当前界面，等用户再点一次。
                });
        }, 2000);
        return () => window.clearInterval(timer);
    }, [accountSetId, applyResponse, open, phase, sessionId, token]);

    useEffect(() => {
        if (countdown <= 0) return;
        const timer = window.setTimeout(() => setCountdown((n) => Math.max(0, n - 1)), 1000);
        return () => window.clearTimeout(timer);
    }, [countdown]);

    useEffect(() => {
        setCaptchaClicks([]);
    }, [captcha?.image]);

    /** 重新发起一次完整登录（短信重发、失败重试都走这里）。 */
    const restart = useCallback(async () => {
        if (busy) return;
        setBusy(true);
        setSmsCode("");
        setCountdown(0);
        setCaptcha(null);
        setCaptchaClicks([]);
        setPhase("starting");
        setFlowMessage("正在重新登录电子税务局…");
        try {
            applyResponse(await startTaxPortalLogin(accountSetId, token));
        } catch (err) {
            setPhase("failed");
            setFlowMessage(err instanceof Error && err.message ? err.message : "登录电子税务局失败，请稍后重试。");
        } finally {
            setBusy(false);
        }
    }, [accountSetId, applyResponse, busy, token]);

    const handleVerify = useCallback(async () => {
        if (busy || !sessionId || phase !== "sms_required") return;
        if (!/^\d{4,8}$/.test(smsCode.trim())) {
            message.warning("请输入税局短信里的数字验证码");
            return;
        }
        setBusy(true);
        try {
            applyResponse(await verifyTaxPortalLogin(accountSetId, token, smsCode.trim()));
        } catch (err) {
            message.error(err instanceof Error && err.message ? err.message : "提交验证码失败，请重试。");
        } finally {
            setBusy(false);
        }
    }, [accountSetId, applyResponse, busy, message, phase, sessionId, smsCode, token]);

    const handleSubmitCaptcha = useCallback(async () => {
        if (busy || !sessionId || phase !== "captcha_required") return;
        if (captchaClicks.length === 0) {
            message.warning("请按提示顺序点击图中文字");
            return;
        }
        setBusy(true);
        try {
            applyResponse(await submitTaxPortalCaptcha(accountSetId, token, captchaClicks));
        } catch (err) {
            message.error(err instanceof Error && err.message ? err.message : "提交验证码失败，请重试。");
        } finally {
            setBusy(false);
        }
    }, [accountSetId, applyResponse, busy, captchaClicks, message, phase, sessionId, token]);

    const handleCaptchaClick = useCallback((event: ReactMouseEvent<HTMLImageElement>) => {
        const rect = event.currentTarget.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        setCaptchaClicks((prev) =>
            prev.length >= 6
                ? prev
                : [...prev, { x: (event.clientX - rect.left) / rect.width, y: (event.clientY - rect.top) / rect.height }],
        );
    }, []);

    const waiting = phase === "checking" || phase === "starting";

    return (
        <Modal
            open={open}
            title="登录电子税务局"
            width={560}
            onCancel={onClose}
            maskClosable={false}
            footer={null}
            destroyOnHidden
        >
            <div className="flex flex-col gap-3">
                <Alert type="warning" showIcon message={reason || DEFAULT_REASON} />

                {waiting ? (
                    <div className="flex items-center gap-3 py-6 text-[13px] text-slate-500">
                        <Spin size="small" />
                        <span>{flowMessage || "正在连接电子税务局…"}</span>
                    </div>
                ) : phase === "sms_required" ? (
                    <>
                        <div className="text-[13px] text-slate-600">
                            税局已向实名账号绑定的手机发送短信，请填写验证码：
                        </div>
                        <div className="flex items-center gap-2">
                            <Input
                                value={smsCode}
                                onChange={(event) => setSmsCode(event.target.value)}
                                placeholder="税局短信验证码"
                                maxLength={8}
                                onPressEnter={() => void handleVerify()}
                                autoFocus
                            />
                            <Button disabled={busy || countdown > 0} onClick={() => void restart()}>
                                {countdown > 0 ? `${countdown}s 后重发` : "重新获取"}
                            </Button>
                            <Button type="primary" loading={busy} onClick={() => void handleVerify()}>
                                提交验证码
                            </Button>
                        </div>
                    </>
                ) : phase === "captcha_required" ? (
                    captcha ? (
                        <div className="rounded-lg border border-slate-200 p-3">
                            <div className="flex flex-wrap items-center gap-2 text-[13px] text-slate-700">
                                <span>请依次点击：</span>
                                {captcha.promptImage && (
                                    <img src={captcha.promptImage} alt="验证码提示字" className="h-12 select-none" draggable={false} />
                                )}
                                <span className="ml-auto text-[12px] text-slate-400">服务器未能自动通过，请手动点选</span>
                            </div>
                            <div
                                className="relative mx-auto mt-2 cursor-crosshair select-none"
                                style={{
                                    width: `min(100%, ${Math.round((captcha.width || 300) * 1.5)}px)`,
                                    aspectRatio: `${captcha.width || 300} / ${captcha.height || 180}`,
                                }}
                            >
                                <img
                                    src={captcha.image}
                                    alt="验证码底图"
                                    className="h-full w-full cursor-crosshair rounded"
                                    draggable={false}
                                    onClick={handleCaptchaClick}
                                />
                                {captchaClicks.map((point, index) => (
                                    <span
                                        key={`${index}-${point.x.toFixed(4)}-${point.y.toFixed(4)}`}
                                        className="pointer-events-none absolute flex h-5 w-5 -translate-x-1/2 -translate-y-1/2 items-center justify-center rounded-full bg-[#2f6bff] text-[11px] text-white shadow"
                                        style={{ left: `${point.x * 100}%`, top: `${point.y * 100}%` }}
                                    >
                                        {index + 1}
                                    </span>
                                ))}
                            </div>
                            <div className="mt-2 flex items-center gap-2 text-[12px] text-slate-400">
                                <span>已点选 {captchaClicks.length} 个</span>
                                <Button size="small" disabled={captchaClicks.length === 0} onClick={() => setCaptchaClicks([])}>
                                    清空
                                </Button>
                                <Button
                                    size="small"
                                    type="primary"
                                    className="ml-auto"
                                    loading={busy}
                                    disabled={captchaClicks.length === 0}
                                    onClick={() => void handleSubmitCaptcha()}
                                >
                                    提交验证
                                </Button>
                            </div>
                        </div>
                    ) : (
                        <div className="flex items-center gap-3 py-6 text-[13px] text-slate-500">
                            <Spin size="small" />
                            <span>税局要求完成验证码，正在获取题目…</span>
                        </div>
                    )
                ) : (
                    <Alert
                        type="error"
                        showIcon
                        message="登录没有完成"
                        description={flowMessage || "税局登录失败，请稍后重试。"}
                    />
                )}

                {phase === "sms_required" && flowMessage && (
                    <div className="text-[12.5px] leading-5 text-slate-500">{flowMessage}</div>
                )}

                <div className="flex items-center justify-between gap-3 border-t border-slate-100 pt-3">
                    <span className="text-[12px] leading-5 text-slate-400">
                        验证码由服务器自动识别；只有税局发来短信时才需要你填写。
                    </span>
                    <Space>
                        {phase === "failed" && (
                            <Button onClick={() => navigate(`/${accountSetId}/tax/login`)}>维护税局资料</Button>
                        )}
                        {phase === "failed" ? (
                            <Button type="primary" loading={busy} onClick={() => void restart()}>
                                重新登录
                            </Button>
                        ) : (
                            <Button onClick={onClose}>关闭</Button>
                        )}
                    </Space>
                </div>
            </div>
        </Modal>
    );
}
