import { Moon, Sun } from "lucide-react"

import { Button } from "@/components/ui/button"
import { useTheme } from "./theme-provider"

/** 浅色 / 深色主题切换按钮，图标随当前主题淡入淡出。 */
export function ModeToggle() {
  const { resolvedTheme, toggleTheme } = useTheme()

  return (
    <Button
      variant="outline"
      size="icon"
      className="relative"
      onClick={toggleTheme}
      title={resolvedTheme === "dark" ? "切换到浅色" : "切换到深色"}
    >
      <Sun className="size-4 scale-100 rotate-0 transition-all dark:scale-0 dark:-rotate-90" />
      <Moon className="absolute size-4 scale-0 rotate-90 transition-all dark:scale-100 dark:rotate-0" />
      <span className="sr-only">切换主题</span>
    </Button>
  )
}
