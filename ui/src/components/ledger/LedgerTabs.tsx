import { Segmented } from "antd"
import { useNavigate, useParams } from "react-router"

import { LEDGER_PAGES } from "./types"

/**
 * 账页之间的横向切换条。当前页由 URL 末段判定，切换时只更换账页段，
 * 保留 /:accountSetId 前缀，并清掉下钻带过来的查询参数。
 */
export function LedgerTabs({ current }: { current: string }) {
    const { accountSetId } = useParams<{ accountSetId: string }>()
    const navigate = useNavigate()

    return (
        <nav className="rounded-2xl border border-slate-100 bg-white p-1.5 shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
            <Segmented
                block
                options={LEDGER_PAGES.map((page) => ({ label: page.label, value: page.path }))}
                value={current}
                onChange={(next) => navigate(`/${accountSetId}/ledger/${next as string}`)}
            />
        </nav>
    )
}
