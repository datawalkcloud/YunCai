/**
 * 智能报税工作台（全屏独立外壳，对应菜单「一键报税」）。
 *
 * 与记账端并列：左侧深色导航 + 顶部账套/税款所属期 + 工作区页签，
 * 顶栏「去记账」回 /{accountSetId}。发票获取页已接本系统发票池，税费缴纳、
 * 税费种管理、个税申报、报表查询用本地模拟数据复刻税局页面；其余板块依赖
 * 税务助手登录电子税务局取数，统一给「建设中 + 前往登录」出口。
 */
import { useState, type ReactNode } from "react";
import { useNavigate, useParams } from "react-router";
import { App, Button, DatePicker, Dropdown } from "antd";
import {
    BarChartOutlined,
    CalendarOutlined,
    CheckOutlined,
    CheckSquareOutlined,
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
    ShopOutlined,
    SyncOutlined,
    UserOutlined,
} from "@ant-design/icons";
import dayjs from "dayjs";

import { AccountSetsApi } from "@/apis-gen";
import { useAccountSets } from "@/accountSets/AccountSetsContext";
import { makePeriod, useDefaultAccountingPeriod, type AccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { useAuth } from "@/auth/AuthContext";
import { apiConfig } from "@/lib/api-client";
import { cn } from "@/lib/utils";

import { CompanyInfoModal } from "./CompanyInfoModal";
import { InvoiceFetchPage } from "./InvoiceFetchPage";
import { TaxCorrectionPage } from "./TaxCorrectionPage";
import { TaxIitPage } from "./TaxIitPage";
import { TaxKindsPage } from "./TaxKindsPage";
import { TaxPaymentWorkspace } from "./TaxPaymentWorkspace";
import { TaxSettingsPage } from "./TaxSettingsPage";
import { TaxDeclarePage } from "./TaxDeclarePage";
import { TaxReportQueryPage } from "./TaxReportQueryPage";

type SectionKey =
    | "fetch"
    | "selection"
    | "declare"
    | "correction"
    | "payment"
    | "reports"
    | "iit"
    | "taxKinds"
    | "settings"
    | "tasks";

type NavKey = SectionKey | "etax";

const NAV_ITEMS: Array<{ key: NavKey; label: string; icon: ReactNode }> = [
    { key: "fetch", label: "发票获取", icon: <FileTextOutlined /> },
    { key: "selection", label: "发票勾选", icon: <CheckSquareOutlined /> },
    { key: "declare", label: "税种申报", icon: <FolderOpenOutlined /> },
    { key: "correction", label: "作废与更正", icon: <FormOutlined /> },
    { key: "payment", label: "税费缴纳", icon: <MoneyCollectOutlined /> },
    { key: "reports", label: "报表查询", icon: <BarChartOutlined /> },
    { key: "iit", label: "个税申报", icon: <UserOutlined /> },
    { key: "taxKinds", label: "税费种管理", icon: <ControlOutlined /> },
    { key: "settings", label: "报税设置", icon: <DesktopOutlined /> },
    { key: "tasks", label: "任务中心", icon: <SyncOutlined /> },
    { key: "etax", label: "电子税务局", icon: <GlobalOutlined /> },
];

export function TaxWorkspace() {
    const { accountSetId = "" } = useParams();
    const navigate = useNavigate();
    const { token } = useAuth();
    const { accountSets, refresh } = useAccountSets();
    const { message } = App.useApp();
    const accountSet = accountSets.find((set) => set.id === accountSetId);

    const defaultPeriod = useDefaultAccountingPeriod();
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
        setSection(key);
    }

    const activeLabel =
        section === "fetch"
            ? "发票获取"
            : section === "iit"
                ? "人员管理"
                : NAV_ITEMS.find((item) => item.key === section)?.label ?? "发票获取";

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
                    {NAV_ITEMS.map((item) => {
                        const active = item.key === section;
                        return (
                            <button
                                key={item.key}
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
                            </button>
                        );
                    })}
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
                    ) : section === "payment" ? (
                        <TaxPaymentWorkspace period={period} />
                    ) : section === "correction" ? (
                        <TaxCorrectionPage />
                    ) : section === "iit" ? (
                        <TaxIitPage />
                    ) : section === "reports" ? (
                        <TaxReportQueryPage />
                    ) : section === "taxKinds" ? (
                        <TaxKindsPage />
                    ) : section === "settings" ? (
                        <TaxSettingsPage />
                    ) : (
                        <SectionPlaceholder title={activeLabel} onLogin={toTaxLogin} />
                    )}
                </main>
            </div>

            <CompanyInfoModal open={companyOpen} accountSet={accountSet} onClose={() => setCompanyOpen(false)} />
        </div>
    );
}

/** 尚未接入税局取数的板块：说明原因，并给出唯一的真实出口（一键登录税局）。 */
function SectionPlaceholder({ title, onLogin }: { title: string; onLogin: () => void }) {
    return (
        <div className="flex h-full flex-col items-center justify-center gap-3 px-6 text-center">
            <span className="flex h-14 w-14 items-center justify-center rounded-full bg-[#eef4ff] text-[24px] text-[#2f6bff]">
                <FileProtectOutlined />
            </span>
            <div className="text-[15px] font-medium text-slate-700">{title}</div>
            <div className="max-w-[460px] text-[13px] leading-6 text-slate-500">
                该板块的税局取数正在接入中。税局登录由服务器完成，不需要安装助手或打开税局页面。
            </div>
            <Button type="primary" onClick={onLogin}>
                前往一键登录税局
            </Button>
        </div>
    );
}
