import { useMemo } from "react"
import { Outlet, useLocation, useNavigate, useParams } from "react-router"
import { Check, ChevronDown } from "lucide-react"

import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar"
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { PlatformSidebar, navGroups } from "./platform-sidebar"
import { useAccountSets } from "@/accountSets/AccountSetsContext"

/**
 * 平台风格布局（shadcn sidebar inset 变体）：侧边栏与页面同底色，
 * 右侧顶部为面包屑（账套切换），主内容承载在一个圆角浮层面板中。
 */
export default function PlatformLayout() {
  const { accountSets } = useAccountSets()
  const location = useLocation()
  const navigate = useNavigate()
  const accountSetId = useParams().accountSetId ?? ""

  const base = `/${accountSetId}`
  const currentSet = accountSets.find((s) => s.id === accountSetId)

  // 根据当前路径解析页面标题，用于面包屑第二段。
  const pageTitle = useMemo(() => {
    const rest = location.pathname.startsWith(base)
      ? location.pathname.slice(base.length).replace(/^\//, "")
      : ""
    for (const group of navGroups) {
      const hit = group.items.find((item) => item.path === rest)
      if (hit) return hit.title
    }
    return ""
  }, [location.pathname, base])

  // 面包屑上的账套切换：保留当前子页面。
  function switchAccountSet(nextId: string) {
    if (!nextId || nextId === accountSetId) return
    const rest = location.pathname.startsWith(base) ? location.pathname.slice(base.length) : ""
    navigate(`/${nextId}${rest}`)
  }

  return (
    <SidebarProvider>
      <PlatformSidebar />
      {/* 覆盖 inset 默认的浮层样式：面包屑留在页面底色上，内容面板自行圆角 */}
      <SidebarInset className="h-svh overflow-hidden bg-transparent shadow-none md:peer-data-[variant=inset]:m-0 md:peer-data-[variant=inset]:rounded-none">
        <header className="flex h-12 shrink-0 items-center gap-1.5 px-4">
          {/* 移动端侧边栏收进抽屉，需要页头上的开关 */}
          <SidebarTrigger className="text-muted-foreground -ml-1 md:hidden" />
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <DropdownMenu>
                  <DropdownMenuTrigger className="hover:bg-accent flex items-center gap-1 rounded-md px-2 py-1 text-[13px] font-semibold">
                    {currentSet?.name ?? "账套"}
                    <ChevronDown className="text-muted-foreground size-3.5" />
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="start">
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
              </BreadcrumbItem>
              <BreadcrumbSeparator />
              {pageTitle && (
                <BreadcrumbItem>
                  <BreadcrumbPage className="text-muted-foreground text-[13px]">
                    {pageTitle}
                  </BreadcrumbPage>
                </BreadcrumbItem>
              )}
            </BreadcrumbList>
          </Breadcrumb>
        </header>

        {/* 主内容浮层面板 */}
        <main className="bg-background mr-2 mb-2 ml-2 min-h-0 flex-1 overflow-auto rounded-xl border p-4 md:ml-0">
          <Outlet />
        </main>
      </SidebarInset>
    </SidebarProvider>
  )
}
