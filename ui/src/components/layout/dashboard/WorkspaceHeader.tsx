import { useState } from "react";
import { useLocation, useNavigate, useNavigation, useParams } from "react-router";
import {
    BankOutlined,
    CheckOutlined,
    DownOutlined,
    LogoutOutlined,
    QuestionCircleOutlined,
} from "@ant-design/icons";
import { App, Avatar, Dropdown, Tooltip } from "antd";

import { useAuth } from "@/auth/AuthContext";
import { useAccountSets } from "@/accountSets/AccountSetsContext";
import { useDefaultAccountingPeriod } from "@/accountSets/useCurrentAccountSet";
import { AccountSetsApi } from "@/apis-gen";
import { apiConfig } from "@/lib/api-client";
import { ICON_STYLE } from "@/components/page/styles";
import { RouteTabs } from "./RouteTabs";

/* ------------------------------------------------------------------ */
/*  工作区头：第一行账套切换 + 当前会计期间 + 用户，第二行页签条。          */
/* ------------------------------------------------------------------ */

export function WorkspaceHeader() {
    // 路由模块还在加载时导航不会提交，旧页面继续留在屏上；这一条细光带就是加载反馈。
    const loading = useNavigation().state === "loading";

    return (
        <header className="relative shrink-0 bg-white">
            <div className="flex h-12 items-center gap-4 border-b border-slate-100 px-5">
                <AccountSetSwitcher />
                <PeriodLabel />
                <div className="ml-auto">
                    <UserMenu />
                </div>
            </div>
            <RouteTabs />
            {loading && (
                <div className="pointer-events-none absolute inset-x-0 bottom-0 z-10 h-0.5 overflow-hidden">
                    <div className="kj-route-progress h-full w-1/3 bg-[#2f6bff]" />
                </div>
            )}
        </header>
    );
}

/* ------------------------------------------------------------------ */
/*  账套切换                                                          */
/* ------------------------------------------------------------------ */

/**
 * 账套切换器：把选中的账套设为后端「当前账套」，刷新账套上下文（保证各页面「（当前）」标记不失真），
 * 并跳到该账套下的同一子页面。页面取数以路由第一段为准，因此即使设置当前失败也照常跳转，只弹错误提示。
 */
function AccountSetSwitcher() {
    const { accountSetId = "" } = useParams();
    const location = useLocation();
    const navigate = useNavigate();
    const { token } = useAuth();
    const { accountSets, refresh } = useAccountSets();
    const { message } = App.useApp();
    const [busy, setBusy] = useState(false);

    if (accountSets.length === 0) return null;
    const current = accountSets.find((set) => set.id === accountSetId);

    async function choose(id: string) {
        if (!id || id === accountSetId || busy) return;
        setBusy(true);
        try {
            await new AccountSetsApi(apiConfig(token)).accountSetsIdCurrentPost({ id });
            // 切换成功后刷新上下文，各页面「（当前）」标记随之更新。
            refresh();
        } catch (err) {
            message.error(err instanceof Error ? err.message : "设为当前账套失败。");
        } finally {
            setBusy(false);
        }
        const base = `/${accountSetId}`;
        const rest = location.pathname.startsWith(base) ? location.pathname.slice(base.length) : "";
        navigate(`/${id}${rest}`);
    }

    return (
        <Dropdown
            trigger={["click"]}
            placement="bottomLeft"
            menu={{
                style: { minWidth: 224 },
                selectable: false,
                items: accountSets.map((set) => ({
                    key: set.id,
                    label: (
                        <span className="flex items-start justify-between gap-2">
                            <span className="min-w-0">
                                <span className="block truncate text-[13px]">{set.name}</span>
                                {set.companyName && set.companyName !== set.name && (
                                    <span className="block truncate text-[11px] text-slate-400">
                                        {set.companyName}
                                    </span>
                                )}
                            </span>
                            {set.id === accountSetId && (
                                <CheckOutlined className="shrink-0" style={ICON_STYLE.brand} />
                            )}
                        </span>
                    ),
                })),
                onClick: ({ key }) => void choose(key),
            }}
        >
            <button
                type="button"
                disabled={busy}
                title={current?.companyName}
                className="flex h-8 w-[248px] shrink-0 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-left transition-colors hover:border-[#2f6bff]/40 hover:bg-[#f8faff] disabled:cursor-wait disabled:opacity-60"
            >
                <BankOutlined className="shrink-0 text-[14px]" style={ICON_STYLE.brand} />
                <span className="min-w-0 flex-1 truncate text-[13px] font-medium text-slate-800">
                    {current?.name ?? "选择账套"}
                </span>
                <DownOutlined className="shrink-0 text-[9px]" style={ICON_STYLE.muted} />
            </button>
        </Dropdown>
    );
}

/* ------------------------------------------------------------------ */
/*  当前会计期间                                                       */
/* ------------------------------------------------------------------ */

function PeriodLabel() {
    const period = useDefaultAccountingPeriod();
    return (
        <span className="flex shrink-0 items-center gap-1 text-[13px] text-slate-600">
            {period.year}年{period.month}月
            <Tooltip title="账套当前会计期间：尚未结账的最新期间。">
                <QuestionCircleOutlined className="cursor-help text-[12px]" style={ICON_STYLE.placeholder} />
            </Tooltip>
        </span>
    );
}

/* ------------------------------------------------------------------ */
/*  用户                                                              */
/* ------------------------------------------------------------------ */

function UserMenu() {
    const { user, logout } = useAuth();
    const navigate = useNavigate();
    const name = user?.displayName || user?.username || "";

    function handleLogout() {
        logout();
        navigate("/login", { replace: true });
    }

    return (
        <Dropdown
            trigger={["click"]}
            placement="bottomRight"
            menu={{
                items: [{ key: "logout", icon: <LogoutOutlined />, label: "退出登录", onClick: handleLogout }],
            }}
        >
            <button
                type="button"
                className="flex items-center gap-2 rounded-lg py-1 pl-1 pr-2 transition-colors hover:bg-slate-50"
            >
                <Avatar size={26} style={{ background: "linear-gradient(135deg,#5b9bff,#2f6bff)", fontWeight: 500 }}>
                    {name.slice(0, 1)}
                </Avatar>
                <span className="text-[13px] font-medium text-slate-700">{name}</span>
                <DownOutlined className="text-[10px]" style={ICON_STYLE.muted} />
            </button>
        </Dropdown>
    );
}
