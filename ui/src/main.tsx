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
 * antd 主题：沿用项目既有设计 token，直角 + 紧凑密度统一由全局主题控制。
 * compactAlgorithm 把控件高度收到 28、间距收到 8/12、正文 12px；fontSizeSM 抬到 11 以免辅助文字过小。
 * motion: false 把动效时长统一归零，弹层/折叠/切换都即时呈现，操作反馈更快。
 */
const antdTheme = {
  algorithm: theme.compactAlgorithm,
  token: {
    motion: false,
    colorPrimary: '#2f6bff',
    colorLink: '#2f6bff',
    colorBgLayout: '#f4f6fb',
    colorText: '#1e293b',
    colorTextSecondary: '#64748b',
    colorBorder: '#e2e8f0',
    colorBorderSecondary: '#eceff4',
    colorSuccess: '#10b981',
    colorWarning: '#f59e0b',
    colorError: '#ef4444',
    borderRadius: 0,
    fontSizeSM: 11,
    fontFamily: "'Inter Variable', sans-serif",
  },
  components: {
    // 账页切换条：轨道透明（白卡自己就是轨道），选中项取主色实底。
    Segmented: {
      trackBg: 'transparent',
      trackPadding: 2,
      itemColor: '#64748b',
      itemHoverBg: '#eef4ff',
      itemHoverColor: '#2f6bff',
      itemSelectedBg: '#2f6bff',
      itemSelectedColor: '#ffffff',
    },
    // 侧边栏菜单：无描边、无左侧高亮条，选中态用浅蓝实底，字号与旧版菜单一致。
    Menu: {
      fontSize: 13,
      itemHeight: 32,
      itemBg: '#ffffff',
      subMenuItemBg: '#ffffff',
      itemColor: '#475569',
      itemHoverBg: '#f6f8fb',
      itemHoverColor: '#1e293b',
      itemSelectedBg: '#e7f0ff',
      itemSelectedColor: '#2f6bff',
      itemBorderRadius: 0,
      itemMarginInline: 8,
      itemMarginBlock: 2,
      activeBarBorderWidth: 0,
      iconMarginInlineEnd: 8,
    },
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
