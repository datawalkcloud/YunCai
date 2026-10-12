import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

/** 浅灰信息胶囊：小标签 + 取值，用于账页页头与凭证详情。 */
export function Chip({
    label,
    value,
    className,
}: {
    label: string
    value?: ReactNode
    className?: string
}) {
    return (
        <span
            className={cn(
                "inline-flex items-center gap-1.5 rounded-lg bg-muted px-2.5 py-1 text-[12px]",
                className,
            )}
        >
            <span className="text-slate-400">{label}</span>
            <span className="font-medium text-slate-700">{value || "—"}</span>
        </span>
    )
}
