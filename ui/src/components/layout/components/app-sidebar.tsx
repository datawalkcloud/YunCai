import * as React from "react"
import { Link, useLocation } from "react-router"
import { BookOpen, Building2, FileText, GalleryVerticalEnd, House, ListTree, LogOut, Scale, Table2 } from "lucide-react"

import { useAuth } from "@/auth/AuthContext"
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

// 导航分组，后续新增页面时在此登记即可。
const navGroups = [
  {
    label: "导航",
    items: [{ title: "首页", url: "/", icon: House }],
  },
  {
    label: "资金",
    items: [{ title: "现金日记账", url: "/funds/cash-journal", icon: BookOpen }],
  },
  {
    label: "账目",
    items: [{ title: "科目余额表", url: "/ledger/trial-balance", icon: Table2 }],
  },
  {
    label: "凭证",
    items: [{ title: "新增凭证", url: "/vouchers/new", icon: FileText }],
  },
  {
    label: "报表",
    items: [{ title: "资产负债表", url: "/reports/balance-sheet", icon: Scale }],
  },
  {
    label: "设置",
    items: [
      { title: "账套", url: "/settings/account-sets", icon: Building2 },
      { title: "科目", url: "/settings/accounts", icon: ListTree },
    ],
  },
]

export function AppSidebar({ ...props }: React.ComponentProps<typeof Sidebar>) {
  const { user, logout } = useAuth()
  const location = useLocation()

  return (
    <Sidebar collapsible="icon" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link to="/">
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
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        {navGroups.map((group) => (
          <SidebarGroup key={group.label}>
            <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
            <SidebarMenu>
              {group.items.map((item) => (
                <SidebarMenuItem key={item.url}>
                  <SidebarMenuButton
                    asChild
                    isActive={location.pathname === item.url}
                    tooltip={item.title}
                  >
                    <Link to={item.url}>
                      <item.icon />
                      <span>{item.title}</span>
                    </Link>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
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
