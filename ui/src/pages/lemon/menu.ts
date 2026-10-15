/**
 * 柠檬云 J6 侧栏菜单树（与在线系统菜单结构 1:1）。
 * 路由均为 /lemon 下的相对路径。
 */
import type { ComponentType } from "react";
import {
    BankOutlined,
    BookOutlined,
    CalendarOutlined,
    CrownOutlined,
    FileDoneOutlined,
    FileTextOutlined,
    FundOutlined,
    HomeOutlined,
    ProfileOutlined,
    SettingOutlined,
    TeamOutlined,
    WalletOutlined,
} from "@ant-design/icons";

export interface LemonMenuChild {
    label: string;
    path: string;
}

export interface LemonMenuItem {
    label: string;
    icon: ComponentType<{ className?: string }>;
    path?: string;
    children?: LemonMenuChild[];
}

export const LEMON_MENU: LemonMenuItem[] = [
    { label: "首页", icon: HomeOutlined, path: "/lemon/home" },
    {
        label: "凭证",
        icon: BookOutlined,
        children: [
            { label: "新增凭证", path: "/lemon/voucher/new" },
            { label: "查看凭证", path: "/lemon/voucher/list" },
            { label: "会计电子档案", path: "/lemon/erecord" },
            { label: "收票宝", path: "/lemon/spb" },
        ],
    },
    {
        label: "资金",
        icon: WalletOutlined,
        children: [
            { label: "现金日记账", path: "/lemon/funds/cash-journal" },
            { label: "银行日记账", path: "/lemon/funds/deposit-journal" },
            { label: "内部转账", path: "/lemon/funds/transfer" },
            { label: "票据管理", path: "/lemon/funds/draft" },
            { label: "资金报表", path: "/lemon/funds/report" },
            { label: "核对总账", path: "/lemon/funds/check" },
            { label: "收支类别", path: "/lemon/funds/ie-type" },
            { label: "银企互联", path: "/lemon/funds/bank-link" },
            { label: "账户设置", path: "/lemon/funds/account" },
        ],
    },
    {
        label: "发票",
        icon: ProfileOutlined,
        children: [
            { label: "一键取票", path: "/lemon/invoice/fetch" },
            { label: "销项发票", path: "/lemon/invoice/sales" },
            { label: "进项发票", path: "/lemon/invoice/purchase" },
            { label: "费用单据", path: "/lemon/invoice/expense" },
            { label: "发票资金一览表", path: "/lemon/invoice/summary" },
            { label: "发票风险分析", path: "/lemon/invoice/risk" },
        ],
    },
    {
        label: "工资",
        icon: TeamOutlined,
        children: [
            { label: "工资管理", path: "/lemon/salary/manage" },
            { label: "员工信息", path: "/lemon/salary/employee" },
            { label: "工资统计表", path: "/lemon/salary/summary" },
            { label: "五险一金设置", path: "/lemon/salary/insurance" },
        ],
    },
    {
        label: "资产",
        icon: BankOutlined,
        children: [
            { label: "资产管理", path: "/lemon/asset/manage" },
            { label: "折旧摊销明细表", path: "/lemon/asset/depreciation" },
            { label: "资产汇总表", path: "/lemon/asset/summary" },
            { label: "资产类别设置", path: "/lemon/asset/category" },
            { label: "资产核对总账", path: "/lemon/asset/check" },
        ],
    },
    { label: "期末结转", icon: CalendarOutlined, path: "/lemon/closing" },
    {
        label: "账簿",
        icon: FileTextOutlined,
        children: [
            { label: "明细账", path: "/lemon/ledger/subsidiary" },
            { label: "总账", path: "/lemon/ledger/general" },
            { label: "科目余额表", path: "/lemon/ledger/trial-balance" },
            { label: "凭证汇总表", path: "/lemon/ledger/voucher-summary" },
            { label: "序时账", path: "/lemon/ledger/journal" },
            { label: "核算项目明细账", path: "/lemon/ledger/assist-subsidiary" },
            { label: "核算项目余额表", path: "/lemon/ledger/assist-balance" },
            { label: "核算项目组合表", path: "/lemon/ledger/assist-combine" },
            { label: "多栏账", path: "/lemon/ledger/multi-column" },
            { label: "账龄分析表", path: "/lemon/ledger/ageing" },
        ],
    },
    {
        label: "报表",
        icon: FundOutlined,
        children: [
            { label: "资产负债表", path: "/lemon/report/balance-sheet" },
            { label: "利润表", path: "/lemon/report/income" },
            { label: "利润表季报", path: "/lemon/report/income-quarter" },
            { label: "现金流量表", path: "/lemon/report/cash-flow" },
            { label: "现金流量表季报", path: "/lemon/report/cash-flow-quarter" },
            { label: "标准现金流量表", path: "/lemon/report/standard-cash-flow" },
            { label: "部门利润表", path: "/lemon/report/department-income" },
            { label: "项目利润表", path: "/lemon/report/project-income" },
            { label: "数据透视表", path: "/lemon/report/pivot" },
            { label: "费用明细表", path: "/lemon/report/expense" },
            { label: "所有者权益变动表", path: "/lemon/report/equity-change" },
            { label: "纳税统计表", path: "/lemon/report/tax" },
            { label: "财务概要信息", path: "/lemon/report/financial-summary" },
        ],
    },
    {
        label: "一键报税",
        icon: FileDoneOutlined,
        children: [{ label: "一键登录税局", path: "/lemon/tax/login" }],
    },
    {
        label: "设置",
        icon: SettingOutlined,
        children: [
            { label: "账套", path: "/lemon/settings/account-sets" },
            { label: "科目", path: "/lemon/settings/accounts" },
            { label: "期初", path: "/lemon/settings/opening" },
            { label: "币别", path: "/lemon/settings/currency" },
            { label: "凭证字", path: "/lemon/settings/voucher-word" },
            { label: "辅助核算", path: "/lemon/settings/assist" },
            { label: "凭证模板", path: "/lemon/settings/voucher-template" },
            { label: "权限设置", path: "/lemon/settings/permission" },
            { label: "老板看账", path: "/lemon/settings/boss" },
            { label: "备份恢复", path: "/lemon/settings/backup" },
            { label: "操作日志", path: "/lemon/settings/log" },
            { label: "关联云发票", path: "/lemon/settings/eis-relation" },
            { label: "关联进销存", path: "/lemon/settings/scm-relation" },
            { label: "旧账导入", path: "/lemon/settings/import-old" },
            { label: "重新初始化", path: "/lemon/settings/reinit" },
        ],
    },
];

/** 底部固定项（原版点击打开付费对比页）。 */
export const LEMON_EXTRA = { label: "升级专业版", icon: CrownOutlined, path: "/lemon/pro" };

/** 依据路径找到页名（页签条用）。 */
export function menuTitleOf(pathname: string): string | undefined {
    if (pathname === LEMON_EXTRA.path) return LEMON_EXTRA.label;
    for (const m of LEMON_MENU) {
        if (m.path === pathname) return m.label;
        for (const c of m.children ?? []) if (c.path === pathname) return c.label;
    }
    return undefined;
}

/** 依据路径找到所属模块（侧栏展开态用）。 */
export function moduleOf(pathname: string): LemonMenuItem | undefined {
    return LEMON_MENU.find(
        (m) => m.path === pathname || (m.children ?? []).some((c) => c.path === pathname),
    );
}
