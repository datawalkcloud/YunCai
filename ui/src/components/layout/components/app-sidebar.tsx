import { Link, useLocation, useNavigate, useParams } from "react-router"
import { BookOpen, Building2, FileText, GalleryVerticalEnd, House, ListTree, LogOut, Scale, Table2 } from "lucide-react"
import { Button, Menu, Select, Typography, theme } from "antd"
import type { MenuProps } from "antd"

import { useAuth } from "@/auth/AuthContext"
import { useAccountSets } from "@/accountSets/AccountSetsContext"

// 导航分组，path 为账套下的相对子路径（首页为空串）。新增页面时在此登记即可。
const navGroups = [
  {
    label: "导航",
    items: [{ title: "首页", path: "", icon: House }],
  },
  {
    label: "资金",
    items: [{ title: "现金日记账", path: "funds/cash-journal", icon: BookOpen }],
  },
  {
    label: "凭证",
    items: [
      { title: "凭证管理", path: "vouchers/manage", icon: FileText },
    ],
  },
  {
    label: "账目",
    items: [{ title: "科目余额表", path: "ledger/trial-balance", icon: Table2 }],
  },
  {
    label: "报表",
    items: [{ title: "资产负债表", path: "reports/balance-sheet", icon: Scale }],
  },
  {
    label: "设置",
    items: [
      { title: "账套", path: "settings/account-sets", icon: Building2 },
      { title: "科目", path: "settings/accounts", icon: ListTree },
    ],
  },
]

export function AppSidebar({ collapsed }: { collapsed: boolean }) {
  const { user, logout } = useAuth()
  const { accountSets } = useAccountSets()
  const location = useLocation()
  const navigate = useNavigate()
  const accountSetId = useParams().accountSetId ?? ""
  const { token } = theme.useToken()

  // 账套内的绝对路径：/{accountSetId} 或 /{accountSetId}/{path}
  const base = `/${accountSetId}`
  const hrefOf = (path: string) => (path ? `${base}/${path}` : base)

  // 切换账套时保留当前子页面：/{old}/xxx → /{new}/xxx
  function switchAccountSet(nextId: string) {
    if (!nextId || nextId === accountSetId) return
    const rest = location.pathname.startsWith(base) ? location.pathname.slice(base.length) : ""
    navigate(`/${nextId}${rest}`)
  }

  // 分组菜单项：每个 navGroup 映射为一个 group，其下为可点击的页面项。
  const menuItems: MenuProps["items"] = navGroups.map((group) => ({
    type: "group",
    key: group.label,
    label: group.label,
    children: group.items.map((item) => {
      const Icon = item.icon
      return {
        key: hrefOf(item.path),
        icon: <Icon className="size-4" />,
        label: item.title,
      }
    }),
  }))

  return (
    <div className="flex h-full flex-col">
      {/* 品牌 + 账套切换器 */}
      <div className="flex flex-col gap-2 p-2">
        <Link
          to={base}
          className="flex items-center gap-2 rounded-md p-2 no-underline"
          style={{ color: token.colorText }}
        >
          <span
            className="flex size-8 shrink-0 items-center justify-center rounded-lg"
            style={{ background: token.colorPrimary, color: token.colorWhite }}
          >
            <GalleryVerticalEnd className="size-4" />
          </span>
          {!collapsed && (
            <span className="flex flex-col leading-tight">
              <Typography.Text strong>好会计</Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                记账管理
              </Typography.Text>
            </span>
          )}
        </Link>

        {!collapsed && accountSets.length > 0 && (
          <Select
            aria-label="切换账套"
            value={accountSetId}
            onChange={switchAccountSet}
            style={{ width: "100%" }}
            options={accountSets.map((s) => ({
              value: s.id,
              label: `${s.name}${s.isCurrent ? "（当前）" : ""}`,
            }))}
          />
        )}
      </div>

      {/* 导航菜单 */}
      <div className="min-h-0 flex-1 overflow-y-auto">
        <Menu
          mode="inline"
          inlineCollapsed={collapsed}
          selectedKeys={[location.pathname]}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
          style={{ borderInlineEnd: "none", background: "transparent" }}
        />
      </div>

      {/* 用户信息 + 退出 */}
      <div className="flex flex-col gap-1 p-2">
        <div className="flex items-center gap-2 rounded-md p-2">
          <span
            className="flex size-8 shrink-0 items-center justify-center rounded-lg"
            style={{ background: token.colorFillSecondary, color: token.colorText }}
          >
            {(user?.displayName ?? "?").slice(0, 1)}
          </span>
          {!collapsed && (
            <span className="flex min-w-0 flex-col leading-tight">
              <Typography.Text strong ellipsis>
                {user?.displayName ?? "未登录"}
              </Typography.Text>
              <Typography.Text type="secondary" ellipsis style={{ fontSize: 12 }}>
                {user?.username ?? ""}
              </Typography.Text>
            </span>
          )}
        </div>
        <Button
          type="text"
          block={!collapsed}
          onClick={logout}
          title="退出登录"
          icon={<LogOut className="size-4" />}
          style={{ justifyContent: collapsed ? "center" : "flex-start" }}
        >
          {!collapsed && "退出登录"}
        </Button>
      </div>
    </div>
  )
}
