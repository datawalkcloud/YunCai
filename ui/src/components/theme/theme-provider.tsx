import { createContext, useContext, useEffect, useState, type ReactNode } from "react"

/** light / dark 为固定主题，system 跟随操作系统偏好。 */
export type Theme = "light" | "dark" | "system"

interface ThemeContextValue {
  /** 用户选择的主题（可能为 system）。 */
  theme: Theme
  /** 实际生效的主题，始终为 light 或 dark。 */
  resolvedTheme: "light" | "dark"
  setTheme: (theme: Theme) => void
  /** 在 light / dark 之间切换（基于当前生效主题）。 */
  toggleTheme: () => void
}

const STORAGE_KEY = "kuaiji.theme"

const ThemeContext = createContext<ThemeContextValue | null>(null)

function getSystemTheme(): "light" | "dark" {
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"
}

function readStoredTheme(fallback: Theme): Theme {
  try {
    const stored = localStorage.getItem(STORAGE_KEY) as Theme | null
    if (stored === "light" || stored === "dark" || stored === "system") {
      return stored
    }
  } catch {
    // 忽略无法访问 localStorage 的情况
  }
  return fallback
}

function applyTheme(theme: Theme): "light" | "dark" {
  const resolved = theme === "system" ? getSystemTheme() : theme
  document.documentElement.classList.toggle("dark", resolved === "dark")
  return resolved
}

export function ThemeProvider({
  children,
  defaultTheme = "system",
}: {
  children: ReactNode
  defaultTheme?: Theme
}) {
  const [theme, setThemeState] = useState<Theme>(() => readStoredTheme(defaultTheme))
  const [resolvedTheme, setResolvedTheme] = useState<"light" | "dark">(() =>
    (readStoredTheme(defaultTheme) === "system" ? getSystemTheme() : readStoredTheme(defaultTheme)) as "light" | "dark",
  )

  // 主题变化时更新 <html> 的 class。
  useEffect(() => {
    setResolvedTheme(applyTheme(theme))
  }, [theme])

  // 仅在 system 模式下跟随系统偏好变化。
  useEffect(() => {
    if (theme !== "system") return
    const mql = window.matchMedia("(prefers-color-scheme: dark)")
    const handler = () => setResolvedTheme(applyTheme("system"))
    mql.addEventListener("change", handler)
    return () => mql.removeEventListener("change", handler)
  }, [theme])

  const setTheme = (next: Theme) => {
    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // 忽略无法写入 localStorage 的情况
    }
    setThemeState(next)
  }

  const toggleTheme = () => {
    setTheme(resolvedTheme === "dark" ? "light" : "dark")
  }

  return (
    <ThemeContext value={{ theme, resolvedTheme, setTheme, toggleTheme }}>
      {children}
    </ThemeContext>
  )
}

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext)
  if (!ctx) {
    throw new Error("useTheme 必须在 ThemeProvider 内使用")
  }
  return ctx
}
