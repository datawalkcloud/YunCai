import type { ReactNode } from "react"

import { cn } from "@/lib/utils"
import { PILL, type PillTone } from "./styles"

/** 状态胶囊：账页与凭证列表共用的小号圆角标签。 */
export function StatusPill({
    tone,
    children,
    className,
}: {
    tone: PillTone
    children: ReactNode
    className?: string
}) {
    return (
        <span
            className={cn(
                "inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-[11px] font-medium",
                PILL[tone],
                className,
            )}
        >
            {children}
        </span>
    )
}
