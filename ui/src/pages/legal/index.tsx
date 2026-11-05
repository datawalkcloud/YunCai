import { Link } from "react-router";

/* ============================================================
 * 服务条款 / 隐私政策
 *
 * 登录弹窗底部的两个链接指向这里，避免出现死链。
 * 内容为面向中小企业财务 SaaS 的通用条款骨架，正式上线前需由法务定稿。
 * ============================================================ */

const C = {
  ink: "#1a1a1a",
  sub: "#646464",
  faint: "#b0b0b0",
  brand: "#079d55",
  line: "#ececec",
};

type Block = { h: string; p: string[] };

function LegalPage({
  tag,
  title,
  updated,
  blocks,
}: {
  tag: string;
  title: string;
  updated: string;
  blocks: Block[];
}) {
  return (
    <div className="min-h-svh bg-white">
      <header
        className="h-[65px] border-b"
        style={{ borderColor: C.line }}
      >
        <div className="mx-auto flex h-full w-full max-w-[1240px] items-center justify-between px-5 md:px-8">
          <Link to="/" className="flex items-baseline gap-1.5">
            <span className="text-[16px] font-medium tracking-tight" style={{ color: C.ink }}>
              云财
            </span>
            <span
              className="text-[9.5px] font-light uppercase tracking-[0.2em]"
              style={{ color: "#c0c0c0" }}
            >
              KuaiJi
            </span>
          </Link>
          <Link
            to="/"
            className="text-[14px] font-light transition-opacity hover:opacity-70"
            style={{ color: C.sub }}
          >
            返回首页
          </Link>
        </div>
      </header>

      <main className="mx-auto w-full max-w-[720px] px-5 py-14 md:px-0 md:py-20">
        <span
          className="inline-flex h-6 items-center rounded-full px-[14px] text-[12.5px] font-medium"
          style={{ background: "#f3f9f6", color: C.brand }}
        >
          {tag}
        </span>
        <h1 className="mt-5 text-[34px] font-normal leading-[1.2] tracking-[-0.01em] md:text-[44px]" style={{ color: C.ink }}>
          {title}
        </h1>
        <p className="mt-3 text-[13px] font-light" style={{ color: C.faint }}>
          最近更新：{updated}
        </p>

        <div className="mt-12 flex flex-col gap-9">
          {blocks.map((b) => (
            <section key={b.h}>
              <h2 className="text-[17px] font-medium" style={{ color: C.ink }}>
                {b.h}
              </h2>
              {b.p.map((t) => (
                <p
                  key={t}
                  className="mt-2.5 text-[15px] font-light leading-[1.85]"
                  style={{ color: C.sub }}
                >
                  {t}
                </p>
              ))}
            </section>
          ))}
        </div>

        <p
          className="mt-14 border-t pt-6 text-[13px] font-light leading-[1.8]"
          style={{ borderColor: C.line, color: C.faint }}
        >
          本文为产品条款的公开说明。如需就具体条款与我们沟通，可通过官网页面提供的联系方式与我们取得联系。
        </p>
      </main>
    </div>
  );
}

const TERMS: Block[] = [
  {
    h: "一、服务内容",
    p: [
      "云财向你提供在线的财务核算服务，包含凭证填制与入账、账簿查询、财务报表生成、资金出纳与期末处理等功能。",
      "我们会持续迭代功能，具体可用的模块以你登录后系统中实际展示为准。功能调整不会影响你已录入的历史数据。",
    ],
  },
  {
    h: "二、账号与账套",
    p: [
      "你需要使用我们分配的账号登录系统。请妥善保管账号与密码，因你自身原因导致账号泄露所产生的后果由你自行承担。",
      "一个账号可管理多个账套，不同账套之间的数据相互隔离。你应确保录入的会计资料真实、完整、合法。",
    ],
  },
  {
    h: "三、使用规范",
    p: [
      "你不得利用本服务从事违反法律法规的活动，不得通过技术手段干扰系统正常运行或非法获取他人数据。",
      "如你违反前述约定，我们有权在通知后暂停或终止对你的服务。",
    ],
  },
  {
    h: "四、服务可用性与数据",
    p: [
      "我们会采取合理措施保障服务稳定与数据安全，但因不可抗力、基础电信或云服务故障等原因导致的服务中断，我们不承担责任。",
      "你录入的业务数据归你所有。建议你定期导出重要报表作为备份。",
    ],
  },
];

const PRIVACY: Block[] = [
  {
    h: "一、我们收集的信息",
    p: [
      "注册与登录时，我们会收集你的账号名与登录凭证；使用过程中会记录操作日志，用于安全审计与问题排查。",
      "你在系统中录入的凭证、账簿、报表等业务数据，仅用于为你提供财务核算服务。",
    ],
  },
  {
    h: "二、我们如何使用信息",
    p: [
      "用于完成身份验证、提供与维护本服务、排查故障、以及在必要时与你联系。",
      "我们不会将你的业务数据用于广告投放，也不会出售给第三方。",
    ],
  },
  {
    h: "三、数据存储与隔离",
    p: [
      "数据集中存储于云端，不同账套之间逻辑隔离；我们通过权限控制确保只有被授权的用户可以访问对应账套。",
      "传输过程采用加密通道，敏感凭证不以明文形式保存。",
    ],
  },
  {
    h: "四、你的权利",
    p: [
      "你可以随时查看、更正你在系统中的业务数据，也可以导出报表。",
      "如需注销账号或删除数据，可通过官网提供的联系方式向我们提出，我们会在核实身份后处理。",
    ],
  },
];

export function TermsPage() {
  return <LegalPage tag="条款 TERMS" title="用户服务协议" updated="2026-10-28" blocks={TERMS} />;
}

export function PrivacyPage() {
  return <LegalPage tag="隐私 PRIVACY" title="隐私政策" updated="2026-10-28" blocks={PRIVACY} />;
}
