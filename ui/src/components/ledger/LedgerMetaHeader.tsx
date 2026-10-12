import { TriangleAlert } from "lucide-react"

import type { LedgerMetaDto } from "@/apis-gen"
import { Chip } from "@/components/page/chip"

/**
 * 账页公共页头：账套、记账单位、查询期间与结账进度；
 * 区间内存在未过账凭证时给出显式提示（账簿只统计已过账凭证）。
 */
export function LedgerMetaHeader({ meta }: { meta?: LedgerMetaDto | null }) {
    if (!meta) return null
    const unposted = meta.unpostedVoucherCount ?? 0

    return (
        <div className="flex flex-col gap-2">
            <div className="flex flex-wrap items-center gap-1.5">
                <Chip label="账套" value={meta.accountSetName} />
                <Chip label="记账单位" value={meta.companyName} />
                <Chip label="期间" value={meta.periodText} />
                <Chip
                    label="已结账期间"
                    value={
                        meta.closedPeriodText
                            ? `${meta.closedPeriodText}${meta.isEndPeriodClosed ? "（截止期间已结账）" : ""}`
                            : "尚未结账"
                    }
                />
            </div>
            {unposted > 0 && (
                <div className="flex items-start gap-2 rounded-xl border border-[#fde68a] bg-[#fef3c7] px-3 py-2 text-[12.5px] text-[#b45309]">
                    <TriangleAlert className="mt-0.5 size-4 shrink-0" />
                    <span>
                        {meta.periodText} 内有 {unposted} 张未过账凭证未纳入账簿，请先在凭证页完成审核并过账。
                    </span>
                </div>
            )}
        </div>
    )
}
