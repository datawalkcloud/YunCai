/**
 * 一键登录税局（/tax/login）。
 *
 * 登录由服务器完成：账号密码加密保存在账套里，服务端按账套资料打开对应省份的电子税务局、
 * 自动填表并触发短信；用户只把短信验证码填回本页，登录态留在服务器。
 * 因此不需要安装本机助手，也不会在浏览器里打开税局页面。
 *
 * 报税各功能页（/tax/*）与本页同一外壳，菜单在顶部「报税」分类下，见 pages/tax/*。
 *
 * 弹窗两步与柠檬云一致：① 确认公司信息（含报税地区）② 填写税局登录信息。
 * 已填过一次的话第二步整页预填，密码不回显、留空即沿用已保存的那份。
 * 第二步的流程与「一键取票」弹窗共用 useTaxPortalLogin + TaxLoginStepFields，本页只管资料字段。
 */
import { useCallback, useEffect, useState } from "react";

import { CheckCircleFilled } from "@ant-design/icons";
import { Spin } from "antd";
import { useNavigate } from "react-router";

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsApi } from "@/apis-gen";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import { resumeTaxPortalLogin, type TaxPortalLoginResponse } from "@/lib/tax-portal";
import { downloadAssistant, launchAssistant, probeAssistant, requestTaxBrowserTicket } from "@/lib/tax-assistant";
import { TaxLoginPrompt } from "@/pages/tax/TaxLoginPrompt";
import { DEFAULT_TAX_IDENTITY_TYPE, DEFAULT_TAX_LOGIN_TYPE, TAX_REGIONS } from "@/lib/tax-regions";

import { AgreementRow, FieldRow, StepBar, TaxLoginStepFields } from "./components/TaxLoginStep";
import {
    DEFAULT_TAX_LOGIN_VALUES,
    useTaxPortalLogin,
    type TaxLoginValues,
} from "./components/useTaxPortalLogin";
import { Btn, Dialog, HintBox, Page, Select, TextInput, Toast, WipNotice } from "./components/ui";
import { FORM_CONTROL_W } from "@/components/page/styles";
import { setPath } from "@/accountSets/url-id";

/** 页面顶部的说明步骤，与本页真实行为一致。 */
const STEPS = [
    "进入本页会自动确认服务器上的税局登录状态，仍然有效就直接使用，不必再登录一次",
    "登录失效时自动弹窗重新登录：账号密码取自账套里加密保存的税局资料，不再回显",
    "服务器打开电子税务局并自动填写、提交登录，验证码由服务器自动识别",
    "只有税局发来短信时才需要填写验证码；登录态留在服务器，智能报税各页面可直接取数",
];

/** 弹窗步骤条。 */
const DIALOG_STEPS = ["确认公司信息", "填写税局登录信息"];

export function LemonTaxLogin() {
    const accountSetId = useCurrentAccountSetId();
    const { token } = useAuth();
    const { accountSets, refresh } = useAccountSets();
    const accountSet = accountSets.find((s) => s.id === accountSetId);
    const navigate = useNavigate();

    const [guideOpen, setGuideOpen] = useState(false);
    const [helpOpen, setHelpOpen] = useState(false);
    const [agreed, setAgreed] = useState(false);
    const [step, setStep] = useState<1 | 2>(1);
    /** 企业与税务身份：默认取自账套，也能在弹窗里直接改，保存时一并写回账套。 */
    const [companyName, setCompanyName] = useState("");
    const [taxpayerNumber, setTaxpayerNumber] = useState("");
    const [region, setRegion] = useState("");
    const [login, setLogin] = useState<TaxLoginValues>(DEFAULT_TAX_LOGIN_VALUES);
    const [toast, setToast] = useState<string | null>(null);
    /** 打开电子税务局：唤起本机助手，未检测到助手时弹出下载提示。 */
    const [etaxBusy, setEtaxBusy] = useState(false);
    const [assistantMissingOpen, setAssistantMissingOpen] = useState(false);

    /** 进入本页只确认后端登录态还能不能用；失效时由用户自己点「税局登录」，不自动发起登录。 */
    const [sessionCheck, setSessionCheck] = useState<{ accountSetId: string; state: "valid" | "expired" } | null>(null);
    const [loginPromptOpen, setLoginPromptOpen] = useState(false);
    /** 换账套时旧结果作废，重新回到「确认中」。 */
    const sessionState = sessionCheck && sessionCheck.accountSetId === accountSetId ? sessionCheck.state : "checking";

    useEffect(() => {
        if (!accountSetId || !token) return;
        let cancelled = false;
        void resumeTaxPortalLogin(accountSetId, token)
            .then((result) => {
                if (cancelled) return;
                setSessionCheck({ accountSetId, state: result.state === "success" ? "valid" : "expired" });
            })
            .catch(() => {
                // 确认请求本身失败（后端重启、网络抖动）也按未登录处理，点「税局登录」可以重来。
                if (cancelled) return;
                setSessionCheck({ accountSetId, state: "expired" });
            });
        return () => {
            cancelled = true;
        };
    }, [accountSetId, token]);

    const patchLogin = useCallback((patch: Partial<TaxLoginValues>) => {
        setLogin((prev) => ({ ...prev, ...patch }));
    }, []);

    /** 打开弹窗时按账套已存的资料预填，用户不必每次从头选。 */
    const openDialog = useCallback(() => {
        setCompanyName(accountSet?.companyName ?? "");
        setTaxpayerNumber(accountSet?.taxpayerNumber ?? "");
        setRegion(accountSet?.taxRegion ?? "");
        setLogin({
            loginType: accountSet?.taxLoginType ?? DEFAULT_TAX_LOGIN_TYPE,
            identityType: accountSet?.taxIdentityType ?? DEFAULT_TAX_IDENTITY_TYPE,
            account: accountSet?.taxLoginAccount ?? "",
            password: "",
            passwordSaved: accountSet?.taxLoginPasswordSet ?? false,
        });
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

    /**
     * 把税局资料写回账套。后端更新接口要求整包提交必填项，所以先按 apis-gen 的类型取回完整账套
     * 再回填，避免用页面上下文里那份精简 DTO 漏字段把准则、纳税人性质冲成默认值。
     */
    const saveTaxInfo = useCallback(async () => {
        if (!accountSetId) return;
        const api = new AccountSetsApi(apiConfig(token));
        const full = await api.accountSetsIdGet({ id: accountSetId });
        await api.accountSetsIdPut({
            id: accountSetId,
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
                cityBuildTaxRate: full.cityBuildTaxRate,
                educationSurchargeRate: full.educationSurchargeRate,
                localEducationSurchargeRate: full.localEducationSurchargeRate,
                taxRegion: region,
                taxLoginType: login.loginType,
                taxIdentityType: login.identityType,
                taxLoginAccount: login.account.trim(),
                // 留空表示沿用已存密码；后端把 null 与空串区分对待。
                taxLoginPassword: login.password.trim() || undefined,
            },
        });
        refresh();
    }, [accountSetId, companyName, login, refresh, region, taxpayerNumber, token]);

    /** 发起登录前的校验：缺哪一项就把原因原样吐回给用户，登录不发出。 */
    const validateLogin = useCallback(() => {
        if (!accountSetId) return "未识别当前账套，请从侧边栏重新选择账套后再试";
        if (!login.account.trim()) return "请填写税局实名账号";
        if (!login.password.trim() && !login.passwordSaved) {
            return "请填写税局实名密码，或先到【设置-账套】维护税局登录信息";
        }
        if (!taxpayerNumber.trim() || !region) return "请先补全统一社会信用代码与报税地区";
        return null;
    }, [accountSetId, login, region, taxpayerNumber]);

    /** 登录进展：成功就关弹窗并把页面切到「已登录」，其余状态变化只在首次提示。 */
    const handleFlowEvent = useCallback((result: TaxPortalLoginResponse, changed: boolean) => {
        if (result.state === "success") {
            setGuideOpen(false);
            if (accountSetId) setSessionCheck({ accountSetId, state: "valid" });
            setToast("已登录电子税务局，可以直接在本系统继续办理");
            return;
        }
        if (!changed) return;
        if (result.state === "failed") setToast(result.message || "税局登录失败，请重试。");
        if (result.state === "captcha_required") setToast("税局要求完成验证码，请按顺序点选图中文字");
        if (result.state === "sms_required") setToast(result.message || "验证码已发送，请查收短信");
    }, [accountSetId]);

    const flow = useTaxPortalLogin({
        accountSetId: accountSetId ?? "",
        token,
        active: guideOpen && step === 2,
        validate: validateLogin,
        prepare: async () => {
            try {
                await saveTaxInfo();
            } catch (err) {
                // 资料没存进去就没法让服务器按账套登录，把后端的校验文案透出去。
                throw new Error(await apiErrorMessage(err, "保存税局资料失败，请重试。"));
            }
        },
        onEvent: handleFlowEvent,
    });

    /** 「登录税局」与短信重发：登录由服务器推进，被拦下时把原因提示出来。 */
    const startLogin = useCallback(async () => {
        const blocked = await flow.startLogin();
        if (blocked) setToast(blocked);
    }, [flow]);

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

    /** 关弹窗只停网页侧轮询；服务器上的登录会话保留，用户仍可继续办理。 */
    const closeDialog = useCallback(() => setGuideOpen(false), []);

    return (
        <Page>
            <WipNotice text="一键登录已接通：服务器用账套里保存的税局资料打开电子税务局并自动填表，账号密码加密存库且不再回显；收到短信后把验证码填到本页即可完成登录。需要自己进税局办理时，点「一键登录电子税务局」，本机助手会带着服务器上的登录态直接打开税局页面。" />
            <div className="px-4 pb-2">
                <div className="flex flex-col gap-2 rounded-xl bg-(--table-zebra-bg) p-4 text-[13px] leading-6 text-(--foreground)">
                    {STEPS.map((s, i) => (
                        <div key={s} className="flex items-start gap-2">
                            <span className="mt-0.5 flex h-[18px] w-[18px] shrink-0 items-center justify-center rounded-full bg-(--brand-fill) text-[11px] text-white">
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
                    <Btn onClick={() => navigate(setPath(accountSetId, "/tax/fetch"))}>进入智能报税</Btn>
                )}
                {sessionState === "checking" ? (
                    <span className="flex items-center gap-2 text-[13px] text-(--muted-foreground)">
                        <Spin size="small" /> 正在确认税局登录状态…
                    </span>
                ) : sessionState === "valid" ? (
                    <span className="flex items-center gap-2 text-[13px] text-(--success)">
                        <CheckCircleFilled /> 税局登录状态有效，可以直接取数
                    </span>
                ) : (
                    <span className="text-[13px] text-(--warn-text)">税局登录状态已失效，请重新登录</span>
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
                                    <Btn onClick={() => setStep(1)} disabled={flow.busy}>
                                        上一步
                                    </Btn>
                                    {flow.captchaRequired ? (
                                        <Btn
                                            variant="primary"
                                            disabled={flow.busy || flow.captchaClicks.length === 0}
                                            onClick={async () => {
                                                const blocked = await flow.submitCaptchaClicks();
                                                if (blocked) setToast(blocked);
                                            }}
                                        >
                                            {flow.busyKind === "verifying" ? "验证中…" : "提交验证"}
                                        </Btn>
                                    ) : flow.smsRequired ? (
                                        <Btn
                                            variant="primary"
                                            disabled={flow.busy}
                                            onClick={async () => {
                                                const blocked = await flow.submitSmsCode();
                                                if (blocked) setToast(blocked);
                                            }}
                                        >
                                            {flow.busyKind === "verifying" ? "登录中…" : "提交验证码"}
                                        </Btn>
                                    ) : (
                                        <Btn variant="primary" disabled={flow.busy} onClick={() => void startLogin()}>
                                            {flow.busy ? "登录中…" : "登录税局"}
                                        </Btn>
                                    )}
                                </>
                            )}
                        </div>
                    }
                >
                    <div className="rounded-xl bg-(--table-zebra-bg) p-4 text-[13px] leading-6">
                        <div className="flex items-start gap-2">
                            <span className="text-(--warn-text)">⚠</span>
                            <div className="text-(--foreground)">
                                <div>1.一键自动跳转登录电子税局，方便且高效</div>
                                <div>
                                    2.首次使用或<span className="text-(--danger)">税务信息填写不准确</span>，均需要认证税局登录账号后，才能使用一键登录电子税局功能；税务信息填写完成后，可前往
                                    <button
                                        type="button"
                                        onClick={() => accountSetId && navigate(setPath(accountSetId, "/settings/account-sets"))}
                                        className="cursor-pointer text-(--brand) hover:underline"
                                    >
                                        【设置-账套】
                                    </button>
                                    中再次查看和修改
                                </div>
                                <div>
                                    3.如您在使用过程中有任何疑问，请查看{" "}
                                    <button
                                        type="button"
                                        onClick={() => setHelpOpen(true)}
                                        className="cursor-pointer text-(--brand) hover:underline"
                                    >
                                        帮助文档
                                    </button>{" "}
                                    或联系
                                    <button
                                        type="button"
                                        onClick={() => setHelpOpen(true)}
                                        className="ml-1 inline-flex h-6 items-center rounded-md bg-(--brand-fill) px-2 text-[12px] text-white"
                                    >
                                        在线客服
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                    <StepBar steps={DIALOG_STEPS} current={step} />

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
                            <AgreementRow
                                checked={agreed}
                                onChange={setAgreed}
                                label={<>我已阅读并同意 <span className="text-(--brand)">《税局登录业务隐私政策》</span></>}
                            />
                        </div>
                    ) : (
                        <TaxLoginStepFields
                            flow={flow}
                            values={login}
                            onChange={patchLogin}
                            onResend={() => void startLogin()}
                            hint={
                                <HintBox>
                                    点「登录税局」后，服务器会打开电子税务局并自动填写、提交登录；
                                    只有税局真的发来短信时，本页才会出现验证码输入框，填回提交即可。
                                    账号密码加密保存在当前账套，登录态只留在服务器，可以继续使用税费种、申报等在线业务。
                                    登录的账号非本人手机、需要他人转验证码时，请用税局自己的「分享验证」流程。
                                </HintBox>
                            }
                        />
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
                    title="云财税务助手没有响应"
                    width={480}
                    onClose={() => setAssistantMissingOpen(false)}
                    footer={
                        <div className="flex justify-center gap-3">
                            <Btn onClick={() => setAssistantMissingOpen(false)}>取消</Btn>
                            <Btn
                                onClick={() => {
                                    downloadAssistant();
                                    setAssistantMissingOpen(false);
                                    setToast("安装包已开始下载，安装并启动助手后再点一次");
                                }}
                            >
                                下载安装包
                            </Btn>
                            <Btn
                                variant="primary"
                                onClick={() => {
                                    setAssistantMissingOpen(false);
                                    openEtaxInAssistant();
                                }}
                            >
                                重新检测
                            </Btn>
                        </div>
                    }
                >
                    <div className="text-[13px] leading-6 text-(--foreground)">
                        「一键登录电子税务局」由本机的云财税务助手打开税局页面。助手窗口关掉后会收进系统托盘继续待命，托盘图标双击或右键「打开助手」都能唤回来。
                    </div>
                    <HintBox className="mt-3">装过但一直没响应，多半是助手从托盘退出了——重新打开助手再点一次「重新检测」；从没装过才需要下载安装包。</HintBox>
                </Dialog>
            )}

            {helpOpen && (
                <Dialog
                    title="一键登录税局 · 使用说明"
                    width={560}
                    onClose={() => setHelpOpen(false)}
                    footer={
                        <Btn variant="primary" onClick={() => setHelpOpen(false)}>
                            我知道了
                        </Btn>
                    }
                >
                    <ol className="m-0 flex list-decimal flex-col gap-2 pl-5 text-[13px] leading-6 text-(--foreground)">
                        {STEPS.map((step) => (
                            <li key={step}>{step}</li>
                        ))}
                    </ol>
                    <div className="mt-4 flex flex-col gap-1 rounded-lg bg-(--table-zebra-bg) px-3 py-2 text-[12px] leading-5 text-(--muted-foreground)">
                        <span>• 税局账号密码加密保存在当前账套，页面不回显；登录态只留在服务器。</span>
                        <span>• 登录失败先检查「设置-账套」里的公司名称、纳税人识别号与报税地区是否与税局一致。</span>
                        <span>• 系统办理业务期间请勿在别处登录同一税局账号，否则服务器会话会被挤掉。</span>
                        <span>• 客服服务时间：周一至周日 8:30-12:00、13:30-20:30。</span>
                    </div>
                </Dialog>
            )}

            {toast && <Toast text={toast} onClose={() => setToast(null)} />}
        </Page>
    );
}
