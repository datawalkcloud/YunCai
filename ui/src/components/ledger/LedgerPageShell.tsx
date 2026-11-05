import type { ReactNode } from "react"
import { Alert } from "antd"

import type { LedgerMetaDto } from "@/apis-gen"
import { SectionCard } from "@/components/page/section"
import { LedgerMetaHeader } from "./LedgerMetaHeader"
import { LedgerTabs } from "./LedgerTabs"

export interface LedgerPageShellProps {
    /** 当前账页标识，用于 Tab 高亮与互相切换。 */
    page: string
    /** 查询面板。 */
    panel?: ReactNode
    meta?: LedgerMetaDto | null
    /** 接口返回的中文错误，直接展示。 */
    error?: string | null
    children: ReactNode
}

/**
 * 账页统一骨架：账页 Tab + 查询面板 + 公共页头 + 正文。
 * 金额单位固定为元，故在正文下方统一标注。
 */
export function LedgerPageShell({ page, panel, meta, error, children }: LedgerPageShellProps) {
    return (
        <div className="flex flex-col gap-4">
            <LedgerTabs current={page} />

            <SectionCard bodyClassName="flex flex-col gap-4">
                {panel}
                <LedgerMetaHeader meta={meta} />
                {error && <Alert type="error" showIcon title={error} />}
                {children}
                <span className="text-[12px] text-slate-400">单位：元</span>
            </SectionCard>
        </div>
    )
}
