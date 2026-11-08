import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router";
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

import { useAuth } from "@/auth/AuthContext";

/* ============================================================
 * 登录弹窗
 *
 * 结构与排版对齐统一登录弹窗（实测规格）：
 * - 卡片 480 宽 / 圆角 16 / 上下内边距 48，无副标题
 * - 输入框：h52 / 圆角 12 / 纯填充 #f4f4f4 / 无边框 / 无左图标
 * - 登录按钮：h52 / 圆角 12 / 与输入框同形，禁用 #c4c4c4 白字，激活 #1a1a1a
 * - 「其他登录方式」分隔行 + 圆形渠道图标 + 标签
 * - 底部协议行
 *
 * 后端当前只有 Auth/login（用户名 + 密码）一种凭据，因此主入口是账号密码；
 * 其余渠道保留展示，点击时提示未开放，不做假跳转。
 * ============================================================ */

const C = {
  ink: "#1a1a1a",
  sub: "#646464",
  faint: "#b8b8b8",
  brand: "#079d55",
  field: "#f4f4f4",
  disabled: "#c4c4c4",
  line: "#ececec",
  danger: "#d4380d",
};

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

export function LoginModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPwd, setShowPwd] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hint, setHint] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const userRef = useRef<HTMLInputElement | null>(null);
  const pwdRef = useRef<HTMLInputElement | null>(null);

  // 登录后回到来源页；从 /login 直接进入时没有来源，落到应用区。
  const from = (location.state as { from?: string } | null)?.from ?? "/app";

  const canSubmit = username.trim().length > 0 && password.length > 0 && !submitting;

  // 打开时聚焦账号框，并锁定背景滚动
  useEffect(() => {
    if (!open) return;
    setError(null);
    setHint(null);
    setSubmitting(false);
    setShowPwd(false);
    const t = window.setTimeout(() => userRef.current?.focus(), 120);
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      window.clearTimeout(t);
      document.body.style.overflow = prev;
    };
  }, [open]);

  // Esc 关闭
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, onClose]);

  if (!open) return null;

  async function handleSubmit() {
    if (!canSubmit) return;
    setError(null);
    setSubmitting(true);
    try {
      await login(username.trim(), password);
      onClose();
      navigate(from, { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : "登录失败，请重试");
      setSubmitting(false);
      pwdRef.current?.focus();
    }
  }

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center px-5"
      style={{ background: "rgba(0,0,0,.45)", backdropFilter: "blur(3px)" }}
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      role="dialog"
      aria-modal="true"
      aria-label="登录云财"
    >
      <div
        className="relative max-h-[92svh] w-full max-w-[480px] overflow-y-auto rounded-[16px] bg-(--card) px-8 py-12 sm:px-12 sm:py-14"
        style={{ boxShadow: "0 24px 60px -20px rgba(10,20,16,.28)" }}
      >
        {/* 关闭 */}
        <button
          type="button"
          onClick={onClose}
          aria-label="关闭登录"
          className="absolute right-4 top-4 flex size-8 items-center justify-center rounded-full text-[13px] transition-colors hover:bg-(--muted)"
          style={{ color: "#b8b8b8" }}
        >
          <CloseOutlined />
        </button>

        {/* 品牌 */}
        <div className="flex items-center justify-center gap-2.5">
          <span
            className="flex size-9 items-center justify-center rounded-[10px]"
            style={{ background: C.brand }}
          >
            <svg viewBox="0 0 24 24" className="size-[19px]" fill="none">
              <path
                d="M5 8.5h11.5a2.5 2.5 0 0 1 2.5 2.5v6.5a2.5 2.5 0 0 1-2.5 2.5H7.5A2.5 2.5 0 0 1 5 17.5v-9Z"
                stroke="#fff"
                strokeWidth="1.8"
                strokeLinejoin="round"
              />
              <path d="M5 8.5 7 5h8l2 3.5" stroke="#fff" strokeWidth="1.8" strokeLinejoin="round" />
              <path d="M9 13.6h6" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" />
            </svg>
          </span>
          <span className="flex items-baseline gap-1.5">
            <span className="text-[16px] font-medium tracking-tight" style={{ color: C.ink }}>
              云财
            </span>
            <span
              className="text-[9.5px] font-light uppercase tracking-[0.2em]"
              style={{ color: "#c0c0c0" }}
            >
              KuaiJi
            </span>
          </span>
        </div>

        <h2 className="mt-9 text-center text-[21px] font-medium" style={{ color: C.ink }}>
          账号密码登录
        </h2>

        {/* 表单 */}
        <form
          className="mt-9 flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault();
            void handleSubmit();
          }}
        >
          <input
            ref={userRef}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            placeholder="请输入用户名"
            className="h-[52px] w-full rounded-[12px] px-4 text-[15px] font-normal outline-none transition-shadow placeholder:font-light placeholder:text-(--muted-foreground) focus:shadow-[0_0_0_2px_rgba(7,157,85,.18)]"
            style={{ background: C.field, color: C.ink }}
          />

          <div
            className="flex h-[52px] items-center rounded-[12px] px-4 transition-shadow focus-within:shadow-[0_0_0_2px_rgba(7,157,85,.18)]"
            style={{ background: C.field }}
          >
            <input
              ref={pwdRef}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              type={showPwd ? "text" : "password"}
              autoComplete="current-password"
              placeholder="请输入密码"
              className="h-full w-full bg-transparent text-[15px] font-normal outline-none placeholder:font-light placeholder:text-(--muted-foreground)"
              style={{ color: C.ink }}
            />
            <button
              type="button"
              onClick={() => setShowPwd((v) => !v)}
              aria-label={showPwd ? "隐藏密码" : "显示密码"}
              className="ml-2 flex size-6 shrink-0 items-center justify-center text-[14px] transition-colors hover:text-(--foreground)!"
              style={{ color: "#b8b8b8" }}
            >
              {showPwd ? <EyeOutlined /> : <EyeInvisibleOutlined />}
            </button>
          </div>

          {error && (
            <p className="px-0.5 text-[12.5px] font-light" style={{ color: C.danger }}>
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={!canSubmit}
            className="mt-2 h-[52px] w-full rounded-[12px] text-[16px] font-medium text-white transition-colors"
            style={canSubmit ? { background: C.ink } : { background: C.disabled, cursor: "not-allowed" }}
          >
            {submitting ? "登录中…" : "登录"}
          </button>
        </form>

        {/* 其他登录方式 */}
        <div className="mt-11 flex items-center gap-3">
          <span className="h-px flex-1" style={{ background: C.line }} />
          <span className="text-[12px] font-light" style={{ color: "#b8b8b8" }}>
            其他登录方式
          </span>
          <span className="h-px flex-1" style={{ background: C.line }} />
        </div>

        <div className="mt-6 flex items-center justify-center gap-6">
          {CHANNELS.map((ch) => (
            <button
              key={ch.key}
              type="button"
              onClick={() => setHint(`${ch.label}登录暂未开放，请先用账号密码登录`)}
              aria-label={`${ch.label}登录`}
              title={`${ch.label}登录`}
              className="flex w-[52px] flex-col items-center gap-2 transition-opacity hover:opacity-70"
            >
              <span
                className="flex size-[52px] items-center justify-center rounded-full"
                style={{ background: C.field, color: "#8f8f8f" }}
              >
                <ch.icon className="text-[20px]" />
              </span>
              <span className="text-[11px] font-light" style={{ color: "#b0b0b0" }}>
                {ch.label}
              </span>
            </button>
          ))}
        </div>

        {hint && (
          <p className="mt-3 text-center text-[12px] font-light" style={{ color: C.brand }}>
            {hint}
          </p>
        )}

        {/* 协议 */}
        <p
          className="mt-9 text-center text-[12.5px] font-light leading-[1.7]"
          style={{ color: "#b0b0b0" }}
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
      </div>
    </div>
  );
}
