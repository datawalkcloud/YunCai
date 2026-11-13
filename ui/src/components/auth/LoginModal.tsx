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
 * 配色全部取 index.css 的主题 token，跟「主题设置」的主题色与明暗走。
 * 后端当前只有 Auth/login（用户名 + 密码）一种凭据，因此主入口是账号密码；
 * 其余渠道保留展示，点击时提示未开放，不做假跳转。
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
  const { login } = useAuth();

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [showPwd, setShowPwd] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hint, setHint] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const userRef = useRef<HTMLInputElement | null>(null);
  const pwdRef = useRef<HTMLInputElement | null>(null);

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
      // 跳转交给宿主页面：/login 直达由 RedirectIfAuthed 负责，官网就地开窗由 Landing 负责。
      // 这里绝不调用 onClose（它会把 /login 回落成 /，与登录跳转互相覆盖）。
      if (onSuccess) onSuccess();
      else onClose();
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
        className="relative max-h-[92svh] w-full max-w-[480px] overflow-y-auto rounded-2xl bg-(--card) px-8 py-12 sm:px-12 sm:py-14"
        style={{ boxShadow: "0 24px 60px -20px rgba(10,20,16,.28)" }}
      >
        {/* 关闭 */}
        <button
          type="button"
          onClick={onClose}
          aria-label="关闭登录"
          className="absolute right-4 top-4 flex size-8 items-center justify-center rounded-full text-[13px] transition-colors hover:bg-(--muted)"
          style={{ color: C.faint }}
        >
          <CloseOutlined />
        </button>

        {/* 品牌 */}
        <div className="flex items-center justify-center gap-2.5">
          <BrandLogo size={36} />
          <span className="text-[16px] font-medium tracking-tight" style={{ color: C.ink }}>
            云财
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
            {submitting ? "登录中…" : "登录"}
          </button>
        </form>

        {/* 其他登录方式 */}
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
              onClick={() => setHint(`${ch.label}登录暂未开放，请先用账号密码登录`)}
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
      </div>
    </div>
  );
}
