import { theme as antdTheme, ConfigProvider, App as AntdApp } from "antd"
import zhCN from "antd/locale/zh_CN"
import dayjs from "dayjs"
import "dayjs/locale/zh-cn"
import type { ReactNode } from "react"

import { useTheme } from "./theme-provider"

dayjs.locale("zh-cn")

/**
 * 将项目的 light / dark 主题桥接到 antd。
 * antd 通过 ConfigProvider 的算法切换明暗，而非读取 <html> 的 class，
 * 因此这里根据 resolvedTheme 选择对应算法，让 antd 组件跟随全局主题。
 */
export function AntdConfigProvider({ children }: { children: ReactNode }) {
  const { resolvedTheme } = useTheme()

  return (
    <ConfigProvider
      locale={zhCN}
      theme={{
        algorithm:
          resolvedTheme === "dark" ? antdTheme.darkAlgorithm : antdTheme.defaultAlgorithm,
        token: {
          // 与 index.css 中的品牌色 --primary / --radius 保持一致。
          colorPrimary: "#27bd51",
          colorInfo: "#27bd51",
          borderRadius: 10,
        },
      }}
    >
      <AntdApp>{children}</AntdApp>
    </ConfigProvider>
  )
}
