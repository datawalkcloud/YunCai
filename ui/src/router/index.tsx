import { createBrowserRouter, Outlet, useLocation, useNavigate } from "react-router";
import { useEffect, useRef } from "react";
import type { ComponentType, ReactNode } from "react";

import Landing from "@/pages/landing";
import { PrivacyPage, TermsPage } from "@/pages/legal";
import DashboardLayout from "@/components/layout/dashboard/DashboardLayout";
import { ContentErrorElement, ErrorBoundary, RouteErrorElement } from "@/components/error/ErrorBoundary";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsProvider, useAccountSets } from "@/accountSets/AccountSetsContext";

// 首屏关键路径保持静态引入：官网首页、登录页、账套外壳布局。
// 账套下的业务页面按功能域拆成动态 chunk（见 ./groups/*），同域页面落进同一个 chunk，
// 避免 70 多个路由各生成一个碎片文件。

/** 全站加载占位：令牌校验、账套解析、路由初始化期间都显示它，绝不渲染纯白。 */
function AppLoading() {
    return (
        <div className="flex min-h-svh items-center justify-center text-muted-foreground">
            正在加载…
        </div>
    );
}

/**
 * 命令式跳转：to 非空时在 effect 里 navigate，同一个目标只跳一次（StrictMode 下也不重复）。
 * 声明式 <Navigate> 自身渲染 null，目标路由若是懒加载，chunk 就绪前整页会白掉；
 * 用 hook 后由调用方继续渲染自己的占位/页面，跳转待提交期间界面始终有内容。
 */
function useRedirect(to: string | null, state?: unknown) {
    const navigate = useNavigate();
    const stateRef = useRef(state);
    stateRef.current = state;
    const lastTo = useRef<string | null>(null);

    useEffect(() => {
        if (!to || lastTo.current === to) return;
        lastTo.current = to;
        navigate(to, { replace: true, state: stateRef.current });
    }, [navigate, to]);
}

/** 需要登录才能访问；未登录时跳转到登录页并记录来源，期间显示加载占位。 */
function RequireAuth({ children }: { children: ReactNode }) {
    const { status, needsPasswordSetup } = useAuth();
    const location = useLocation();
    // 验证码首次登录自动建的号还没设密码，先回登录页补设，不放进后台。
    const shouldRedirect = (status !== "loading" && status !== "authenticated") || needsPasswordSetup;
    useRedirect(shouldRedirect ? "/login" : null, shouldRedirect ? { from: location.pathname } : undefined);

    if (status === "loading" || shouldRedirect) return <AppLoading />;
    return <>{children}</>;
}

/** 已登录用户访问登录页时回到来源页（默认 /app），跳转期间官网保持可见。 */
function RedirectIfAuthed({ children }: { children: ReactNode }) {
    const { status, needsPasswordSetup } = useAuth();
    const location = useLocation();
    const from = (location.state as { from?: string } | null)?.from ?? "/app";
    useRedirect(status === "authenticated" && !needsPasswordSetup ? from : null);

    if (status === "loading") return <AppLoading />;
    return <>{children}</>;
}

/**
 * 根路径落地：解析默认账套后跳转到「/{accountSetId}」。
 * 无任何账套时提示先创建账套。
 */
function DefaultAccountSetRedirect() {
    const { loading, defaultId, error } = useAccountSets();
    useRedirect(!loading && !error && defaultId ? `/${defaultId}` : null);

    if (loading) return <AppLoading />;
    if (error) {
        return (
            <div className="flex min-h-svh items-center justify-center text-destructive">
                {error}
            </div>
        );
    }
    if (!defaultId) {
        return (
            <div className="flex min-h-svh flex-col items-center justify-center gap-2 text-muted-foreground">
                <span>尚未创建任何账套。</span>
                <span className="text-sm">请联系管理员在后台创建账套后再使用。</span>
            </div>
        );
    }
    // 跳转待提交期间保留加载占位，不要渲染 null 造成空白帧。
    return <AppLoading />;
}

/**
 * 懒加载叶子路由：交给 react-router 的 route.lazy——导航会等模块加载完再提交，
 * 加载期间旧页面原样留在屏幕上，不会先闪一块「正在加载…」再出内容。
 * errorElement 兜住该页的渲染异常与 chunk 拉取失败，出错时只替换内容区、绝不白屏。
 */
function lazyRoute<M extends { [K in keyof M]: ComponentType }, K extends keyof M>(
    loader: () => Promise<M>,
    key: K,
): { lazy: () => Promise<{ Component: ComponentType }>; errorElement: ReactNode } {
    return {
        lazy: async () => ({ Component: (await loader())[key] }),
        errorElement: <ContentErrorElement />,
    };
}

const router = createBrowserRouter([
    // 公开官网首页，右上角提供登录入口，无需登录即可访问。
    {
        path: "/",
        element: <Landing />,
        errorElement: <RouteErrorElement />,
        hydrateFallbackElement: <AppLoading />,
    },
    // 登录弹窗底部的协议链接指向这里，避免出现死链。
    {
        path: "/terms",
        element: <TermsPage />,
        errorElement: <RouteErrorElement />,
        hydrateFallbackElement: <AppLoading />,
    },
    {
        path: "/privacy",
        element: <PrivacyPage />,
        errorElement: <RouteErrorElement />,
        hydrateFallbackElement: <AppLoading />,
    },
    // 停车平台风格主题演示（云中停·智慧停车 1:1 复刻 layout），公开可访问。
    {
        path: "/parking-demo",
        lazy: async () => ({ Component: (await import("@/pages/parking-demo")).default }),
        errorElement: <RouteErrorElement />,
        hydrateFallbackElement: <AppLoading />,
    },
    // 受保护的应用区：登录后可访问，统一挂载账套上下文。
    // 外层 ErrorBoundary 是这一块的最后一道防线（路由级兜底之上的渲染异常）。
    {
        element: (
            <ErrorBoundary>
                <RequireAuth>
                    <AccountSetsProvider>
                        <Outlet />
                    </AccountSetsProvider>
                </RequireAuth>
            </ErrorBoundary>
        ),
        errorElement: <RouteErrorElement />,
        hydrateFallbackElement: <AppLoading />,
        children: [
            // 登录后的默认落地：解析默认账套并跳转到 /{accountSetId}。
            { path: "app", element: <DefaultAccountSetRedirect />, errorElement: <RouteErrorElement /> },
            {
                // 账套 id 作为路由第一段，其下所有页面均归属该账套。
                path: ":accountSetId",
                element: <DashboardLayout />,
                errorElement: <RouteErrorElement />,
                children: [
                    { index: true, ...lazyRoute(() => import("./groups/dashboard"), "DashboardHome") },
                    { path: "dev", ...lazyRoute(() => import("./groups/dashboard"), "ComingSoon") },
                    { path: "funds/cash-journal", ...lazyRoute(() => import("./groups/funds"), "CashJournal") },
                    { path: "funds/entry-categories", ...lazyRoute(() => import("./groups/funds"), "EntryCategories") },
                    { path: "funds/transfer", ...lazyRoute(() => import("./groups/funds"), "Transfer") },
                    { path: "funds/report", ...lazyRoute(() => import("./groups/funds"), "FundReport") },
                    { path: "funds/reconciliation", ...lazyRoute(() => import("./groups/funds"), "FundReconciliation") },
                    { path: "ledger/trial-balance", ...lazyRoute(() => import("./groups/ledger"), "TrialBalance") },
                    { path: "ledger/subsidiary", ...lazyRoute(() => import("./groups/ledger"), "SubsidiaryLedger") },
                    { path: "ledger/general", ...lazyRoute(() => import("./groups/ledger"), "GeneralLedger") },
                    { path: "ledger/journal", ...lazyRoute(() => import("./groups/ledger"), "VoucherJournal") },
                    { path: "ledger/voucher-summary", ...lazyRoute(() => import("./groups/ledger"), "VoucherSummary") },
                    { path: "ledger/multi-column", ...lazyRoute(() => import("./groups/ledger"), "MultiColumnLedger") },
                    { path: "ledger/assist-balance", ...lazyRoute(() => import("./groups/ledger"), "AssistBalance") },
                    { path: "ledger/assist-subsidiary", ...lazyRoute(() => import("./groups/ledger"), "AssistSubsidiary") },
                    { path: "ledger/assist-combine", ...lazyRoute(() => import("./groups/ledger"), "AssistCombine") },
                    { path: "ledger/ageing", ...lazyRoute(() => import("./groups/ledger"), "AgeingAnalysis") },
                    { path: "vouchers/manage", ...lazyRoute(() => import("./groups/vouchers"), "VoucherManage") },
                    { path: "budget/manage", ...lazyRoute(() => import("./groups/budget"), "BudgetManage") },
                    { path: "reports/balance-sheet", ...lazyRoute(() => import("./groups/reports"), "BalanceSheet") },
                    { path: "reports/income-statement", ...lazyRoute(() => import("./groups/reports"), "IncomeStatement") },
                    { path: "settings/accounts", ...lazyRoute(() => import("./groups/settings"), "Accounts") },
                    { path: "settings/account-sets", ...lazyRoute(() => import("./groups/settings"), "AccountSets") },
                    { path: "settings/opening-balances", ...lazyRoute(() => import("./groups/settings"), "OpeningBalances") },
                    { path: "settings/assist-items", ...lazyRoute(() => import("./groups/settings"), "AssistItems") },
                    { path: "closing", ...lazyRoute(() => import("./groups/closing"), "Closing") },
                    // —— 按柠檬云菜单层级补齐的页面（复刻实现，mock 数据） ——
                    { path: "vouchers/new", ...lazyRoute(() => import("./groups/vouchers"), "VoucherNew") },
                    { path: "vouchers/erecord", ...lazyRoute(() => import("./groups/archives"), "LemonERecord") },
                    { path: "vouchers/spb", ...lazyRoute(() => import("./groups/invoice"), "LemonSpb") },
                    { path: "funds/deposit-journal", ...lazyRoute(() => import("./groups/funds"), "LemonDepositJournal") },
                    { path: "funds/draft", ...lazyRoute(() => import("./groups/funds"), "LemonDraft") },
                    { path: "funds/bank-link", ...lazyRoute(() => import("./groups/invoice"), "LemonBankLink") },
                    { path: "funds/account", ...lazyRoute(() => import("./groups/funds"), "LemonAccountSettings") },
                    { path: "invoices/fetch", ...lazyRoute(() => import("./groups/invoice"), "LemonInvoiceFetch") },
                    { path: "invoices/sales", ...lazyRoute(() => import("./groups/invoice"), "LemonSalesInvoice") },
                    { path: "invoices/purchase", ...lazyRoute(() => import("./groups/invoice"), "LemonPurchaseInvoice") },
                    { path: "invoices/expense", ...lazyRoute(() => import("./groups/invoice"), "LemonExpenseBill") },
                    { path: "invoices/summary", ...lazyRoute(() => import("./groups/invoice"), "LemonInvoiceSummary") },
                    { path: "invoices/vat-calc", ...lazyRoute(() => import("./groups/invoice"), "LemonVatCalc") },
                    { path: "invoices/risk", ...lazyRoute(() => import("./groups/invoice"), "LemonInvoiceRisk") },
                    { path: "salary/manage", ...lazyRoute(() => import("./groups/salary"), "LemonSalaryManage") },
                    { path: "salary/employee", ...lazyRoute(() => import("./groups/salary"), "LemonSalaryEmployee") },
                    { path: "salary/summary", ...lazyRoute(() => import("./groups/salary"), "LemonSalarySummary") },
                    { path: "salary/insurance", ...lazyRoute(() => import("./groups/salary"), "LemonSalaryInsurance") },
                    { path: "assets/manage", ...lazyRoute(() => import("./groups/assets"), "LemonAssetManage") },
                    { path: "assets/depreciation", ...lazyRoute(() => import("./groups/assets"), "LemonAssetDepreciation") },
                    { path: "assets/summary", ...lazyRoute(() => import("./groups/assets"), "LemonAssetSummary") },
                    { path: "assets/category", ...lazyRoute(() => import("./groups/assets"), "LemonAssetCategory") },
                    { path: "assets/check", ...lazyRoute(() => import("./groups/assets"), "LemonAssetCheck") },
                    { path: "reports/income-quarter", ...lazyRoute(() => import("./groups/reports"), "LemonIncomeQuarter") },
                    { path: "reports/cash-flow", ...lazyRoute(() => import("./groups/reports"), "LemonCashFlow") },
                    { path: "reports/cash-flow-quarter", ...lazyRoute(() => import("./groups/reports"), "LemonCashFlowQuarter") },
                    { path: "reports/standard-cash-flow", ...lazyRoute(() => import("./groups/reports"), "LemonStandardCashFlow") },
                    { path: "reports/department-income", ...lazyRoute(() => import("./groups/reports"), "LemonDepartmentIncome") },
                    { path: "reports/project-income", ...lazyRoute(() => import("./groups/reports"), "LemonProjectIncome") },
                    { path: "reports/pivot", ...lazyRoute(() => import("./groups/reports"), "LemonPivot") },
                    { path: "reports/expense", ...lazyRoute(() => import("./groups/reports"), "LemonExpenseDetail") },
                    { path: "reports/equity-change", ...lazyRoute(() => import("./groups/reports"), "LemonEquityChange") },
                    { path: "reports/tax", ...lazyRoute(() => import("./groups/reports"), "LemonTaxReport") },
                    { path: "reports/financial-summary", ...lazyRoute(() => import("./groups/reports"), "LemonFinancialSummary") },
                    // 报税分类：原智能报税工作台的各板块拆成独立页面，与记账页共用外壳。
                    { path: "tax/fetch", ...lazyRoute(() => import("./groups/tax"), "InvoiceFetchPage") },
                    { path: "tax/declare", ...lazyRoute(() => import("./groups/tax"), "TaxDeclarePage") },
                    { path: "tax/correction", ...lazyRoute(() => import("./groups/tax"), "TaxCorrectionPage") },
                    { path: "tax/pay", ...lazyRoute(() => import("./groups/tax"), "TaxPaymentPayPage") },
                    { path: "tax/payment-query", ...lazyRoute(() => import("./groups/tax"), "TaxPaymentPage") },
                    { path: "tax/certificate", ...lazyRoute(() => import("./groups/tax"), "TaxCertificatePage") },
                    { path: "tax/certificate-download", ...lazyRoute(() => import("./groups/tax"), "TaxCertificateDownloadPage") },
                    { path: "tax/reports", ...lazyRoute(() => import("./groups/tax"), "TaxReportQueryPage") },
                    { path: "tax/iit/people", ...lazyRoute(() => import("./groups/tax"), "TaxIitPage") },
                    { path: "tax/iit/report", ...lazyRoute(() => import("./groups/tax"), "TaxIitReportPage") },
                    { path: "tax/iit/pay", ...lazyRoute(() => import("./groups/tax"), "TaxIitDeclarePage") },
                    { path: "tax/iit/stats", ...lazyRoute(() => import("./groups/tax"), "TaxIitStatsPage") },
                    { path: "tax/kinds", ...lazyRoute(() => import("./groups/tax"), "TaxKindsPage") },
                    { path: "tax/settings", ...lazyRoute(() => import("./groups/tax"), "TaxSettingsPage") },
                    { path: "tax/tasks", ...lazyRoute(() => import("./groups/tax"), "TaxTaskCenterPage") },
                    { path: "tax/login", ...lazyRoute(() => import("./groups/invoice"), "LemonTaxLogin") },
                    { path: "settings/currency", ...lazyRoute(() => import("./groups/settings"), "LemonCurrency") },
                    { path: "settings/voucher-word", ...lazyRoute(() => import("./groups/settings"), "VoucherWord") },
                    { path: "settings/voucher-template", ...lazyRoute(() => import("./groups/settings"), "LemonVoucherTemplate") },
                    { path: "settings/permission", ...lazyRoute(() => import("./groups/settings"), "LemonPermission") },
                    { path: "settings/boss", ...lazyRoute(() => import("./groups/settings"), "LemonBoss") },
                    { path: "settings/backup", ...lazyRoute(() => import("./groups/settings"), "LemonBackup") },
                    { path: "settings/log", ...lazyRoute(() => import("./groups/settings"), "OperationLog") },
                    { path: "settings/users", ...lazyRoute(() => import("./groups/settings"), "Users") },
                    { path: "settings/eis-relation", ...lazyRoute(() => import("./groups/settings"), "LemonEisRelation") },
                    { path: "settings/scm-relation", ...lazyRoute(() => import("./groups/settings"), "LemonScmRelation") },
                    { path: "settings/import-old", ...lazyRoute(() => import("./groups/settings"), "LemonImportOld") },
                    { path: "settings/reinit", ...lazyRoute(() => import("./groups/settings"), "LemonReinit") },
                ],
            },
        ],
    },
    {
        // 登录不再是独立页面：/login 直接落在官网上并自动打开登录弹窗，
        // 保护区回跳时带上的 state.from 由 RedirectIfAuthed / Landing 在登录成功后消费。
        path: "/login",
        element: (
            <RedirectIfAuthed>
                <Landing autoOpenLogin />
            </RedirectIfAuthed>
        ),
        errorElement: <RouteErrorElement />,
        hydrateFallbackElement: <AppLoading />,
    },
]);

export default router;
