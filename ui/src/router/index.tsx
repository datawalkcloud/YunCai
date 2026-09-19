import { createBrowserRouter, Navigate, Outlet, useLocation } from "react-router";
import type { ReactNode } from "react";

import Login from "@/pages/login/Login";
import Landing from "@/pages/landing";
import Home from "@/pages/home";
import CashJournal from "@/pages/funds/cash-journal";
import EntryCategories from "@/pages/funds/entry-categories";
import TrialBalance from "@/pages/ledger/trial-balance";
import VoucherManage from "@/pages/vouchers/manage";
import BalanceSheet from "@/pages/reports/balance-sheet";
import IncomeStatement from "@/pages/reports/income-statement";
import Accounts from "@/pages/settings/accounts";
import AccountSets from "@/pages/settings/account-sets";
import OpeningBalances from "@/pages/settings/opening-balances";
import Layout from "@/components/layout/Layout";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsProvider, useAccountSets } from "@/accountSets/AccountSetsContext";

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

const router = createBrowserRouter([
    // 公开官网首页，右上角提供登录入口，无需登录即可访问。
    {
        path: "/",
        element: <Landing />,
    },
    // 受保护的应用区：登录后可访问，统一挂载账套上下文。
    {
        element: (
            <RequireAuth>
                <AccountSetsProvider>
                    <Outlet />
                </AccountSetsProvider>
            </RequireAuth>
        ),
        children: [
            // 登录后的默认落地：解析默认账套并跳转到 /{accountSetId}。
            { path: "app", element: <DefaultAccountSetRedirect /> },
            {
                // 账套 id 作为路由第一段，其下所有页面均归属该账套。
                path: ":accountSetId",
                element: <Layout />,
                children: [
                    { index: true, element: <Home /> },
                    { path: "funds/cash-journal", element: <CashJournal /> },
                    { path: "funds/entry-categories", element: <EntryCategories /> },
                    { path: "ledger/trial-balance", element: <TrialBalance /> },
                    { path: "vouchers/manage", element: <VoucherManage /> },
                    { path: "reports/balance-sheet", element: <BalanceSheet /> },
                    { path: "reports/income-statement", element: <IncomeStatement /> },
                    { path: "settings/accounts", element: <Accounts /> },
                    { path: "settings/account-sets", element: <AccountSets /> },
                    { path: "settings/opening-balances", element: <OpeningBalances /> },
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
