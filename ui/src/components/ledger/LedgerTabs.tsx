import { useNavigate, useParams } from "react-router"

import { cn } from "@/lib/utils"
import { LEDGER_PAGES } from "./types"

/**
 * 账页之间的横向切换条。当前页由 URL 末段判定，切换时只更换账页段，
 * 保留 /:accountSetId 前缀，并清掉下钻带过来的查询参数。
 */
export function LedgerTabs({ current }: { current: string }) {
    const { accountSetId } = useParams<{ accountSetId: string }>()
    const navigate = useNavigate()

    return (
        <nav className="flex flex-wrap items-center gap-1.5 rounded-2xl border border-slate-100 bg-white p-1.5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            {LEDGER_PAGES.map((page) => {
                const active = page.path === current
                return (
                    <button
                        key={page.path}
                        type="button"
                        aria-current={active ? "page" : undefined}
                        onClick={() => navigate(`/${accountSetId}/ledger/${page.path}`)}
                        className={cn(
                            "rounded-full px-3 py-1.5 text-[12.5px] whitespace-nowrap transition-colors",
                            active
                                ? "bg-gradient-to-r from-[#5b9bff] to-[#2f6bff] font-medium text-white shadow-[0_4px_12px_rgba(47,107,255,0.25)]"
                                : "text-slate-500 hover:bg-[#eef4ff] hover:text-[#2f6bff]",
                        )}
                    >
                        {page.label}
                    </button>
                )
            })}
        </nav>
    )
}
