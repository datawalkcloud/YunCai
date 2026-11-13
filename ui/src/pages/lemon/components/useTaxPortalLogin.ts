/**
 * 税局登录流程：发起登录、跟进服务器上的登录进展、点选验证码与短信验证码。
 *
 * 「一键登录税局」页的第二步和「一键取票」弹窗的第二步是同一段流程，状态机放在这里，
 * 页面只保留自己的资料字段，以及「登录前把资料写回账套」的 prepare 回调。
 * 登录由服务器上的无头浏览器推进，网页每 2 秒查一次进展；同一状态只通知页面一次，
 * 免得轮询把「验证码已发送」反复弹出来。
 */
import { useCallback, useEffect, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";

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
import { DEFAULT_TAX_IDENTITY_TYPE, DEFAULT_TAX_LOGIN_TYPE } from "@/lib/tax-regions";

/** 短信重发窗口：税局短信 4 分钟内有效，这里给 2 分钟。 */
const SMS_RESEND_SECONDS = 120;
const POLL_INTERVAL_MS = 2000;

/** 登录资料字段：由页面自己持有（要预填账套里存的值），TaxLoginStepFields 负责渲染。 */
export interface TaxLoginValues {
    loginType: string;
    identityType: string;
    account: string;
    password: string;
    /** 库里是否已有密码：决定占位文案与「留空即沿用」的判定。 */
    passwordSaved: boolean;
}

export const DEFAULT_TAX_LOGIN_VALUES: TaxLoginValues = {
    loginType: DEFAULT_TAX_LOGIN_TYPE,
    identityType: DEFAULT_TAX_IDENTITY_TYPE,
    account: "",
    password: "",
    passwordSaved: false,
};

export interface TaxLoginFlowOptions {
    accountSetId: string;
    token: string | null;
    /** 弹窗或页面打开时才跟进登录进展，关闭时停止轮询并清空会话。 */
    active: boolean;
    /** 发起登录前的校验，返回文案表示拦下这次登录并说明原因。 */
    validate?: () => string | null;
    /** 发起登录前把税局资料写回账套；抛错则按登录失败处理。 */
    prepare?: () => Promise<void>;
    /**
     * 登录进展回调。changed 表示状态相对上一次有变化：轮询期间同一个状态会重复回来，
     * 页面只在 changed 时提示，免得「验证码已发送」被反复弹出。
     */
    onEvent?: (result: TaxPortalLoginResponse, changed: boolean) => void;
}

export interface TaxLoginFlow {
    sessionId: string | null;
    flowState: TaxPortalLoginState | null;
    flowMessage: string;
    captcha: TaxPortalCaptcha | null;
    captchaClicks: TaxPortalCaptchaClick[];
    smsCode: string;
    setSmsCode: (value: string) => void;
    countdown: number;
    /** 按钮是否该显示「登录中…」并禁用。 */
    busy: boolean;
    busyKind: "none" | "sending" | "verifying";
    captchaRequired: boolean;
    smsRequired: boolean;
    /** 清空全部登录进展，弹窗开关与换账套时调用。 */
    reset: () => void;
    /** 发起一次登录：待登录、短信重发、失败重试都走这里。返回文案表示没有发起成功。 */
    startLogin: () => Promise<string | null>;
    /** 提交短信验证码。 */
    submitSmsCode: () => Promise<string | null>;
    /** 提交点选坐标。 */
    submitCaptchaClicks: () => Promise<string | null>;
    /** 在验证码底图上按提示顺序点一下。 */
    onCaptchaImageClick: (event: ReactMouseEvent<HTMLImageElement>) => void;
    clearCaptchaClicks: () => void;
}

export function useTaxPortalLogin({
    accountSetId,
    token,
    active,
    validate,
    prepare,
    onEvent,
}: TaxLoginFlowOptions): TaxLoginFlow {
    const [sessionId, setSessionId] = useState<string | null>(null);
    const [flowState, setFlowState] = useState<TaxPortalLoginState | null>(null);
    const [flowMessage, setFlowMessage] = useState("");
    const [captcha, setCaptcha] = useState<TaxPortalCaptcha | null>(null);
    const [captchaClicks, setCaptchaClicks] = useState<TaxPortalCaptchaClick[]>([]);
    const [smsCode, setSmsCode] = useState("");
    const [countdown, setCountdown] = useState(0);
    const [busyKind, setBusyKind] = useState<"none" | "sending" | "verifying">("none");

    // 校验、保存与回调每次渲染都是新的闭包，放进 effect 依赖会让轮询反复重启，这里只取最新值。
    const validateRef = useRef(validate);
    const prepareRef = useRef(prepare);
    const onEventRef = useRef(onEvent);
    const lastNotifiedRef = useRef<TaxPortalLoginState | null>(null);
    validateRef.current = validate;
    prepareRef.current = prepare;
    onEventRef.current = onEvent;

    const reset = useCallback(() => {
        setSessionId(null);
        setFlowState(null);
        setFlowMessage("");
        setCaptcha(null);
        setCaptchaClicks([]);
        setSmsCode("");
        setCountdown(0);
        setBusyKind("none");
        lastNotifiedRef.current = null;
    }, []);

    /** 把服务器返回的状态落到界面，并按「状态是否变化」通知页面。 */
    const applyResponse = useCallback((result: TaxPortalLoginResponse) => {
        const changed = lastNotifiedRef.current !== result.state;
        lastNotifiedRef.current = result.state;
        setSessionId(result.sessionId ?? null);
        setFlowState(result.state);
        setFlowMessage(result.message || "");
        setCaptcha(result.state === "captcha_required" ? (result.captcha ?? null) : null);
        if (result.state === "sms_required") {
            setCountdown((n) => (n > 0 ? n : SMS_RESEND_SECONDS));
        }
        onEventRef.current?.(result, changed);
    }, []);

    // 关闭时停掉轮询并忘掉会话；服务器上的登录会话本身保留。
    useEffect(() => {
        if (!active) reset();
    }, [active, reset]);

    /** 登录在服务器后台推进，轮询到成功或失败为止。 */
    useEffect(() => {
        if (!active || !sessionId || flowState === "success" || flowState === "failed") return;
        const timer = window.setInterval(() => {
            void getTaxPortalLoginStatus(accountSetId, token)
                .then(applyResponse)
                .catch(() => {
                    // 后端重启或会话被回收时轮询会失败，保持当前界面，等用户再次操作。
                });
        }, POLL_INTERVAL_MS);
        return () => window.clearInterval(timer);
    }, [accountSetId, active, applyResponse, flowState, sessionId, token]);

    useEffect(() => {
        if (countdown <= 0) return;
        const timer = window.setTimeout(() => setCountdown((n) => Math.max(0, n - 1)), 1000);
        return () => window.clearTimeout(timer);
    }, [countdown]);

    /** 换了一张验证码底图就清空上一轮的点选。 */
    useEffect(() => {
        setCaptchaClicks([]);
    }, [captcha?.image]);

    const startLogin = useCallback(async (): Promise<string | null> => {
        if (busyKind !== "none") return "正在处理上一步，请稍候。";
        const blocked = validateRef.current?.();
        if (blocked) return blocked;

        setBusyKind("sending");
        setSmsCode("");
        setCaptcha(null);
        setCaptchaClicks([]);
        setFlowState("starting");
        setFlowMessage("正在打开电子税务局…");
        try {
            await prepareRef.current?.();
            applyResponse(await startTaxPortalLogin(accountSetId, token));
            return null;
        } catch (err) {
            setFlowState("failed");
            setFlowMessage("");
            return err instanceof Error && err.message ? err.message : "登录税局失败，请重试。";
        } finally {
            setBusyKind("none");
        }
    }, [accountSetId, applyResponse, busyKind, token]);

    const submitSmsCode = useCallback(async (): Promise<string | null> => {
        if (busyKind !== "none") return null;
        if (!sessionId || flowState !== "sms_required") {
            return "当前没有等待验证码的税局登录，请重新发起登录。";
        }
        if (!/^\d{4,8}$/.test(smsCode.trim())) {
            return "请输入税局短信里的数字验证码";
        }
        setBusyKind("verifying");
        try {
            const result = await verifyTaxPortalLogin(accountSetId, token, smsCode.trim());
            applyResponse(result);
            if (result.state === "success") setSmsCode("");
            return null;
        } catch (err) {
            return err instanceof Error && err.message ? err.message : "提交验证码失败，请重试。";
        } finally {
            setBusyKind("none");
        }
    }, [accountSetId, applyResponse, busyKind, flowState, sessionId, smsCode, token]);

    const submitCaptchaClicks = useCallback(async (): Promise<string | null> => {
        if (busyKind !== "none") return null;
        if (!sessionId || flowState !== "captcha_required") {
            return "当前没有等待点选的税局验证码，请重新发起登录。";
        }
        if (captchaClicks.length === 0) {
            return "请按提示顺序点击图中文字";
        }
        setBusyKind("verifying");
        try {
            applyResponse(await submitTaxPortalCaptcha(accountSetId, token, captchaClicks));
            return null;
        } catch (err) {
            return err instanceof Error && err.message ? err.message : "提交验证码失败，请重试。";
        } finally {
            setBusyKind("none");
        }
    }, [accountSetId, applyResponse, busyKind, captchaClicks, flowState, sessionId, token]);

    const onCaptchaImageClick = useCallback((event: ReactMouseEvent<HTMLImageElement>) => {
        const rect = event.currentTarget.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        setCaptchaClicks((prev) =>
            prev.length >= 6
                ? prev
                : [...prev, { x: (event.clientX - rect.left) / rect.width, y: (event.clientY - rect.top) / rect.height }],
        );
    }, []);

    return {
        sessionId,
        flowState,
        flowMessage,
        captcha,
        captchaClicks,
        smsCode,
        setSmsCode,
        countdown,
        busy: busyKind !== "none" || flowState === "starting",
        busyKind,
        captchaRequired: flowState === "captcha_required",
        smsRequired: flowState === "sms_required",
        reset,
        startLogin,
        submitSmsCode,
        submitCaptchaClicks,
        onCaptchaImageClick,
        clearCaptchaClicks: () => setCaptchaClicks([]),
    };
}
