import { MoonOutlined, SunOutlined } from "@ant-design/icons"
import { Button } from "antd"

import { useTheme } from "./theme-provider"

/** 浅色 / 深色主题切换按钮。 */
export function ModeToggle() {
  const { resolvedTheme, toggleTheme } = useTheme()
  const isDark = resolvedTheme === "dark"

  return (
    <Button
      type="text"
      shape="circle"
      icon={isDark ? <MoonOutlined /> : <SunOutlined />}
      onClick={toggleTheme}
      title={isDark ? "切换到浅色" : "切换到深色"}
    />
  )
}
