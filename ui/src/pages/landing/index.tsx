import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router";
import {
  ApartmentOutlined,
  ArrowRightOutlined,
  AuditOutlined,
  BankOutlined,
  BarChartOutlined,
  BookOutlined,
  CheckOutlined,
  CloudOutlined,
  ContainerOutlined,
  DownOutlined,
  FileTextOutlined,
  FolderOpenOutlined,
  LockOutlined,
  SafetyCertificateOutlined,
  TeamOutlined,
  WalletOutlined,
} from "@ant-design/icons";

import { useAuth } from "@/auth/AuthContext";
import { LoginModal } from "@/components/auth/LoginModal";

/* ============================================================
 * 云财官网 · 落地页
 *
 * 规格全部取自参考站实测计算值（1440 视口，逐个 getComputedStyle 校对）：
 * - 容器 .home-v2-container：100% 宽 + 左右 120px 内边距（内容 1200）
 * - 区块节奏：hero section padding-top 153px；普通 section 96px；CTA section 120px
 * - Hero：h1 88px/500/-0.88px/1.02 单行 → lead p 16/300/28 #1a1a1a（非灰）w560
 *         → actions（黑胶囊 h48 px26 r999 + 绿链接 14/300）→ 应用窗 w992，上距 96px
 *         → 底部 238px 白色渐隐；光晕为 1590×1239 的绝对定位层（left -75 / top 323）
 * - 字阶：h2 60/400/72（生态区 52/400/61）；场景卡标题 h3 28/300/43.4；
 *         卡片标题 19/400/26.6；正文 16/300/28 #646464；小正文 14/300/26.6；页脚 13.5/300
 * - 颜色：主文 #1a1a1a、次文 #646464、品牌绿 #079d55（标签/图标）、链接绿 #00614d
 *         标签底 #f3f9f6 / 描边标签 #edf8f2 + #dff2e8、视觉面板底 #f7fcfb、
 *         轨道底 #f7f8f9 + 边框 #f2f4f8、卡片细边框 #ebebeb
 * - 组件：主按钮 h48/px26/r999/#1a1a1a/16·500；tab 轨道 p4 gap2 r999 + 1px #f2f4f8，
 *         item h40/px24/14·400，激活态白底 + 1px #ebebeb；
 *         场景面板 w1200/h612/r20/1px #ebebeb，左 360 文本区（padding 24/40/24/48）+
 *         右视觉区 #f7fcfb；合规带 w1200/padding 80/r20/#f3f9f6，内 3 列 gap16
 * - 页脚：#0a0a0a；top 1418 宽 / padding 80px 120px 64px / space-between /
 *         下边框 rgba(255,255,255,.1)；链接列 gap80，列内 gap16，标题 14/500 白、
 *         条目 13.5/300 白 60%、行高 32；底部满宽拉伸 wordmark（1440×22）
 * - 动效：进入视口 opacity 0→1、translateY(28) scale(.985)→0，0.45s
 * ============================================================ */

const C = {
  ink: "#1a1a1a",
  sub: "#646464",
  faint: "#bfbfbf",
  brand: "#079d55", // 标签 / 图标绿（实测 rgb(7,157,85)）
  brandDeep: "#00614d", // 链接绿（实测 rgb(0,97,77)）
  tagBg: "#f3f9f6", // .wb2-head__tag 底
  tagBg2: "#edf8f2", // .wb2-download__tag 底
  tagLine: "#dff2e8", // 描边标签边框
  panel: "#f7fcfb", // 场景视觉区底
  track: "#f7f8f9", // tab 轨道 / 生态面板底
  line: "#ebebeb", // 卡片细边框
  lineSoft: "#f2f4f8", // tab 轨道边框
};

const NAV = [
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

const SECURITY = [
  {
    icon: SafetyCertificateOutlined,
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
];

/* ------------------------------------------------------------------ */
/*  布局与动效                                                          */
/* ------------------------------------------------------------------ */

/** 内容容器：满宽 + 左右 120px（与参考站 .home-v2-container 一致）。 */
function Container({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return <div className={"w-full px-6 md:px-12 lg:px-[120px] " + className}>{children}</div>;
}

/** 进入视口渐显：opacity 0→1 + 位移 28px + 轻微缩放，0.45s。 */
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
    const io = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) {
          setShown(true);
          io.disconnect();
        }
      },
      { threshold: 0.12, rootMargin: "0px 0px -40px 0px" },
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
        transform: shown ? "none" : "translateY(28px) scale(.985)",
        transition: `opacity .45s ease ${delay}ms, transform .45s cubic-bezier(.22,.61,.36,1) ${delay}ms`,
        willChange: "opacity, transform",
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
      <span className="flex size-8 items-center justify-center rounded-[9px]" style={{ background: C.brand }}>
        <svg viewBox="0 0 24 24" className="size-[18px]" fill="none">
          <path
            d="M5 8.5h11.5a2.5 2.5 0 0 1 2.5 2.5v6.5a2.5 2.5 0 0 1-2.5 2.5H7.5A2.5 2.5 0 0 1 5 17.5v-9Z"
            stroke="#fff"
            strokeWidth="1.7"
            strokeLinejoin="round"
          />
          <path d="M5 8.5 7 5h8l2 3.5" stroke="#fff" strokeWidth="1.7" strokeLinejoin="round" />
          <path d="M9 13.6h6" stroke="#fff" strokeWidth="1.7" strokeLinecap="round" />
        </svg>
      </span>
      <span className="flex items-baseline gap-1.5">
        <span className="text-[17px] font-medium tracking-tight" style={{ color: tone === "dark" ? C.ink : "#fff" }}>
          云财
        </span>
        <span
          className="text-[10px] font-light uppercase tracking-[0.22em]"
          style={{ color: tone === "dark" ? C.faint : "#7a7a7a" }}
        >
          KuaiJi
        </span>
      </span>
    </span>
  );
}

/** 按钮：h48 / px26 / 全胶囊；primary 黑底，ghost 描边。 */
function Button({
  children,
  onClick,
  href,
  variant = "primary",
}: {
  children: React.ReactNode;
  onClick?: () => void;
  href?: string;
  variant?: "primary" | "ghost";
}) {
  const style =
    variant === "primary"
      ? { background: C.ink, color: "#fff", border: "1px solid transparent" }
      : { background: "transparent", color: C.ink, border: "1px solid #dcdcdc" };
  const cls =
    "inline-flex h-12 items-center justify-center gap-2 rounded-full px-[26px] text-[16px] font-medium transition-all duration-200 " +
    (variant === "primary" ? "hover:bg-[#333]" : "hover:border-[#b9b9b9] hover:bg-[#fafafa]");
  if (href) {
    return (
      <a href={href} className={cls} style={style}>
        {children}
      </a>
    );
  }
  return (
    <button type="button" onClick={onClick} className={cls} style={style}>
      {children}
    </button>
  );
}

/**
 * 区块标签：h25 / padding 6px 14px / gap 8px / 全胶囊。
 * solid  = .wb2-head__tag（底 #f3f9f6，无边框）
 * outline= .wb2-download__tag（底 #edf8f2 + 1px #dff2e8）
 */
function Tag({ cn, en, variant = "solid" }: { cn: string; en: string; variant?: "solid" | "outline" }) {
  const box =
    variant === "solid"
      ? { background: C.tagBg, border: "1px solid transparent" }
      : { background: C.tagBg2, border: `1px solid ${C.tagLine}` };
  return (
    <span
      className="inline-flex items-center gap-[8px] rounded-full px-[14px] py-[6px] text-[12.5px] leading-none"
      style={{ ...box, color: C.brand }}
    >
      <em className="font-medium not-italic">{cn}</em>
      <em className="font-light not-italic tracking-[0.5px] uppercase">{en}</em>
    </span>
  );
}

/** 区块头：左列标签+标题，右列说明；参考站为 420 / 394 两列、gap 40。 */
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
          <h2 className="mt-4 text-[32px] font-normal leading-[1.2] md:text-[52px]" style={{ color: C.ink }}>
            {title}
          </h2>
        </Reveal>
        {desc && (
          <Reveal delay={120}>
            <p
              className="mt-[18px] max-w-[560px] text-[14px] font-light leading-[1.9] md:text-[16px] md:leading-[1.75]"
              style={{ color: C.sub }}
            >
              {desc}
            </p>
          </Reveal>
        )}
      </div>
    );
  }
  return (
    <div className="grid gap-8 md:grid-cols-[minmax(0,420px)_minmax(0,1fr)] md:items-end md:gap-10">
      <div className="flex flex-col items-start gap-4">
        <Reveal>
          <Tag cn={cn} en={en} />
        </Reveal>
        <Reveal delay={60}>
          <h2 className="text-[32px] font-normal leading-[1.2] md:text-[52px] lg:text-[60px]" style={{ color: C.ink }}>
            {title}
          </h2>
        </Reveal>
      </div>
      {desc && (
        <Reveal delay={120}>
            <p
              className="max-w-[400px] text-[14px] font-light leading-[1.9] md:text-[16px] md:leading-[1.75]"
              style={{ color: C.sub }}
            >
            {desc}
          </p>
        </Reveal>
      )}
    </div>
  );
}

/* ------------------------------------------------------------------ */
/*  Hero 应用窗口                                                       */
/* ------------------------------------------------------------------ */

const MENU = ["总览", "凭证", "账簿", "报表", "资金", "发票", "工资", "资产"];

function AppWindow() {
  return (
    <div
      className="overflow-hidden rounded-[14px] bg-white"
      style={{ border: "1px solid #e9eae9", boxShadow: "0 24px 60px -32px rgba(10,40,34,.35)" }}
    >
      <div className="flex h-8 items-center gap-3 border-b px-4" style={{ borderColor: "#f2f2f2", background: "#fcfcfc" }}>
        <span className="flex gap-1.5">
          <span className="size-[9px] rounded-full bg-[#e6e6e6]" />
          <span className="size-[9px] rounded-full bg-[#e6e6e6]" />
          <span className="size-[9px] rounded-full bg-[#e6e6e6]" />
        </span>
        <span className="mx-auto rounded-[4px] bg-white px-3 py-[2px] text-[10px] font-light text-[#b5b5b5] ring-1 ring-[#efefef]">
          app.yuncai.cn / 总览
        </span>
        <span className="w-8" />
      </div>

      <div className="grid grid-cols-1 lg:min-h-[574px] lg:grid-cols-[148px_1fr] xl:grid-cols-[176px_1fr]">
        <aside className="hidden border-r p-3 lg:block" style={{ borderColor: "#e8f4ef", background: "#ecf8f2" }}>
          <div className="mb-3 flex items-center gap-2 px-2">
            <span
              className="flex size-5 items-center justify-center rounded-[5px] text-[9px] text-white"
              style={{ background: C.brand }}
            >
              云
            </span>
            <span className="truncate text-[11px] font-normal" style={{ color: "#4a4a4a" }}>
              杭州数从科技
            </span>
          </div>
          <div className="flex flex-col gap-[2px]">
            {MENU.map((m, i) => (
              <span
                key={m}
                className={"rounded-[6px] px-2 py-[6px] text-[11px] " + (i === 0 ? "font-medium" : "font-light")}
                style={i === 0 ? { background: C.tagBg, color: "#00795f" } : { color: "#909090" }}
              >
                {m}
              </span>
            ))}
          </div>
        </aside>

        <div
          className="min-w-0 p-4 lg:p-5"
          style={{ background: "linear-gradient(to bottom, #f9fdfb 0%, #ffffff 180px)" }}
        >
          <div className="flex items-center justify-between">
            <div>
              <p className="text-[13px] font-medium" style={{ color: C.ink }}>
                2026 年 10 月
              </p>
              <p className="mt-[2px] text-[10px] font-light" style={{ color: "#b0b0b0" }}>
                数据来自已过账凭证
              </p>
            </div>
            <span className="rounded-full px-3 py-1 text-[10px] font-medium text-white" style={{ background: C.ink }}>
              + 新增凭证
            </span>
          </div>

          <div className="mt-4 grid grid-cols-2 gap-2 md:grid-cols-4">
            {[
              { k: "资产总额", v: "3,071.20", d: "+2.6%" },
              { k: "本月收入", v: "339.10", d: "+18.7%" },
              { k: "本月费用", v: "32.40", d: "-4.1%" },
              { k: "净利润", v: "306.70", d: "+21.3%" },
            ].map((c) => (
              <div key={c.k} className="rounded-[10px] p-3" style={{ border: "1px solid #f1f1f1" }}>
                <p className="text-[10px] font-light" style={{ color: "#a0a0a0" }}>
                  {c.k}
                </p>
                <p className="mt-1 text-[15px] font-normal tracking-tight" style={{ color: "#111" }}>
                  {c.v}
                </p>
                <p className="mt-[2px] text-[9px] font-light" style={{ color: C.brand }}>
                  {c.d}
                </p>
              </div>
            ))}
          </div>

          <div className="mt-3 overflow-hidden rounded-[10px]" style={{ border: "1px solid #f1f1f1" }}>
            <div
              className="flex items-center justify-between border-b px-3 py-2"
              style={{ borderColor: "#f4f4f4", background: "#fcfcfc" }}
            >
              <span className="text-[10.5px] font-normal" style={{ color: "#4b4b4b" }}>
                资产负债表 · 简表
              </span>
              <span className="text-[9px] font-light" style={{ color: "#b0b0b0" }}>
                会企 01 表
              </span>
            </div>
            <div>
              {[
                ["货币资金", "1,842.60", "短期借款", "300.00"],
                ["应收账款", "226.30", "应付账款", "186.40"],
                ["存货", "612.10", "实收资本", "2,000.00"],
                ["固定资产", "390.20", "未分配利润", "306.70"],
                ["资产总计", "3,071.20", "负债和所有者权益总计", "3,071.20"],
              ].map(([l1, v1, l2, v2], i) => (
                <div
                  key={l1}
                  className="grid grid-cols-2 gap-3 border-b px-3 py-[6px] text-[10px] last:border-b-0"
                  style={{ borderColor: "#f7f7f7", background: i === 4 ? "#fbfdfc" : undefined }}
                >
                  <span className="flex justify-between gap-2">
                    <span
                      className={i === 4 ? "truncate font-normal" : "truncate font-light"}
                      style={{ color: i === 4 ? C.ink : "#6f6f6f" }}
                    >
                      {l1}
                    </span>
                    <span style={{ color: i === 4 ? C.brand : "#3f3f3f" }}>{v1}</span>
                  </span>
                  <span className="flex justify-between gap-2 border-l pl-3" style={{ borderColor: "#f4f4f4" }}>
                    <span
                      className={i === 4 ? "truncate font-normal" : "truncate font-light"}
                      style={{ color: i === 4 ? C.ink : "#6f6f6f" }}
                    >
                      {l2}
                    </span>
                    <span style={{ color: i === 4 ? C.brand : "#3f3f3f" }}>{v2}</span>
                  </span>
                </div>
              ))}
            </div>
          </div>

          {/* 近 6 期收入趋势（纯 CSS 柱状） */}
          <div className="mt-3 rounded-[10px] p-3" style={{ border: "1px solid #f1f1f1" }}>
            <div className="flex items-center justify-between">
              <span className="text-[10.5px] font-normal" style={{ color: "#4b4b4b" }}>
                近 6 期收入趋势
              </span>
              <span className="text-[9px] font-light" style={{ color: "#b0b0b0" }}>
                单位：万元
              </span>
            </div>
            <div className="mt-3 flex h-[86px] items-end gap-[10px]">
              {[
                { m: "05", v: 58 },
                { m: "06", v: 42 },
                { m: "07", v: 71 },
                { m: "08", v: 64 },
                { m: "09", v: 88 },
                { m: "10", v: 100 },
              ].map((b, i) => (
                <div key={b.m} className="flex flex-1 flex-col items-center gap-[6px]">
                  <div className="flex w-full flex-1 items-end">
                    <div
                      className="w-full rounded-t-[3px]"
                      style={{
                        height: `${b.v}%`,
                        background: i === 5 ? C.brand : i === 4 ? "#8fdcbb" : "#dff0e8",
                      }}
                    />
                  </div>
                  <span className="text-[9px] font-light" style={{ color: "#b0b0b0" }}>
                    {b.m}
                  </span>
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

type Tok = { t: string; c?: "kw" | "num" | "dim" };

const SCENES = [
  {
    key: "voucher",
    tab: "凭证录入",
    title: "从录入到入账，",
    titleMuted: "一步完成",
    desc: "结构化的分录录入，借贷平衡与金额大写实时校验；保存即入账，账簿与报表立即取到数。历史凭证可从 Excel 批量导入，借贷平衡的直接入账。",
    points: ["借贷自动平衡校验", "金额大写自动生成", "Excel 批量导入历史凭证"],
    file: "记账凭证 记-0007",
    stateTag: "已过账",
    code: [
      [{ t: "记账凭证", c: "kw" }, { t: "   记-0007 · 2026-10-25" }],
      [{ t: "借", c: "kw" }, { t: "  银行存款" }, { t: "           120,000.00", c: "num" }],
      [{ t: "贷", c: "kw" }, { t: "  主营业务收入" }, { t: "       113,207.55", c: "num" }],
      [{ t: "贷", c: "kw" }, { t: "  应交税费—应交增值税" }, { t: "  6,792.45", c: "num" }],
      [{ t: "合计", c: "kw" }, { t: "  壹拾贰万元整" }, { t: "       120,000.00", c: "num" }],
      [{ t: "借贷平衡，保存即入账", c: "dim" }],
    ] as Tok[][],
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
    titleMuted: "不用再等月底",
    desc: "科目余额表、资产负债表、利润表、现金流量表全部从已过账凭证实时归集，期初余额一并参与，任意期间即时切换。",
    points: ["会企 01 表标准行次", "任意期间即时切换", "资产 = 负债 + 所有者权益自动平衡"],
    file: "资产负债表 2026-10",
    stateTag: "已出表",
    code: [
      [{ t: "资产总计", c: "kw" }, { t: "             1,286,400.00", c: "num" }],
      [{ t: "负债合计" }, { t: "               486,400.00", c: "num" }],
      [{ t: "所有者权益合计" }, { t: "         800,000.00", c: "num" }],
      [{ t: "负债和所有者权益总计" }, { t: "   1,286,400.00", c: "num" }],
      [{ t: "资产 = 负债 + 所有者权益", c: "dim" }],
    ] as Tok[][],
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
    titleMuted: "不再两头对",
    desc: "日记账逐笔登记余额，内部转账两端自动落账；流水可按日期、类别、方向合并批量生成凭证，出纳台账与总账自动对得上。",
    points: ["账户启停用与开户行信息", "内部转账自动双边登记", "批量出票支持合并方式"],
    file: "现金日记账 2026-10",
    stateTag: "已对账",
    code: [
      [{ t: "10-24", c: "kw" }, { t: "  收到货款" }, { t: "         +86,000.00", c: "num" }],
      [{ t: "10-25", c: "kw" }, { t: "  支付供应商" }, { t: "       -42,300.00", c: "num" }],
      [{ t: "10-26", c: "kw" }, { t: "  内部转账" }, { t: "         -20,000.00", c: "num" }],
      [{ t: "余额" }, { t: "  招商银行 6225" }, { t: "      1,842,600.00", c: "num" }],
      [{ t: "本月已生成凭证 26 张", c: "dim" }],
    ] as Tok[][],
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
    titleMuted: "一次点完",
    desc: "一键结转损益，系统自动生成本年利润结转凭证并直接过账；随后批量结账，报表口径在结转前后保持完全一致。",
    points: ["结转损益自动生成凭证", "支持自定义结转模板", "批量结账、可撤销重做"],
    file: "期末处理 2026-10",
    stateTag: "已结账",
    code: [
      [{ t: "结转损益", c: "kw" }, { t: "      已生成凭证 · 已过账", c: "num" }],
      [{ t: "结转未分配利润", c: "kw" }, { t: "  已生成凭证 · 已过账", c: "num" }],
      [{ t: "关账检查" }, { t: "          试算平衡，无异常" }],
      [{ t: "结账期间" }, { t: "            2026-10 已结账", c: "num" }],
      [{ t: "下一期间：2026-11 · 待处理", c: "dim" }],
    ] as Tok[][],
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
  const { status } = useAuth();
  const navigate = useNavigate();
  const authed = status === "authenticated";
  const primaryHref = authed ? "/app" : "/login";
  const primaryLabel = authed ? "进入控制台" : "免费开始记账";
  const [activeTab, setActiveTab] = useState(SCENES[0].key);
  const active = SCENES.find((s) => s.key === activeTab) ?? SCENES[0];
  const [scrolled, setScrolled] = useState(false);
  // 登录改为弹窗：未登录时所有「开始使用 / 登录」入口都就地开窗，不再跳独立登录页。
  // autoOpenLogin 用于 /login 直达（含保护区回跳带上的 from），保持深链可用。
  const [loginOpen, setLoginOpen] = useState(autoOpenLogin);

  const goApp = () => (authed ? navigate(primaryHref) : setLoginOpen(true));

  // 关闭弹窗时若停在 /login 上，回落到官网根路径，避免 URL 停在登录态。
  const closeLogin = () => {
    setLoginOpen(false);
    if (window.location.pathname === "/login") {
      navigate("/", { replace: true });
    }
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
    <div
      id="top"
      className="flex min-h-svh scroll-smooth flex-col bg-white antialiased [font-family:'PingFang_SC','Microsoft_YaHei',-apple-system,'Segoe_UI',sans-serif]"
      style={{ color: C.ink }}
    >
      {/* ============ 导航 ============ */}
      <header
        className="fixed inset-x-0 top-0 z-50 h-[65px] transition-all duration-300"
        style={{
          background: "rgba(255,255,255,.85)",
          backdropFilter: "blur(14px)",
          borderBottom: scrolled ? "1px solid #f0f0f0" : "1px solid transparent",
        }}
      >
        <Container className="grid h-full grid-cols-[1fr_auto_1fr] items-center">
          <Link to="/" aria-label="云财首页" className="justify-self-start">
            <Logo />
          </Link>

          <nav className="hidden items-center gap-[8px] lg:flex">
            {NAV.map((l, i) => (
              <a
                key={l.href}
                href={l.href}
                className={"relative px-[16px] py-[10px] text-[14px] transition-colors " + (i === 0 ? "font-medium" : "font-normal")}
                style={{ color: i === 0 ? C.brand : "#3d3d3d" }}
              >
                {l.label}
                {i === 0 && <span className="absolute inset-x-[16px] -bottom-[3px] h-[2px]" style={{ background: C.brand }} />}
              </a>
            ))}
          </nav>

          <div className="flex items-center justify-self-end gap-[12px]">
            {!authed && (
              <button
                type="button"
                onClick={() => setLoginOpen(true)}
                className="hidden px-[16px] py-[10px] text-[14px] transition-colors hover:text-[#1a1a1a]! sm:block"
                style={{ color: "#3d3d3d" }}
              >
                登录
              </button>
            )}
            <button
              type="button"
              onClick={goApp}
              className="inline-flex h-[38px] items-center gap-1.5 rounded-full px-[20px] text-[13.5px] font-medium text-white transition-all hover:bg-[#333]"
              style={{ background: C.ink }}
            >
              {authed ? "进入控制台" : "开始使用"}
              <ArrowRightOutlined className="text-[11px]" style={{ color: "#fff" }} />
            </button>
          </div>
        </Container>
      </header>

      <main className="flex-1">
        {/* ============ Hero ============ */}
        <section className="relative overflow-hidden">
          {/* 柔和光晕：只压在中段，顶部保持纯白 */}
          <div className="pointer-events-none absolute inset-x-0 top-0 z-0 h-[1560px] overflow-hidden">
            <div
              className="absolute left-[-380px] top-[530px] h-[700px] w-[2200px] rounded-[50%] blur-[50px]"
              style={{
                background:
                  "radial-gradient(66% 66% at 50% 50%, rgba(188,236,215,.82) 0%, rgba(208,244,231,.6) 55%, rgba(234,252,246,.26) 80%, rgba(255,255,255,0) 100%)",
              }}
            />
          </div>

          <Container className="relative z-10 flex flex-col items-center pb-0 pt-[96px] text-center md:pt-[153px]">
            <h1
              className="max-w-[900px] text-[40px] font-medium leading-[1.08] tracking-[-0.01em] sm:text-[56px] md:text-[68px] lg:text-[80px] xl:text-[88px]"
              style={{ color: C.ink }}
            >
              记账，本该如此简单
            </h1>

            <Reveal delay={80}>
              <p className="mt-[24px] w-full max-w-[560px] text-[15px] font-light leading-[1.75] md:text-[16px]" style={{ color: C.ink }}>
                云财把凭证、账簿、报表、出纳连成一条线。
                <br className="hidden md:block" />
                录入即入账，入账即出表，让财务工作回到它本来的样子。
              </p>
            </Reveal>

            <Reveal delay={140}>
              <div className="mt-[32px] flex flex-col items-center gap-[16px]">
                <Button onClick={goApp}>
                  {primaryLabel}
                  <ArrowRightOutlined className="text-[12px]" />
                </Button>
                <a href="#scenarios" className="text-[14px] font-light transition-opacity hover:opacity-70" style={{ color: C.brandDeep }}>
                  看看它怎么工作
                </a>
              </div>
            </Reveal>
          </Container>

          {/* 应用窗口：1080 宽居中，两侧留出光晕，底部渐隐进白色 */}
          <div className="relative z-10 mx-auto mt-[56px] w-full max-w-[1080px] px-6 md:mt-[96px] md:px-0">
            <Reveal delay={200}>
              <AppWindow />
            </Reveal>
          </div>
          <div
            className="pointer-events-none absolute inset-x-0 bottom-0 h-[238px]"
            style={{ background: "linear-gradient(to top, #fff 14%, rgba(255,255,255,0) 100%)" }}
          />
        </section>

        {/* ============ 场景 ============ */}
        <section id="scenarios" className="scroll-mt-[65px] pt-[48px] md:pt-[96px]">
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

            {/* Tab：轨道 #f7f8f9 / padding 4 / gap 2 / 全胶囊 */}
            <Reveal delay={60}>
              <div
                className="mt-[28px] flex w-full max-w-full gap-[2px] overflow-x-auto rounded-full p-[4px] [scrollbar-width:none] md:mt-[40px] md:w-fit md:inline-flex [&::-webkit-scrollbar]:hidden"
                style={{ background: C.track, border: `1px solid ${C.lineSoft}` }}
              >
                {SCENES.map((s) => (
                  <button
                    key={s.key}
                    type="button"
                    onClick={() => setActiveTab(s.key)}
                    className="shrink-0 rounded-full border border-transparent px-[24px] py-[9px] text-[14px] transition-all duration-200"
                    style={
                      s.key === activeTab
                        ? { background: "#fff", color: C.ink, fontWeight: 500, borderColor: C.line }
                        : { background: "transparent", color: C.sub, fontWeight: 400 }
                    }
                  >
                    {s.tab}
                  </button>
                ))}
              </div>
            </Reveal>

            {/* 面板：radius 20 / 白底；左 355 文本区，右视觉区 #f7fcfb */}
            <Reveal delay={100}>
              <div className="mt-[24px] overflow-hidden rounded-[20px] bg-white md:mt-[44px]" style={{ border: `1px solid ${C.line}` }}>
                <div className="grid lg:grid-cols-[360px_1fr]">
                  <div className="flex flex-col justify-center px-6 py-10 md:px-12 md:py-12 lg:px-[48px] lg:py-[24px] lg:pr-[40px]">
                    <h3 className="text-[24px] font-light leading-[1.55] md:text-[28px]" style={{ color: C.ink }}>
                      {active.title}
                      <span className="block" style={{ color: C.brand }}>
                        {active.titleMuted}
                      </span>
                    </h3>
                    <p className="mt-[18px] text-[14px] font-light leading-[1.9]" style={{ color: C.sub }}>
                      {active.desc}
                    </p>
                    <ul className="mt-[24px] flex flex-col gap-[12px]">
                      {active.points.map((p) => (
                        <li key={p} className="flex items-center gap-[10px] text-[14px] font-light" style={{ color: "#3f3f3f" }}>
                          <CheckOutlined className="text-[10px]" style={{ color: C.brand }} />
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
                      className="mt-[28px] inline-flex w-fit items-center gap-1.5 text-[14px] font-normal transition-all hover:gap-2.5"
                      style={{ color: C.brand }}
                    >
                      进入系统体验
                      <ArrowRightOutlined className="text-[11px]" style={{ color: C.brand }} />
                    </a>
                  </div>

                  {/* 右侧视觉区：#f7fcfb 底 + 单据窗口 */}
                  <div className="relative flex flex-col justify-center gap-4 p-6 md:p-10" style={{ background: C.panel }}>
                    <div className="rounded-[14px] bg-white px-5 py-5" style={{ border: "1px solid #eef6f2" }}>
                      <div className="flex items-center justify-between">
                        <span
                          className="rounded-[6px] px-[10px] py-[4px] font-mono text-[10.5px] tracking-wide"
                          style={{ background: C.tagBg, color: "#00795f" }}
                        >
                          {active.file}
                        </span>
                        <span className="text-[10px] font-light" style={{ color: "#b5b5b5" }}>
                          {active.stateTag}
                        </span>
                      </div>
                      <pre className="mt-[14px] overflow-x-auto text-[12.5px] leading-[2] [font-variant-numeric:tabular-nums]">
                        {active.code.map((line, i) => (
                          <div key={i} className="whitespace-pre">
                            {line.map((tok, j) => (
                              <span
                                key={j}
                                style={{
                                  color: tok.c === "kw" ? C.brand : tok.c === "num" ? C.ink : "#a9a9a9",
                                  fontWeight: tok.c === "num" ? 400 : 300,
                                }}
                              >
                                {tok.t}
                              </span>
                            ))}
                          </div>
                        ))}
                      </pre>
                    </div>

                    <div className="rounded-[14px] bg-white/70 px-4 py-3">
                      <p className="mb-[6px] text-[10.5px] font-light uppercase tracking-[0.14em]" style={{ color: "#a6a6a6" }}>
                        {active.mockLabel}
                      </p>
                      <div>
                        {active.mockRows.map((r) => (
                          <div
                            key={r.name}
                            className="flex items-center justify-between border-b py-[9px] text-[12.5px] last:border-b-0"
                            style={{ borderColor: "#f2f4f3" }}
                          >
                            <span className="font-light" style={{ color: "#3f3f3f" }}>
                              {r.name}
                            </span>
                            <span
                              className="flex items-center gap-1.5"
                              style={{ color: r.ok ? C.brand : "#adadad", fontWeight: r.ok ? 500 : 300 }}
                            >
                              <span className="size-[3px] rounded-full" style={{ background: r.ok ? C.brand : "#d8d8d8" }} />
                              {r.state}
                            </span>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </Reveal>
          </Container>
        </section>

        {/* ============ 能力 ============ */}
        <section id="capabilities" className="relative scroll-mt-[65px] overflow-hidden pt-[72px] md:pt-[96px]">
          <div className="pointer-events-none absolute inset-0 bg-[linear-gradient(to_right,#f1f2f1_1px,transparent_1px),linear-gradient(to_bottom,#f1f2f1_1px,transparent_1px)] bg-[length:64px_64px] [mask-image:radial-gradient(ellipse_at_center,black,transparent_72%)]" />
          <Container className="relative">
            <SectionHead
              cn="产品能力"
              en="Capabilities"
              center
              title="一套系统，覆盖全链路账务"
              desc="凭证、账簿、报表、资金、发票、工资、资产、档案，八个模块共用一套科目与数据底座，不再各算各的。"
            />

            <div className="mt-[48px] grid grid-cols-2 gap-[12px] md:mt-[64px] md:grid-cols-4 md:gap-[16px]">
              {CAPABILITIES.map((c, i) => (
                <Reveal key={c.title} delay={i * 40}>
                  <div
                    className="flex h-full flex-col justify-center rounded-[12px] bg-white px-5 py-5 transition-all duration-300 hover:-translate-y-[2px] hover:shadow-[0_16px_36px_-26px_rgba(10,40,34,.35)] md:h-[104px] md:px-6"
                    style={{ border: `1px solid ${C.line}` }}
                  >
                    <c.icon className="text-[19px]" style={{ color: C.brand }} />
                    <p className="mt-[12px] text-[15.5px] font-normal" style={{ color: C.ink }}>
                      {c.title}
                    </p>
                    <p className="mt-[3px] text-[13px] font-light" style={{ color: C.sub }}>
                      {c.desc}
                    </p>
                  </div>
                </Reveal>
              ))}
            </div>
          </Container>
        </section>

        {/* ============ 安全 ============ */}
        <section id="security" className="scroll-mt-[65px] pt-[72px] md:pt-[96px]">
          <Container>
            <div
              className="relative overflow-hidden rounded-[20px] px-6 py-12 md:px-[80px] md:py-[80px]"
              style={{ background: C.tagBg }}
            >
              <div className="pointer-events-none absolute inset-0 bg-[linear-gradient(to_right,#e6f2ed_1px,transparent_1px),linear-gradient(to_bottom,#e6f2ed_1px,transparent_1px)] bg-[length:56px_56px] opacity-60 [mask-image:radial-gradient(ellipse_at_30%_20%,black,transparent_70%)]" />
              <div className="relative">
                <SectionHead
                  cn="安全合规"
                  en="Privacy & Security"
                  title="数据与合规"
                  desc="财务数据的底线是准确与安全。云财从账套隔离、访问控制到准则口径，把这三件事做在系统里，而不是靠人盯。"
                />
                <div className="mt-[40px] grid gap-[16px] md:mt-[48px] md:grid-cols-3">
                  {SECURITY.map((s, i) => (
                    <Reveal key={s.title} delay={i * 60}>
                      <div className="h-full rounded-[16px] bg-white p-[32px]" style={{ border: `1px solid ${C.tagLine}` }}>
                        <s.icon className="text-[22px]" style={{ color: C.brand }} />
                        <p className="mt-[22px] text-[19px] font-normal" style={{ color: C.ink }}>
                          {s.title}
                        </p>
                        <p className="mt-[12px] text-[14px] font-light leading-[1.9]" style={{ color: C.sub }}>
                          {s.desc}
                        </p>
                      </div>
                    </Reveal>
                  ))}
                </div>
              </div>
            </div>
          </Container>
        </section>

        {/* ============ 流程 ============ */}
        <section className="pt-[72px] md:pt-[96px]">
          <Container>
            <SectionHead
              cn="四步开始"
              en="Get Started"
              title="从建账到出表，一个下午"
              desc="不需要实施顾问，也不需要导数据模板。建好账套、选好科目，当天就能把第一张凭证录进去。"
            />
            <div className="mt-[48px] grid gap-y-10 md:mt-[64px] md:grid-cols-4 md:gap-0">
              {STEPS.map((s, i) => (
                <Reveal key={s.title} delay={i * 60}>
                  <div
                    className="h-full px-0 md:px-8 md:first:pl-0 md:last:pr-0"
                    style={{ borderLeft: i > 0 ? `1px solid ${C.line}` : undefined }}
                  >
                    <span className="font-mono text-[12px] font-light tracking-[0.12em]" style={{ color: C.faint }}>
                      0{i + 1}
                    </span>
                    <p className="mt-[18px] text-[19px] font-normal" style={{ color: C.ink }}>
                      {s.title}
                    </p>
                    <p className="mt-[10px] max-w-[240px] text-[14px] font-light leading-[1.9]" style={{ color: C.sub }}>
                      {s.desc}
                    </p>
                  </div>
                </Reveal>
              ))}
            </div>
          </Container>
        </section>

        {/* ============ FAQ ============ */}
        <section id="faq" className="scroll-mt-[65px] pt-[72px] md:pt-[96px]">
          <Container>
            <div className="grid gap-10 md:grid-cols-[420px_1fr] md:gap-[80px]">
              <div className="flex flex-col items-start gap-4">
                <Reveal>
                  <Tag cn="常见问题" en="FAQ" />
                </Reveal>
                <Reveal delay={60}>
                  <h2 className="text-[32px] font-normal leading-[1.2] md:text-[44px]" style={{ color: C.ink }}>
                    你可能
                    <br className="hidden md:block" />
                    想了解这些
                  </h2>
                </Reveal>
                <Reveal delay={120}>
                  <p className="max-w-[300px] text-[14px] font-light leading-[1.9]" style={{ color: C.sub }}>
                    还有别的问题？登录后可在系统内查看完整帮助文档。
                  </p>
                </Reveal>
              </div>

              <div style={{ borderTop: `1px solid ${C.line}` }}>
                {FAQS.map((f) => (
                  <details key={f.q} className="group" style={{ borderBottom: `1px solid ${C.line}` }}>
                    <summary
                      className="flex cursor-pointer list-none items-center justify-between gap-6 py-[22px] text-[15.5px] font-normal [&::-webkit-details-marker]:hidden"
                      style={{ color: C.ink }}
                    >
                      {f.q}
                      <DownOutlined
                        className="shrink-0 text-[11px] transition-transform duration-200 group-open:rotate-180"
                        style={{ color: C.faint }}
                      />
                    </summary>
                    <p className="max-w-[620px] pb-[22px] text-[14px] font-light leading-[1.9]" style={{ color: C.sub }}>
                      {f.a}
                    </p>
                  </details>
                ))}
              </div>
            </div>
          </Container>
        </section>

        {/* ============ 结尾 CTA ============ */}
        <section className="relative overflow-hidden pt-[88px] md:pt-[120px]">
          <div className="pointer-events-none absolute inset-x-0 bottom-0 z-0 h-[560px]">
            <div
              className="absolute left-1/2 top-[40px] h-[620px] w-[1400px] -translate-x-1/2 rounded-[50%] opacity-70 blur-[90px]"
              style={{ background: "radial-gradient(40% 40% at 50% 50%, #d6f0e4 0%, rgba(255,255,255,0) 76%)" }}
            />
          </div>
          <Container className="relative z-10 flex flex-col items-center pb-[96px] text-center md:pb-[120px]">
            <Reveal>
              <Tag cn="免费开始" en="Get Started" variant="outline" />
            </Reveal>
            <Reveal delay={60}>
              <h2 className="mt-[16px] text-[36px] font-normal leading-[1.2] tracking-[-0.01em] md:text-[60px]" style={{ color: C.ink }}>
                今天就把账理清
              </h2>
            </Reveal>
            <Reveal delay={100}>
              <p className="mt-[18px] max-w-[520px] text-[15px] font-light leading-[1.75] md:text-[16px]" style={{ color: C.sub }}>
                登录云财，录入第一张凭证，你会看到报表在同一秒生成。
              </p>
            </Reveal>
            <Reveal delay={140}>
              <div className="mt-[36px] flex flex-col items-center gap-[16px] sm:flex-row sm:gap-[24px]">
                <Button onClick={goApp}>
                  {primaryLabel}
                  <ArrowRightOutlined className="text-[12px]" />
                </Button>
                <a
                  href="#capabilities"
                  className="text-[14px] font-light transition-opacity hover:opacity-70"
                  style={{ color: C.brandDeep }}
                >
                  先看看功能
                </a>
              </div>
            </Reveal>
            <Reveal delay={180}>
              <div
                className="mt-[36px] flex flex-wrap items-center justify-center gap-x-[28px] gap-y-3 text-[13px] font-light"
                style={{ color: "#9a9a9a" }}
              >
                {[
                  { icon: CloudOutlined, t: "浏览器即用" },
                  { icon: ApartmentOutlined, t: "多账套隔离" },
                  { icon: AuditOutlined, t: "准则口径出表" },
                ].map((b) => (
                  <span key={b.t} className="flex items-center gap-1.5">
                    <b.icon className="text-[13px]" style={{ color: C.brand }} />
                    {b.t}
                  </span>
                ))}
              </div>
            </Reveal>
          </Container>
        </section>
      </main>

      {/* ============ 页脚 ============ */}
      <footer className="relative overflow-hidden" style={{ background: "#0a0a0a" }}>
        {/* 顶部：1418 宽 / padding 80px 120px 64px / space-between / 底部 1px 白 10% */}
        <div
          className="mx-auto flex w-full max-w-[1418px] flex-col gap-[40px] px-6 pt-[64px] pb-[48px] md:flex-row md:justify-between md:px-[120px] md:pt-[80px] md:pb-[64px]"
          style={{ borderBottom: "1px solid rgba(255,255,255,.1)" }}
        >
          <div className="flex max-w-[320px] flex-col gap-[22px]">
            <Logo tone="light" />
            <p className="text-[13.5px] font-light leading-[1.9]" style={{ color: "rgba(255,255,255,.55)" }}>
              面向中小企业的在线财务系统。凭证、账簿、报表、出纳一体化，让每一笔账都清楚。
            </p>
          </div>

          <nav className="flex flex-wrap gap-x-[56px] gap-y-[28px] md:gap-x-[80px]">
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
                title: "开始使用",
                items: [
                  { label: authed ? "进入控制台" : "登录系统", href: primaryHref },
                  { label: "创建账套", href: primaryHref },
                  { label: "帮助文档", href: "#faq" },
                ],
              },
            ].map((col) => (
              <div key={col.title} className="flex flex-col gap-[16px]">
                <h3 className="text-[14px] font-medium text-white">{col.title}</h3>
                <ul className="flex flex-col">
                  {col.items.map((it) => (
                    <li key={it.label} className="leading-[32px]">
                      <a
                        href={it.href}
                        className="text-[13.5px] font-light transition-colors hover:text-white!"
                        style={{ color: "rgba(255,255,255,.6)" }}
                      >
                        {it.label}
                      </a>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </nav>
        </div>

        {/* 版权行 */}
        <div className="mx-auto flex w-full max-w-[1418px] flex-col gap-[24px] px-6 py-[26px] text-[12px] font-light md:flex-row md:items-start md:justify-between md:px-[120px]">
          <span style={{ color: "rgba(255,255,255,.45)" }}>© {new Date().getFullYear()} 云财 · 保留所有权利</span>
          <span className="flex flex-wrap items-center gap-[24px]" style={{ color: "rgba(255,255,255,.45)" }}>
            <span className="flex items-center gap-1.5">
              <span className="size-[6px] rounded-full" style={{ background: C.brand }} />
              系统运行正常
            </span>
            <a
              href="#top"
              onClick={(e) => {
                e.preventDefault();
                window.scrollTo({ top: 0, behavior: "smooth" });
              }}
              className="transition-colors hover:text-white!"
              style={{ color: "rgba(255,255,255,.45)" }}
            >
              回到顶部
            </a>
          </span>
        </div>

        {/* 通栏字标：满宽拉伸的极扁 wordmark（参考站为 1440×22 图片） */}
        <div className="pointer-events-none w-full select-none px-0 pt-[11px] pb-[18px]" aria-hidden>
          <svg
            viewBox="0 0 1200 44"
            preserveAspectRatio="none"
            width="100%"
            height="22"
            className="block"
            role="presentation"
          >
            <text
              x="600"
              y="34"
              textAnchor="middle"
              fill="#1d1d1d"
              style={{ fontSize: "38px", fontWeight: 500, letterSpacing: "26px" }}
            >
              KUAIJI
            </text>
          </svg>
        </div>
      </footer>

      <LoginModal open={loginOpen && !authed} onClose={closeLogin} />
    </div>
  );
}
