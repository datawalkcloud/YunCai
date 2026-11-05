import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App as AntdApp, ConfigProvider, theme } from 'antd'
import zhCN from 'antd/locale/zh_CN'
import dayjs from 'dayjs'
import 'dayjs/locale/zh-cn'
import './index.css'
import { AuthProvider } from './auth/AuthContext.tsx'
import { ThemeProvider } from './components/theme/theme-provider.tsx'
import { ErrorBoundary } from './components/error/ErrorBoundary.tsx'
import { installUnauthorizedFetchGuard } from './lib/api-client.ts'
import router from './router/index.tsx'
import { RouterProvider } from "react-router/dom";

dayjs.locale('zh-cn')

/**
 * antd 主题：沿用项目既有设计 token，圆角 + 紧凑密度统一由全局主题控制。
 * compactAlgorithm 把控件高度收到 28、间距收到 8/12、正文 12px；fontSizeSM 抬到 11 以免辅助文字过小。
 * motion: false 把动效时长统一归零，弹层/折叠/切换都即时呈现，操作反馈更快。
 * borderRadius 显式声明为 6（即 antd 默认圆角），作为全站圆角基准；手写 Tailwind 容器在
 * index.css 里用 --radius(0.5rem) 对齐到同一量级（rounded-lg≈antd 卡片 8 / rounded-md≈antd 控件 6）。
 */
const antdTheme = {
  algorithm: theme.compactAlgorithm,
  token: {
    borderRadius: 6,
  },
}

// 裸 fetch（导出/下载/导入）的 401 也走集中处理，页面无需各自关心登录态过期。
installUnauthorizedFetchGuard()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {/* wave 关掉点击水波纹，避免整站按钮都被一圈扩散动画拖慢手感 */}
    <ConfigProvider locale={zhCN} theme={antdTheme} wave={{ disabled: true }}>
      {/* antd 的 message/notification/modal 静态方法需要挂在 App 上下文里 */}
      <AntdApp>
        <ThemeProvider>
          <AuthProvider>
            {/* 全站最后一道防线：路由树自身出错也不能白屏 */}
            <ErrorBoundary>
              <RouterProvider router={router} />
            </ErrorBoundary>
          </AuthProvider>
        </ThemeProvider>
      </AntdApp>
    </ConfigProvider>
  </StrictMode>,
)
