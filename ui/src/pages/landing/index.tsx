import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import {
  AimOutlined,
  ApartmentOutlined,
  ArrowRightOutlined,
  AuditOutlined,
  BankOutlined,
  BarChartOutlined,
  BookOutlined,
  CheckOutlined,
  CloseOutlined,
  ContainerOutlined,
  DatabaseOutlined,
  DownOutlined,
  FileTextOutlined,
  FolderOpenOutlined,
  LockOutlined,
  RobotOutlined,
  SafetyCertificateOutlined,
  SyncOutlined,
  TeamOutlined,
  ThunderboltOutlined,
  WalletOutlined,
} from "@ant-design/icons";

import { useAuth } from "@/auth/AuthContext";
import { LoginModal } from "@/components/auth/LoginModal";
import { BrandLogo } from "@/components/brand/BrandLogo";

/* ============================================================
 * 云财官网 · 落地页
 *
 * 视觉口径与工作台同源：颜色全部取 index.css 的品牌/中性 token（--brand、
 * --brand-fill、--brand-soft、--card、--border、--muted-foreground…），圆角取
 * rounded-* 档位（由 --seed-radius 派生），因此「主题设置」换主题色 / 圆角 /
 * 暗色时官网跟着变，不再出现第二套硬编码配色。
 * 字重只用 400/500/600，正文不小于 13px；区块节奏 64/96px，内容宽 1200。
 * ============================================================ */

const NAV = [
  { label: "为什么是云财", href: "#why" },
  { label: "产品能力", href: "#capabilities" },
  { label: "使用场景", href: "#scenarios" },
  { label: "安全合规", href: "#security" },
  { label: "常见问题", href: "#faq" },
];

const CAPABILITIES = [
  { icon: FileTextOutlined, title: "凭证管理", desc: "录入 · 入账 · 出表" },
  { icon: BookOutlined, title: "账簿查询", desc: "总账 · 明细 · 日记账" },
  { icon: BarChartOutlined, title: "财务报表", desc: "资产负债 · 利润 · 现金流" },
  { icon: WalletOutlined, title: "资金出纳", desc: "日记账 · 转账 · 出票" },
  { icon: ContainerOutlined, title: "发票管理", desc: "销项 · 进项 · 查验" },
  { icon: TeamOutlined, title: "工资核算", desc: "计提 · 发放 · 代扣" },
  { icon: BankOutlined, title: "固定资产", desc: "卡片 · 折旧 · 清理" },
  { icon: FolderOpenOutlined, title: "电子档案", desc: "影像 · 附件 · 归档" },
];

const DIFFERENTIATORS: {
  key: string;
  icon: typeof RobotOutlined;
  title: string;
  desc: string;
  points?: string[];
}[] = [
  {
    key: "ai",
    icon: RobotOutlined,
    title: "拍张发票，凭证自己长出来",
    desc: "上传发票、回单或收据照片，自动识别金额、税额、开票方，匹配科目后直接生成凭证——你只需要点确认。",
    points: ["票据识别 · 自动分录用", "摘要、往来、税率自动补全", "上百张票据一次成账"],
  },
  {
    key: "tax",
    icon: SafetyCertificateOutlined,
    title: "电子税务局直连",
    desc: "发票、申报记录、完税证明在系统里直接取回，不用在两个窗口之间来回抄数。",
  },
  {
    key: "filing",
    icon: ThunderboltOutlined,
    title: "一键报税",
    desc: "按已入账数据算好申报口径，增值税、附加税、企业所得税预填核对，一键推送。",
  },
  {
    key: "bank",
    icon: SyncOutlined,
    title: "银行流水自动入账",
    desc: "授权一次，流水每日自动同步，按规则归类、批量生成凭证，银企对账一键对平。",
  },
  {
    key: "drill",
    icon: AimOutlined,
    title: "每个数字都能钻到底",
    desc: "报表任意一行双击即下钻到明细账，再下钻到原始凭证与票据影像，查账不用换系统。",
  },
  {
    key: "open",
    icon: DatabaseOutlined,
    title: "数据是你的，随时带走",
    desc: "全量凭证、账簿与报表一键导出 Excel / PDF，开放 API 对接业务系统，不做数据绑架。",
  },
];

const COMPARISON: { old: string; now: string }[] = [
  { old: "一张一张翻票、敲 Excel", now: "票据拍照识别，凭证自动生成" },
  { old: "税局、账务两个窗口来回抄数", now: "税局数据直接进系统，取回即入账" },
  { old: "月底手工对银行流水", now: "流水自动同步、规则归类、一键对账" },
  { old: "报表出完还要回头翻凭证", now: "报表双击下钻到明细与原始票据" },
  { old: "数据锁在软件里，换系统从头再来", now: "全量数据一键导出，随时迁走" },
];

const SECURITY = [
  {
    icon: ApartmentOutlined,
    title: "账套级数据隔离",
    desc: "每家公司的凭证、账簿、报表相互独立，跨账套不可见，从数据结构上杜绝串账。",
  },
  {
    icon: LockOutlined,
    title: "登录鉴权与留痕",
    desc: "令牌校验保护每次访问，关键操作写入日志，谁在什么时候改了什么一目了然。",
  },
  {
    icon: AuditOutlined,
    title: "准则口径出表",
    desc: "科目编码与报表行次遵循企业会计准则，结转前后口径一致，可直接对外报送。",
  },
];

const STEPS = [
  { title: "创建账套", desc: "填写企业信息与启用期间，账套即刻可用。" },
  { title: "初始化科目", desc: "套用预置科目模板，按需增删、调整层级。" },
  { title: "录入凭证", desc: "手工录入或 Excel 批量导入，借贷自动校验。" },
  { title: "保存入账出表", desc: "保存凭证即入账，账簿与报表实时归集，随点随出。" },
];

const FAQS = [
  { q: "需要安装客户端吗？", a: "不需要。云财是纯在线的财务系统，打开浏览器登录即可使用，换台设备也能接着做账。" },
  { q: "一个账号可以管理多家公司吗？", a: "可以。一个账号支持创建多个账套，账套之间数据完全隔离，在顶部随时切换，互不干扰。" },
  { q: "历史凭证怎么迁移？", a: "在「凭证导入」中上传符合模板的 Excel（.xls / .xlsx）文件即可批量导入，系统会自动补建缺失科目，借贷平衡的凭证导入后直接入账。" },
  { q: "报表数据是实时的吗？", a: "是。科目余额表、资产负债表、利润表、现金流量表都从已过账凭证实时归集计算，期初余额一并参与，任意期间可即时切换。" },
  { q: "期末结转和结账怎么处理？", a: "系统提供一键结转损益与批量结账，自动生成本年利润结转凭证并直接过账；资产负债表会把损益并入未分配利润，保证结转前后口径一致。" },
  { q: "能直连电子税务局和银行吗？", a: "电子税务局已接通数字账户，发票、申报记录与完税证明可以在系统内直接取回，个税走自然人电子税务局扣缴端；银行流水支持按模板导入并自动归类，直连同步正在陆续开通。" },
];

/* ------------------------------------------------------------------ */
/*  布局与动效                                                          */
/* ------------------------------------------------------------------ */

function Container({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return <div className={"mx-auto w-full max-w-[1200px] px-5 md:px-8 " + className}>{children}</div>;
}

/** 进入视口渐显；系统偏好「减少动效」时直接呈现。 */
function Reveal({
  children,
  delay = 0,
  className = "",
}: {
  children: React.ReactNode;
  delay?: number;
  className?: string;
}) {
  const ref = useRef<HTMLDivElement | null>(null);
  const [shown, setShown] = useState(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      setShown(true);
      return;
    }
    const io = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) {
          setShown(true);
          io.disconnect();
        }
      },
      { threshold: 0.1, rootMargin: "0px 0px -32px 0px" },
    );
    io.observe(el);
    return () => io.disconnect();
  }, []);

  return (
    <div
      ref={ref}
      className={className}
      style={{
        opacity: shown ? 1 : 0,
        transform: shown ? "none" : "translateY(20px)",
        transition: `opacity .5s ease ${delay}ms, transform .5s cubic-bezier(.22,.61,.36,1) ${delay}ms`,
      }}
    >
      {children}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  通用组件                                                           */
/* ------------------------------------------------------------------ */

function Logo({ tone = "dark" }: { tone?: "dark" | "light" }) {
  return (
    <span className="flex items-center gap-2.5">
      <BrandLogo size={32} />
      <span
        className="text-[17px] font-semibold tracking-tight"
        style={{ color: tone === "dark" ? "var(--foreground)" : "#fff" }}
      >
        云财
      </span>
    </span>
  );
}

function Button({
  children,
  onClick,
  href,
  variant = "primary",
  size = "md",
}: {
  children: React.ReactNode;
  onClick?: () => void;
  href?: string;
  variant?: "primary" | "ghost" | "inverse";
  size?: "md" | "sm";
}) {
  const box =
    variant === "primary"
      ? { background: "var(--brand-fill)", color: "#fff" }
      : variant === "inverse"
        ? { background: "#fff", color: "var(--brand-fill)" }
        : { background: "transparent", color: "var(--foreground)", boxShadow: "inset 0 0 0 1px var(--border)" };
  const cls =
    "inline-flex items-center justify-center gap-2 rounded-lg font-medium transition-all duration-200 " +
    (size === "md" ? "h-12 px-7 text-[15px]" : "h-9 px-4 text-[13px]") +
    (variant === "primary" ? " hover:brightness-110" : variant === "inverse" ? " hover:bg-white/90" : " hover:bg-(--muted)");
  if (href) {
    return (
      <a href={href} className={cls} style={box}>
        {children}
      </a>
    );
  }
  return (
    <button type="button" onClick={onClick} className={cls} style={box}>
      {children}
    </button>
  );
}

function Tag({ cn, en }: { cn: string; en: string }) {
  return (
    <span
      className="inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 text-[12px] leading-none"
      style={{ background: "var(--brand-soft)", color: "var(--brand)" }}
    >
      <em className="font-medium not-italic">{cn}</em>
      <em className="font-normal not-italic tracking-[0.08em] uppercase opacity-70">{en}</em>
    </span>
  );
}

function SectionHead({
  cn,
  en,
  title,
  desc,
  center = false,
}: {
  cn: string;
  en: string;
  title: React.ReactNode;
  desc?: string;
  center?: boolean;
}) {
  if (center) {
    return (
      <div className="flex flex-col items-center text-center">
        <Reveal>
          <Tag cn={cn} en={en} />
        </Reveal>
        <Reveal delay={60}>
          <h2 className="mt-4 text-[30px] font-semibold leading-[1.25] tracking-[-0.01em] md:text-[40px]">{title}</h2>
        </Reveal>
        {desc && (
          <Reveal delay={120}>
            <p className="mt-4 max-w-[600px] text-[15px] leading-[1.8] text-(--muted-foreground)">{desc}</p>
          </Reveal>
        )}
      </div>
    );
  }
  return (
    <div className="grid gap-6 md:grid-cols-[minmax(0,460px)_minmax(0,1fr)] md:items-end md:gap-12">
      <div className="flex flex-col items-start gap-4">
        <Reveal>
          <Tag cn={cn} en={en} />
        </Reveal>
        <Reveal delay={60}>
          <h2 className="text-[30px] font-semibold leading-[1.25] tracking-[-0.01em] md:text-[40px]">{title}</h2>
        </Reveal>
      </div>
      {desc && (
        <Reveal delay={120}>
          <p className="max-w-[440px] text-[15px] leading-[1.8] text-(--muted-foreground)">{desc}</p>
        </Reveal>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Hero 应用窗口（示意数据，观感与工作台同源）                          */
/* ------------------------------------------------------------------ */

const MENU = ["总览", "凭证", "账簿", "报表", "资金", "发票", "工资", "资产"];

const KPIS = [
  { k: "资产总额", v: "3,071.20", d: "+2.6%" },
  { k: "本月收入", v: "339.10", d: "+18.7%" },
  { k: "本月费用", v: "32.40", d: "-4.1%" },
  { k: "净利润", v: "306.70", d: "+21.3%" },
];

const BALANCE_ROWS: [string, string, string, string][] = [
  ["货币资金", "1,842.60", "短期借款", "300.00"],
  ["应收账款", "226.30", "应付账款", "186.40"],
  ["存货", "612.10", "实收资本", "2,000.00"],
  ["固定资产", "390.20", "未分配利润", "306.70"],
  ["资产总计", "3,071.20", "负债和所有者权益总计", "3,071.20"],
];

const BARS = [
  { m: "05", v: 58 },
  { m: "06", v: 42 },
  { m: "07", v: 71 },
  { m: "08", v: 64 },
  { m: "09", v: 88 },
  { m: "10", v: 100 },
];

function AppWindow() {
  return (
    <div
      className="overflow-hidden rounded-xl bg-(--card)"
      style={{
        border: "1px solid var(--border)",
        boxShadow: "0 40px 90px -48px color-mix(in srgb, var(--seed-primary) 45%, transparent)",
      }}
    >
      {/* 浏览器铬 */}
      <div className="flex h-9 items-center gap-3 border-b border-(--border) bg-(--card) px-4">
        <span className="flex gap-1.5">
          <span className="size-2 rounded-full bg-(--icon-placeholder)" />
          <span className="size-2 rounded-full bg-(--icon-placeholder)" />
          <span className="size-2 rounded-full bg-(--icon-placeholder)" />
        </span>
        <span className="mx-auto rounded-md bg-(--muted) px-3 py-0.5 text-[11px] text-(--muted-foreground)">
          app.yuncai.cn / 总览
        </span>
        <span className="w-8" />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-[168px_1fr]">
        {/* 侧栏：与工作台同一组 sidebar token */}
        <aside className="hidden border-r border-(--sidebar-border) bg-(--sidebar) p-3 lg:block">
          <div className="mb-3 flex items-center gap-2 px-2">
            <span
              className="flex size-5 items-center justify-center rounded-md text-[10px] font-medium text-white"
              style={{ background: "var(--brand-fill)" }}
            >
              云
            </span>
            <span className="truncate text-[12px] font-medium text-(--sidebar-foreground)">杭州数从科技</span>
          </div>
          <div className="flex flex-col gap-0.5">
            {MENU.map((m, i) => (
              <span
                key={m}
                className={
                  "rounded-md px-2.5 py-1.5 text-[12.5px] " +
                  (i === 0 ? "bg-(--sidebar-accent) font-medium text-(--sidebar-foreground)" : "text-(--muted-foreground)")
                }
              >
                {m}
              </span>
            ))}
          </div>
        </aside>

        <div className="min-w-0 bg-(--background) p-4 md:p-5">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-[14px] font-semibold">2026 年 10 月</p>
              <p className="mt-0.5 text-[12px] text-(--muted-foreground)">数据来自已过账凭证</p>
            </div>
            <span className="rounded-md px-3 py-1.5 text-[12px] font-medium text-white" style={{ background: "var(--brand-fill)" }}>
              + 新增凭证
            </span>
          </div>

          <div className="mt-4 grid grid-cols-2 gap-2.5 md:grid-cols-4">
            {KPIS.map((c) => (
              <div key={c.k} className="rounded-lg border border-(--border) bg-(--card) p-3">
                <p className="text-[12px] text-(--muted-foreground)">{c.k}</p>
                <p className="mt-1 text-[19px] font-semibold tabular-nums tracking-tight">{c.v}</p>
                <p className="mt-0.5 text-[12px] font-medium tabular-nums" style={{ color: "var(--success)" }}>
                  {c.d}
                </p>
              </div>
            ))}
          </div>

          <div className="mt-3 overflow-hidden rounded-lg border border-(--border) bg-(--card)">
            <div className="flex items-center justify-between border-b border-(--border) px-3 py-2">
              <span className="text-[12.5px] font-medium">资产负债表 · 简表</span>
              <span className="text-[12px] text-(--muted-foreground)">会企 01 表</span>
            </div>
            {BALANCE_ROWS.map(([l1, v1, l2, v2], i) => {
              const total = i === BALANCE_ROWS.length - 1;
              return (
                <div
                  key={l1}
                  className="grid grid-cols-2 gap-3 border-b border-(--border) px-3 py-1.5 text-[12.5px] last:border-b-0"
                  style={total ? { background: "var(--table-zebra-bg)" } : undefined}
                >
                  <span className="flex justify-between gap-2">
                    <span className={"truncate " + (total ? "font-medium" : "text-(--muted-foreground)")}>{l1}</span>
                    <span
                      className={"tabular-nums " + (total ? "font-semibold" : "")}
                      style={total ? { color: "var(--brand)" } : undefined}
                    >
                      {v1}
                    </span>
                  </span>
                  <span className="flex justify-between gap-2 border-l border-(--border) pl-3">
                    <span className={"truncate " + (total ? "font-medium" : "text-(--muted-foreground)")}>{l2}</span>
                    <span
                      className={"tabular-nums " + (total ? "font-semibold" : "")}
                      style={total ? { color: "var(--brand)" } : undefined}
                    >
                      {v2}
                    </span>
                  </span>
                </div>
              );
            })}
          </div>

          {/* 近 6 期收入趋势：柱高用 px，避免百分比高度在 auto 高父级里塌陷 */}
          <div className="mt-3 rounded-lg border border-(--border) bg-(--card) p-3">
            <div className="flex items-center justify-between">
              <span className="text-[12.5px] font-medium">近 6 期收入趋势</span>
              <span className="text-[12px] text-(--muted-foreground)">单位：万元</span>
            </div>
            <div className="mt-3 flex h-[104px] items-end gap-3">
              {BARS.map((b, i) => (
                <div key={b.m} className="flex h-full flex-1 flex-col items-center justify-end gap-1.5">
                  <div
                    className="w-full rounded-t-md"
                    style={{
                      height: `${Math.round(b.v * 0.72)}px`,
                      background:
                        i === BARS.length - 1
                          ? "var(--brand-fill)"
                          : `color-mix(in srgb, var(--seed-primary) ${i === BARS.length - 2 ? 45 : 22}%, transparent)`,
                    }}
                  />
                  <span className="text-[11px] tabular-nums text-(--muted-foreground)">{b.m}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  场景数据                                                            */
/* ------------------------------------------------------------------ */

type DocRow = { l: string; r?: string; kind?: "head" | "total" | "dim" | "ok" };

const SCENES: {
  key: string;
  tab: string;
  title: string;
  titleAccent: string;
  desc: string;
  points: string[];
  file: string;
  stateTag: string;
  rows: DocRow[];
  note: string;
  mockLabel: string;
  mockRows: { name: string; state: string; ok: boolean }[];
}[] = [
  {
    key: "voucher",
    tab: "凭证录入",
    title: "从录入到入账，",
    titleAccent: "一步完成",
    desc: "结构化的分录录入，借贷平衡与金额大写实时校验；保存即入账，账簿与报表立即取到数。历史凭证可从 Excel 批量导入，借贷平衡的直接入账。",
    points: ["借贷自动平衡校验", "金额大写自动生成", "Excel 批量导入历史凭证"],
    file: "记账凭证 记-0007",
    stateTag: "已过账",
    rows: [
      { l: "借 · 银行存款", r: "120,000.00", kind: "head" },
      { l: "贷 · 主营业务收入", r: "113,207.55" },
      { l: "贷 · 应交税费—应交增值税", r: "6,792.45" },
      { l: "合计 · 壹拾贰万元整", r: "120,000.00", kind: "total" },
    ],
    note: "借贷平衡，保存即入账",
    mockLabel: "凭证状态",
    mockRows: [
      { name: "记-0007 银行收款", state: "已过账", ok: true },
      { name: "记-0008 采购付款", state: "已过账", ok: true },
      { name: "记-0009 计提工资", state: "草稿", ok: false },
    ],
  },
  {
    key: "report",
    tab: "报表出表",
    title: "报表随点随出，",
    titleAccent: "不用再等月底",
    desc: "科目余额表、资产负债表、利润表、现金流量表全部从已过账凭证实时归集，期初余额一并参与，任意期间即时切换。",
    points: ["会企 01 表标准行次", "任意期间即时切换", "资产 = 负债 + 所有者权益自动平衡"],
    file: "资产负债表 2026-10",
    stateTag: "已出表",
    rows: [
      { l: "资产总计", r: "1,286,400.00", kind: "head" },
      { l: "负债合计", r: "486,400.00" },
      { l: "所有者权益合计", r: "800,000.00" },
      { l: "负债和所有者权益总计", r: "1,286,400.00", kind: "total" },
    ],
    note: "资产 = 负债 + 所有者权益",
    mockLabel: "会计期间",
    mockRows: [
      { name: "2026 年 10 期", state: "当前", ok: true },
      { name: "2026 年 09 期", state: "已结账", ok: false },
      { name: "2026 年 08 期", state: "已结账", ok: false },
    ],
  },
  {
    key: "funds",
    tab: "资金出纳",
    title: "流水与凭证，",
    titleAccent: "不再两头对",
    desc: "日记账逐笔登记余额，内部转账两端自动落账；流水可按日期、类别、方向合并批量生成凭证，出纳台账与总账自动对得上。",
    points: ["账户启停用与开户行信息", "内部转账自动双边登记", "批量出票支持合并方式"],
    file: "现金日记账 2026-10",
    stateTag: "已对账",
    rows: [
      { l: "10-24 · 收到货款", r: "+86,000.00", kind: "head" },
      { l: "10-25 · 支付供应商", r: "-42,300.00" },
      { l: "10-26 · 内部转账", r: "-20,000.00" },
      { l: "余额 · 招商银行 6225", r: "1,842,600.00", kind: "total" },
    ],
    note: "本月已生成凭证 26 张",
    mockLabel: "生成凭证",
    mockRows: [
      { name: "按日期合并", state: "推荐", ok: true },
      { name: "按类别合并", state: "可选", ok: false },
      { name: "按方向合并", state: "可选", ok: false },
    ],
  },
  {
    key: "closing",
    tab: "期末结转",
    title: "结转与结账，",
    titleAccent: "一次点完",
    desc: "一键结转损益，系统自动生成本年利润结转凭证并直接过账；随后批量结账，报表口径在结转前后保持完全一致。",
    points: ["结转损益自动生成凭证", "支持自定义结转模板", "批量结账、可撤销重做"],
    file: "期末处理 2026-10",
    stateTag: "已结账",
    rows: [
      { l: "结转损益", r: "已过账", kind: "ok" },
      { l: "结转未分配利润", r: "已过账", kind: "ok" },
      { l: "关账检查", r: "试算平衡", kind: "ok" },
      { l: "结账期间 2026-10", r: "已结账", kind: "total" },
    ],
    note: "下一期间：2026-11 · 待处理",
    mockLabel: "处理进度",
    mockRows: [
      { name: "结转损益", state: "完成", ok: true },
      { name: "结转本年利润", state: "完成", ok: true },
      { name: "批量结账", state: "完成", ok: true },
    ],
  },
];

/* ------------------------------------------------------------------ */
/*  页面                                                               */
/* ------------------------------------------------------------------ */

export default function LandingPage({ autoOpenLogin = false }: { autoOpenLogin?: boolean } = {}) {
  const { status, needsPasswordSetup } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const navigatedRef = useRef(false);
  const authed = status === "authenticated";
  // 验证码首次登录的账号还没设密码，弹窗要继续开着走完设置流程，且在设完前不算可进入后台。
  const canEnterApp = authed && !needsPasswordSetup;
  const primaryHref = canEnterApp ? "/app" : "/login";
  const primaryLabel = canEnterApp ? "进入控制台" : "免费开始使用";
  const [activeTab, setActiveTab] = useState(SCENES[0].key);
  const active = SCENES.find((s) => s.key === activeTab) ?? SCENES[0];
  const [scrolled, setScrolled] = useState(false);
  // 登录改为弹窗：未登录时所有「开始使用 / 登录」入口都就地开窗，不再跳独立登录页。
  // autoOpenLogin 用于 /login 直达（含保护区回跳带上的 from），保持深链可用。
  const [loginOpen, setLoginOpen] = useState(autoOpenLogin);

  const goApp = () => (canEnterApp ? navigate(primaryHref) : setLoginOpen(true));

  // 关闭弹窗时若停在 /login 上，回落到官网根路径，避免 URL 停在登录态。
  // 仅在用户主动关闭（× / Esc / 点遮罩）时调用，登录成功不走这里。
  const closeLogin = () => {
    setLoginOpen(false);
    if (window.location.pathname === "/login") {
      navigate("/", { replace: true });
    }
  };

  /** 登录成功后回到来源页；一次会话只跳一次，避免多处导航互相覆盖。 */
  const goAfterLogin = () => {
    if (navigatedRef.current) return;
    navigatedRef.current = true;
    const from = (location.state as { from?: string } | null)?.from ?? "/app";
    navigate(from, { replace: true });
  };

  /** 登录成功：只关弹窗；跳转交给唯一一处负责，避免与路由守卫重复导航。 */
  const handleLoginSuccess = () => {
    setLoginOpen(false);
    // /login 直达时 RedirectIfAuthed 会在认证状态变更后统一跳转；
    // 官网首页就地开窗没有该守卫，由这里自己跳。
    if (!autoOpenLogin) goAfterLogin();
  };

  useEffect(() => {
    setLoginOpen(autoOpenLogin);
  }, [autoOpenLogin]);

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <div id="top" className="flex min-h-svh scroll-smooth flex-col bg-(--card) font-sans text-(--foreground) antialiased">
      {/* ============ 导航 ============ */}
      <header
        className="fixed inset-x-0 top-0 z-50 h-16 transition-all duration-300"
        style={{
          background: "color-mix(in srgb, var(--card) 86%, transparent)",
          backdropFilter: "blur(14px)",
          borderBottom: scrolled ? "1px solid var(--border)" : "1px solid transparent",
        }}
      >
        <Container className="grid h-full grid-cols-[1fr_auto_1fr] items-center">
          <Link to="/" aria-label="云财首页" className="justify-self-start">
            <Logo />
          </Link>

          <nav className="hidden items-center gap-1 lg:flex">
            {NAV.map((l) => (
              <a
                key={l.href}
                href={l.href}
                className="rounded-md px-4 py-2 text-[14px] text-(--muted-foreground) transition-colors hover:bg-(--muted) hover:text-(--foreground)"
              >
                {l.label}
              </a>
            ))}
          </nav>

          <div className="flex items-center justify-self-end gap-2">
            {!authed && (
              <button
                type="button"
                onClick={() => setLoginOpen(true)}
                className="hidden rounded-md px-4 py-2 text-[14px] text-(--muted-foreground) transition-colors hover:text-(--foreground) sm:block"
              >
                登录
              </button>
            )}
            <Button onClick={goApp} size="sm">
              {authed ? "进入控制台" : "开始使用"}
              <ArrowRightOutlined className="text-[11px]" />
            </Button>
          </div>
        </Container>
      </header>

      <main className="flex-1">
        {/* ============ Hero ============ */}
        <section className="relative overflow-hidden">
          <div
            className="pointer-events-none absolute inset-x-0 top-0 h-[720px]"
            style={{
              background:
                "radial-gradient(60% 52% at 50% 0%, color-mix(in srgb, var(--seed-primary) 10%, transparent) 0%, transparent 100%)",
            }}
          />
          <div className="pointer-events-none absolute inset-x-0 top-0 h-[560px] bg-[linear-gradient(to_right,var(--border)_1px,transparent_1px),linear-gradient(to_bottom,var(--border)_1px,transparent_1px)] bg-[length:56px_56px] opacity-40 [mask-image:radial-gradient(70%_60%_at_50%_0%,black,transparent)]" />

          <Container className="relative flex flex-col items-center pt-[120px] text-center md:pt-[144px]">
            <Reveal>
              <span
                className="inline-flex items-center gap-2 rounded-full px-3.5 py-1.5 text-[12.5px] font-medium"
                style={{ background: "var(--brand-soft)", color: "var(--brand)" }}
              >
                <span className="size-1.5 rounded-full" style={{ background: "var(--brand-fill)" }} />
                AI 记账 · 税局直连 · 浏览器即用
              </span>
            </Reveal>

            <Reveal delay={60}>
              <h1 className="mt-6 max-w-[880px] text-[clamp(28px,9.2vw,40px)] font-semibold leading-[1.15] tracking-[-0.02em] sm:text-[52px] md:text-[64px]">
                做账，本该如此简单
              </h1>
            </Reveal>

            <Reveal delay={120}>
              <p className="mt-5 w-full max-w-[620px] text-[16px] leading-[1.8] text-(--muted-foreground)">
                拍张发票就出凭证，税局数据直接进账，报表在入账那一秒生成。
                <br className="hidden md:block" />
                凭证、账簿、报表、出纳、发票、工资、资产、档案，一条线走完。
              </p>
            </Reveal>

            <Reveal delay={180}>
              <div className="mt-8 flex flex-col items-center">
                <Button onClick={goApp}>
                  {primaryLabel}
                  <ArrowRightOutlined className="text-[12px]" />
                </Button>
              </div>
            </Reveal>

            <Reveal delay={220}>
              <div className="mt-7 flex flex-wrap items-center justify-center gap-x-7 gap-y-2 text-[13px] text-(--muted-foreground)">
                {[
                  { icon: RobotOutlined, t: "AI 自动生成凭证" },
                  { icon: SafetyCertificateOutlined, t: "电子税务局直连" },
                  { icon: AimOutlined, t: "报表逐级下钻" },
                ].map((b) => (
                  <span key={b.t} className="flex items-center gap-1.5">
                    <b.icon className="text-[13px]" style={{ color: "var(--brand)" }} />
                    {b.t}
                  </span>
                ))}
              </div>
            </Reveal>
          </Container>

          <Container className="relative mt-12 md:mt-16">
            <Reveal delay={260}>
              <AppWindow />
            </Reveal>
          </Container>
        </section>

        {/* ============ 场景 ============ */}
        <section id="scenarios" className="scroll-mt-16 pt-16 md:pt-24">
          <Container>
            <SectionHead
              cn="使用场景"
              en="Scenarios"
              title={
                <>
                  一天的账，
                  <br />
                  一套系统走完
                </>
              }
              desc="从早上录入第一张凭证，到月底结转结账，云财把财务的每一个动作串成一条不断线的流程——你不用在几个系统之间搬运数据。"
            />

            <Reveal delay={60}>
              <div
                className="mt-8 inline-flex max-w-full gap-1 overflow-x-auto rounded-lg p-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
                style={{ background: "var(--muted)" }}
                role="tablist"
                aria-label="使用场景"
              >
                {SCENES.map((s) => (
                  <button
                    key={s.key}
                    type="button"
                    role="tab"
                    aria-selected={s.key === activeTab}
                    onClick={() => setActiveTab(s.key)}
                    className={
                      "shrink-0 rounded-md px-5 py-2 text-[14px] transition-all duration-200 " +
                      (s.key === activeTab
                        ? "bg-(--card) font-medium text-(--foreground) shadow-sm"
                        : "text-(--muted-foreground) hover:text-(--foreground)")
                    }
                  >
                    {s.tab}
                  </button>
                ))}
              </div>
            </Reveal>

            <Reveal delay={100}>
              <div className="mt-6 overflow-hidden rounded-xl border border-(--border) bg-(--card)">
                <div className="grid lg:grid-cols-[400px_1fr]">
                  <div className="flex flex-col justify-center px-6 py-8 md:px-10 md:py-10">
                    <h3 className="text-[24px] font-semibold leading-[1.4] md:text-[26px]">
                      {active.title}
                      <span className="block" style={{ color: "var(--brand)" }}>
                        {active.titleAccent}
                      </span>
                    </h3>
                    <p className="mt-4 text-[14px] leading-[1.9] text-(--muted-foreground)">{active.desc}</p>
                    <ul className="mt-6 flex flex-col gap-3">
                      {active.points.map((p) => (
                        <li key={p} className="flex items-center gap-2.5 text-[14px]">
                          <span
                            className="flex size-4.5 shrink-0 items-center justify-center rounded-full"
                            style={{ background: "var(--brand-soft)", color: "var(--brand)" }}
                          >
                            <CheckOutlined className="text-[9px]" />
                          </span>
                          {p}
                        </li>
                      ))}
                    </ul>
                    <a
                      href={primaryHref}
                      onClick={(e) => {
                        e.preventDefault();
                        goApp();
                      }}
                      className="mt-7 inline-flex w-fit items-center gap-1.5 text-[14px] font-medium transition-all hover:gap-2.5"
                      style={{ color: "var(--brand)" }}
                    >
                      进入系统体验
                      <ArrowRightOutlined className="text-[11px]" />
                    </a>
                  </div>

                  <div
                    className="flex flex-col justify-center gap-4 border-t border-(--border) p-6 md:p-8 lg:border-t-0 lg:border-l"
                    style={{ background: "var(--brand-soft)" }}
                  >
                    {/* 单据窗：label/value 两栏对齐，替代手工空格排版的等宽文本 */}
                    <div className="rounded-lg border border-(--border) bg-(--card) p-5">
                      <div className="flex items-center justify-between gap-3">
                        <span
                          className="truncate rounded-md px-2.5 py-1 text-[12px] font-medium"
                          style={{ background: "var(--brand-tint)", color: "var(--brand)" }}
                        >
                          {active.file}
                        </span>
                        <span className="shrink-0 text-[12px] text-(--muted-foreground)">{active.stateTag}</span>
                      </div>
                      <div className="mt-4">
                        {active.rows.map((r) => (
                          <div
                            key={r.l}
                            className={
                              "flex items-baseline justify-between gap-4 border-b border-(--border) py-2 text-[13px] last:border-b-0 " +
                              (r.kind === "total" ? "font-medium" : "")
                            }
                            style={r.kind === "total" ? { background: "var(--table-zebra-bg)" } : undefined}
                          >
                            <span className={r.kind === "head" || r.kind === "total" ? "font-medium" : "text-(--muted-foreground)"}>
                              {r.l}
                            </span>
                            <span
                              className="shrink-0 tabular-nums"
                              style={{
                                color:
                                  r.kind === "ok"
                                    ? "var(--success)"
                                    : r.kind === "total" || r.kind === "head"
                                      ? "var(--foreground)"
                                      : "var(--muted-foreground)",
                                fontWeight: r.kind === "total" || r.kind === "ok" ? 500 : 400,
                              }}
                            >
                              {r.r}
                            </span>
                          </div>
                        ))}
                      </div>
                      <p className="mt-3 text-[12px] text-(--muted-foreground)">{active.note}</p>
                    </div>

                    <div className="rounded-lg border border-(--border) bg-(--card) px-4 py-3">
                      <p className="mb-1 text-[12px] font-medium tracking-[0.06em] text-(--muted-foreground)">{active.mockLabel}</p>
                      {active.mockRows.map((r) => (
                        <div
                          key={r.name}
                          className="flex items-center justify-between border-b border-(--border) py-2 text-[13px] last:border-b-0"
                        >
                          <span className="text-(--muted-foreground)">{r.name}</span>
                          <span
                            className="flex items-center gap-1.5 font-medium"
                            style={{ color: r.ok ? "var(--brand)" : "var(--icon-muted)" }}
                          >
                            <span
                              className="size-1.5 rounded-full"
                              style={{ background: r.ok ? "var(--brand-fill)" : "var(--icon-placeholder)" }}
                            />
                            {r.state}
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              </div>
            </Reveal>
          </Container>
        </section>

        {/* ============ 为什么是云财 ============ */}
        <section id="why" className="scroll-mt-16 pt-16 md:pt-24">
          <Container>
            <SectionHead
              cn="为什么是云财"
              en="Why YunCai"
              title={
                <>
                  别人给你一本电子账簿，
                  <br />
                  我们替你把手上的活干完
                </>
              }
              desc="录凭证、翻票、抄税局数据、对银行流水——系统能代劳的重复动作，就不该再占一个人的时间。"
            />

            <div className="mt-10 grid gap-3 md:mt-14 md:auto-rows-fr md:grid-cols-3 md:gap-4">
              {DIFFERENTIATORS.map((d, i) => {
                const featured = i === 0;
                return (
                  <Reveal key={d.key} delay={i * 50} className={featured ? "md:col-span-2 md:row-span-2" : ""}>
                    <div
                      className={
                        "flex h-full flex-col rounded-xl border p-6 transition-all duration-300 hover:-translate-y-0.5 " +
                        (featured
                          ? "border-(--brand-border)"
                          : "border-(--border) bg-(--card) hover:border-(--brand-border)")
                      }
                      style={featured ? { background: "var(--brand-soft)" } : undefined}
                    >
                      <span
                        className="flex size-10 items-center justify-center rounded-lg"
                        style={{
                          background: featured ? "var(--card)" : "var(--brand-soft)",
                          color: "var(--brand)",
                        }}
                      >
                        <d.icon className="text-[18px]" />
                      </span>
                      <p className={featured ? "mt-5 text-[22px] font-semibold leading-[1.35]" : "mt-4 text-[17px] font-semibold"}>
                        {d.title}
                      </p>
                      <p className={"mt-2 leading-[1.85] text-(--muted-foreground) " + (featured ? "max-w-[460px] text-[14.5px]" : "text-[13.5px]")}>
                        {d.desc}
                      </p>

                      {d.points && (
                        <div className="mt-auto flex flex-col gap-2.5 pt-7">
                          {d.points.map((p, idx) => (
                            <div key={p} className="flex items-center gap-3 text-[13.5px]">
                              <span
                                className="flex size-5 shrink-0 items-center justify-center rounded-full text-[11px] font-medium tabular-nums"
                                style={{ background: "var(--card)", color: "var(--brand)" }}
                              >
                                {idx + 1}
                              </span>
                              {p}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </Reveal>
                );
              })}
            </div>

            <Reveal delay={80}>
              <div className="mt-4 overflow-hidden rounded-xl border border-(--border) bg-(--card)">
                <div className="grid md:grid-cols-2">
                  <div
                    className="border-b border-(--border) px-6 py-3.5 md:border-b-0 md:border-r"
                    style={{ background: "var(--muted)" }}
                  >
                    <p className="text-[12px] font-medium tracking-[0.08em] text-(--muted-foreground)">还在这样做账</p>
                  </div>
                  <div className="px-6 py-3.5" style={{ background: "var(--brand-soft)" }}>
                    <p className="text-[12px] font-medium tracking-[0.08em]" style={{ color: "var(--brand)" }}>
                      交给云财
                    </p>
                  </div>
                </div>
                {COMPARISON.map((r) => (
                  <div key={r.old} className="grid border-t border-(--border) md:grid-cols-2">
                    <div className="flex items-center gap-2.5 px-6 py-3.5 text-[13.5px] text-(--muted-foreground)">
                      <CloseOutlined className="shrink-0 text-[11px] text-(--icon-placeholder)" />
                      {r.old}
                    </div>
                    <div className="flex items-center gap-2.5 border-t border-(--border) px-6 py-3.5 text-[13.5px] md:border-t-0 md:border-l">
                      <CheckOutlined className="shrink-0 text-[11px]" style={{ color: "var(--brand)" }} />
                      {r.now}
                    </div>
                  </div>
                ))}
              </div>
            </Reveal>
          </Container>
        </section>

        {/* ============ 能力 ============ */}
        <section id="capabilities" className="scroll-mt-16 pt-16 md:pt-24">
          <Container>
            <SectionHead
              cn="产品能力"
              en="Capabilities"
              center
              title="一套系统，覆盖全链路账务"
              desc="凭证、账簿、报表、资金、发票、工资、资产、档案，八个模块共用一套科目与数据底座，不再各算各的。"
            />

            <div className="mt-10 grid grid-cols-2 gap-3 md:mt-14 md:grid-cols-4 md:gap-4">
              {CAPABILITIES.map((c, i) => (
                <Reveal key={c.title} delay={i * 40} className="h-full">
                  <div className="flex h-full flex-col rounded-xl border border-(--border) bg-(--card) p-5 transition-all duration-300 hover:-translate-y-0.5 hover:border-(--brand-border) hover:shadow-[0_16px_36px_-24px_color-mix(in_srgb,var(--seed-primary)_40%,transparent)]">
                    <span
                      className="flex size-10 items-center justify-center rounded-lg"
                      style={{ background: "var(--brand-soft)", color: "var(--brand)" }}
                    >
                      <c.icon className="text-[18px]" />
                    </span>
                    <p className="mt-4 text-[16px] font-semibold">{c.title}</p>
                    <p className="mt-1 text-[13px] text-(--muted-foreground)">{c.desc}</p>
                  </div>
                </Reveal>
              ))}
            </div>
          </Container>
        </section>

        {/* ============ 安全 ============ */}
        <section id="security" className="scroll-mt-16 pt-16 md:pt-24">
          <Container>
            <div className="rounded-2xl p-6 md:p-14" style={{ background: "var(--brand-soft)" }}>
              <SectionHead
                cn="安全合规"
                en="Privacy & Security"
                title="数据与合规"
                desc="财务数据的底线是准确与安全。云财从账套隔离、访问控制到准则口径，把这三件事做在系统里，而不是靠人盯。"
              />
              <div className="mt-8 grid gap-4 md:mt-12 md:grid-cols-3">
                {SECURITY.map((s, i) => (
                  <Reveal key={s.title} delay={i * 60} className="h-full">
                    <div className="h-full rounded-xl border border-(--border) bg-(--card) p-7">
                      <span
                        className="flex size-10 items-center justify-center rounded-lg"
                        style={{ background: "var(--brand-tint)", color: "var(--brand)" }}
                      >
                        <s.icon className="text-[18px]" />
                      </span>
                      <p className="mt-5 text-[17px] font-semibold">{s.title}</p>
                      <p className="mt-2.5 text-[14px] leading-[1.85] text-(--muted-foreground)">{s.desc}</p>
                    </div>
                  </Reveal>
                ))}
              </div>
            </div>
          </Container>
        </section>

        {/* ============ 流程 ============ */}
        <section className="pt-16 md:pt-24">
          <Container>
            <SectionHead
              cn="四步开始"
              en="Get Started"
              title="从建账到出表，一个下午"
              desc="不需要实施顾问，也不需要导数据模板。建好账套、选好科目，当天就能把第一张凭证录进去。"
            />
            <div className="mt-10 grid gap-y-8 md:mt-14 md:grid-cols-4 md:gap-y-0">
              {STEPS.map((s, i) => (
                <Reveal key={s.title} delay={i * 60} className="h-full">
                  <div className="h-full border-t border-(--border) pt-6 md:border-t-0 md:border-l md:px-7 md:pt-0 md:first:border-l-0 md:first:pl-0 md:last:pr-0">
                    <span className="text-[26px] font-semibold tabular-nums tracking-tight" style={{ color: "var(--brand)" }}>
                      0{i + 1}
                    </span>
                    <p className="mt-3 text-[17px] font-semibold">{s.title}</p>
                    <p className="mt-2 max-w-[240px] text-[14px] leading-[1.85] text-(--muted-foreground)">{s.desc}</p>
                  </div>
                </Reveal>
              ))}
            </div>
          </Container>
        </section>

        {/* ============ FAQ ============ */}
        <section id="faq" className="scroll-mt-16 pt-16 md:pt-24">
          <Container>
            <div className="grid gap-8 md:grid-cols-[360px_1fr] md:gap-16">
              <div className="flex flex-col items-start gap-4">
                <Reveal>
                  <Tag cn="常见问题" en="FAQ" />
                </Reveal>
                <Reveal delay={60}>
                  <h2 className="text-[30px] font-semibold leading-[1.25] tracking-[-0.01em] md:text-[36px]">
                    你可能
                    <br className="hidden md:block" />
                    想了解这些
                  </h2>
                </Reveal>
                <Reveal delay={120}>
                  <p className="max-w-[300px] text-[14px] leading-[1.85] text-(--muted-foreground)">
                    还有别的问题？登录后可在系统内查看完整帮助文档。
                  </p>
                </Reveal>
              </div>

              <Reveal delay={100}>
                <div className="border-t border-(--border)">
                  {FAQS.map((f) => (
                    <details key={f.q} className="group border-b border-(--border)">
                      <summary className="flex cursor-pointer list-none items-center justify-between gap-6 py-5 text-[15px] font-medium [&::-webkit-details-marker]:hidden">
                        {f.q}
                        <DownOutlined className="shrink-0 text-[11px] text-(--icon-muted) transition-transform duration-200 group-open:rotate-180" />
                      </summary>
                      <p className="max-w-[640px] pb-5 text-[14px] leading-[1.9] text-(--muted-foreground)">{f.a}</p>
                    </details>
                  ))}
                </div>
              </Reveal>
            </div>
          </Container>
        </section>

        {/* ============ 结尾 CTA：整幅品牌色收口 ============ */}
        <section className="mt-16 md:mt-24" style={{ background: "var(--brand-fill)" }}>
          <Container className="relative flex flex-col items-center py-16 text-center md:py-24">
            <div
              className="pointer-events-none absolute inset-0 opacity-60 [mask-image:radial-gradient(60%_60%_at_50%_50%,black,transparent)]"
              style={{ background: "radial-gradient(50% 60% at 50% 0%, rgba(255,255,255,.16) 0%, transparent 100%)" }}
            />
            <Reveal className="relative">
              <h2 className="text-[32px] font-semibold leading-[1.25] tracking-[-0.01em] text-white md:text-[44px]">
                今天就把账理清
              </h2>
            </Reveal>
            <Reveal delay={80} className="relative">
              <p className="mt-4 max-w-[520px] text-[15px] leading-[1.8] text-white/75">
                登录云财，拍一张发票，你会看到凭证和报表同时生成。
              </p>
            </Reveal>
            <Reveal delay={140} className="relative">
              <div className="mt-8 flex flex-col items-center">
                <Button onClick={goApp} variant="inverse">
                  {primaryLabel}
                  <ArrowRightOutlined className="text-[12px]" />
                </Button>
              </div>
            </Reveal>
          </Container>
        </section>
      </main>

      {/* ============ 页脚 ============ */}
      <footer className="bg-[#0c1424] text-white dark:bg-[#0a0a0a]">
        <Container className="flex flex-col gap-10 border-b border-white/10 py-14 md:flex-row md:justify-between md:py-16">
          <div className="flex max-w-[320px] flex-col gap-5">
            <Logo tone="light" />
            <p className="text-[13px] leading-[1.9] text-white/55">
              面向中小企业的在线财务系统。凭证、账簿、报表、出纳、发票、工资、资产、档案一体化，让每一笔账都清楚。
            </p>
          </div>

          <nav className="flex flex-wrap gap-x-14 gap-y-8 md:gap-x-20">
            {[
              { title: "产品", items: NAV.map((l) => ({ label: l.label, href: l.href })) },
              {
                title: "模块",
                items: [
                  { label: "凭证管理", href: "#capabilities" },
                  { label: "账簿查询", href: "#capabilities" },
                  { label: "财务报表", href: "#capabilities" },
                  { label: "资金出纳", href: "#capabilities" },
                ],
              },
              {
                title: "支持",
                items: [
                  { label: authed ? "进入控制台" : "登录系统", href: primaryHref },
                  { label: "帮助文档", href: "#faq" },
                  { label: "服务条款", href: "/terms" },
                  { label: "隐私政策", href: "/privacy" },
                ],
              },
            ].map((col) => (
              <div key={col.title} className="flex flex-col gap-4">
                <h3 className="text-[13px] font-semibold text-white">{col.title}</h3>
                <ul className="flex flex-col">
                  {col.items.map((it) => (
                    <li key={it.label} className="leading-[30px]">
                      {it.href.startsWith("#") ? (
                        <a href={it.href} className="text-[13px] text-white/60 transition-colors hover:text-white">
                          {it.label}
                        </a>
                      ) : (
                        <Link to={it.href} className="text-[13px] text-white/60 transition-colors hover:text-white">
                          {it.label}
                        </Link>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>
        </Container>

        <Container className="flex flex-col gap-4 py-6 text-[12px] text-white/45 md:flex-row md:items-center md:justify-between">
          <span>© {new Date().getFullYear()} 云财 · 保留所有权利</span>
          <a
            href="#top"
            onClick={(e) => {
              e.preventDefault();
              window.scrollTo({ top: 0, behavior: "smooth" });
            }}
            className="w-fit transition-colors hover:text-white"
          >
            回到顶部
          </a>
        </Container>
      </footer>

      <LoginModal
        open={loginOpen && (!authed || needsPasswordSetup)}
        onClose={closeLogin}
        onSuccess={handleLoginSuccess}
      />
    </div>
  );
}