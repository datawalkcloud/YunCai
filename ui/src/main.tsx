import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import dayjs from 'dayjs'
import 'dayjs/locale/zh-cn'
import './index.css'
import { AuthProvider } from './auth/AuthContext.tsx'
import { ThemeProvider } from './components/theme/theme-provider.tsx'
import { TooltipProvider } from './components/ui/tooltip.tsx'
import router from './router/index.tsx'
import { RouterProvider } from "react-router/dom";

dayjs.locale('zh-cn')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      {/* radix-nova 的 SidebarProvider 不再内置 TooltipProvider，需在根部提供 */}
      <TooltipProvider>
        <AuthProvider>
          <RouterProvider router={router} />
        </AuthProvider>
      </TooltipProvider>
    </ThemeProvider>
  </StrictMode>,
)
