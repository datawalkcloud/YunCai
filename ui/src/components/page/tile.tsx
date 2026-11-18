import { cn } from "@/lib/utils"
import { useTheme } from "@/components/theme/theme-provider"
import { ACCENT, ACCENT_BG } from "./styles"

export type TileTone = keyof typeof ACCENT

/**
 * 粉彩指标瓦片：浅色底 + 强调色图标块 + 大数字，与首页统计卡同一视觉。
 * ratio 传 0~1 时底部显示占比条，hint 作为补充说明。
 */
export function Tile({
    label,
    value,
    tone = "blue",
    color = ACCENT[tone],
    bg = ACCENT_BG[tone],
    icon: Icon,
    hint,
    ratio,
    className,
}: {
    label: string
    value: string | number
    /** 强调色档位，决定默认配色；显式传 color / bg 可覆盖。 */
    tone?: TileTone
    color?: string
    bg?: string
    icon: React.ComponentType<{ className?: string }>
    hint?: string
    ratio?: number
    className?: string
}) {
    const { resolvedTheme } = useTheme()
    // 粉彩底在暗色下会发荧光：把原底色按低比例混进卡片色，保留色相、压回亮度。
    const surface = resolvedTheme === "dark" ? `color-mix(in srgb, ${bg} 18%, var(--card))` : bg

    return (
        <div className={cn("p-4", className)} style={{ background: surface }}>
            <div className="flex items-center gap-2.5">
                <span
                    className="flex size-8 shrink-0 items-center justify-center text-white shadow-sm"
                    style={{ background: color }}
                >
                    <Icon className="size-4" />
                </span>
                <span className="truncate text-[12.5px] font-medium" style={{ color }}>
                    {label}
                </span>
            </div>
            <div className="mt-3 text-[21px] leading-none font-bold tracking-tight text-(--foreground) tabular-nums">
                {value}
            </div>
            {ratio != null ? (
                <div className="mt-3 flex items-center gap-2">
                    <span className="h-1 flex-1 overflow-hidden rounded-full bg-(--card)/70">
                        <span
                            className="block h-full rounded-full"
                            style={{ width: `${Math.min(100, Math.max(0, Math.round(ratio * 100)))}%`, background: color }}
                        />
                    </span>
                    {hint && <span className="shrink-0 text-[11px] text-(--muted-foreground)">{hint}</span>}
                </div>
            ) : (
                hint && <div className="mt-2 text-[11px] text-(--muted-foreground)">{hint}</div>
            )}
        </div>
    )
}
