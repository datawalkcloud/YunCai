import { Link, useLocation, useNavigate, useParams } from "react-router"
import {
  BookOpen,
  Building2,
  Calculator,
  Check,
  ChevronsUpDown,
  FileText,
  House,
  Info,
  ListTree,
  LogOut,
  Moon,
  Scale,
  Sigma,
  Sun,
  Table2,
  Tags,
  TrendingUp,
} from "lucide-react"

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import { useAuth } from "@/auth/AuthContext"
import { useAccountSets } from "@/accountSets/AccountSetsContext"
import { useTheme } from "@/components/theme/theme-provider"

// 导航分组，path 为账套下的相对子路径（首页为空串）。新增页面时在此登记即可。
export const navGroups = [
  {
    label: "导航",
    items: [{ title: "首页", path: "", icon: House }],
  },
  {
    label: "资金",
    items: [
      { title: "现金日记账", path: "funds/cash-journal", icon: BookOpen },
      { title: "收支类别", path: "funds/entry-categories", icon: Tags },
    ],
  },
  {
    label: "凭证",
    items: [{ title: "凭证管理", path: "vouchers/manage", icon: FileText }],
  },
  {
    label: "账目",
    items: [{ title: "科目余额表", path: "ledger/trial-balance", icon: Table2 }],
  },
  {
    label: "报表",
    items: [
      { title: "资产负债表", path: "reports/balance-sheet", icon: Scale },
      { title: "利润表", path: "reports/income-statement", icon: TrendingUp },
    ],
  },
  {
    label: "设置",
    items: [
      { title: "账套", path: "settings/account-sets", icon: Building2 },
      { title: "科目", path: "settings/accounts", icon: ListTree },
      { title: "期初余额", path: "settings/opening-balances", icon: Sigma },
    ],
  },
]

export function PlatformSidebar() {
  const { user, logout } = useAuth()
  const { accountSets } = useAccountSets()
  const { resolvedTheme, toggleTheme } = useTheme()
  const location = useLocation()
  const navigate = useNavigate()
  const accountSetId = useParams().accountSetId ?? ""

  // 账套内的绝对路径：/{accountSetId} 或 /{accountSetId}/{path}
  const base = `/${accountSetId}`
  const hrefOf = (path: string) => (path ? `${base}/${path}` : base)
  const currentSet = accountSets.find((s) => s.id === accountSetId)

  // 切换账套时保留当前子页面：/{old}/xxx → /{new}/xxx
  function switchAccountSet(nextId: string) {
    if (!nextId || nextId === accountSetId) return
    const rest = location.pathname.startsWith(base) ? location.pathname.slice(base.length) : ""
    navigate(`/${nextId}${rest}`)
  }

  return (
    <Sidebar collapsible="icon">
      {/* 品牌 + 账套切换器（对应图中 Projects 选择框） */}
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link to={base}>
                <span className="bg-primary text-primary-foreground flex aspect-square size-8 shrink-0 items-center justify-center rounded-lg">
                  <Calculator className="size-4" />
                </span>
                <span className="truncate">好会计</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>

        {accountSets.length > 0 && (
          <SidebarMenu>
            <SidebarMenuItem>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <SidebarMenuButton className="border data-[state=open]:bg-sidebar-accent">
                    <Building2 className="size-4 shrink-0" />
                    <span className="truncate">{currentSet?.name ?? "选择账套"}</span>
                    <ChevronsUpDown className="text-muted-foreground ml-auto size-3.5" />
                  </SidebarMenuButton>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                  className="w-(--radix-dropdown-menu-trigger-width) min-w-56"
                  align="start"
                >
                  <DropdownMenuLabel className="text-muted-foreground text-xs">
                    切换账套
                  </DropdownMenuLabel>
                  {accountSets.map((s) => (
                    <DropdownMenuItem key={s.id} onClick={() => switchAccountSet(s.id)}>
                      <span className="truncate">
                        {s.name}
                        {s.isCurrent ? "（当前）" : ""}
                      </span>
                      {s.id === accountSetId && <Check className="ml-auto size-4" />}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuContent>
              </DropdownMenu>
            </SidebarMenuItem>
          </SidebarMenu>
        )}
      </SidebarHeader>

      {/* 分组导航 */}
      <SidebarContent>
        {navGroups.map((group) => (
          <SidebarGroup key={group.label}>
            {group.items.length > 1 && <SidebarGroupLabel>{group.label}</SidebarGroupLabel>}
            <SidebarGroupContent>
              <SidebarMenu>
                {group.items.map((item) => {
                  const href = hrefOf(item.path)
                  return (
                    <SidebarMenuItem key={href}>
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
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>

      <SidebarFooter>
        {/* 当前账套信息卡片（对应图中 Credits 卡片） */}
        {currentSet && (
          <div className="rounded-xl border p-3 group-data-[collapsible=icon]:hidden">
            <div className="text-muted-foreground flex items-center gap-1 text-xs">
              当前账套
              <Tooltip>
                <TooltipTrigger asChild>
                  <Info className="size-3" />
                </TooltipTrigger>
                <TooltipContent>启用期间 {currentSet.startPeriod}</TooltipContent>
              </Tooltip>
            </div>
            <div className="mt-0.5 truncate text-[15px] font-semibold">{currentSet.name}</div>
            <div className="text-muted-foreground truncate text-xs">{currentSet.companyName}</div>
          </div>
        )}

        {/* 用户页脚：头像 + 姓名 + 展开菜单 */}
        <SidebarMenu>
          <SidebarMenuItem>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <SidebarMenuButton size="lg" className="data-[state=open]:bg-sidebar-accent">
                  <Avatar className="size-7">
                    <AvatarFallback className="text-xs">
                      {(user?.displayName ?? "?").slice(0, 1)}
                    </AvatarFallback>
                  </Avatar>
                  <span className="flex min-w-0 flex-1 flex-col leading-tight">
                    <span className="truncate text-[13px] font-semibold">
                      {user?.displayName ?? "未登录"}
                    </span>
                    <span className="text-muted-foreground truncate text-xs">
                      {user?.username ?? ""}
                    </span>
                  </span>
                  <ChevronsUpDown className="text-muted-foreground ml-auto size-3.5" />
                </SidebarMenuButton>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                className="w-(--radix-dropdown-menu-trigger-width) min-w-56"
                side="top"
                align="start"
              >
                <DropdownMenuItem onClick={toggleTheme}>
                  {resolvedTheme === "dark" ? <Sun /> : <Moon />}
                  {resolvedTheme === "dark" ? "切换到浅色" : "切换到深色"}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" onClick={logout}>
                  <LogOut />
                  退出登录
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarFooter>
    </Sidebar>
  )
}
