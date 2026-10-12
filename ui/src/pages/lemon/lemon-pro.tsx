/**
 * 复刻区升级专业版页（对应柠檬云 /Pro/PayDialog，整页非弹窗）：
 * 到期提示 + 免费版/专业版功能对比表 + 立即购买按钮。
 */
import { Crown, X } from "lucide-react";

import { Page } from "./components/ui";

/** 行状态：✓绿勾 / ✗红叉 / 文字。 */
function CellMark({ ok, text }: { ok?: boolean; text?: string }) {
    if (text) return <span className="text-[13px] text-[#333]">{text}</span>;
    return ok ? (
        <span className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-[#44b449] text-[11px] text-white">✓</span>
    ) : (
        <span className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-[#f5222d] text-[11px] text-white">✕</span>
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

export default function LemonPro() {
    const td = "border-b border-[#f0f0f0] px-4 py-3 align-middle text-[13px] text-[#333]";
    return (
        <Page>
            <div className="mx-auto w-[1200px] pb-10">
                {/* 头部米黄横幅 */}
                <div className="relative mt-5 flex items-center justify-center rounded-md bg-[#fbeed3] py-4">
                    <span className="flex items-center gap-2 text-[18px] font-bold text-[#8a5a00]">
                        <Crown className="h-5 w-5 text-[#f5a623]" /> 升级专业版
                    </span>
                    <X className="absolute right-4 cursor-pointer text-[#c9a86a]" />
                </div>
                {/* 到期文案 */}
                <div className="mt-6 text-center">
                    <div className="text-[16px] text-[#333]">
                        专业版体验期 <span className="text-[22px] text-[#ff9f2e]">已到期</span> ！
                        <span className="text-[18px] font-bold text-[#ff9f2e]">建议立即升级！</span>
                    </div>
                    <div className="mt-3 text-sm text-[#333]">
                        该账套已自动转为免费版，之前录入专业版专属模块中的数据仅供查询和导出。
                    </div>
                </div>
                {/* 对比表 */}
                <table className="mt-6 w-full border-collapse">
                    <thead>
                        <tr className="bg-[#f5f5f5] text-[14px] text-[#333]">
                            <th className="border border-[#e8e8e8] px-4 py-3 text-left font-bold">功能模块</th>
                            <th className="w-[220px] border border-[#e8e8e8] px-4 py-3 text-center font-bold">免费版</th>
                            <th className="w-[640px] border border-[#e8e8e8] px-4 py-3 text-center font-bold">
                                <span className="mr-1 inline-flex h-5 w-5 items-center justify-center rounded-[3px] bg-gradient-to-r from-[#f7c948] to-[#f5a623] align-[-3px] text-[11px] text-white">
                                    👑
                                </span>
                                专业版
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {PRO_ROWS.map((r) => (
                            <tr key={r.module}>
                                <td className={td}>
                                    {r.paid && <Crown className="mr-1.5 inline h-3.5 w-3.5 align-[-2px] text-[#f5a623]" />}
                                    {r.module}
                                </td>
                                <td className={`${td} text-center`}>
                                    <CellMark ok={r.free.ok} text={r.free.text} />
                                </td>
                                <td className={`${td} text-center leading-5`}>
                                    <CellMark ok={r.pro.ok} text={r.pro.text} />
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {/* 立即购买 */}
                <div className="mt-10 flex justify-center">
                    <span className="inline-flex h-[55px] w-[250px] cursor-pointer items-center justify-center rounded-[6px] bg-gradient-to-r from-[#f7d794] to-[#e8b04a] text-[20px] font-bold text-[#6b4a00] shadow-[0_2px_10px_rgba(232,176,74,0.4)]">
                        立即购买
                    </span>
                </div>
            </div>
        </Page>
    );
}
