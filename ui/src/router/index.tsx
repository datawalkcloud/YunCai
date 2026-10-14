import { createBrowserRouter, Navigate, Outlet, useLocation } from "react-router";
import { createElement, lazy, Suspense } from "react";
import type { ComponentType, ReactNode } from "react";

import Login from "@/pages/login/Login";
import Landing from "@/pages/landing";
import DashboardLayout from "@/components/layout/dashboard/DashboardLayout";
import { ErrorBoundary, RouteErrorElement } from "@/components/error/ErrorBoundary";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsProvider, useAccountSets } from "@/accountSets/AccountSetsContext";

// 首屏关键路径保持静态引入：官网首页、登录页、账套外壳布局。
// 账套下的业务页面按功能域拆成动态 chunk（见 ./groups/*），同域页面落进同一个 chunk，
// 避免 70 多个路由各生成一个碎片文件。

/** 校验令牌期间的占位界面，避免闪烁跳转。 */
function AuthLoading() {
    return (
        <div className="flex min-h-svh items-center justify-center text-muted-foreground">
            正在加载…
        </div>
    );
}

/** 需要登录才能访问；未登录时跳转到登录页并记录来源。 */
function RequireAuth({ children }: { children: ReactNode }) {
    const { status } = useAuth();
    const location = useLocation();

    if (status === "loading") return <AuthLoading />;
    if (status !== "authenticated") {
        return <Navigate to="/login" replace state={{ from: location.pathname }} />;
    }
    return <>{children}</>;
}

/** 已登录用户访问登录页时直接回到首页。 */
function RedirectIfAuthed({ children }: { children: ReactNode }) {
    const { status } = useAuth();

    if (status === "loading") return <AuthLoading />;
    if (status === "authenticated") return <Navigate to="/app" replace />;
    return <>{children}</>;
}

/**
 * 根路径落地：解析默认账套后跳转到「/{accountSetId}」。
 * 无任何账套时提示先创建账套。
 */
function DefaultAccountSetRedirect() {
    const { loading, defaultId, error } = useAccountSets();

    if (loading) return <AuthLoading />;
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
    return <Navigate to={`/${defaultId}`} replace />;
}

/**
 * 懒加载叶子路由：element 为「功能域 chunk + Suspense 占位」（占位沿用 AuthLoading 观感，
 * 侧边栏不跟着闪烁），errorElement 兜住该页的渲染异常与 chunk 拉取失败，
 * 出错时只替换内容区、绝不白屏。
 */
function lazyRoute<M extends { [K in keyof M]: ComponentType }, K extends keyof M>(
    loader: () => Promise<M>,
    key: K,
): { element: ReactNode; errorElement: ReactNode } {
    // 变量名保持小写并配 createElement：写成 PascalCase 组件形式会多出一条 fast-refresh 警告。
    const view = lazy<ComponentType>(() => loader().then((m) => ({ default: m[key] })));
    return {
        element: <Suspense fallback={<AuthLoading />}>{createElement(view)}</Suspense>,
        errorElement: <RouteErrorElement />,
    };
}

const router = createBrowserRouter([
    // 公开官网首页，右上角提供登录入口，无需登录即可访问。
    {
        path: "/",
        element: <Landing />,
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
                    { path: "tax/login", ...lazyRoute(() => import("./groups/invoice"), "LemonTaxLogin") },
                    { path: "settings/currency", ...lazyRoute(() => import("./groups/settings"), "LemonCurrency") },
                    { path: "settings/voucher-word", ...lazyRoute(() => import("./groups/settings"), "LemonVoucherWord") },
                    { path: "settings/voucher-template", ...lazyRoute(() => import("./groups/settings"), "LemonVoucherTemplate") },
                    { path: "settings/permission", ...lazyRoute(() => import("./groups/settings"), "LemonPermission") },
                    { path: "settings/boss", ...lazyRoute(() => import("./groups/settings"), "LemonBoss") },
                    { path: "settings/backup", ...lazyRoute(() => import("./groups/settings"), "LemonBackup") },
                    { path: "settings/log", ...lazyRoute(() => import("./groups/settings"), "LemonLog") },
                    { path: "settings/eis-relation", ...lazyRoute(() => import("./groups/settings"), "LemonEisRelation") },
                    { path: "settings/scm-relation", ...lazyRoute(() => import("./groups/settings"), "LemonScmRelation") },
                    { path: "settings/import-old", ...lazyRoute(() => import("./groups/settings"), "LemonImportOld") },
                    { path: "settings/reinit", ...lazyRoute(() => import("./groups/settings"), "LemonReinit") },
                    { path: "pro", ...lazyRoute(() => import("./groups/dashboard"), "LemonPro") },
                ],
            },
        ],
    },
    {
        path: "/login",
        element: (
            <RedirectIfAuthed>
                <Login />
            </RedirectIfAuthed>
        ),
    },
]);

export default router;
