import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import dayjs from 'dayjs'
import 'dayjs/locale/zh-cn'
import './index.css'
import { AuthProvider } from './auth/AuthContext.tsx'
import { ThemeProvider } from './components/theme/theme-provider.tsx'
import { TooltipProvider } from './components/ui/tooltip.tsx'
import { ErrorBoundary } from './components/error/ErrorBoundary.tsx'
import { installUnauthorizedFetchGuard } from './lib/api-client.ts'
import router from './router/index.tsx'
import { RouterProvider } from "react-router/dom";

dayjs.locale('zh-cn')

// 裸 fetch（导出/下载/导入）的 401 也走集中处理，页面无需各自关心登录态过期。
installUnauthorizedFetchGuard()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      {/* radix-nova 的 SidebarProvider 不再内置 TooltipProvider，需在根部提供 */}
      <TooltipProvider>
        <AuthProvider>
          {/* 全站最后一道防线：路由树自身出错也不能白屏 */}
          <ErrorBoundary>
            <RouterProvider router={router} />
          </ErrorBoundary>
        </AuthProvider>
      </TooltipProvider>
    </ThemeProvider>
  </StrictMode>,
)
