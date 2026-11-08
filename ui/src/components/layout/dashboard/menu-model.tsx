import {
    AdjustmentsHorizontalIcon,
    ArchiveBoxIcon,
    ArrowPathIcon,
    ArrowsRightLeftIcon,
    ArrowUpTrayIcon,
    BanknotesIcon,
    BookOpenIcon,
    BuildingLibraryIcon,
    BuildingOffice2Icon,
    CalculatorIcon,
    CalendarDaysIcon,
    ChartBarIcon,
    ChartBarSquareIcon,
    ChartPieIcon,
    CircleStackIcon,
    ClockIcon,
    CloudArrowDownIcon,
    Cog6ToothIcon,
    CreditCardIcon,
    CubeIcon,
    CurrencyYenIcon,
    DocumentCheckIcon,
    DocumentDuplicateIcon,
    DocumentMagnifyingGlassIcon,
    DocumentTextIcon,
    ExclamationTriangleIcon,
    EyeIcon,
    FlagIcon,
    FolderIcon,
    InformationCircleIcon,
    InboxArrowDownIcon,
    InboxIcon,
    KeyIcon,
    LinkIcon,
    ListBulletIcon,
    PlusCircleIcon,
    QueueListIcon,
    ReceiptPercentIcon,
    RectangleStackIcon,
    ScaleIcon,
    ShieldCheckIcon,
    Squares2X2Icon,
    TableCellsIcon,
    TagIcon,
    TicketIcon,
    UserGroupIcon,
    UsersIcon,
    ViewColumnsIcon,
    WalletIcon,
} from "@heroicons/react/24/solid";

/* ------------------------------------------------------------------ */
/*  菜单模型：to 为账套下的相对子路径（每项都有真实链接）。                */
/*  带 children 的是折叠分组；无 children 的是直达项（如期末结转）。        */
/*  wip 项按柠檬云 J6 的原位置归入各分组，仅标记「需第三方渠道、尚未接入」   */
/*  （见 docs/柠檬云J6功能差距清单.md 第四节）。                            */
/* ------------------------------------------------------------------ */

const leafIcon = "size-[15px]";

export type MenuLeaf = { label: string; wip?: boolean; to: string; icon: React.ReactNode };
export type MenuGroup = { label: string; icon: React.ReactNode; to?: string; wip?: boolean; children?: MenuLeaf[] };
/**
 * 一级分类。to 为点击分类后进入的账套相对子路径（空串 = 首页）；
 * 两个分类共用同一套外壳，切换分类只换侧栏菜单与顶栏右侧的期间控件。
 * home 表示该分类是否有首页可回：记账有，报税没有（侧栏与页签条都不出现「首页」）。
 */
export type MenuCategory = { label: string; to: string; groups: MenuGroup[]; home?: boolean };

/** 记账分类的分组层级：与柠檬云 J6 一致（凭证/资金/发票/工资/资产/期末结转/账簿/报表/设置）。 */
const accountingGroups: MenuGroup[] = [
    {
        label: "凭证",
        icon: <DocumentTextIcon className="size-[17px]" />,
        children: [
            { label: "新增凭证", to: "vouchers/new", icon: <PlusCircleIcon className={leafIcon} /> },
            { label: "查看凭证", to: "vouchers/manage", icon: <DocumentMagnifyingGlassIcon className={leafIcon} /> },
            { label: "会计电子档案", to: "vouchers/erecord", icon: <ArchiveBoxIcon className={leafIcon} /> },
            { label: "收票宝", to: "vouchers/spb", wip: true, icon: <InboxIcon className={leafIcon} /> },
        ],
    },
    {
        label: "资金",
        icon: <WalletIcon className="size-[17px]" />,
        children: [
            { label: "现金日记账", to: "funds/cash-journal", icon: <BanknotesIcon className={leafIcon} /> },
            { label: "银行日记账", to: "funds/deposit-journal", icon: <BuildingLibraryIcon className={leafIcon} /> },
            { label: "内部转账", to: "funds/transfer", icon: <ArrowsRightLeftIcon className={leafIcon} /> },
            { label: "票据管理", to: "funds/draft", icon: <TicketIcon className={leafIcon} /> },
            { label: "资金报表", to: "funds/report", icon: <ChartBarSquareIcon className={leafIcon} /> },
            { label: "核对总账", to: "funds/reconciliation", icon: <ScaleIcon className={leafIcon} /> },
            { label: "收支类别", to: "funds/entry-categories", icon: <TagIcon className={leafIcon} /> },
            { label: "银企互联", to: "funds/bank-link", wip: true, icon: <LinkIcon className={leafIcon} /> },
            { label: "账户设置", to: "funds/account", icon: <CreditCardIcon className={leafIcon} /> },
        ],
    },
    {
        label: "发票",
        icon: <ReceiptPercentIcon className="size-[17px]" />,
        children: [
            { label: "一键取票", to: "invoices/fetch", wip: true, icon: <CloudArrowDownIcon className={leafIcon} /> },
            { label: "销项发票", to: "invoices/sales", icon: <ArrowUpTrayIcon className={leafIcon} /> },
            { label: "进项发票", to: "invoices/purchase", icon: <InboxArrowDownIcon className={leafIcon} /> },
            { label: "费用单据", to: "invoices/expense", icon: <DocumentTextIcon className={leafIcon} /> },
            { label: "增值税测算", to: "invoices/vat-calc", icon: <CalculatorIcon className={leafIcon} /> },
            { label: "发票资金一览表", to: "invoices/summary", icon: <TableCellsIcon className={leafIcon} /> },
            { label: "发票风险分析", to: "invoices/risk", wip: true, icon: <ExclamationTriangleIcon className={leafIcon} /> },
        ],
    },
    {
        label: "工资",
        icon: <UsersIcon className="size-[17px]" />,
        children: [
            { label: "工资管理", to: "salary/manage", icon: <BanknotesIcon className={leafIcon} /> },
            { label: "员工信息", to: "salary/employee", icon: <UserGroupIcon className={leafIcon} /> },
            { label: "工资统计表", to: "salary/summary", icon: <ChartPieIcon className={leafIcon} /> },
            { label: "五险一金设置", to: "salary/insurance", icon: <ShieldCheckIcon className={leafIcon} /> },
        ],
    },
    {
        label: "资产",
        icon: <BuildingOffice2Icon className="size-[17px]" />,
        children: [
            { label: "资产管理", to: "assets/manage", icon: <CubeIcon className={leafIcon} /> },
            { label: "折旧摊销明细表", to: "assets/depreciation", icon: <ChartBarIcon className={leafIcon} /> },
            { label: "资产汇总表", to: "assets/summary", icon: <TableCellsIcon className={leafIcon} /> },
            { label: "资产类别设置", to: "assets/category", icon: <TagIcon className={leafIcon} /> },
            { label: "资产核对总账", to: "assets/check", icon: <ScaleIcon className={leafIcon} /> },
        ],
    },
    { label: "期末结转", icon: <CalendarDaysIcon className="size-[17px]" />, to: "closing" },
    {
        label: "账簿",
        icon: <BookOpenIcon className="size-[17px]" />,
        children: [
            { label: "明细账", to: "ledger/subsidiary", icon: <ListBulletIcon className={leafIcon} /> },
            { label: "总账", to: "ledger/general", icon: <BookOpenIcon className={leafIcon} /> },
            { label: "科目余额表", to: "ledger/trial-balance", icon: <TableCellsIcon className={leafIcon} /> },
            { label: "凭证汇总表", to: "ledger/voucher-summary", icon: <Squares2X2Icon className={leafIcon} /> },
            { label: "序时账", to: "ledger/journal", icon: <QueueListIcon className={leafIcon} /> },
            { label: "核算项目明细账", to: "ledger/assist-subsidiary", icon: <ListBulletIcon className={leafIcon} /> },
            { label: "核算项目余额表", to: "ledger/assist-balance", icon: <TableCellsIcon className={leafIcon} /> },
            { label: "核算项目组合表", to: "ledger/assist-combine", icon: <Squares2X2Icon className={leafIcon} /> },
            { label: "多栏账", to: "ledger/multi-column", icon: <ViewColumnsIcon className={leafIcon} /> },
            { label: "账龄分析表", to: "ledger/ageing", icon: <ClockIcon className={leafIcon} /> },
        ],
    },
    {
        label: "报表",
        icon: <ChartBarIcon className="size-[17px]" />,
        children: [
            { label: "资产负债表", to: "reports/balance-sheet", icon: <ScaleIcon className={leafIcon} /> },
            { label: "利润表", to: "reports/income-statement", icon: <ChartPieIcon className={leafIcon} /> },
            { label: "利润表季报", to: "reports/income-quarter", icon: <ChartPieIcon className={leafIcon} /> },
            { label: "现金流量表", to: "reports/cash-flow", icon: <BanknotesIcon className={leafIcon} /> },
            { label: "现金流量表季报", to: "reports/cash-flow-quarter", icon: <BanknotesIcon className={leafIcon} /> },
            { label: "标准现金流量表", to: "reports/standard-cash-flow", icon: <BanknotesIcon className={leafIcon} /> },
            { label: "部门利润表", to: "reports/department-income", icon: <BuildingOffice2Icon className={leafIcon} /> },
            { label: "项目利润表", to: "reports/project-income", icon: <FolderIcon className={leafIcon} /> },
            { label: "数据透视表", to: "reports/pivot", icon: <TableCellsIcon className={leafIcon} /> },
            { label: "费用明细表", to: "reports/expense", icon: <ReceiptPercentIcon className={leafIcon} /> },
            { label: "所有者权益变动表", to: "reports/equity-change", icon: <ArrowsRightLeftIcon className={leafIcon} /> },
            { label: "纳税统计表", to: "reports/tax", icon: <ReceiptPercentIcon className={leafIcon} /> },
            { label: "财务概要信息", to: "reports/financial-summary", icon: <InformationCircleIcon className={leafIcon} /> },
        ],
    },
    {
        label: "设置",
        icon: <Cog6ToothIcon className="size-[17px]" />,
        children: [
            { label: "账套", to: "settings/account-sets", icon: <BuildingOffice2Icon className={leafIcon} /> },
            { label: "科目", to: "settings/accounts", icon: <RectangleStackIcon className={leafIcon} /> },
            { label: "期初", to: "settings/opening-balances", icon: <FlagIcon className={leafIcon} /> },
            { label: "币别", to: "settings/currency", icon: <CurrencyYenIcon className={leafIcon} /> },
            { label: "凭证字", to: "settings/voucher-word", icon: <TagIcon className={leafIcon} /> },
            { label: "辅助核算", to: "settings/assist-items", icon: <AdjustmentsHorizontalIcon className={leafIcon} /> },
            { label: "凭证模板", to: "settings/voucher-template", icon: <DocumentDuplicateIcon className={leafIcon} /> },
            { label: "权限设置", to: "settings/permission", icon: <ShieldCheckIcon className={leafIcon} /> },
            { label: "老板看账", to: "settings/boss", icon: <EyeIcon className={leafIcon} /> },
            { label: "备份恢复", to: "settings/backup", icon: <CircleStackIcon className={leafIcon} /> },
            { label: "旧账导入", to: "settings/import-old", icon: <ArrowUpTrayIcon className={leafIcon} /> },
            { label: "操作日志", to: "settings/log", icon: <ClockIcon className={leafIcon} /> },
            { label: "关联云发票", to: "settings/eis-relation", wip: true, icon: <LinkIcon className={leafIcon} /> },
            { label: "关联进销存", to: "settings/scm-relation", wip: true, icon: <LinkIcon className={leafIcon} /> },
            { label: "重新初始化", to: "settings/reinit", icon: <ArrowPathIcon className={leafIcon} /> },
        ],
    },
];

/** 报税分类的菜单：与原智能报税工作台的左侧导航一一对应，每项都是账套下的独立页面。 */
const taxGroups: MenuGroup[] = [
    { label: "发票获取", icon: <CloudArrowDownIcon className="size-[17px]" />, to: "tax/fetch" },
    { label: "税种申报", icon: <DocumentCheckIcon className="size-[17px]" />, to: "tax/declare" },
    { label: "作废与更正", icon: <ArrowPathIcon className="size-[17px]" />, to: "tax/correction" },
    {
        label: "税费缴纳",
        icon: <BanknotesIcon className="size-[17px]" />,
        children: [
            { label: "缴税", to: "tax/pay", icon: <WalletIcon className={leafIcon} /> },
            { label: "缴款信息查询", to: "tax/payment-query", icon: <DocumentMagnifyingGlassIcon className={leafIcon} /> },
            { label: "开具完税证明", to: "tax/certificate", icon: <ReceiptPercentIcon className={leafIcon} /> },
            { label: "下载完税证明", to: "tax/certificate-download", icon: <ArchiveBoxIcon className={leafIcon} /> },
        ],
    },
    { label: "报表查询", icon: <ChartBarSquareIcon className="size-[17px]" />, to: "tax/reports" },
    {
        label: "个税申报",
        icon: <UsersIcon className="size-[17px]" />,
        children: [
            { label: "人员管理", to: "tax/iit/people", icon: <UserGroupIcon className={leafIcon} /> },
            { label: "报表填写", to: "tax/iit/report", icon: <DocumentTextIcon className={leafIcon} /> },
            { label: "申报缴款", to: "tax/iit/pay", icon: <BanknotesIcon className={leafIcon} /> },
            { label: "查询统计", to: "tax/iit/stats", icon: <ChartPieIcon className={leafIcon} /> },
        ],
    },
    { label: "税费种管理", icon: <AdjustmentsHorizontalIcon className="size-[17px]" />, to: "tax/kinds" },
    { label: "报税设置", icon: <Cog6ToothIcon className="size-[17px]" />, to: "tax/settings" },
    { label: "任务中心", icon: <QueueListIcon className="size-[17px]" />, to: "tax/tasks" },
    { label: "电子税务局", icon: <KeyIcon className="size-[17px]" />, to: "tax/login" },
];

export const menuCategories: MenuCategory[] = [
    { label: "记账", to: "", groups: accountingGroups, home: true },
    { label: "报税", to: "tax/fetch", groups: taxGroups, home: false },
];

const allGroups: MenuGroup[] = menuCategories.flatMap((category) => category.groups);

/** 相对路径 → 菜单标题（首页单独处理，不在分组里）。顶部页签靠它取名。 */
export const ROUTE_TITLES: Record<string, string> = (() => {
    const map: Record<string, string> = {};
    for (const group of allGroups) {
        if (group.to) map[group.to] = group.label;
        for (const child of group.children ?? []) map[child.to] = child.label;
    }
    return map;
})();

/** 当前相对路径所属的一级分类；首页与未收录路径归入记账。 */
export function findCategoryOf(tabKey: string): MenuCategory {
    return (
        menuCategories.find((category) =>
            category.groups.some((group) => group.to === tabKey || group.children?.some((leaf) => leaf.to === tabKey)),
        ) ?? menuCategories[0]
    );
}
