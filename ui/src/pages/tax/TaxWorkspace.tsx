/**
 * 智能报税工作台（全屏独立外壳，对应菜单「一键报税」）。
 *
 * 与记账端并列：左侧深色导航 + 顶部账套/税款所属期 + 工作区页签，
 * 顶栏「去记账」回 /{accountSetId}。发票获取页已接本系统发票池，个税申报四个板块按本系统工资表
 * 与电子税务局取数复刻，税费缴纳四个页面按税局菜单取数（开具与下载仍在税局办理）；任务中心读后端
 * tax_tasks 台账（服务器每向税局办一次事留一行）。
 */
import { Fragment, useMemo, useState, type ReactNode } from "react";
import { useNavigate, useParams } from "react-router";
import { App, Button, ConfigProvider, DatePicker, Dropdown, Tooltip } from "antd";
import {
    BarChartOutlined,
    CalendarOutlined,
    CheckOutlined,
    CloseOutlined,
    ControlOutlined,
    DesktopOutlined,
    DownOutlined,
    ExpandOutlined,
    FileProtectOutlined,
    FileTextOutlined,
    FolderOpenOutlined,
    FormOutlined,
    GlobalOutlined,
    LeftOutlined,
    MoneyCollectOutlined,
    QuestionCircleOutlined,
    RightOutlined,
    ShopOutlined,
    SyncOutlined,
    UserOutlined,
} from "@ant-design/icons";
import dayjs from "dayjs";

import { AccountSetsApi } from "@/apis-gen";
import { useAccountSets } from "@/accountSets/AccountSetsContext";
import { makePeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { useAuth } from "@/auth/AuthContext";
import { apiConfig } from "@/lib/api-client";
import { defaultFilingPeriod } from "@/lib/tax-format";
import { getCachedTaxKinds, type TaxKindRow } from "@/lib/tax-portal";
import { TAX_THEME } from "@/lib/tax-theme";
import { cn } from "@/lib/utils";

import { CompanyInfoModal } from "./CompanyInfoModal";
import { InvoiceFetchPage } from "./InvoiceFetchPage";
import { TaxCorrectionPage } from "./TaxCorrectionPage";
import { TaxCertificateDownloadPage } from "./TaxCertificateDownloadPage";
import { TaxCertificatePage } from "./TaxCertificatePage";
import { TaxIitPage } from "./TaxIitPage";
import { TaxIitDeclarePage } from "./TaxIitDeclarePage";
import { TaxIitReportPage } from "./TaxIitReportPage";
import { TaxIitStatsPage } from "./TaxIitStatsPage";
import { TaxKindsPage } from "./TaxKindsPage";
import { TaxPaymentPage } from "./TaxPaymentPage";
import { TaxPaymentPayPage } from "./TaxPaymentPayPage";
import { TaxSettingsPage } from "./TaxSettingsPage";
import { TaxTaskCenterPage } from "./TaxTaskCenterPage";
import { TaxDeclarePage } from "./TaxDeclarePage";
import { TaxReportQueryPage } from "./TaxReportQueryPage";

type SectionKey =
    | "fetch"
    | "declare"
    | "correction"
    | "pay"
    | "paymentQuery"
    | "certificate"
    | "certificateDownload"
    | "reports"
    | "iitPeople"
    | "iitReport"
    | "iitPay"
    | "iitStats"
    | "taxKinds"
    | "settings"
    | "tasks";

/** 税费缴纳与个税申报在税局侧都是若干独立功能，做成一级菜单，子功能悬浮时从右侧弹出。 */
const PAYMENT_GROUP_KEY = "payment";
const IIT_GROUP_KEY = "iit";

const PAYMENT_CHILDREN: Array<{ key: SectionKey; label: string }> = [
    { key: "pay", label: "缴税" },
    { key: "paymentQuery", label: "缴款信息查询" },
    { key: "certificate", label: "开具完税证明" },
    { key: "certificateDownload", label: "下载完税证明" },
];

const IIT_CHILDREN: Array<{ key: SectionKey; label: string }> = [
    { key: "iitPeople", label: "人员管理" },
    { key: "iitReport", label: "报表填写" },
    { key: "iitPay", label: "申报缴款" },
    { key: "iitStats", label: "查询统计" },
];

/** 分组标题本身不是页面，点击直接进入第一个子功能。 */
const NAV_GROUPS: Record<string, Array<{ key: SectionKey; label: string }>> = {
    [PAYMENT_GROUP_KEY]: PAYMENT_CHILDREN,
    [IIT_GROUP_KEY]: IIT_CHILDREN,
};

type NavKey = SectionKey | typeof PAYMENT_GROUP_KEY | typeof IIT_GROUP_KEY | "etax";

const NAV_ITEMS: Array<{ key: NavKey; label: string; icon: ReactNode }> = [
    { key: "fetch", label: "发票获取", icon: <FileTextOutlined /> },
    { key: "declare", label: "税种申报", icon: <FolderOpenOutlined /> },
    { key: "correction", label: "作废与更正", icon: <FormOutlined /> },
    { key: PAYMENT_GROUP_KEY, label: "税费缴纳", icon: <MoneyCollectOutlined /> },
    { key: "reports", label: "报表查询", icon: <BarChartOutlined /> },
    { key: "iit", label: "个税申报", icon: <UserOutlined /> },
    { key: "taxKinds", label: "税费种管理", icon: <ControlOutlined /> },
    { key: "settings", label: "报税设置", icon: <DesktopOutlined /> },
    { key: "tasks", label: "任务中心", icon: <SyncOutlined /> },
    { key: "etax", label: "电子税务局", icon: <GlobalOutlined /> },
];

/** 页签与占位页要显示当前功能名，子菜单同样按自身标题显示。 */
function navLabel(key: SectionKey): string {
    for (const children of Object.values(NAV_GROUPS)) {
        const child = children.find((item) => item.key === key);
        if (child) return child.label;
    }
    return NAV_ITEMS.find((item) => item.key === key)?.label ?? "发票获取";
}

/** 税款所属期按认定里主税种的申报期限推算（按季申报取上一季度、按月申报取上月），不跟账套的记账期间走。 */
function defaultTaxPeriod(taxKinds: TaxKindRow[]): AccountingPeriod {
    const { year, month } = defaultFilingPeriod(taxKinds);
    return makePeriod(year, month);
}

export function TaxWorkspace() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { token } = useAuth();
    const { accountSets, refresh } = useAccountSets();
    const { message } = App.useApp();
    const accountSet = accountSets.find((set) => set.id === accountSetId);

    const taxKinds = getCachedTaxKinds();
    const defaultPeriod = useMemo(() => defaultTaxPeriod(taxKinds), [taxKinds]);
    const [userPeriod, setUserPeriod] = useState<AccountingPeriod | null>(null);
    const period = userPeriod ?? defaultPeriod;

    const [section, setSection] = useState<SectionKey>("fetch");
    const [companyOpen, setCompanyOpen] = useState(false);

    const toAccounting = () => navigate(`/${accountSetId}`);
    const toTaxLogin = () => navigate(`/${accountSetId}/tax/login`);
    const companyName = accountSet?.companyName || accountSet?.name || "选择账套";

    /** 切换账套后停在报税工作台的同一板块，记账端「当前账套」标记也一并刷新。 */
    async function switchAccount(id: string) {
        if (!id || id === accountSetId) return;
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdCurrentPost({ id });
            refresh();
        } catch (err) {
            message.error(err instanceof Error ? err.message : "设为当前账套失败。");
        }
        navigate(`/${id}/tax/declare`);
    }

    async function toggleFullscreen() {
        try {
            if (document.fullscreenElement) {
                await document.exitFullscreen();
            } else {
                await document.documentElement.requestFullscreen();
            }
        } catch {
            message.warning("当前浏览器不允许进入全屏模式。");
        }
    }

    function handleNav(key: NavKey) {
        // 电子税务局由本地助手打开，不在工作台内嵌。
        if (key === "etax") {
            toTaxLogin();
            return;
        }
        if (key === PAYMENT_GROUP_KEY || key === IIT_GROUP_KEY) {
            // 分组本身不是页面：点击直接进入第一个子功能，子功能由悬浮弹板选择。
            setSection(NAV_GROUPS[key][0].key);
            return;
        }
        setSection(key);
    }

    const activeLabel = navLabel(section);

    return (
        <div className="flex h-screen w-screen overflow-hidden bg-white font-sans text-slate-700 antialiased">
            <aside className="flex w-[132px] shrink-0 flex-col bg-[#0f172a]">
                <div className="flex h-12 items-center gap-2 px-3">
                    <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded bg-gradient-to-br from-[#3b82f6] to-[#1d4ed8] text-[13px] text-white shadow-[0_0_14px_rgba(59,130,246,0.55)]">
                        <FileProtectOutlined />
                    </span>
                    <span className="text-[14px] font-semibold tracking-wide text-white">智能报税</span>
                </div>
                <nav className="flex-1 overflow-y-auto py-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                    {/* 悬浮弹板选中的子功能用云财蓝高亮，与右侧工作区同一口径 */}
                    <ConfigProvider theme={TAX_THEME}>
                        {NAV_ITEMS.map((item) => {
                            const children = NAV_GROUPS[item.key];
                            const active = children
                                ? children.some((child) => child.key === section)
                                : item.key === section;
                            const button = (
                                <button
                                    type="button"
                                    onClick={() => handleNav(item.key)}
                                    className={cn(
                                        "relative flex h-10 w-full items-center gap-2 px-3 text-left text-[13px] transition-colors",
                                        active ? "bg-[#16233d] font-medium text-white" : "text-[#8f9bb3] hover:bg-white/[0.06] hover:text-white",
                                    )}
                                >
                                    {active && <span className="absolute inset-y-2 left-0 w-[3px] rounded-r bg-[#2f6bff]" />}
                                    <span
                                        className={cn(
                                            "text-[15px]",
                                            active ? "text-[#6b93ff]" : item.key === "etax" ? "text-[#4f8cff]" : "",
                                        )}
                                    >
                                        {item.icon}
                                    </span>
                                    <span className="truncate">{item.label}</span>
                                    {children && <RightOutlined className="ml-auto shrink-0 text-[9px] text-[#8f9bb3]" />}
                                </button>
                            );
                            if (!children) return <Fragment key={item.key}>{button}</Fragment>;
                            return (
                                <Dropdown
                                    key={item.key}
                                    trigger={["hover"]}
                                    placement="rightTop"
                                    mouseEnterDelay={0.08}
                                    menu={{
                                        items: children.map((child) => ({ key: child.key, label: child.label })),
                                        selectable: true,
                                        selectedKeys: [section],
                                        style: { minWidth: 132 },
                                        onClick: ({ key }) => setSection(key as SectionKey),
                                    }}
                                >
                                    {button}
                                </Dropdown>
                            );
                        })}
                    </ConfigProvider>
                </nav>
            </aside>

            <div className="flex min-w-0 flex-1 flex-col">
                <header className="flex h-12 shrink-0 items-center gap-3 border-b border-slate-200 bg-white px-4">
                    <Dropdown
                        trigger={["click"]}
                        placement="bottomLeft"
                        menu={{
                            style: { minWidth: 240 },
                            items: accountSets.map((set) => ({
                                key: set.id,
                                label: (
                                    <span className="flex items-center justify-between gap-3">
                                        <span className="min-w-0">
                                            <span className="block truncate text-[13px]">{set.name}</span>
                                            {set.companyName && set.companyName !== set.name && (
                                                <span className="block truncate text-[11px] text-slate-400">
                                                    {set.companyName}
                                                </span>
                                            )}
                                        </span>
                                        {set.id === accountSetId && <CheckOutlined className="shrink-0 text-[#2f6bff]" />}
                                    </span>
                                ),
                            })),
                            onClick: ({ key }) => void switchAccount(key),
                        }}
                    >
                        <Button type="text" title={companyName} className="!px-1.5 hover:!bg-slate-50">
                            <span className="flex items-center gap-2">
                                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-[#1e293b] text-[12px] text-white">
                                    <ShopOutlined />
                                </span>
                                <span className="max-w-[240px] truncate text-[13px] font-medium text-slate-800">{companyName}</span>
                                <DownOutlined className="text-[9px] text-slate-400" />
                            </span>
                        </Button>
                    </Dropdown>
                    <Button onClick={() => setCompanyOpen(true)}>详情</Button>

                    <div className="ml-auto flex items-center gap-3">
                        <Button className="!border-[#2f6bff] !text-[#2f6bff]" onClick={toAccounting}>
                            去记账
                        </Button>
                        <span className="flex items-center gap-1.5 text-[13px] text-slate-500">
                            税款所属期：
                            <Tooltip title="默认按税费种认定里主税种的申报期限推算：按月申报取上月，按季申报取上一季度末月。认定可在【税费种管理】更新，也可在这里直接改月份。">
                                <QuestionCircleOutlined className="text-[12px] text-slate-400" />
                            </Tooltip>
                            <DatePicker
                                picker="month"
                                allowClear={false}
                                className="w-[124px]"
                                value={dayjs(period.startDate)}
                                format="YYYY-MM"
                                suffixIcon={<CalendarOutlined />}
                                onChange={(value) => {
                                    if (value) setUserPeriod(makePeriod(value.year(), value.month() + 1));
                                }}
                            />
                        </span>
                    </div>
                </header>

                <div className="flex h-9 shrink-0 items-center gap-1 border-b border-slate-200 bg-[#f7f8fa] pl-1 pr-4">
                    <Button
                        type="text"
                        aria-label="返回记账"
                        title="返回记账"
                        onClick={toAccounting}
                        className="!text-slate-400 hover:!text-slate-600"
                        icon={<LeftOutlined className="text-[12px]" />}
                    />
                    <span className="px-1 text-[12.5px] text-slate-400">税种申报</span>
                    <div className="flex h-[30px] items-center gap-2 border border-b-0 border-slate-200 bg-white px-3 text-[13px] text-slate-700 shadow-[inset_0_2px_0_#2f6bff]">
                        <span>{activeLabel}</span>
                        <Button
                            type="text"
                            aria-label="关闭工作区"
                            title="关闭并返回记账"
                            onClick={toAccounting}
                            className="!text-slate-400 hover:!text-slate-600"
                            icon={<CloseOutlined className="text-[10px]" />}
                        />
                    </div>
                    <Button
                        type="text"
                        onClick={() => void toggleFullscreen()}
                        className="ml-auto !text-slate-500 hover:!text-[#2f6bff]"
                        icon={<ExpandOutlined />}
                    >
                        全屏
                    </Button>
                </div>

                <main className="min-h-0 flex-1 overflow-auto bg-white">
                    {section === "fetch" ? (
                        <InvoiceFetchPage period={period} />
                    ) : section === "declare" ? (
                        <TaxDeclarePage period={period} onOpenTaxKinds={() => setSection("taxKinds")} />
                    ) : section === "pay" ? (
                        <TaxPaymentPayPage period={period} />
                    ) : section === "paymentQuery" ? (
                        <TaxPaymentPage />
                    ) : section === "certificate" ? (
                        <TaxCertificatePage />
                    ) : section === "certificateDownload" ? (
                        <TaxCertificateDownloadPage onOpenCertificate={() => setSection("certificate")} />
                    ) : section === "correction" ? (
                        <TaxCorrectionPage />
                    ) : section === "iitPeople" ? (
                        <TaxIitPage />
                    ) : section === "iitReport" ? (
                        <TaxIitReportPage period={period} />
                    ) : section === "iitPay" ? (
                        <TaxIitDeclarePage period={period} onOpenReport={() => setSection("iitReport")} />
                    ) : section === "iitStats" ? (
                        <TaxIitStatsPage period={period} />
                    ) : section === "reports" ? (
                        <TaxReportQueryPage />
                    ) : section === "taxKinds" ? (
                        <TaxKindsPage />
                    ) : section === "settings" ? (
                        <TaxSettingsPage />
                    ) : section === "tasks" ? (
                        <TaxTaskCenterPage />
                    ) : null}
                </main>
            </div>

            <CompanyInfoModal open={companyOpen} accountSet={accountSet} onClose={() => setCompanyOpen(false)} />
        </div>
    );
}
