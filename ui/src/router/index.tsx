import { createBrowserRouter, Navigate, useLocation } from "react-router";
import type { ReactNode } from "react";

import Login from "@/pages/login/Login";
import Home from "@/pages/home";
import CashJournal from "@/pages/funds/cash-journal";
import TrialBalance from "@/pages/ledger/trial-balance";
import NewVoucher from "@/pages/vouchers/new";
import BalanceSheet from "@/pages/reports/balance-sheet";
import Accounts from "@/pages/settings/accounts";
import AccountSets from "@/pages/settings/account-sets";
import Layout from "@/components/layout/Layout";
import { useAuth } from "@/auth/AuthContext";

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
    if (status === "authenticated") return <Navigate to="/" replace />;
    return <>{children}</>;
}

const router = createBrowserRouter([
    {
        path: "/",
        element: (
            <RequireAuth>
                <Layout />
            </RequireAuth>
        ),
        children: [
            { index: true, element: <Home /> },
            { path: "funds/cash-journal", element: <CashJournal /> },
            { path: "ledger/trial-balance", element: <TrialBalance /> },
            { path: "vouchers/new", element: <NewVoucher /> },
            { path: "reports/balance-sheet", element: <BalanceSheet /> },
            { path: "settings/accounts", element: <Accounts /> },
            { path: "settings/account-sets", element: <AccountSets /> },
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
