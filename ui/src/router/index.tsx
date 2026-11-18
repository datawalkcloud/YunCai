import { createBrowserRouter, Outlet, useLocation, useNavigate } from "react-router";
import { useEffect, useRef } from "react";
import type { ComponentType, ReactNode } from "react";

import NoAccountSetGuide from "@/pages/onboarding";
import { PrivacyPage, TermsPage } from "@/pages/legal";
import LoginPage from "@/pages/login";
import DashboardLayout from "@/components/layout/dashboard/DashboardLayout";
import { ContentErrorElement, ErrorBoundary, RouteErrorElement } from "@/components/error/ErrorBoundary";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsProvider, useAccountSets } from "@/accountSets/AccountSetsContext";
import { setPath } from "@/accountSets/url-id";

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
 * 官网首页整页跳转兜底：生产由 nginx、开发由 Vite 中间件把 GET / 直接指向静态稿
 * public/home.html。但 react-router 的客户端导航不会经过服务器，落到这个
 * 组件后需要整页跳转才能真正拿到静态稿。
 *
 * 目标必须是明确的静态文件路径 /home.html，而不是 /：跳到 / 时若 React 仍
 * 被挂载（如中间件未命中、命中旧 SPA 缓存），会再次匹配本组件 -> replace("/") ->
 * 又回 SPA，形成「正在打开官网…」无限闪。/home.html 在 dev 由 Vite 直接
 * serve public 文件、在生产由 nginx serve，永远是静态稿，绝不会进 SPA，故不会循环。
 */
function HomeReload() {
    useEffect(() => {
        window.location.replace("/home.html");
    }, []);
    return <div className="flex min-h-svh items-center justify-center text-muted-foreground">正在打开官网…</div>;
}

/**
 * 根路径落地：解析默认账套后跳转到「/{accountSetId}」。
 * 没有任何账套时（如刚注册的账号）直接进入建账引导。
 */
function DefaultAccountSetRedirect() {
    const { loading, defaultId, error } = useAccountSets();
    useRedirect(!loading && !error && defaultId ? setPath(defaultId) : null);

    if (loading) return <AppLoading />;
    if (error) {
        return (
            <div className="flex min-h-svh items-center justify-center text-destructive">
                {error}
            </div>
        );
    }
    if (!defaultId) return <NoAccountSetGuide />;
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
    // 官网首页：GET / 由 nginx / Vite 中间件直接返回静态稿 home.html。
    // 这里的组件只在客户端导航（不经服务器）落到 / 时兜底，整页重载回静态稿。
    {
        path: "/",
        element: <HomeReload />,
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
                    // —— 菜单层级补齐的页面（复刻实现，mock 数据） ——
                    { path: "vouchers/new", ...lazyRoute(() => import("./groups/vouchers"), "VoucherNew") },
                    { path: "vouchers/erecord", ...lazyRoute(() => import("./groups/archives"), "ERecord") },
                    { path: "vouchers/spb", ...lazyRoute(() => import("./groups/invoice"), "Spb") },
                    { path: "funds/deposit-journal", ...lazyRoute(() => import("./groups/funds"), "DepositJournal") },
                    { path: "funds/draft", ...lazyRoute(() => import("./groups/funds"), "Draft") },
                    { path: "funds/bank-link", ...lazyRoute(() => import("./groups/invoice"), "BankLink") },
                    { path: "funds/account", ...lazyRoute(() => import("./groups/funds"), "AccountSettings") },
                    { path: "invoices/fetch", ...lazyRoute(() => import("./groups/invoice"), "InvoiceFetch") },
                    { path: "invoices/sales", ...lazyRoute(() => import("./groups/invoice"), "SalesInvoice") },
                    { path: "invoices/purchase", ...lazyRoute(() => import("./groups/invoice"), "PurchaseInvoice") },
                    { path: "invoices/expense", ...lazyRoute(() => import("./groups/invoice"), "ExpenseBill") },
                    { path: "invoices/summary", ...lazyRoute(() => import("./groups/invoice"), "InvoiceSummary") },
                    { path: "invoices/vat-calc", ...lazyRoute(() => import("./groups/invoice"), "VatCalc") },
                    { path: "invoices/risk", ...lazyRoute(() => import("./groups/invoice"), "InvoiceRisk") },
                    { path: "salary/manage", ...lazyRoute(() => import("./groups/salary"), "SalaryManage") },
                    { path: "salary/employee", ...lazyRoute(() => import("./groups/salary"), "SalaryEmployee") },
                    { path: "salary/summary", ...lazyRoute(() => import("./groups/salary"), "SalarySummary") },
                    { path: "salary/insurance", ...lazyRoute(() => import("./groups/salary"), "SalaryInsurance") },
                    { path: "assets/manage", ...lazyRoute(() => import("./groups/assets"), "AssetManage") },
                    { path: "assets/depreciation", ...lazyRoute(() => import("./groups/assets"), "AssetDepreciation") },
                    { path: "assets/summary", ...lazyRoute(() => import("./groups/assets"), "AssetSummary") },
                    { path: "assets/category", ...lazyRoute(() => import("./groups/assets"), "AssetCategory") },
                    { path: "assets/check", ...lazyRoute(() => import("./groups/assets"), "AssetCheck") },
                    { path: "reports/income-quarter", ...lazyRoute(() => import("./groups/reports"), "IncomeQuarter") },
                    { path: "reports/cash-flow", ...lazyRoute(() => import("./groups/reports"), "CashFlow") },
                    { path: "reports/cash-flow-quarter", ...lazyRoute(() => import("./groups/reports"), "CashFlowQuarter") },
                    { path: "reports/standard-cash-flow", ...lazyRoute(() => import("./groups/reports"), "StandardCashFlow") },
                    { path: "reports/department-income", ...lazyRoute(() => import("./groups/reports"), "DepartmentIncome") },
                    { path: "reports/project-income", ...lazyRoute(() => import("./groups/reports"), "ProjectIncome") },
                    { path: "reports/pivot", ...lazyRoute(() => import("./groups/reports"), "Pivot") },
                    { path: "reports/expense", ...lazyRoute(() => import("./groups/reports"), "ExpenseDetail") },
                    { path: "reports/equity-change", ...lazyRoute(() => import("./groups/reports"), "EquityChange") },
                    { path: "reports/tax", ...lazyRoute(() => import("./groups/reports"), "TaxReport") },
                    { path: "reports/financial-summary", ...lazyRoute(() => import("./groups/reports"), "FinancialSummary") },
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
                    { path: "tax/login", ...lazyRoute(() => import("./groups/invoice"), "TaxLogin") },
                    { path: "settings/currency", ...lazyRoute(() => import("./groups/settings"), "Currency") },
                    { path: "settings/voucher-word", ...lazyRoute(() => import("./groups/settings"), "VoucherWord") },
                    { path: "settings/voucher-template", ...lazyRoute(() => import("./groups/settings"), "VoucherTemplate") },
                    { path: "settings/permission", ...lazyRoute(() => import("./groups/settings"), "Permission") },
                    { path: "settings/boss", ...lazyRoute(() => import("./groups/settings"), "Boss") },
                    { path: "settings/backup", ...lazyRoute(() => import("./groups/settings"), "Backup") },
                    { path: "settings/log", ...lazyRoute(() => import("./groups/settings"), "OperationLog") },
                    { path: "settings/users", ...lazyRoute(() => import("./groups/settings"), "Users") },
                    { path: "settings/eis-relation", ...lazyRoute(() => import("./groups/settings"), "EisRelation") },
                    { path: "settings/scm-relation", ...lazyRoute(() => import("./groups/settings"), "ScmRelation") },
                    { path: "settings/import-old", ...lazyRoute(() => import("./groups/settings"), "ImportOld") },
                    { path: "settings/reinit", ...lazyRoute(() => import("./groups/settings"), "Reinit") },
                ],
            },
        ],
    },
    {
        // 独立登录页：表单在登录页内完成，登录成功后由 RedirectIfAuthed 依据
        // state.from 跳回来源页（默认 /app）。官网静态稿的「登录 / 免费使用」入口指向此路由。
        path: "/login",
        element: (
            <RedirectIfAuthed>
                <LoginPage />
            </RedirectIfAuthed>
        ),
        errorElement: <RouteErrorElement />,
        hydrateFallbackElement: <AppLoading />,
    },
]);

export default router;
