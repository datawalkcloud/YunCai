import { Moon, Sun } from "lucide-react"

import { Button } from "@/components/ui/button"
import { useTheme } from "./theme-provider"

/** 浅色 / 深色主题切换按钮。 */
export function ModeToggle() {
  const { resolvedTheme, toggleTheme } = useTheme()
  const isDark = resolvedTheme === "dark"

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={toggleTheme}
      title={isDark ? "切换到浅色" : "切换到深色"}
    >
      {isDark ? <Moon /> : <Sun />}
    </Button>
  )
}
