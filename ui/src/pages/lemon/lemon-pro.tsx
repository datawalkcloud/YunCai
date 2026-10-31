/**
 * 复刻区升级专业版页（对应柠檬云 /Pro/PayDialog，整页非弹窗）：
 * 纯静态营销页——本项目暂无付费/版本体系，后端未提供升级、购买、到期状态接口，
 * 页面对比表仅为柠檬云界面复刻展示，不代表当前账套的真实版本状态。
 * 视觉遵循全站统一风格（云财蓝）：白卡 + slate 灰阶文字，✓ 用成功色 #10b981、✕ 用 #ef4444；
 * 专业版皇冠为品牌装饰，保留金色体系（#fffbeb / #fde68a / #92640b）。
 */
import { CrownOutlined } from "@ant-design/icons";

import { Page, WipBtn, WipNotice } from "./components/ui";

/** 行状态：✓绿勾 / ✗红叉 / 文字。 */
function CellMark({ ok, text }: { ok?: boolean; text?: string }) {
    if (text) return <span className="text-[13px] text-slate-700">{text}</span>;
    return ok ? (
        <span className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-[#10b981] text-[11px] text-white">✓</span>
    ) : (
        <span className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-[#ef4444] text-[11px] text-white">✕</span>
    );
}

interface ProRow {
    module: string;
    free: { ok?: boolean; text?: string };
    pro: { ok?: boolean; text?: string };
    paid?: boolean;
}

const PRO_ROWS: ProRow[] = [
    { module: "基础凭证、账簿、报表", free: { ok: true }, pro: { ok: true } },
    { module: "多辅助核算", free: { ok: true }, pro: { ok: true } },
    { module: "智能期末结转", free: { ok: true }, pro: { ok: true } },
    { module: "备份恢复", free: { ok: true }, pro: { ok: true } },
    { module: "移动记账", free: { ok: true }, pro: { ok: true } },
    {
        module: "资金管理",
        free: { ok: false },
        pro: { text: "银企互联自动获取银行流水 / 票据管理与贴现 / 一键生成凭证 / 随时查看资金报表" },
        paid: true,
    },
    {
        module: "发票管理",
        free: { ok: false },
        pro: { text: "一键导入进销项发票 / 发票一键生成凭证 / 发票一键生成出入库单据 / 增值税测算" },
        paid: true,
    },
    {
        module: "工资管理",
        free: { ok: false },
        pro: { text: "一键生成工资表 / 一键发放工资条 / 一键申报个税 / 一键生成计提发放工资凭证 / 员工信息智能管理 / 员工五险一金灵活设置" },
        paid: true,
    },
    {
        module: "资产管理",
        free: { ok: false },
        pro: { text: "资产包含固定资产、无形资产和长期待摊费用 / 资产新增、变更、处置全生命周期管理 / 自动计提折旧、摊销 / 一键生成资产凭证" },
        paid: true,
    },
    {
        module: "高级账簿、报表",
        free: { ok: false },
        pro: { text: "核算项目组合表 / 费用明细表 / 账龄分析表 / 所有者权益变动表 / 数据透视表" },
        paid: true,
    },
    { module: "关联进销存", free: { ok: false }, pro: { text: "一键导入进销存单据 / 自动生成凭证并记账" } },
    { module: "用户限制", free: { text: "【限1人】不可申购" }, pro: { text: "支持按使用人数购买，多人同时使用" } },
    { module: "会计电子档案", free: { text: "10M 不可扩容" }, pro: { text: "10G超大存储空间" } },
    { module: "售后服务", free: { text: "工作日 社群服务" }, pro: { text: "7×12专属客服一对一服务，提供远程协助" } },
];

/** 单元格统一口径：px-3 py-2.5、仅横向分隔线，与基元 Table 一致。 */
const TD = "border-b border-slate-100 px-3 py-2.5 align-middle text-[13px] text-slate-700";

export default function LemonPro() {
    return (
        <Page title="升级专业版" subtitle="免费版与专业版功能对比（静态展示）" icon={CrownOutlined}>
            <WipNotice text="本页为『升级专业版』界面复刻：项目当前没有付费版本体系，升级/购买流程尚未接入后端，以下内容为静态展示，不代表账套真实状态。" />
            <div className="mx-auto w-full max-w-[1200px] p-4">
                {/* 免费版/专业版功能对比（静态营销文案，非账套真实数据） */}
                <div className="overflow-x-auto">
                    <table className="w-full border-collapse">
                        <thead>
                            <tr className="bg-[#f8fafc] text-[13px] text-slate-800">
                                <th className="border-b border-slate-200 px-3 py-2.5 text-left font-medium">功能模块</th>
                                <th className="w-[220px] border-b border-slate-200 px-3 py-2.5 text-center font-medium">免费版</th>
                                <th className="w-[640px] border-b border-slate-200 px-3 py-2.5 text-center font-medium">
                                    <span className="mr-1 inline-flex h-5 w-5 items-center justify-center rounded-md bg-gradient-to-r from-[#fde68a] to-[#f59e0b] align-[-3px] text-[11px] text-[#92640b]">
                                        <CrownOutlined />
                                    </span>
                                    专业版
                                </th>
                            </tr>
                        </thead>
                        <tbody>
                            {PRO_ROWS.map((r, i) => (
                                <tr
                                    key={r.module}
                                    className={`transition-colors hover:bg-[#f6f9ff] ${i % 2 === 1 ? "bg-[#fafbfd]" : ""}`}
                                >
                                    <td className={TD}>
                                        {r.paid && <CrownOutlined className="mr-1.5 text-[14px]" style={{ color: "#f59e0b" }} />}
                                        {r.module}
                                    </td>
                                    <td className={`${TD} text-center`}>
                                        <CellMark ok={r.free.ok} text={r.free.text} />
                                    </td>
                                    <td className={`${TD} text-center leading-5`}>
                                        <CellMark ok={r.pro.ok} text={r.pro.text} />
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
                {/* 立即购买（尚未接入后端，建设中） */}
                <div className="mt-4 flex justify-center">
                    <WipBtn title="购买/升级流程尚未接入后端，建设中">立即购买</WipBtn>
                </div>
            </div>
        </Page>
    );
}
