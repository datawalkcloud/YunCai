import { useMemo } from "react"
import { Outlet, useLocation, useNavigate, useParams } from "react-router"
import { Check } from "lucide-react"

import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar"
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from "@/components/ui/breadcrumb"
import { Separator } from "@/components/ui/separator"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { PlatformSidebar, navGroups } from "./platform-sidebar"
import { useAccountSets } from "@/accountSets/AccountSetsContext"

/** 平台布局：shadcn 官方 sidebar 模板，页头承载面包屑与账套切换。 */
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
      <SidebarInset>
        <header className="flex h-16 shrink-0 items-center gap-2 transition-[width,height] ease-linear group-has-data-[collapsible=icon]/sidebar-wrapper:h-12">
          <div className="flex items-center gap-2 px-4">
            <SidebarTrigger className="-ml-1" />
            <Separator
              orientation="vertical"
              className="mr-2 data-[orientation=vertical]:h-4"
            />
            <Breadcrumb>
              <BreadcrumbList>
                <BreadcrumbItem className="hidden md:block">
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <BreadcrumbLink href="#" onClick={(e) => e.preventDefault()}>
                        {currentSet?.name ?? "账套"}
                      </BreadcrumbLink>
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
                <BreadcrumbSeparator className="hidden md:block" />
                <BreadcrumbItem>
                  <BreadcrumbPage>{pageTitle || "首页"}</BreadcrumbPage>
                </BreadcrumbItem>
              </BreadcrumbList>
            </Breadcrumb>
          </div>
        </header>

        <div className="flex flex-1 flex-col gap-4 p-4 pt-0">
          <Outlet />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
