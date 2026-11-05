/**
 * 「个税申报登录」弹窗（自然人电子税务局）。
 *
 * 个税与企业电子税务局是两套站点、两套登录态：这里登录的是办税人员的实名账号，
 * 登录后服务器才能取回人员信息与专项附加扣除。填完点「登录」即发起服务端登录——
 * 账号密码先写回账套（服务端按账套取凭据），再由无头浏览器填表提交并自动过人机验证。
 * 税局发来短信时页面才需要填一次验证码；选「扫码登录」则直接扫二维码，不需要账号密码。
 */
import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, App, Button, Form, Image, Input, Modal, Radio, Select, Space, Spin } from "antd";

import { useAccountSets } from "@/accountSets/AccountSetsContext";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsApi } from "@/apis-gen";
import { apiConfig, apiErrorMessage } from "@/lib/api-client";
import {
    getIitLoginStatus,
    startIitLogin,
    verifyIitLogin,
    type TaxIitLoginMode,
} from "@/lib/tax-iit";
import type { TaxPortalLoginResponse, TaxPortalLoginState } from "@/lib/tax-portal";

/** 表单阶段单独列出来：还没发起登录时不显示短信与二维码那一截。 */
type Stage = "form" | TaxPortalLoginState;

interface IitLoginModalProps {
    open: boolean;
    accountSetId: string;
    onClose: () => void;
    /** 登录成功后回调，页面据此重新取数。 */
    onSuccess: () => void;
}

/** 申报渠道目前只有个税网页端一种，保留控件与柠檬云对齐，也便于日后加扣缴端。 */
const DECLARE_CHANNELS = [{ value: "web", label: "个税网页端" }];

/** 短信重发窗口：税局短信 4 分钟内有效，这里给 2 分钟。 */
const SMS_COOLDOWN = 120;

export function IitLoginModal({ open, accountSetId, onClose, onSuccess }: IitLoginModalProps) {
    const { token } = useAuth();
    const { accountSets } = useAccountSets();
    const { message } = App.useApp();
    const accountSet = accountSets.find((set) => set.id === accountSetId);

    const [stage, setStage] = useState<Stage>("form");
    const [mode, setMode] = useState<TaxIitLoginMode>("password");
    const [account, setAccount] = useState("");
    const [password, setPassword] = useState("");
    const [passwordSaved, setPasswordSaved] = useState(false);
    /** 密码格默认只读，聚焦才放开：挡掉浏览器在弹窗打开瞬间自动填进来的旧密码。 */
    const [passwordLocked, setPasswordLocked] = useState(true);
    const [smsCode, setSmsCode] = useState("");
    const [countdown, setCountdown] = useState(0);
    const [sessionId, setSessionId] = useState<string | null>(null);
    const [qrImage, setQrImage] = useState("");
    const [failureShot, setFailureShot] = useState("");
    const [flowMessage, setFlowMessage] = useState("");
    const [busy, setBusy] = useState(false);

    // 弹窗关闭后迟到的响应不能再 setState；每次打开换一个序号，旧请求自己作废。
    const attemptRef = useRef(0);

    useEffect(() => {
        if (!open) return;
        attemptRef.current += 1;
        setStage("form");
        setMode("password");
        setAccount(accountSet?.iitLoginAccount ?? "");
        setPassword("");
        setPasswordSaved(accountSet?.iitLoginPasswordSet ?? false);
        setPasswordLocked(true);
        setSmsCode("");
        setCountdown(0);
        setSessionId(null);
        setQrImage("");
        setFailureShot("");
        setFlowMessage("");
        setBusy(false);
    }, [open, accountSet?.iitLoginAccount, accountSet?.iitLoginPasswordSet]);

    /** 把服务器返回的登录进展落到界面。 */
    const applyResponse = useCallback(
        (result: TaxPortalLoginResponse) => {
            setSessionId(result.sessionId ?? null);
            setStage(result.state);
            setFlowMessage(result.message || "");
            setQrImage(result.state === "scan_required" ? (result.qrImage ?? "") : "");
            setFailureShot(result.state === "failed" ? (result.failureShot ?? "") : "");
            if (result.state === "sms_required") {
                setCountdown((prev) => (prev > 0 ? prev : SMS_COOLDOWN));
            }
            if (result.state === "success") {
                message.success("已登录自然人电子税务局");
                onSuccess();
                onClose();
            }
        },
        [message, onClose, onSuccess],
    );

    /**
     * 把实名账号（以及新填的密码）写回账套：服务端登录只认账套里保存的资料。
     * 更新接口要求整包必填项，所以先取回完整账套再回填，避免把准则、纳税人性质冲成默认值。
     */
    const saveCredentials = useCallback(async () => {
        const api = new AccountSetsApi(apiConfig(token));
        const full = await api.accountSetsIdGet({ id: accountSetId });
        await api.accountSetsIdPut({
            id: accountSetId,
            updateAccountSetRequest: {
                name: full.name,
                companyName: full.companyName,
                standard: full.standard,
                taxpayerType: full.taxpayerType,
                baseCurrency: full.baseCurrency,
                accountCodingScheme: full.accountCodingScheme,
                voucherWords: full.voucherWords,
                taxpayerNumber: full.taxpayerNumber,
                vatRate: full.vatRate,
                incomeTaxRate: full.incomeTaxRate,
                taxRegion: full.taxRegion,
                taxLoginType: full.taxLoginType,
                taxIdentityType: full.taxIdentityType,
                iitLoginAccount: account.trim(),
                // 留空表示沿用已存密码；后端区分 null（不改）与空串（清除）。
                iitLoginPassword: password.trim() || undefined,
            },
        });
    }, [account, accountSetId, password, token]);

    /** 发起登录，或在短信/扫码阶段重新发起一次。 */
    const startLogin = useCallback(async () => {
        if (busy) return;
        if (mode === "password" && !account.trim()) {
            message.warning("请填写实名登录账号");
            return;
        }
        if (mode === "password" && !password.trim() && !passwordSaved) {
            message.warning("请填写实名登录密码");
            return;
        }

        setBusy(true);
        setStage("starting");
        setSmsCode("");
        setQrImage("");
        setFailureShot("");
        setCountdown(0);
        setFlowMessage("正在登录自然人电子税务局…");
        try {
            if (mode === "password") {
                const savedAccount = accountSet?.iitLoginAccount?.trim() ?? "";
                const trimmed = account.trim();
                // 只有真的改过才回写，避免每次登录都发一次全量更新。
                if (password.trim() || trimmed !== savedAccount) {
                    await saveCredentials();
                }
            }
            applyResponse(await startIitLogin(accountSetId, token, mode));
        } catch (err) {
            setStage("failed");
            setFlowMessage(await apiErrorMessage(err, "登录个税系统失败，请稍后重试。"));
        } finally {
            setBusy(false);
        }
    }, [account, accountSet?.iitLoginAccount, accountSetId, applyResponse, busy, message, mode, password, passwordSaved, saveCredentials, token]);

    /** 登录在服务器后台推进，这里每两秒拉一次进展。 */
    useEffect(() => {
        const pending = stage === "starting" || stage === "sms_required" || stage === "scan_required";
        if (!open || !sessionId || !pending) return;
        const attempt = attemptRef.current;
        const timer = window.setInterval(() => {
            void getIitLoginStatus(accountSetId, token)
                .then((result) => {
                    if (attemptRef.current === attempt) applyResponse(result);
                })
                .catch(() => {
                    // 后端重启或会话被回收时轮询会失败，保持当前界面，等用户再点一次。
                });
        }, 2000);
        return () => window.clearInterval(timer);
    }, [accountSetId, applyResponse, open, sessionId, stage, token]);

    useEffect(() => {
        if (countdown <= 0) return;
        const timer = window.setTimeout(() => setCountdown((n) => Math.max(0, n - 1)), 1000);
        return () => window.clearTimeout(timer);
    }, [countdown]);

    const handleVerify = useCallback(async () => {
        if (busy || !sessionId || stage !== "sms_required") return;
        if (!/^\d{4,8}$/.test(smsCode.trim())) {
            message.warning("请输入个税系统短信里的数字验证码");
            return;
        }
        setBusy(true);
        try {
            applyResponse(await verifyIitLogin(accountSetId, token, smsCode.trim()));
        } catch (err) {
            message.error(await apiErrorMessage(err, "提交验证码失败，请重试。"));
        } finally {
            setBusy(false);
        }
    }, [accountSetId, applyResponse, busy, message, sessionId, smsCode, stage, token]);

    const editing = stage === "form" || stage === "failed";
    const waiting = stage === "starting";
    const showSms = stage === "sms_required";
    const showQr = stage === "scan_required";
    const failed = stage === "failed";

    return (
        <Modal
            open={open}
            title="个税申报登录"
            width={620}
            onCancel={onClose}
            maskClosable={false}
            destroyOnHidden
            footer={
                <div className="flex justify-end gap-2">
                    <Button onClick={onClose}>取消</Button>
                    {failed || stage === "form" ? (
                        <Button type="primary" loading={busy} onClick={() => void startLogin()}>
                            登录
                        </Button>
                    ) : showSms ? (
                        <Button type="primary" loading={busy} disabled={!smsCode.trim()} onClick={() => void handleVerify()}>
                            提交验证码
                        </Button>
                    ) : (
                        <Button type="primary" disabled>
                            等待税局响应
                        </Button>
                    )}
                </div>
            }
        >
            <div className="flex flex-col gap-4 pt-2">
                <Alert
                    type="warning"
                    showIcon
                    message={
                        <span className="text-[12.5px] leading-5">
                            个税申报登录的是办税人员的实名账号，与企业电子税务局分开保存登录态。
                            首次使用或账号信息不准确时都需要重新验证；资料保存在账套的「个税申报登录」里，也可在账套设置中查看和修改。
                        </span>
                    }
                />

                <Form layout="horizontal" labelCol={{ flex: "0 0 116px" }} labelAlign="right">
                    <Form.Item label="公司名称" required>
                        <Input readOnly disabled value={accountSet?.companyName ?? ""} placeholder="未填写" />
                    </Form.Item>
                    <Form.Item label="统一社会信用代码" required>
                        <Input readOnly disabled value={accountSet?.taxpayerNumber ?? ""} placeholder="未填写" />
                    </Form.Item>
                    <Form.Item label="地区" required>
                        <Select disabled value={accountSet?.taxRegion || undefined} placeholder="未选择" options={[]} />
                    </Form.Item>
                    <Form.Item label="申报渠道" required>
                        <Select disabled defaultValue="web" options={DECLARE_CHANNELS} />
                    </Form.Item>
                    <Form.Item label="登录方式" required>
                        <Radio.Group
                            value={mode}
                            disabled={!editing}
                            onChange={(event) => setMode(event.target.value as TaxIitLoginMode)}
                            options={[
                                { value: "password", label: "实名账号登录" },
                                { value: "scan", label: "个人所得税APP扫码登录" },
                            ]}
                        />
                    </Form.Item>

                    {mode === "password" && (
                        <>
                            <Form.Item label="实名登录账号" required>
                                <Input
                                    value={account}
                                    disabled={!editing}
                                    maxLength={30}
                                    placeholder="手机号或身份证号"
                                    autoComplete="off"
                                    onChange={(event) => setAccount(event.target.value)}
                                />
                            </Form.Item>
                            <Form.Item label="实名登录密码" required>
                                {/* 浏览器会自动往里灌一个存过的密码，而留空才是「沿用服务端已存密码」；
                                    填错的那份一旦提交就把好密码覆盖掉了，所以未聚焦前只读。 */}
                                <Input.Password
                                    value={password}
                                    disabled={!editing}
                                    readOnly={passwordLocked}
                                    autoComplete="new-password"
                                    placeholder={passwordSaved ? "已保存，留空即沿用" : "税局实名账号的登录密码"}
                                    onFocus={() => setPasswordLocked(false)}
                                    onChange={(event) => setPassword(event.target.value)}
                                />
                            </Form.Item>
                        </>
                    )}

                    {/* 扫码登录不走短信；只在密码方式或税局真的发来短信时占这一行。 */}
                    {(mode === "password" || showSms) && (
                        <Form.Item label="短信验证码" required={showSms}>
                            <div className="flex items-center gap-2">
                                <Input
                                    value={smsCode}
                                    disabled={!showSms}
                                    maxLength={8}
                                    placeholder={showSms ? "请输入验证码" : "登录后可用"}
                                    onChange={(event) => setSmsCode(event.target.value)}
                                    onPressEnter={() => void handleVerify()}
                                />
                                <Button className="shrink-0" disabled={!showSms || countdown > 0 || busy} onClick={() => void startLogin()}>
                                    {countdown > 0 ? `重新获取(${countdown}s)` : "获取验证码"}
                                </Button>
                            </div>
                        </Form.Item>
                    )}
                </Form>

                {showQr && (
                    <div className="flex flex-col items-center gap-2 rounded-lg border border-slate-200 py-4">
                        {qrImage ? (
                            <img src={qrImage} alt="个税扫码登录二维码" className="h-[200px] w-[200px] select-none" />
                        ) : (
                            <Spin />
                        )}
                        <span className="text-[12.5px] text-slate-500">请用个人所得税 APP 扫码并确认登录</span>
                    </div>
                )}

                {waiting && (
                    <div className="flex items-center gap-3 py-2 text-[13px] text-slate-500">
                        <Spin size="small" />
                        <span>{flowMessage || "正在连接自然人电子税务局…"}</span>
                    </div>
                )}

                {failed && <Alert type="error" showIcon message="登录没有完成" description={flowMessage} />}

                {failed && failureShot && (
                    <div className="flex flex-col gap-1.5">
                        <span className="text-[12.5px] text-slate-500">失败那一刻服务器里浏览器看到的画面：</span>
                        <Image
                            src={failureShot}
                            alt="个税登录失败页面截图"
                            className="rounded-lg border border-slate-200"
                            width="100%"
                        />
                    </div>
                )}

                {flowMessage && (showSms || showQr) && (
                    <div className="text-[12.5px] leading-5 text-slate-500">{flowMessage}</div>
                )}

                <Space className="text-[12px] text-slate-400">
                    登录由服务器完成，验证码自动识别；只有税局发来短信时才需要你填写。
                </Space>
            </div>
        </Modal>
    );
}
