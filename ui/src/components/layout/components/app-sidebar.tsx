import * as React from "react"
import { Link, useLocation, useNavigate, useParams } from "react-router"
import { BookOpen, Building2, FileText, GalleryVerticalEnd, House, ListTree, LogOut, Scale, Table2, Upload } from "lucide-react"

import { useAuth } from "@/auth/AuthContext"
import { useAccountSets } from "@/accountSets/AccountSetsContext"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from "@/components/ui/sidebar"

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
    label: "账目",
    items: [{ title: "科目余额表", path: "ledger/trial-balance", icon: Table2 }],
  },
  {
    label: "凭证",
    items: [
      { title: "凭证管理", path: "vouchers/manage", icon: FileText },
      { title: "凭证导入", path: "vouchers/import", icon: Upload },
    ],
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

export function AppSidebar({ ...props }: React.ComponentProps<typeof Sidebar>) {
  const { user, logout } = useAuth()
  const { accountSets } = useAccountSets()
  const location = useLocation()
  const navigate = useNavigate()
  const accountSetId = useParams().accountSetId ?? ""

  // 账套内的绝对路径：/{accountSetId} 或 /{accountSetId}/{path}
  const base = `/${accountSetId}`
  const hrefOf = (path: string) => (path ? `${base}/${path}` : base)

  // 切换账套时保留当前子页面：/{old}/xxx → /{new}/xxx
  function switchAccountSet(nextId: string) {
    if (!nextId || nextId === accountSetId) return
    const rest = location.pathname.startsWith(base) ? location.pathname.slice(base.length) : ""
    navigate(`/${nextId}${rest}`)
  }

  return (
    <Sidebar collapsible="icon" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link to={base}>
                <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-primary text-sidebar-primary-foreground">
                  <GalleryVerticalEnd className="size-4" />
                </div>
                <div className="grid flex-1 text-left text-sm leading-tight">
                  <span className="truncate font-semibold">好会计</span>
                  <span className="truncate text-xs text-muted-foreground">记账管理</span>
                </div>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>

          {/* 账套切换器：切换后停留在当前页面。仅有多个账套时可切换。 */}
          {accountSets.length > 0 && (
            <SidebarMenuItem className="group-data-[collapsible=icon]:hidden">
              <select
                aria-label="切换账套"
                className="h-9 w-full min-w-0 rounded-md border border-sidebar-border bg-transparent px-2 text-sm outline-none focus-visible:ring-[3px] focus-visible:ring-sidebar-ring/50"
                value={accountSetId}
                onChange={(e) => switchAccountSet(e.target.value)}
              >
                {accountSets.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name}
                    {s.isCurrent ? "（当前）" : ""}
                  </option>
                ))}
              </select>
            </SidebarMenuItem>
          )}
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        {navGroups.map((group) => (
          <SidebarGroup key={group.label}>
            <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
            <SidebarMenu>
              {group.items.map((item) => {
                const href = hrefOf(item.path)
                return (
                  <SidebarMenuItem key={item.path}>
                    <SidebarMenuButton
                      asChild
                      isActive={location.pathname === href}
                      tooltip={item.title}
                    >
                      <Link to={href}>
                        <item.icon />
                        <span>{item.title}</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                )
              })}
            </SidebarMenu>
          </SidebarGroup>
        ))}
      </SidebarContent>

      <SidebarFooter>
        <SidebarMenu>
          <SidebarMenuItem>
            <div className="flex items-center gap-2 rounded-md p-2 text-left text-sm">
              <div className="flex aspect-square size-8 items-center justify-center rounded-lg bg-sidebar-accent text-sidebar-accent-foreground">
                {(user?.displayName ?? "?").slice(0, 1)}
              </div>
              <div className="grid flex-1 leading-tight group-data-[collapsible=icon]:hidden">
                <span className="truncate font-medium">{user?.displayName ?? "未登录"}</span>
                <span className="truncate text-xs text-muted-foreground">{user?.username ?? ""}</span>
              </div>
            </div>
          </SidebarMenuItem>
          <SidebarMenuItem>
            <SidebarMenuButton onClick={logout} tooltip="退出登录">
              <LogOut />
              <span>退出登录</span>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>

      <SidebarRail />
    </Sidebar>
  )
}
