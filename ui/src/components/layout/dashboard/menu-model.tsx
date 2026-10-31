import {
    BankOutlined,
    BookOutlined,
    CalendarOutlined,
    FileDoneOutlined,
    FileTextOutlined,
    FundOutlined,
    SettingOutlined,
    TeamOutlined,
    ToolOutlined,
    WalletOutlined,
} from "@ant-design/icons";

/* ------------------------------------------------------------------ */
/*  菜单模型：to 为账套下的相对子路径（每项都有真实链接）。                */
/*  带 children 的是折叠分组；无 children 的是直达项（如期末结转）。        */
/*  wip 子项属「外部对接 · 未接入」分组（需第三方渠道，见 docs/柠檬云J6功能差距清单.md 第四节）。 */
/* ------------------------------------------------------------------ */

export type MenuLeaf = { label: string; wip?: boolean; to: string };
export type MenuGroup = { label: string; icon: React.ReactNode; to?: string; children?: MenuLeaf[] };

/** 菜单层级：与柠檬云 J6 一致（首页 + 凭证/资金/发票/工资/资产/期末结转/账簿/报表/设置），末尾为「外部对接 · 未接入」折叠分组。 */
export const menuGroups: MenuGroup[] = [
    {
        label: "凭证",
        icon: <BookOutlined />,
        children: [
            { label: "新增凭证", to: "vouchers/new" },
            { label: "查看凭证", to: "vouchers/manage" },
            { label: "会计电子档案", to: "vouchers/erecord" },
        ],
    },
    {
        label: "资金",
        icon: <WalletOutlined />,
        children: [
            { label: "现金日记账", to: "funds/cash-journal" },
            { label: "银行日记账", to: "funds/deposit-journal" },
            { label: "内部转账", to: "funds/transfer" },
            { label: "票据管理", to: "funds/draft" },
            { label: "资金报表", to: "funds/report" },
            { label: "核对总账", to: "funds/reconciliation" },
            { label: "收支类别", to: "funds/entry-categories" },
            { label: "账户设置", to: "funds/account" },
        ],
    },
    {
        label: "发票",
        icon: <FileDoneOutlined />,
        children: [
            { label: "销项发票", to: "invoices/sales" },
            { label: "进项发票", to: "invoices/purchase" },
            { label: "费用单据", to: "invoices/expense" },
            { label: "发票资金一览表", to: "invoices/summary" },
        ],
    },
    {
        label: "工资",
        icon: <TeamOutlined />,
        children: [
            { label: "工资管理", to: "salary/manage" },
            { label: "员工信息", to: "salary/employee" },
            { label: "工资统计表", to: "salary/summary" },
            { label: "五险一金设置", to: "salary/insurance" },
        ],
    },
    {
        label: "资产",
        icon: <BankOutlined />,
        children: [
            { label: "资产管理", to: "assets/manage" },
            { label: "折旧摊销明细表", to: "assets/depreciation" },
            { label: "资产汇总表", to: "assets/summary" },
            { label: "资产类别设置", to: "assets/category" },
            { label: "资产核对总账", to: "assets/check" },
        ],
    },
    { label: "期末结转", icon: <CalendarOutlined />, to: "closing" },
    {
        label: "账簿",
        icon: <FileTextOutlined />,
        children: [
            { label: "明细账", to: "ledger/subsidiary" },
            { label: "总账", to: "ledger/general" },
            { label: "科目余额表", to: "ledger/trial-balance" },
            { label: "凭证汇总表", to: "ledger/voucher-summary" },
            { label: "序时账", to: "ledger/journal" },
            { label: "核算项目明细账", to: "ledger/assist-subsidiary" },
            { label: "核算项目余额表", to: "ledger/assist-balance" },
            { label: "核算项目组合表", to: "ledger/assist-combine" },
            { label: "多栏账", to: "ledger/multi-column" },
            { label: "账龄分析表", to: "ledger/ageing" },
        ],
    },
    {
        label: "报表",
        icon: <FundOutlined />,
        children: [
            { label: "资产负债表", to: "reports/balance-sheet" },
            { label: "利润表", to: "reports/income-statement" },
            { label: "利润表季报", to: "reports/income-quarter" },
            { label: "现金流量表", to: "reports/cash-flow" },
            { label: "现金流量表季报", to: "reports/cash-flow-quarter" },
            { label: "标准现金流量表", to: "reports/standard-cash-flow" },
            { label: "部门利润表", to: "reports/department-income" },
            { label: "项目利润表", to: "reports/project-income" },
            { label: "数据透视表", to: "reports/pivot" },
            { label: "费用明细表", to: "reports/expense" },
            { label: "所有者权益变动表", to: "reports/equity-change" },
            { label: "纳税统计表", to: "reports/tax" },
            { label: "财务概要信息", to: "reports/financial-summary" },
        ],
    },
    {
        label: "设置",
        icon: <SettingOutlined />,
        children: [
            { label: "账套", to: "settings/account-sets" },
            { label: "科目", to: "settings/accounts" },
            { label: "期初", to: "settings/opening-balances" },
            { label: "币别", to: "settings/currency" },
            { label: "凭证字", to: "settings/voucher-word" },
            { label: "辅助核算", to: "settings/assist-items" },
            { label: "凭证模板", to: "settings/voucher-template" },
            { label: "权限设置", to: "settings/permission" },
            { label: "老板看账", to: "settings/boss" },
            { label: "备份恢复", to: "settings/backup" },
            { label: "旧账导入", to: "settings/import-old" },
            { label: "操作日志", to: "settings/log" },
            { label: "重新初始化", to: "settings/reinit" },
        ],
    },
    {
        label: "外部对接 · 未接入",
        icon: <ToolOutlined />,
        children: [
            { label: "收票宝", to: "vouchers/spb", wip: true },
            { label: "银企互联", to: "funds/bank-link", wip: true },
            { label: "一键取票", to: "invoices/fetch", wip: true },
            { label: "发票风险分析", to: "invoices/risk", wip: true },
            { label: "一键登录税局", to: "tax/login", wip: true },
            { label: "关联云发票", to: "settings/eis-relation", wip: true },
            { label: "关联进销存", to: "settings/scm-relation", wip: true },
            { label: "升级专业版", to: "pro", wip: true },
        ],
    },
];

/** 相对路径 → 菜单标题（首页单独处理，不在分组里）。顶部页签靠它取名。 */
export const ROUTE_TITLES: Record<string, string> = (() => {
    const map: Record<string, string> = {};
    for (const group of menuGroups) {
        if (group.to) map[group.to] = group.label;
        for (const child of group.children ?? []) map[child.to] = child.label;
    }
    return map;
})();
