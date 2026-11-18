import type { ReactNode } from "react"

import { cn } from "@/lib/utils"

/** 浅灰信息标签：小标签 + 取值，用于账页页头与凭证详情。 */
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
                "inline-flex items-center gap-1.5 rounded-md bg-muted px-2.5 py-1 text-[12px]",
                className,
            )}
        >
            <span className="text-(--muted-foreground)">{label}</span>
            <span className="font-medium text-(--foreground)">{value || "—"}</span>
        </span>
    )
}
