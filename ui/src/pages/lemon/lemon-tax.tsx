/**
 * 一键登录税局（/tax/login）。
 *
 * 登录由服务器完成：账号密码加密保存在账套里，服务端按账套资料打开对应省份的电子税务局、
 * 自动填表并触发短信；用户只把短信验证码填回本页，登录态留在服务器。
 * 因此不需要安装本机助手，也不会在浏览器里打开税局页面。
 *
 * 智能报税工作台（/tax/declare）是独立全屏页面，见 pages/tax/TaxWorkspace。
 *
 * 弹窗两步与柠檬云一致：① 确认公司信息（含报税地区）② 填写税局登录信息。
 * 已填过一次的话第二步整页预填，密码不回显、留空即沿用已保存的那份。
 */
import { useCallback, useEffect, useRef, useState, type MouseEvent as ReactMouseEvent, type ReactNode } from "react";

import { CheckCircleFilled } from "@ant-design/icons";
import { Input, Spin } from "antd";
import { useNavigate } from "react-router";

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsApi } from "@/apis-gen";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import {
    getTaxPortalLoginStatus,
    resumeTaxPortalLogin,
    startTaxPortalLogin,
    submitTaxPortalCaptcha,
    verifyTaxPortalLogin,
    type TaxPortalCaptcha,
    type TaxPortalCaptchaClick,
    type TaxPortalLoginResponse,
    type TaxPortalLoginState,
} from "@/lib/tax-portal";
import { downloadAssistant, launchAssistant, probeAssistant, requestTaxBrowserTicket } from "@/lib/tax-assistant";
import { TaxLoginPrompt } from "@/pages/tax/TaxLoginPrompt";
import { DEFAULT_TAX_IDENTITY_TYPE, DEFAULT_TAX_LOGIN_TYPE, TAX_IDENTITY_TYPES, TAX_LOGIN_TYPES, TAX_REGIONS } from "@/lib/tax-regions";

import {
    Btn,
    CheckBox,
    Dialog,
    HintBox,
    Page,
    Select,
    TextInput,
    Toast,
    WipNotice,
} from "./components/ui";
import { FORM_CONTROL_W } from "@/components/page/styles";

/** 页面顶部的说明步骤，与本页真实行为一致。 */
const STEPS = [
    "进入本页会自动确认服务器上的税局登录状态，仍然有效就直接使用，不必再登录一次",
    "登录失效时自动弹窗重新登录：账号密码取自账套里加密保存的税局资料，不再回显",
    "服务器打开电子税务局并自动填写、提交登录，验证码由服务器自动识别",
    "只有税局发来短信时才需要填写验证码；登录态留在服务器，智能报税各页面可直接取数",
];

/** 表单行：左标签右控件，与柠檬云弹窗的对齐方式一致。 */
function FieldRow({ label, required, children, hint }: { label: string; required?: boolean; children: ReactNode; hint?: string }) {
    return (
        <div className="flex items-center">
            <span className="w-[170px] shrink-0 text-right text-[13px] text-slate-700">
                {required && <span className="text-[#ef4444]">* </span>}
                {label}：
            </span>
            {children}
            {hint && <span className="ml-2 text-[12px] text-slate-400">{hint}</span>}
        </div>
    );
}

/** 步骤条：当前步实心，未到的步骤灰圈。 */
function StepBar({ step }: { step: 1 | 2 }) {
    const items: Array<{ no: 1 | 2; text: string }> = [
        { no: 1, text: "确认公司信息" },
        { no: 2, text: "填写税局登录信息" },
    ];
    return (
        <div className="mt-5 flex items-center gap-3 text-[13px]">
            {items.map((item, i) => {
                const done = i === 0;
                const active = step === item.no;
                const tone = done || active ? "bg-[#2f6bff] text-white" : "border border-slate-300 text-slate-400";
                return (
                    <div key={item.no} className="flex items-center gap-3">
                        {i > 0 && <span className="h-px w-16 bg-slate-200" />}
                        <span className="flex items-center gap-1.5">
                            <span className={`flex h-6 w-6 items-center justify-center rounded-full text-[12px] ${tone}`}>{item.no}</span>
                            <span className={active || done ? "text-[#2f6bff]" : "text-slate-400"}>{item.text}</span>
                        </span>
                    </div>
                );
            })}
        </div>
    );
}

export function LemonTaxLogin() {
    const accountSetId = useCurrentAccountSetId();
    const { token } = useAuth();
    const { accountSets, refresh } = useAccountSets();
    const accountSet = accountSets.find((s) => s.id === accountSetId);
    const navigate = useNavigate();

    const [guideOpen, setGuideOpen] = useState(false);
    const [agreed, setAgreed] = useState(false);
    const [step, setStep] = useState<1 | 2>(1);
    /** 企业与税务身份：默认取自账套，也能在弹窗里直接改，保存时一并写回账套。 */
    const [companyName, setCompanyName] = useState("");
    const [taxpayerNumber, setTaxpayerNumber] = useState("");
    const [region, setRegion] = useState("");
    const [loginType, setLoginType] = useState(DEFAULT_TAX_LOGIN_TYPE);
    const [identityType, setIdentityType] = useState(DEFAULT_TAX_IDENTITY_TYPE);
    const [account, setAccount] = useState("");
    const [password, setPassword] = useState("");
    /** 库里是否已有密码：决定第二步的占位文案与「留空即沿用」的判定。 */
    const [passwordSaved, setPasswordSaved] = useState(false);
    const [saving, setSaving] = useState(false);
    const [toast, setToast] = useState<string | null>(null);
    /** 短信验证码与本题登录：验证码提交时按账套找到服务器上的登录会话。 */
    const [smsCode, setSmsCode] = useState("");
    const [countdown, setCountdown] = useState(0);
    const [loginBusy, setLoginBusy] = useState<"none" | "sending" | "verifying">("none");
    const [sessionId, setSessionId] = useState<string | null>(null);
    const [flowState, setFlowState] = useState<TaxPortalLoginState | null>(null);
    const [flowMessage, setFlowMessage] = useState("");
    /** 文字点选验证码：税局要求按顺序点字时，服务器把底图与提示字发过来。 */
    const [captcha, setCaptcha] = useState<TaxPortalCaptcha | null>(null);
    const [captchaClicks, setCaptchaClicks] = useState<TaxPortalCaptchaClick[]>([]);
    /** 打开电子税务局：唤起本机助手，未检测到助手时弹出下载提示。 */
    const [etaxBusy, setEtaxBusy] = useState(false);
    const [assistantMissingOpen, setAssistantMissingOpen] = useState(false);

    /** 进入本页先问后端登录态还能不能用；失效时自动弹窗补一次短信，用户不必自己点。 */
    const [sessionCheck, setSessionCheck] = useState<{ accountSetId: string; state: "valid" | "expired" } | null>(null);
    const [loginPromptOpen, setLoginPromptOpen] = useState(false);
    /** 自动弹窗只弹一次：用户主动关掉后不再强行弹回来。 */
    const autoPromptedRef = useRef(false);
    /** 换账套时旧结果作废，重新回到「确认中」。 */
    const sessionState = sessionCheck && sessionCheck.accountSetId === accountSetId ? sessionCheck.state : "checking";

    useEffect(() => {
        if (!accountSetId || !token) return;
        let cancelled = false;
        autoPromptedRef.current = false;
        void resumeTaxPortalLogin(accountSetId, token)
            .then((result) => {
                if (cancelled) return;
                if (result.state === "success") {
                    setSessionCheck({ accountSetId, state: "valid" });
                    return;
                }
                setSessionCheck({ accountSetId, state: "expired" });
                if (!autoPromptedRef.current) {
                    autoPromptedRef.current = true;
                    setLoginPromptOpen(true);
                }
            })
            .catch(() => {
                if (cancelled) return;
                // 恢复请求本身失败（后端重启、网络抖动）也按未登录处理，弹窗里可以直接重试。
                setSessionCheck({ accountSetId, state: "expired" });
                if (!autoPromptedRef.current) {
                    autoPromptedRef.current = true;
                    setLoginPromptOpen(true);
                }
            });
        return () => {
            cancelled = true;
        };
    }, [accountSetId, token]);

    /** 验证码倒计时：税局短信 4 分钟内有效，这里给 2 分钟重发窗口。 */
    useEffect(() => {
        if (countdown <= 0) return;
        const timer = window.setTimeout(() => setCountdown((n) => Math.max(0, n - 1)), 1000);
        return () => window.clearTimeout(timer);
    }, [countdown]);

    /** 打开弹窗时按账套已存的资料预填，用户不必每次从头选。 */
    const openDialog = useCallback(() => {
        setCompanyName(accountSet?.companyName ?? "");
        setTaxpayerNumber(accountSet?.taxpayerNumber ?? "");
        setRegion(accountSet?.taxRegion ?? "");
        setLoginType(accountSet?.taxLoginType ?? DEFAULT_TAX_LOGIN_TYPE);
        setIdentityType(accountSet?.taxIdentityType ?? DEFAULT_TAX_IDENTITY_TYPE);
        setAccount(accountSet?.taxLoginAccount ?? "");
        setPassword("");
        setPasswordSaved(accountSet?.taxLoginPasswordSet ?? false);
        setSmsCode("");
        setCountdown(0);
        setSessionId(null);
        setFlowState(null);
        setFlowMessage("");
        setCaptcha(null);
        setCaptchaClicks([]);
        setLoginBusy("none");
        setStep(1);
        setGuideOpen(true);
    }, [accountSet]);

    /**
     * 登录有效时直接进税局办理：先向后端换一张一次性票据，再唤起本机助手。
     * 助手用票据取回服务器上的税局 Cookie 并注入自己的浏览器，打开企业电子税务局时就已经是登录状态。
     */
    const openEtaxInAssistant = useCallback(async () => {
        const taxNo = accountSet?.taxpayerNumber?.trim();
        if (!accountSet || !taxNo) {
            setToast("当前账套未填写纳税人识别号，请先在【设置-账套】补全");
            return;
        }
        if (!accountSetId) {
            setToast("未识别当前账套，请从侧边栏重新选择账套后再试");
            return;
        }
        setEtaxBusy(true);
        try {
            if (!(await probeAssistant())) {
                setAssistantMissingOpen(true);
                return;
            }
            let handoff: { ticket: string; url: string };
            try {
                handoff = await requestTaxBrowserTicket(accountSetId, token);
            } catch (err) {
                // 票据换不到说明服务器上的登录态刚刚失效：顺手把页面切回未登录并弹登录框。
                setToast(err instanceof Error && err.message ? err.message : "获取税局登录态失败，请重新登录。");
                setSessionCheck({ accountSetId, state: "expired" });
                setLoginPromptOpen(true);
                return;
            }
            launchAssistant({
                companyName: accountSet.companyName ?? "",
                taxpayerNumber: taxNo,
                region: accountSet.taxRegion ?? undefined,
                loginType: accountSet.taxLoginType ?? undefined,
                identityType: accountSet.taxIdentityType ?? undefined,
                account: accountSet.taxLoginAccount ?? undefined,
                ticket: handoff.ticket,
                url: handoff.url || undefined,
                apiBase: `${window.location.origin}/api`,
            });
            setToast("已唤起云财税务助手，正在以已登录状态打开电子税务局");
        } finally {
            setEtaxBusy(false);
        }
    }, [accountSet, accountSetId, token]);

    /** 关弹窗只停网页侧轮询；服务器上的登录会话保留，用户仍可继续办理。 */
    const closeDialog = useCallback(() => {
        setGuideOpen(false);
        setSessionId(null);
        setFlowState(null);
        setFlowMessage("");
        setCaptcha(null);
        setCaptchaClicks([]);
        setCountdown(0);
        setSmsCode("");
        setLoginBusy("none");
    }, []);

    /** 换了一张验证码底图就清空上一轮的点选。 */
    useEffect(() => {
        setCaptchaClicks([]);
    }, [captcha?.image]);

    /** 把服务器返回的状态落到界面：等点选、等短信、成功或失败。 */
    const applyLoginResponse = useCallback((result: TaxPortalLoginResponse) => {
        setSessionId(result.sessionId ?? null);
        setFlowState(result.state);
        setFlowMessage(result.message || "");
        if (result.state !== "captcha_required") setCaptcha(null);
        if (result.state === "success") {
            setGuideOpen(false);
            if (accountSetId) setSessionCheck({ accountSetId, state: "valid" });
            setToast("已登录电子税务局，可以直接在本系统继续办理");
            return;
        }
        if (result.state === "failed") {
            setToast(result.message || "税局登录失败，请重试。");
            return;
        }
        if (result.state === "captcha_required") {
            setCaptcha(result.captcha ?? null);
            return;
        }
        if (result.state === "sms_required") {
            setCountdown((n) => (n > 0 ? n : 120));
            setToast(result.message || "验证码已发送，请查收短信");
        }
    }, [accountSetId]);

    /** 登录在服务器后台推进，这里轮询状态，拿到「等短信」后自动开始倒计时。 */
    useEffect(() => {
        if (!sessionId || !accountSetId || flowState === "success" || flowState === "failed") return;
        let cancelled = false;
        const timer = window.setInterval(() => {
            void getTaxPortalLoginStatus(accountSetId, token)
                .then((result) => {
                    if (cancelled || !result.state) return;
                    setFlowState(result.state);
                    setFlowMessage(result.message || "");
                    setCaptcha(result.state === "captcha_required" ? (result.captcha ?? null) : null);
                    if (result.state === "captcha_required" && flowState !== "captcha_required") {
                        setToast("税局要求完成验证码，请按顺序点选图中文字");
                    }
                    if (result.state === "sms_required") {
                        setCountdown((n) => (n > 0 ? n : 120));
                        if (flowState !== "sms_required") {
                            setToast(result.message || "验证码已发送，请查收短信");
                        }
                    }
                    if (result.state === "success") {
                        setGuideOpen(false);
                        setSessionCheck({ accountSetId, state: "valid" });
                        setToast("已登录电子税务局，可以直接在本系统继续办理");
                    }
                    if (result.state === "failed") {
                        setToast(result.message || "税局登录失败，请重试。");
                    }
                })
                .catch(() => {
                    // 后端重启或会话被回收时轮询会失败，保持当前界面，等用户再次操作时提示。
                });
        }, 2000);
        return () => {
            cancelled = true;
            window.clearInterval(timer);
        };
    }, [accountSetId, flowState, sessionId, token]);

    /**
     * 把税局资料写回账套。后端更新接口要求整包提交必填项，所以先按 apis-gen 的类型取回完整账套
     * 再回填，避免用页面上下文里那份精简 DTO 漏字段把准则、纳税人性质冲成默认值。
     */
    const saveTaxInfo = useCallback(async (id: string) => {
        const api = new AccountSetsApi(apiConfig(token));
        const full = await api.accountSetsIdGet({ id });
        await api.accountSetsIdPut({
            id,
            updateAccountSetRequest: {
                name: full.name,
                companyName: companyName.trim(),
                standard: full.standard,
                taxpayerType: full.taxpayerType,
                baseCurrency: full.baseCurrency,
                accountCodingScheme: full.accountCodingScheme,
                voucherWords: full.voucherWords,
                taxpayerNumber: taxpayerNumber.trim(),
                vatRate: full.vatRate,
                incomeTaxRate: full.incomeTaxRate,
                taxRegion: region,
                taxLoginType: loginType,
                taxIdentityType: identityType,
                taxLoginAccount: account.trim(),
                // 留空表示沿用已存密码；后端把 null 与空串区分对待。
                taxLoginPassword: password.trim() || undefined,
            },
        });
    }, [account, companyName, identityType, loginType, password, region, taxpayerNumber, token]);

    const handleNext = useCallback(() => {
        if (!agreed) {
            setToast("请先阅读并同意《税局登录业务隐私政策》");
            return;
        }
        // 缺公司名称或纳税人识别号时留在弹窗里，让上方的说明自己解释原因。
        if (!companyName.trim()) {
            setToast("请填写公司名称");
            return;
        }
        if (!taxpayerNumber.trim()) {
            setToast("请填写统一社会信用代码，服务器需要它才能确定要登录哪家企业");
            return;
        }
        if (!region) {
            setToast("请选择报税地区，服务器需要它才能打开对应省份的电子税务局");
            return;
        }
        setStep(2);
    }, [agreed, companyName, region, taxpayerNumber]);

    /**
     * 「登录税局」：保存资料 → 让服务器打开税局并自动填表提交。
     * 税局只有在它自己要求短信验证时才会发码，本页轮询到「等短信」后再露出验证码输入框。
     */
    const handleStartLogin = useCallback(async () => {
        if (saving || loginBusy !== "none" || flowState === "starting") return;
        if (!accountSetId) {
            setToast("未识别当前账套，请从侧边栏重新选择账套后再试");
            return;
        }
        if (!account.trim()) {
            setToast("请填写税局实名账号");
            return;
        }
        if (!password.trim() && !passwordSaved) {
            setToast("请填写税局实名密码，或先到【设置-账套】维护税局登录信息");
            return;
        }
        if (!taxpayerNumber.trim() || !region) {
            setToast("请先补全统一社会信用代码与报税地区");
            return;
        }
        setSaving(true);
        setSmsCode("");
        setCountdown(0);
        setCaptcha(null);
        setCaptchaClicks([]);
        setFlowState("starting");
        setFlowMessage("正在打开电子税务局…");
        try {
            await saveTaxInfo(accountSetId);
            refresh();
            setLoginBusy("sending");
            const result = await startTaxPortalLogin(accountSetId, token);
            applyLoginResponse(result);
        } catch (err) {
            setFlowState("failed");
            setFlowMessage("");
            setToast(await apiErrorMessage(err, "登录税局失败，请重试。"));
        } finally {
            setSaving(false);
            setLoginBusy("none");
        }
    }, [
        account,
        accountSetId,
        applyLoginResponse,
        flowState,
        loginBusy,
        password,
        passwordSaved,
        refresh,
        region,
        saveTaxInfo,
        saving,
        taxpayerNumber,
        token,
    ]);

    /** 「提交验证码」：把网页收到的短信验证码交给服务器提交。 */
    const handleVerify = useCallback(async () => {
        if (saving || loginBusy !== "none" || !accountSetId) return;
        if (!sessionId || flowState !== "sms_required") {
            setToast("当前没有等待验证码的税局登录，请重新点击「登录税局」");
            return;
        }
        if (!/^\d{4,8}$/.test(smsCode.trim())) {
            setToast("请输入税局短信里的数字验证码");
            return;
        }
        setLoginBusy("verifying");
        try {
            const result = await verifyTaxPortalLogin(accountSetId, token, smsCode.trim());
            applyLoginResponse(result);
            if (result.state === "success") setSmsCode("");
        } catch (err) {
            setToast(err instanceof Error && err.message ? err.message : "提交验证码失败，请重试。");
        } finally {
            setLoginBusy("none");
        }
    }, [accountSetId, applyLoginResponse, flowState, loginBusy, saving, sessionId, smsCode, token]);

    /** 「提交验证」：把网页上按顺序点好的坐标交给服务器在无头浏览器里重放。 */
    const handleSubmitCaptcha = useCallback(async () => {
        if (saving || loginBusy !== "none" || !accountSetId) return;
        if (!sessionId || flowState !== "captcha_required") {
            setToast("当前没有等待点选的税局验证码，请重新点击「登录税局」");
            return;
        }
        if (captchaClicks.length === 0) {
            setToast("请按提示顺序点击图中文字");
            return;
        }
        setLoginBusy("verifying");
        try {
            const result = await submitTaxPortalCaptcha(accountSetId, token, captchaClicks);
            applyLoginResponse(result);
        } catch (err) {
            setToast(err instanceof Error && err.message ? err.message : "提交验证码失败，请重试。");
        } finally {
            setLoginBusy("none");
        }
    }, [accountSetId, applyLoginResponse, captchaClicks, flowState, loginBusy, saving, sessionId, token]);

    const handleCaptchaClick = useCallback((event: ReactMouseEvent<HTMLImageElement>) => {
        const rect = event.currentTarget.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        setCaptchaClicks((prev) =>
            prev.length >= 6
                ? prev
                : [...prev, { x: (event.clientX - rect.left) / rect.width, y: (event.clientY - rect.top) / rect.height }],
        );
    }, []);

    const captchaRequired = flowState === "captcha_required";
    const smsRequired = flowState === "sms_required";
    const loginBusyNow = saving || loginBusy !== "none" || flowState === "starting";

    return (
        <Page>
            <WipNotice text="一键登录已接通：服务器用账套里保存的税局资料打开电子税务局并自动填表，账号密码加密存库且不再回显；收到短信后把验证码填到本页即可完成登录。需要自己进税局办理时，点「一键登录电子税务局」，本机助手会带着服务器上的登录态直接打开税局页面。" />
            <div className="px-4 pb-2">
                <div className="flex flex-col gap-2 rounded-xl bg-[#fafbfd] p-4 text-[13px] leading-6 text-slate-600">
                    {STEPS.map((s, i) => (
                        <div key={s} className="flex items-start gap-2">
                            <span className="mt-0.5 flex h-[18px] w-[18px] shrink-0 items-center justify-center rounded-full bg-[#2f6bff] text-[11px] text-white">
                                {i + 1}
                            </span>
                            <span>{s}</span>
                        </div>
                    ))}
                </div>
            </div>
            <div className="flex flex-wrap items-center gap-4 px-4 pb-8 pt-4">
                {sessionState === "valid" ? (
                    <Btn variant="primary" disabled={etaxBusy} onClick={() => void openEtaxInAssistant()}>
                        {etaxBusy ? "正在唤起助手…" : "一键登录电子税务局"}
                    </Btn>
                ) : (
                    <Btn variant="primary" onClick={openDialog}>
                        税局登录
                    </Btn>
                )}
                {sessionState === "valid" && accountSetId && (
                    <Btn onClick={() => navigate(`/${accountSetId}/tax/declare`)}>进入智能报税</Btn>
                )}
                {sessionState === "checking" ? (
                    <span className="flex items-center gap-2 text-[13px] text-slate-500">
                        <Spin size="small" /> 正在确认税局登录状态…
                    </span>
                ) : sessionState === "valid" ? (
                    <span className="flex items-center gap-2 text-[13px] text-[#15803d]">
                        <CheckCircleFilled /> 税局登录状态有效，可以直接取数
                    </span>
                ) : (
                    <span className="text-[13px] text-[#b45309]">税局登录状态已失效，请重新登录</span>
                )}
            </div>

            {guideOpen && (
                <Dialog
                    title="一键登录电子税局"
                    width={900}
                    onClose={closeDialog}
                    footer={
                        <div className="flex justify-center gap-3">
                            {step === 1 ? (
                                <>
                                    <Btn onClick={closeDialog}>取消</Btn>
                                    <Btn variant="primary" onClick={handleNext}>
                                        下一步
                                    </Btn>
                                </>
                            ) : (
                                <>
                                    <Btn onClick={() => setStep(1)} disabled={loginBusyNow}>
                                        上一步
                                    </Btn>
                                    {captchaRequired ? (
                                        <Btn
                                            variant="primary"
                                            disabled={loginBusyNow || captchaClicks.length === 0}
                                            onClick={() => void handleSubmitCaptcha()}
                                        >
                                            {loginBusy === "verifying" ? "验证中…" : "提交验证"}
                                        </Btn>
                                    ) : smsRequired ? (
                                        <Btn
                                            variant="primary"
                                            disabled={loginBusyNow}
                                            onClick={() => void handleVerify()}
                                        >
                                            {loginBusy === "verifying" ? "登录中…" : "提交验证码"}
                                        </Btn>
                                    ) : (
                                        <Btn
                                            variant="primary"
                                            disabled={loginBusyNow}
                                            onClick={() => void handleStartLogin()}
                                        >
                                            {loginBusyNow ? "登录中…" : "登录税局"}
                                        </Btn>
                                    )}
                                </>
                            )}
                        </div>
                    }
                >
                    <div className="rounded-xl bg-[#fafbfd] p-4 text-[13px] leading-6">
                        <div className="flex items-start gap-2">
                            <span className="text-[#b45309]">⚠</span>
                            <div className="text-slate-600">
                                <div>1.一键自动跳转登录电子税局，方便且高效</div>
                                <div>
                                    2.首次使用或<span className="text-[#ef4444]">税务信息填写不准确</span>，均需要认证税局登录账号后，才能使用一键登录电子税局功能；税务信息填写完成后，可前往
                                    <span className="cursor-pointer text-[#2f6bff]">【设置-账套】</span>中再次查看和修改
                                </div>
                                <div>
                                    3.如您在使用过程中有任何疑问，请查看 <span className="cursor-pointer text-[#2f6bff]">帮助文档</span> 或联系
                                    <span className="ml-1 inline-flex h-6 items-center rounded-md bg-[#2f6bff] px-2 text-[12px] text-white">在线客服</span>
                                </div>
                            </div>
                        </div>
                    </div>
                    <StepBar step={step} />

                    {step === 1 ? (
                        <div className="mt-5 flex flex-col gap-4">
                            {!taxpayerNumber.trim() && (
                                <HintBox>当前未填写统一社会信用代码，服务器无法确定要登录哪家企业，请补全后继续。</HintBox>
                            )}
                            <FieldRow label="公司名称" required hint="默认取自当前账套，保存时一并写回账套">
                                <TextInput className={FORM_CONTROL_W} value={companyName} onChange={setCompanyName} placeholder="营业执照上的公司全称" />
                            </FieldRow>
                            <FieldRow label="统一社会信用代码" required>
                                <TextInput className={FORM_CONTROL_W} value={taxpayerNumber} onChange={setTaxpayerNumber} placeholder="18 位统一社会信用代码" />
                            </FieldRow>
                            <FieldRow label="报税地区" required hint="决定服务器打开哪个省的电子税务局">
                                <Select className={FORM_CONTROL_W} value={region} options={TAX_REGIONS} onChange={setRegion} />
                            </FieldRow>
                            <div className="flex items-center">
                                <span className="w-[170px]" />
                                <CheckBox
                                    checked={agreed}
                                    onChange={setAgreed}
                                    label={<>我已阅读并同意 <span className="text-[#2f6bff]">《税局登录业务隐私政策》</span></>}
                                />
                            </div>
                        </div>
                    ) : (
                        <div className="mt-5 flex flex-col gap-4">
                            <div className="flex items-center gap-8">
                                <FieldRow label="登录方式" required>
                                    <Select className={FORM_CONTROL_W} value={loginType} options={TAX_LOGIN_TYPES} onChange={setLoginType} />
                                </FieldRow>
                                <FieldRow label="身份类型" required>
                                    <Select className={FORM_CONTROL_W} value={identityType} options={TAX_IDENTITY_TYPES} onChange={setIdentityType} />
                                </FieldRow>
                            </div>
                            <div className="flex items-center gap-8">
                                <FieldRow label="税局实名账号" required>
                                    <TextInput className={FORM_CONTROL_W} value={account} onChange={setAccount} placeholder="手机号或身份证号" />
                                </FieldRow>
                                <FieldRow label="税局实名密码" required>
                                    <Input.Password
                                        className={FORM_CONTROL_W}
                                        value={password}
                                        autoComplete="new-password"
                                        placeholder={passwordSaved ? "已保存，留空表示不修改" : "税局登录密码"}
                                        onChange={(e) => setPassword(e.target.value)}
                                    />
                                </FieldRow>
                            </div>
                            {captchaRequired && captcha && (
                                <div className="rounded-xl border border-slate-200 bg-white p-3">
                                    <div className="flex flex-wrap items-center gap-2 text-[13px] text-slate-700">
                                        <span>请依次点击：</span>
                                        {captcha.promptImage && (
                                            <img
                                                src={captcha.promptImage}
                                                alt="验证码提示字"
                                                className="h-14 select-none"
                                                draggable={false}
                                            />
                                        )}
                                        <span className="ml-auto text-[12px] text-slate-400">
                                            按提示顺序点击下方图中文字
                                        </span>
                                    </div>
                                    <div
                                        className="relative mx-auto mt-2 cursor-crosshair select-none"
                                        style={{
                                            width: `min(100%, ${Math.round((captcha.width || 300) * 1.6)}px)`,
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
                                        <span>已点选 {captchaClicks.length} 个，点错了可以清空重选。</span>
                                        <Btn disabled={captchaClicks.length === 0} onClick={() => setCaptchaClicks([])}>
                                            清空
                                        </Btn>
                                    </div>
                                </div>
                            )}
                            {smsRequired && (
                                <div className="flex items-center">
                                    <FieldRow label="短信验证码" required>
                                        <span className="flex items-center gap-2">
                                            <TextInput
                                                className="w-[170px]"
                                                value={smsCode}
                                                onChange={setSmsCode}
                                                placeholder="税局短信验证码"
                                            />
                                            <Btn
                                                disabled={loginBusyNow || countdown > 0}
                                                onClick={() => void handleStartLogin()}
                                            >
                                                {countdown > 0 ? `${countdown}s 后重发` : "重新获取"}
                                            </Btn>
                                        </span>
                                    </FieldRow>
                                </div>
                            )}
                            {flowMessage && (
                                <div
                                    className={`rounded-lg px-3 py-2 text-[12.5px] leading-5 ${
                                        flowState === "failed" ? "bg-[#fef2f2] text-[#b91c1c]" : "bg-[#eff6ff] text-[#1d4ed8]"
                                    }`}
                                >
                                    {flowMessage}
                                </div>
                            )}
                            <HintBox>
                                点「登录税局」后，服务器会打开电子税务局并自动填写、提交登录；
                                只有税局真的发来短信时，本页才会出现验证码输入框，填回提交即可。
                                账号密码加密保存在当前账套，登录态只留在服务器，可以继续使用税费种、申报等在线业务。
                                登录的账号非本人手机、需要他人转验证码时，请用税局自己的「分享验证」流程。
                            </HintBox>
                        </div>
                    )}
                </Dialog>
            )}

            {accountSetId && (
                <TaxLoginPrompt
                    open={loginPromptOpen}
                    accountSetId={accountSetId}
                    reason="税局登录状态已失效：服务器会用账套里保存的税局资料重新登录，你只需要填一次短信验证码。"
                    onClose={() => setLoginPromptOpen(false)}
                    onSuccess={() => {
                        setLoginPromptOpen(false);
                        setSessionCheck({ accountSetId, state: "valid" });
                    }}
                />
            )}

            {assistantMissingOpen && (
                <Dialog
                    title="未检测到云财税务助手"
                    width={480}
                    onClose={() => setAssistantMissingOpen(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn onClick={() => setAssistantMissingOpen(false)}>取消</Btn>
                            <Btn
                                variant="primary"
                                onClick={() => {
                                    downloadAssistant();
                                    setAssistantMissingOpen(false);
                                    setToast("安装包已开始下载，安装并启动助手后再点一次");
                                }}
                            >
                                下载安装包
                            </Btn>
                        </div>
                    }
                >
                    <div className="text-[13px] leading-6 text-slate-600">
                        「一键登录电子税务局」由本机的云财税务助手打开税局页面。下载安装包并完成安装、启动助手后，回到本页再点一次即可。
                    </div>
                    <HintBox className="mt-3">已经安装过的话，请先启动助手；助手正在启动时稍等几秒再点。</HintBox>
                </Dialog>
            )}

            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
