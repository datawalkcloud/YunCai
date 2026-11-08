import { StrictMode, useMemo } from 'react'
import { createRoot } from 'react-dom/client'
import { App as AntdApp, ConfigProvider, theme as antdTheme } from 'antd'
import type { ThemeConfig } from 'antd'
import zhCN from 'antd/locale/zh_CN'
import dayjs from 'dayjs'
import 'dayjs/locale/zh-cn'
import './index.css'
import { AuthProvider } from './auth/AuthContext.tsx'
import { ThemeProvider, useTheme } from './components/theme/theme-provider.tsx'
import { ErrorBoundary } from './components/error/ErrorBoundary.tsx'
import { installUnauthorizedFetchGuard } from './lib/api-client.ts'
import router from './router/index.tsx'
import { RouterProvider } from "react-router/dom";

dayjs.locale('zh-cn')

/**
 * antd 主题：明暗、密度、圆角与主题色一律跟随右上角「主题设置」。
 * 明暗配色用 antd 自带的 default / dark 算法，主题色作为唯一的种子色传入；
 * 手写样式侧的同一颗种子在 index.css 里派生成 --brand 系列与 rounded-* 档位，两套口径不会分叉。
 * motion: false 把动效时长统一归零，弹层/折叠/切换都即时呈现，操作反馈更快。
 */
function ThemedRoot() {
  const { resolvedTheme, density, radius, primary } = useTheme()

  const themeConfig = useMemo<ThemeConfig>(
    () => ({
      algorithm: [
        resolvedTheme === 'dark' ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
        ...(density === 'compact' ? [antdTheme.compactAlgorithm] : []),
      ],
      token: {
        // 圆角基准 0 为全站直角；主题色同时驱动链接与信息色，避免一个界面里出现两种主色。
        borderRadius: radius,
        colorPrimary: primary,
        colorInfo: primary,
        colorLink: primary,
      },
      components: {
        // 全站卡片只在这里定一次描边色（明暗由 index.css 的 --border 切换），页面不要再各自写内联边框。
        Card: { colorBorderSecondary: 'var(--border)' },
      },
    }),
    [resolvedTheme, density, radius, primary],
  )

  return (
    /* wave 关掉点击水波纹，避免整站按钮都被一圈扩散动画拖慢手感 */
    <ConfigProvider locale={zhCN} theme={themeConfig} wave={{ disabled: true }}>
      {/* antd 的 message/notification/modal 静态方法需要挂在 App 上下文里 */}
      <AntdApp>
        <AuthProvider>
          {/* 全站最后一道防线：路由树自身出错也不能白屏 */}
          <ErrorBoundary>
            <RouterProvider router={router} />
          </ErrorBoundary>
        </AuthProvider>
      </AntdApp>
    </ConfigProvider>
  )
}

// 裸 fetch（导出/下载/导入）的 401 也走集中处理，页面无需各自关心登录态过期。
installUnauthorizedFetchGuard()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <ThemedRoot />
    </ThemeProvider>
  </StrictMode>,
)
