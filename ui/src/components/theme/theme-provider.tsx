import { createContext, useContext, useEffect, useState, type ReactNode } from "react"

/** light / dark 为固定主题，system 跟随操作系统偏好。 */
export type Theme = "light" | "dark" | "system"

/** default 为 antd 标准密度，compact 启用 antd 紧凑算法。 */
export type Density = "default" | "compact"

/** 圆角档位（px）：0 为全站直角，其余同时是 antd borderRadius 与 CSS --radius 的基准值。 */
export const RADIUS_STEPS = [0, 4, 8, 12] as const
export type Radius = (typeof RADIUS_STEPS)[number]

/** 主题色种子（十六进制）：预设之外的颜色由取色器给出。 */
export const DEFAULT_PRIMARY = "#273cf3"
const HEX_COLOR = /^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/

interface ThemeContextValue {
  /** 用户选择的主题（可能为 system）。 */
  theme: Theme
  /** 实际生效的主题，始终为 light 或 dark。 */
  resolvedTheme: "light" | "dark"
  setTheme: (theme: Theme) => void
  /** 在 light / dark 之间切换（基于当前生效主题）。 */
  toggleTheme: () => void
  /** 用户选择的密度。 */
  density: Density
  setDensity: (density: Density) => void
  /** 圆角档位，落到 antd token 与 --radius 两处。 */
  radius: Radius
  setRadius: (radius: Radius) => void
  /** 主题色，落到 antd colorPrimary 与 --seed-primary。 */
  primary: string
  setPrimary: (primary: string) => void
}

const THEME_KEY = "kuaiji.theme"
const DENSITY_KEY = "kuaiji.density"
const RADIUS_KEY = "kuaiji.radius"
const PRIMARY_KEY = "kuaiji.primary"
const DARK_QUERY = "(prefers-color-scheme: dark)"

const ThemeContext = createContext<ThemeContextValue | null>(null)

function getSystemTheme(): "light" | "dark" {
  return window.matchMedia(DARK_QUERY).matches ? "dark" : "light"
}

function readStored<T extends string>(key: string, allowed: readonly T[], fallback: T): T {
  try {
    const stored = localStorage.getItem(key)
    if (stored && (allowed as readonly string[]).includes(stored)) return stored as T
  } catch {
    // 忽略无法访问 localStorage 的情况
  }
  return fallback
}

function writeStored(key: string, value: string) {
  try {
    localStorage.setItem(key, value)
  } catch {
    // 忽略无法写入 localStorage 的情况
  }
}

/** 只接受十六进制颜色，避免存储里写进任意 CSS 值。 */
function readStoredPrimary(key: string, fallback: string): string {
  try {
    const stored = localStorage.getItem(key)
    if (stored && HEX_COLOR.test(stored)) return stored
  } catch {
    // 忽略无法访问 localStorage 的情况
  }
  return fallback
}

/** 圆角以字符串存储，读回时校验在档位表内。 */
function readStoredRadius(fallback: Radius): Radius {
  const stored = readStored(RADIUS_KEY, RADIUS_STEPS.map(String), String(fallback))
  return Number(stored) as Radius
}

/** 把主题落到 <html>：dark class 驱动 Tailwind/手写样式，color-scheme 让滚动条与原生控件一起变暗。 */
function applyTheme(theme: Theme): "light" | "dark" {
  const resolved = theme === "system" ? getSystemTheme() : theme
  document.documentElement.classList.toggle("dark", resolved === "dark")
  document.documentElement.style.colorScheme = resolved
  return resolved
}

/** 把圆角与主题色落到 <html> 的 CSS 变量：index.css 的 rounded-* 档位与品牌派生色都由这两个变量算出。 */
function applyTokens(radius: Radius, primary: string) {
  document.documentElement.style.setProperty("--seed-radius", `${radius}px`)
  document.documentElement.style.setProperty("--seed-primary", primary)
}

export function ThemeProvider({
  children,
  defaultTheme = "light",
  defaultDensity = "default",
  defaultRadius = 0,
  defaultPrimary = DEFAULT_PRIMARY,
}: {
  children: ReactNode
  defaultTheme?: Theme
  defaultDensity?: Density
  defaultRadius?: Radius
  defaultPrimary?: string
}) {
  const [theme, setThemeState] = useState<Theme>(() =>
    readStored(THEME_KEY, ["light", "dark", "system"] as const, defaultTheme),
  )
  const [density, setDensityState] = useState<Density>(() =>
    readStored(DENSITY_KEY, ["default", "compact"] as const, defaultDensity),
  )
  // 首次渲染就把 class 打上，避免刷新时先闪一帧浅色。
  const [resolvedTheme, setResolvedTheme] = useState<"light" | "dark">(() =>
    applyTheme(readStored(THEME_KEY, ["light", "dark", "system"] as const, defaultTheme)),
  )
  // 圆角与主题色同样落到 <html> 的 CSS 变量上；首帧的取值由 index.html 的防闪烁脚本抢先写好。
  const [radius, setRadiusState] = useState<Radius>(() => readStoredRadius(defaultRadius))
  const [primary, setPrimaryState] = useState<string>(() => readStoredPrimary(PRIMARY_KEY, defaultPrimary))

  // 主题变化时更新 <html> 的 class。
  useEffect(() => {
    setResolvedTheme(applyTheme(theme))
  }, [theme])

  // 圆角 / 主题色变化时更新 <html> 上的 CSS 变量。
  useEffect(() => {
    applyTokens(radius, primary)
  }, [radius, primary])

  // 仅在 system 模式下跟随系统偏好变化。
  useEffect(() => {
    if (theme !== "system") return
    const mql = window.matchMedia(DARK_QUERY)
    const handler = () => setResolvedTheme(applyTheme("system"))
    mql.addEventListener("change", handler)
    return () => mql.removeEventListener("change", handler)
  }, [theme])

  const setTheme = (next: Theme) => {
    writeStored(THEME_KEY, next)
    setThemeState(next)
  }

  const setDensity = (next: Density) => {
    writeStored(DENSITY_KEY, next)
    setDensityState(next)
  }

  const setRadius = (next: Radius) => {
    writeStored(RADIUS_KEY, String(next))
    setRadiusState(next)
  }

  const setPrimary = (next: string) => {
    if (!HEX_COLOR.test(next)) return
    writeStored(PRIMARY_KEY, next)
    setPrimaryState(next)
  }

  const toggleTheme = () => {
    setTheme(resolvedTheme === "dark" ? "light" : "dark")
  }

  return (
    <ThemeContext
      value={{
        theme,
        resolvedTheme,
        setTheme,
        toggleTheme,
        density,
        setDensity,
        radius,
        setRadius,
        primary,
        setPrimary,
      }}
    >
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
