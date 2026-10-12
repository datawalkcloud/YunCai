import { createBrowserRouter, Navigate, Outlet, useLocation } from "react-router";
import type { ReactNode } from "react";

import Login from "@/pages/login/Login";
import Landing from "@/pages/landing";
import DashboardHome from "@/pages/dashboard";
import CashJournal from "@/pages/funds/cash-journal";
import EntryCategories from "@/pages/funds/entry-categories";
import Transfer from "@/pages/funds/transfer";
import FundReport from "@/pages/funds/report";
import FundReconciliation from "@/pages/funds/reconciliation";
import TrialBalance from "@/pages/ledger/trial-balance";
import SubsidiaryLedger from "@/pages/ledger/subsidiary";
import GeneralLedger from "@/pages/ledger/general";
import VoucherJournal from "@/pages/ledger/journal";
import VoucherSummary from "@/pages/ledger/voucher-summary";
import MultiColumnLedger from "@/pages/ledger/multi-column";
import AssistBalance from "@/pages/ledger/assist-balance";
import AssistSubsidiary from "@/pages/ledger/assist-subsidiary";
import AssistCombine from "@/pages/ledger/assist-combine";
import AgeingAnalysis from "@/pages/ledger/ageing";
import VoucherManage from "@/pages/vouchers/manage";
import BalanceSheet from "@/pages/reports/balance-sheet";
import IncomeStatement from "@/pages/reports/income-statement";
import Accounts from "@/pages/settings/accounts";
import AccountSets from "@/pages/settings/account-sets";
import OpeningBalances from "@/pages/settings/opening-balances";
import AssistItems from "@/pages/settings/assist-items";
import Closing from "@/pages/closing";
import ComingSoon from "@/pages/dev/ComingSoon";
import DashboardLayout from "@/components/layout/dashboard/DashboardLayout";
import { useAuth } from "@/auth/AuthContext";
import { AccountSetsProvider, useAccountSets } from "@/accountSets/AccountSetsContext";

// 柠檬云风格页面：已接入真实后端，挂载在账套路由下（与既有实页互补）。
import LemonERecord from "@/pages/lemon/lemon-erecord";
import { VoucherNew } from "@/pages/lemon/lemon-vouchers";
import { LemonDepositJournal, LemonDraft, LemonAccountSettings } from "@/pages/lemon/lemon-funds";
import {
    LemonInvoiceFetch,
    LemonSalesInvoice,
    LemonPurchaseInvoice,
    LemonExpenseBill,
    LemonInvoiceSummary,
    LemonInvoiceRisk,
    LemonSpb,
    LemonTaxLogin,
    LemonBankLink,
} from "@/pages/lemon/lemon-invoice";
import LemonPro from "@/pages/lemon/lemon-pro";
import {
    LemonSalaryEmployee,
    LemonSalaryInsurance,
    LemonSalaryManage,
    LemonSalarySummary,
} from "@/pages/lemon/lemon-salary";
import {
    LemonAssetCategory,
    LemonAssetCheck,
    LemonAssetDepreciation,
    LemonAssetManage,
    LemonAssetSummary,
} from "@/pages/lemon/lemon-assets";
import {
    LemonIncomeQuarter,
    LemonCashFlow,
    LemonCashFlowQuarter,
    LemonStandardCashFlow,
    LemonDepartmentIncome,
    LemonProjectIncome,
    LemonPivot,
    LemonExpenseDetail,
    LemonEquityChange,
    LemonTaxReport,
    LemonFinancialSummary,
} from "@/pages/lemon/lemon-reports";
import {
    LemonCurrency,
    LemonVoucherWord,
    LemonVoucherTemplate,
    LemonPermission,
    LemonBoss,
    LemonBackup,
    LemonLog,
    LemonEisRelation,
    LemonScmRelation,
    LemonImportOld,
    LemonReinit,
} from "@/pages/lemon/lemon-settings";

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
                element: <DashboardLayout />,
                children: [
                    { index: true, element: <DashboardHome /> },
                    { path: "dev", element: <ComingSoon /> },
                    { path: "funds/cash-journal", element: <CashJournal /> },
                    { path: "funds/entry-categories", element: <EntryCategories /> },
                    { path: "funds/transfer", element: <Transfer /> },
                    { path: "funds/report", element: <FundReport /> },
                    { path: "funds/reconciliation", element: <FundReconciliation /> },
                    { path: "ledger/trial-balance", element: <TrialBalance /> },
                    { path: "ledger/subsidiary", element: <SubsidiaryLedger /> },
                    { path: "ledger/general", element: <GeneralLedger /> },
                    { path: "ledger/journal", element: <VoucherJournal /> },
                    { path: "ledger/voucher-summary", element: <VoucherSummary /> },
                    { path: "ledger/multi-column", element: <MultiColumnLedger /> },
                    { path: "ledger/assist-balance", element: <AssistBalance /> },
                    { path: "ledger/assist-subsidiary", element: <AssistSubsidiary /> },
                    { path: "ledger/assist-combine", element: <AssistCombine /> },
                    { path: "ledger/ageing", element: <AgeingAnalysis /> },
                    { path: "vouchers/manage", element: <VoucherManage /> },
                    { path: "reports/balance-sheet", element: <BalanceSheet /> },
                    { path: "reports/income-statement", element: <IncomeStatement /> },
                    { path: "settings/accounts", element: <Accounts /> },
                    { path: "settings/account-sets", element: <AccountSets /> },
                    { path: "settings/opening-balances", element: <OpeningBalances /> },
                    { path: "settings/assist-items", element: <AssistItems /> },
                    { path: "closing", element: <Closing /> },
                    // —— 按柠檬云菜单层级补齐的页面（复刻实现，mock 数据） ——
                    { path: "vouchers/new", element: <VoucherNew /> },
                    { path: "vouchers/erecord", element: <LemonERecord /> },
                    { path: "vouchers/spb", element: <LemonSpb /> },
                    { path: "funds/deposit-journal", element: <LemonDepositJournal /> },
                    { path: "funds/draft", element: <LemonDraft /> },
                    { path: "funds/bank-link", element: <LemonBankLink /> },
                    { path: "funds/account", element: <LemonAccountSettings /> },
                    { path: "invoices/fetch", element: <LemonInvoiceFetch /> },
                    { path: "invoices/sales", element: <LemonSalesInvoice /> },
                    { path: "invoices/purchase", element: <LemonPurchaseInvoice /> },
                    { path: "invoices/expense", element: <LemonExpenseBill /> },
                    { path: "invoices/summary", element: <LemonInvoiceSummary /> },
                    { path: "invoices/risk", element: <LemonInvoiceRisk /> },
                    { path: "salary/manage", element: <LemonSalaryManage /> },
                    { path: "salary/employee", element: <LemonSalaryEmployee /> },
                    { path: "salary/summary", element: <LemonSalarySummary /> },
                    { path: "salary/insurance", element: <LemonSalaryInsurance /> },
                    { path: "assets/manage", element: <LemonAssetManage /> },
                    { path: "assets/depreciation", element: <LemonAssetDepreciation /> },
                    { path: "assets/summary", element: <LemonAssetSummary /> },
                    { path: "assets/category", element: <LemonAssetCategory /> },
                    { path: "assets/check", element: <LemonAssetCheck /> },
                    { path: "reports/income-quarter", element: <LemonIncomeQuarter /> },
                    { path: "reports/cash-flow", element: <LemonCashFlow /> },
                    { path: "reports/cash-flow-quarter", element: <LemonCashFlowQuarter /> },
                    { path: "reports/standard-cash-flow", element: <LemonStandardCashFlow /> },
                    { path: "reports/department-income", element: <LemonDepartmentIncome /> },
                    { path: "reports/project-income", element: <LemonProjectIncome /> },
                    { path: "reports/pivot", element: <LemonPivot /> },
                    { path: "reports/expense", element: <LemonExpenseDetail /> },
                    { path: "reports/equity-change", element: <LemonEquityChange /> },
                    { path: "reports/tax", element: <LemonTaxReport /> },
                    { path: "reports/financial-summary", element: <LemonFinancialSummary /> },
                    { path: "tax/login", element: <LemonTaxLogin /> },
                    { path: "settings/currency", element: <LemonCurrency /> },
                    { path: "settings/voucher-word", element: <LemonVoucherWord /> },
                    { path: "settings/voucher-template", element: <LemonVoucherTemplate /> },
                    { path: "settings/permission", element: <LemonPermission /> },
                    { path: "settings/boss", element: <LemonBoss /> },
                    { path: "settings/backup", element: <LemonBackup /> },
                    { path: "settings/log", element: <LemonLog /> },
                    { path: "settings/eis-relation", element: <LemonEisRelation /> },
                    { path: "settings/scm-relation", element: <LemonScmRelation /> },
                    { path: "settings/import-old", element: <LemonImportOld /> },
                    { path: "settings/reinit", element: <LemonReinit /> },
                    { path: "pro", element: <LemonPro /> },
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
