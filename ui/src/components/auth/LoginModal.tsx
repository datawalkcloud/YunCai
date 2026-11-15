import { useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import {
  CloseOutlined,
  EyeInvisibleOutlined,
  EyeOutlined,
  MailOutlined,
  MobileOutlined,
  QqOutlined,
  SafetyCertificateOutlined,
  WechatWorkOutlined,
} from "@ant-design/icons";

import { resetPassword, sendSmsCode } from "@/api/auth";
import { useAuth } from "@/auth/AuthContext";
import { BrandLogo } from "@/components/brand/BrandLogo";

/* ============================================================
 * 登录弹窗
 *
 * 结构与排版对齐统一登录弹窗：
 * - 卡片 480 宽 / 圆角取 rounded-2xl 档 / 上下内边距 48，无副标题
 * - 输入框：h52 / 圆角 rounded-xl 档 / 填充 --muted / 无边框 / 无左图标
 * - 登录按钮：h52 / 与输入框同形，激活 --brand-fill 白字，禁用 --muted 灰字
 * - 「其他登录方式」分隔行 + 圆形渠道图标 + 标签
 * - 底部协议行
 *
 * 两种凭据：手机验证码（默认入口，未注册手机号验证通过后自动建号）、手机号 + 密码。
 * 本地开发短信为模拟发送，接口会回显验证码，直接显示在提示行里；
 * 其余渠道保留展示，点击时提示未开放，不做假跳转。
 * 注：「短信验证码登录」目前由 ENABLE_SMS_LOGIN（文件内常量，当前 false）临时隐藏，
 * 仅保留「密码登录」；恢复只需把该常量改回 true。
 * ============================================================ */

/* 配色取全站 token：跟「主题设置」的主题色与明暗走，不再硬编码第二套绿/黑。 */
const C = {
  ink: "var(--foreground)",
  sub: "var(--muted-foreground)",
  faint: "var(--icon-muted)",
  brand: "var(--brand)",
  field: "var(--muted)",
  disabled: "var(--muted)",
  line: "var(--border)",
  danger: "var(--danger)",
};

const FOCUS_RING = "focus:shadow-[0_0_0_2px_color-mix(in_srgb,var(--seed-primary)_22%,transparent)]";
const FOCUS_WITHIN_RING = "focus-within:shadow-[0_0_0_2px_color-mix(in_srgb,var(--seed-primary)_22%,transparent)]";
const PHONE_RE = /^1\d{10}$/;

/* 是否开放「短信验证码登录」。临时隐藏改 false 即可，恢复只改回 true。 */
const ENABLE_SMS_LOGIN = false;

const LOGIN_TABS = [
  { key: "phone", label: "验证码登录" },
  { key: "password", label: "密码登录" },
] as const;

type LoginMode = "password" | "phone";
type LoginStep = "login" | "register" | "set-password" | "reset-password";

type Channel = {
  key: string;
  icon: typeof MobileOutlined;
  label: string;
};

const CHANNELS: Channel[] = [
  { key: "wecom", icon: WechatWorkOutlined, label: "企业微信" },
  { key: "phone", icon: MobileOutlined, label: "手机号" },
  { key: "sso", icon: SafetyCertificateOutlined, label: "SSO" },
  { key: "qq", icon: QqOutlined, label: "QQ" },
  { key: "email", icon: MailOutlined, label: "邮箱" },
];

export function LoginModal({
  open,
  onClose,
  onSuccess,
}: {
  open: boolean;
  onClose: () => void;
  /** 登录成功回调：由宿主页面统一关闭弹窗并跳转，避免弹窗与路由守卫同时导航。 */
  onSuccess?: () => void;
}) {
  const { login, loginByPhone, register, setPassword: setAccountPassword, needsPasswordSetup } = useAuth();

  const [mode, setMode] = useState<LoginMode>(ENABLE_SMS_LOGIN ? "phone" : "password");
  const [step, setStep] = useState<LoginStep>("login");
  const [password, setPassword] = useState("");
  const [phone, setPhone] = useState("");
  const [code, setCode] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPwd, setShowPwd] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hint, setHint] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [sendingCode, setSendingCode] = useState(false);
  const [countdown, setCountdown] = useState(0);

  const pwdRef = useRef<HTMLInputElement | null>(null);
  const phoneRef = useRef<HTMLInputElement | null>(null);
  const codeRef = useRef<HTMLInputElement | null>(null);
  const newPwdRef = useRef<HTMLInputElement | null>(null);

  const phoneValid = PHONE_RE.test(phone.trim());
  const resetReady =
    phoneValid && code.trim().length === 6 && newPassword.length >= 8 && confirmPassword.length > 0;
  const canSubmit =
    !submitting &&
    (step === "set-password" || step === "register"
      ? newPassword.length >= 8 && confirmPassword.length > 0
      : step === "reset-password"
        ? resetReady
        : mode === "password"
          ? phoneValid && password.length > 0
          : phoneValid && code.trim().length === 6);

  // 打开时重置表单并聚焦当前步骤的第一个输入框，同时锁定背景滚动。
  // needsPasswordSetup 变化时也要重跑：验证码首次登录成功后立刻切到设置密码步骤。
  useEffect(() => {
    if (!open) return;
    setMode(ENABLE_SMS_LOGIN ? "phone" : "password");
    setStep(needsPasswordSetup ? "set-password" : "login");
    setPhone("");
    setCode("");
    setNewPassword("");
    setConfirmPassword("");
    setCountdown(0);
    setSendingCode(false);
    setError(null);
    setHint(null);
    setSubmitting(false);
    setShowPwd(false);
    const t = window.setTimeout(
      () => (needsPasswordSetup ? newPwdRef : phoneRef).current?.focus(),
      120,
    );
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      window.clearTimeout(t);
      document.body.style.overflow = prev;
    };
  }, [open, needsPasswordSetup]);

  // 验证码重发倒计时
  useEffect(() => {
    if (countdown <= 0) return;
    const t = window.setTimeout(() => setCountdown((v) => v - 1), 1000);
    return () => window.clearTimeout(t);
  }, [countdown]);

  // Esc 关闭；设置密码步骤必须先设完，不允许中途退出。
  useEffect(() => {
    if (!open || step === "set-password") return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        if (step === "reset-password" || step === "register") enterStep("login");
        else onClose();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, step, onClose]);

  if (!open) return null;

  function enterStep(next: LoginStep) {
    setStep(next);
    setError(null);
    setHint(null);
    setNewPassword("");
    setConfirmPassword("");
    setCode("");
    window.setTimeout(
      () => (next === "reset-password" || next === "register" ? phoneRef : newPwdRef).current?.focus(),
      0,
    );
  }

  function switchMode(next: LoginMode) {
    setMode(next);
    setError(null);
    setHint(null);
    window.setTimeout(() => phoneRef.current?.focus(), 0);
  }

  async function handleSendCode() {
    if (!phoneValid) {
      setError("请输入正确的 11 位手机号");
      return;
    }
    setError(null);
    setHint(null);
    setSendingCode(true);
    try {
      const res = await sendSmsCode(phone.trim());
      setCountdown(res.retrySeconds > 0 ? res.retrySeconds : 60);
      setHint(
        res.debugCode
          ? `验证码已发送（本地模拟：${res.debugCode}）`
          : res.message || "验证码已发送",
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "验证码发送失败，请重试");
    } finally {
      setSendingCode(false);
    }
  }

  async function handleSubmit() {
    if (!canSubmit) return;
    setError(null);
    setSubmitting(true);
    try {
      const user =
        mode === "phone"
          ? await loginByPhone(phone.trim(), code.trim())
          : await login(phone.trim(), password);

      // 首次验证码登录自动建的号还没有密码：留在弹窗里补设，设完才进后台。
      if (user.hasPassword === false) {
        setStep("set-password");
        setNewPassword("");
        setConfirmPassword("");
        setSubmitting(false);
        window.setTimeout(() => newPwdRef.current?.focus(), 0);
        return;
      }

      // 跳转交给宿主页面：/login 直达由 RedirectIfAuthed 负责，官网就地开窗由 Landing 负责。
      // 这里绝不调用 onClose（它会把 /login 回落成 /，与登录跳转互相覆盖）。
      if (onSuccess) onSuccess();
      else onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "登录失败，请重试");
      setSubmitting(false);
      if (mode === "phone") codeRef.current?.focus();
      else pwdRef.current?.focus();
    }
  }

  async function handleSetupSubmit() {
    if (!canSubmit) return;
    if (newPassword.length < 8) {
      setError("密码至少 8 位");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("两次输入的密码不一致");
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await setAccountPassword(newPassword);
      if (onSuccess) onSuccess();
      else onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "密码设置失败，请重试");
      setSubmitting(false);
    }
  }

  async function handleRegisterSubmit() {
    if (!canSubmit) return;
    if (newPassword.length < 8) {
      setError("密码至少 8 位");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("两次输入的密码不一致");
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await register(phone.trim(), newPassword);
      if (onSuccess) onSuccess();
      else onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "注册失败，请重试");
      setSubmitting(false);
      phoneRef.current?.focus();
    }
  }

  async function handleResetSubmit() {
    if (!canSubmit) return;
    if (newPassword.length < 8) {
      setError("密码至少 8 位");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("两次输入的密码不一致");
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await resetPassword(phone.trim(), code.trim(), newPassword);
      setMode("password");
      setStep("login");
      setPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setCode("");
      setHint("密码已重置，请使用新密码登录");
      window.setTimeout(() => pwdRef.current?.focus(), 0);
    } catch (err) {
      setError(err instanceof Error ? err.message : "密码重置失败，请重试");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center px-5"
      style={{ background: "rgba(0,0,0,.45)", backdropFilter: "blur(3px)" }}
      onMouseDown={(e) => {
        if (step === "set-password") return;
        if (e.target === e.currentTarget) onClose();
      }}
      role="dialog"
      aria-modal="true"
      aria-label="登录云财"
    >
      <div
        className="relative max-h-[92svh] w-full max-w-[480px] overflow-y-auto rounded-2xl bg-(--card) px-8 py-12 sm:px-12 sm:py-14"
        style={{ boxShadow: "0 24px 60px -20px rgba(10,20,16,.28)" }}
      >
        {/* 关闭：设置密码步骤不可关闭，必须设完再进后台 */}
        {step !== "set-password" && (
          <button
            type="button"
            onClick={onClose}
            aria-label="关闭登录"
            className="absolute right-4 top-4 flex size-8 items-center justify-center rounded-full text-[13px] transition-colors hover:bg-(--muted)"
            style={{ color: C.faint }}
          >
            <CloseOutlined />
          </button>
        )}

        {/* 品牌 */}
        <div className="flex items-center justify-center gap-2.5">
          <BrandLogo size={36} />
          <span className="text-[16px] font-medium tracking-tight" style={{ color: C.ink }}>
            云财
          </span>
        </div>

        <h2 className="mt-9 text-center text-[21px] font-medium" style={{ color: C.ink }}>
          {step === "set-password"
            ? "设置登录密码"
            : step === "reset-password"
              ? "找回密码"
              : step === "register"
                ? "手机号注册"
                : mode === "password"
                  ? "手机号密码登录"
                  : "手机验证码登录"}
        </h2>

        {step === "set-password" && (
          <p className="mt-3 text-center text-[12.5px] font-normal" style={{ color: C.sub }}>
            首次登录请先设置密码，之后可用手机号 + 密码登录
          </p>
        )}

        {step === "reset-password" && (
          <p className="mt-3 text-center text-[12.5px] font-normal" style={{ color: C.sub }}>
            通过注册手机号的短信验证码重置登录密码
          </p>
        )}

        {/* 登录方式切换（设置密码步骤不显示；关闭短信验证码时只余密码登录，隐藏切换条） */}
        {step === "login" &&
          (ENABLE_SMS_LOGIN ? LOGIN_TABS : LOGIN_TABS.filter((t) => t.key !== "phone")).length > 1 && (
            <div className="mt-7 flex gap-1 rounded-xl p-1" style={{ background: C.field }}>
              {(ENABLE_SMS_LOGIN ? LOGIN_TABS : LOGIN_TABS.filter((t) => t.key !== "phone")).map((tab) => (
                <button
                  key={tab.key}
                  type="button"
                  onClick={() => switchMode(tab.key)}
                  className="h-9 flex-1 rounded-lg text-[13.5px] font-normal transition-colors"
                  style={
                    mode === tab.key
                      ? { background: "var(--card)", color: C.ink, boxShadow: "0 1px 3px rgba(10,20,16,.08)" }
                      : { color: C.sub }
                  }
                >
                  {tab.label}
                </button>
              ))}
            </div>
          )}

        {/* 表单 */}
        <form
          className="mt-6 flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault();
            void (
              step === "set-password"
                ? handleSetupSubmit()
                : step === "reset-password"
                  ? handleResetSubmit()
                  : step === "register"
                    ? handleRegisterSubmit()
                    : handleSubmit()
            );
          }}
        >
          {step === "set-password" ? (
            <>
              <div
                className={
                  "flex h-[52px] items-center rounded-xl px-4 transition-shadow " + FOCUS_WITHIN_RING
                }
                style={{ background: C.field }}
              >
                <input
                  ref={newPwdRef}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  type={showPwd ? "text" : "password"}
                  autoComplete="new-password"
                  maxLength={64}
                  placeholder="设置登录密码（至少 8 位）"
                  className="h-full w-full bg-transparent text-[15px] font-normal outline-none placeholder:font-normal placeholder:text-(--muted-foreground)"
                  style={{ color: C.ink }}
                />
                <button
                  type="button"
                  onClick={() => setShowPwd((v) => !v)}
                  aria-label={showPwd ? "隐藏密码" : "显示密码"}
                  className="ml-2 flex size-6 shrink-0 items-center justify-center text-[14px] transition-colors hover:text-(--foreground)!"
                  style={{ color: C.faint }}
                >
                  {showPwd ? <EyeOutlined /> : <EyeInvisibleOutlined />}
                </button>
              </div>

              <input
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                type={showPwd ? "text" : "password"}
                autoComplete="new-password"
                maxLength={64}
                placeholder="再次输入密码"
                className={
                  "h-[52px] w-full rounded-xl px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-normal placeholder:text-(--muted-foreground) " +
                  FOCUS_RING
                }
                style={{ background: C.field, color: C.ink }}
              />
            </>
          ) : step === "reset-password" ? (
            <>
              <input
                ref={phoneRef}
                value={phone}
                onChange={(e) => setPhone(e.target.value.replace(/\D/g, "").slice(0, 11))}
                inputMode="numeric"
                autoComplete="tel"
                maxLength={11}
                placeholder="请输入注册手机号"
                className={
                  "h-[52px] w-full rounded-xl px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-normal placeholder:text-(--muted-foreground) " +
                  FOCUS_RING
                }
                style={{ background: C.field, color: C.ink }}
              />

              <div
                className={
                  "flex h-[52px] items-center gap-3 rounded-xl px-4 transition-shadow " + FOCUS_WITHIN_RING
                }
                style={{ background: C.field }}
              >
                <input
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                  placeholder="短信验证码"
                  className="h-full min-w-0 flex-1 bg-transparent text-[15px] font-normal outline-none placeholder:font-normal placeholder:text-(--muted-foreground)"
                  style={{ color: C.ink }}
                />
                <span className="h-5 w-px shrink-0" style={{ background: C.line }} />
                <button
                  type="button"
                  onClick={() => void handleSendCode()}
                  disabled={countdown > 0 || sendingCode || !phoneValid}
                  className="shrink-0 text-[13px] font-normal transition-opacity disabled:cursor-not-allowed disabled:opacity-60"
                  style={{ color: C.brand }}
                >
                  {countdown > 0 ? `${countdown}s 后重发` : sendingCode ? "发送中…" : "获取验证码"}
                </button>
              </div>

              <input
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                type={showPwd ? "text" : "password"}
                autoComplete="new-password"
                maxLength={64}
                placeholder="新密码（至少 8 位）"
                className={
                  "h-[52px] w-full rounded-xl px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-normal placeholder:text-(--muted-foreground) " +
                  FOCUS_RING
                }
                style={{ background: C.field, color: C.ink }}
              />

              <input
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                type={showPwd ? "text" : "password"}
                autoComplete="new-password"
                maxLength={64}
                placeholder="再次输入新密码"
                className={
                  "h-[52px] w-full rounded-xl px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-normal placeholder:text-(--muted-foreground) " +
                  FOCUS_RING
                }
                style={{ background: C.field, color: C.ink }}
              />

              <button
                type="button"
                onClick={() => enterStep("login")}
                className="self-start text-[12.5px] font-normal transition-opacity hover:opacity-70"
                style={{ color: C.sub }}
              >
                返回登录
              </button>
            </>
          ) : step === "register" ? (
            <>
              <input
                ref={phoneRef}
                value={phone}
                onChange={(e) => setPhone(e.target.value.replace(/\D/g, "").slice(0, 11))}
                inputMode="numeric"
                autoComplete="tel"
                maxLength={11}
                placeholder="请输入手机号"
                className={
                  "h-[52px] w-full rounded-xl px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-normal placeholder:text-(--muted-foreground) " +
                  FOCUS_RING
                }
                style={{ background: C.field, color: C.ink }}
              />

              <div
                className={
                  "flex h-[52px] items-center rounded-xl px-4 transition-shadow " + FOCUS_WITHIN_RING
                }
                style={{ background: C.field }}
              >
                <input
                  ref={newPwdRef}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  type={showPwd ? "text" : "password"}
                  autoComplete="new-password"
                  maxLength={64}
                  placeholder="设置登录密码（至少 8 位）"
                  className="h-full w-full bg-transparent text-[15px] font-normal outline-none placeholder:font-normal placeholder:text-(--muted-foreground)"
                  style={{ color: C.ink }}
                />
                <button
                  type="button"
                  onClick={() => setShowPwd((v) => !v)}
                  aria-label={showPwd ? "隐藏密码" : "显示密码"}
                  className="ml-2 flex size-6 shrink-0 items-center justify-center text-[14px] transition-colors hover:text-(--foreground)!"
                  style={{ color: C.faint }}
                >
                  {showPwd ? <EyeOutlined /> : <EyeInvisibleOutlined />}
                </button>
              </div>

              <input
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                type={showPwd ? "text" : "password"}
                autoComplete="new-password"
                maxLength={64}
                placeholder="再次输入密码"
                className={
                  "h-[52px] w-full rounded-xl px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-normal placeholder:text-(--muted-foreground) " +
                  FOCUS_RING
                }
                style={{ background: C.field, color: C.ink }}
              />

              <button
                type="button"
                onClick={() => enterStep("login")}
                className="self-start text-[12.5px] font-normal transition-opacity hover:opacity-70"
                style={{ color: C.sub }}
              >
                已有账号？返回登录
              </button>
            </>
          ) : mode === "password" ? (
            <>
              <input
                ref={phoneRef}
                value={phone}
                onChange={(e) => setPhone(e.target.value.replace(/\D/g, "").slice(0, 11))}
                inputMode="numeric"
                autoComplete="tel"
                maxLength={11}
                placeholder="请输入手机号"
                className={
                  "h-[52px] w-full rounded-xl px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-normal placeholder:text-(--muted-foreground) " +
                  FOCUS_RING
                }
                style={{ background: C.field, color: C.ink }}
              />

              <div
                className={
                  "flex h-[52px] items-center rounded-xl px-4 transition-shadow " + FOCUS_WITHIN_RING
                }
                style={{ background: C.field }}
              >
                <input
                  ref={pwdRef}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  type={showPwd ? "text" : "password"}
                  autoComplete="current-password"
                  placeholder="请输入密码"
                  className="h-full w-full bg-transparent text-[15px] font-normal outline-none placeholder:font-normal placeholder:text-(--muted-foreground)"
                  style={{ color: C.ink }}
                />
                <button
                  type="button"
                  onClick={() => setShowPwd((v) => !v)}
                  aria-label={showPwd ? "隐藏密码" : "显示密码"}
                  className="ml-2 flex size-6 shrink-0 items-center justify-center text-[14px] transition-colors hover:text-(--foreground)!"
                  style={{ color: C.faint }}
                >
                  {showPwd ? <EyeOutlined /> : <EyeInvisibleOutlined />}
                </button>
              </div>

              <button
                type="button"
                onClick={() => enterStep("reset-password")}
                className="self-end text-[12.5px] font-normal transition-opacity hover:opacity-70"
                style={{ color: C.brand }}
              >
                忘记密码？
              </button>
            </>
          ) : (
            <>
              <input
                ref={phoneRef}
                value={phone}
                onChange={(e) => setPhone(e.target.value.replace(/\D/g, "").slice(0, 11))}
                inputMode="numeric"
                autoComplete="tel"
                maxLength={11}
                placeholder="请输入手机号"
                className={
                  "h-[52px] w-full rounded-xl px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-normal placeholder:text-(--muted-foreground) " +
                  FOCUS_RING
                }
                style={{ background: C.field, color: C.ink }}
              />

              <div
                className={
                  "flex h-[52px] items-center gap-3 rounded-xl px-4 transition-shadow " + FOCUS_WITHIN_RING
                }
                style={{ background: C.field }}
              >
                <input
                  ref={codeRef}
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, "").slice(0, 6))}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                  placeholder="请输入 6 位验证码"
                  className="h-full min-w-0 flex-1 bg-transparent text-[15px] font-normal outline-none placeholder:font-normal placeholder:text-(--muted-foreground)"
                  style={{ color: C.ink }}
                />
                <span className="h-5 w-px shrink-0" style={{ background: C.line }} />
                <button
                  type="button"
                  onClick={() => void handleSendCode()}
                  disabled={countdown > 0 || sendingCode}
                  className="shrink-0 text-[13px] font-normal transition-opacity disabled:cursor-not-allowed disabled:opacity-60"
                  style={{ color: C.brand }}
                >
                  {countdown > 0 ? `${countdown}s 后重发` : sendingCode ? "发送中…" : "获取验证码"}
                </button>
              </div>

              <p className="px-0.5 text-[12px] font-normal" style={{ color: C.sub }}>
                未注册的手机号验证通过后将自动创建账号
              </p>
            </>
          )}

          {error && (
            <p className="px-0.5 text-[12.5px] font-normal" style={{ color: C.danger }}>
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={!canSubmit}
            className="mt-2 h-[52px] w-full rounded-xl text-[16px] font-medium text-white transition-colors"
            style={canSubmit ? { background: "var(--brand-fill)" } : { background: C.disabled, color: "var(--icon-muted)", cursor: "not-allowed" }}
          >
            {step === "set-password"
              ? submitting
                ? "设置中…"
                : "设置密码并进入"
              : step === "reset-password"
                ? submitting
                  ? "重置中…"
                  : "重置密码"
                : step === "register"
                  ? submitting
                    ? "注册中…"
                    : "注册并登录"
                  : submitting
                    ? "登录中…"
                    : "登录"}
          </button>
        </form>

        {step === "login" && (
          <p className="mt-5 text-center text-[12.5px] font-normal" style={{ color: C.sub }}>
            还没有账号？
            <button
              type="button"
              onClick={() => enterStep("register")}
              className="ml-1 font-normal transition-opacity hover:opacity-70"
              style={{ color: C.brand }}
            >
              立即注册
            </button>
          </p>
        )}

        {/* 其他登录方式（设置密码步骤不显示） */}
        {step === "login" && (
          <>
            <div className="mt-11 flex items-center gap-3">
              <span className="h-px flex-1" style={{ background: C.line }} />
              <span className="text-[12px] font-normal" style={{ color: C.faint }}>
                其他登录方式
              </span>
              <span className="h-px flex-1" style={{ background: C.line }} />
            </div>

            <div className="mt-6 flex items-center justify-center gap-6">
              {CHANNELS.map((ch) => (
                <button
                  key={ch.key}
                  type="button"
                  onClick={() =>
                    ch.key === "phone"
                      ? switchMode(mode === "password" ? "phone" : "password")
                      : setHint(`${ch.label}登录暂未开放，请先用手机号密码或验证码登录`)
                  }
                  aria-label={`${ch.label}登录`}
                  title={`${ch.label}登录`}
                  className="flex w-[52px] flex-col items-center gap-2 transition-opacity hover:opacity-70"
                >
                  <span
                    className="flex size-[52px] items-center justify-center rounded-full"
                    style={{ background: C.field, color: "var(--muted-foreground)" }}
                  >
                    <ch.icon className="text-[20px]" />
                  </span>
                  <span className="text-[11px] font-normal" style={{ color: C.faint }}>
                    {ch.label}
                  </span>
                </button>
              ))}
            </div>

            {hint && (
              <p className="mt-3 text-center text-[12px] font-normal" style={{ color: C.brand }}>
                {hint}
              </p>
            )}

            {/* 协议 */}
            <p
              className="mt-9 text-center text-[12.5px] font-normal leading-[1.7]"
              style={{ color: C.sub }}
            >
              登录即表示已阅读并同意{" "}
              <Link
                to="/terms"
                onClick={onClose}
                className="underline underline-offset-2 transition-colors hover:opacity-70"
                style={{ color: C.ink }}
              >
                用户服务协议
              </Link>{" "}
              和{" "}
              <Link
                to="/privacy"
                onClick={onClose}
                className="underline underline-offset-2 transition-colors hover:opacity-70"
                style={{ color: C.ink }}
              >
                隐私政策
              </Link>
            </p>
          </>
        )}
      </div>
    </div>
  );
}
