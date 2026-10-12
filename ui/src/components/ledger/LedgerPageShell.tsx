import type { ComponentType, ReactNode } from "react"
import {
    BookOpen,
    Briefcase,
    Calculator,
    Columns2,
    FolderTree,
    Hourglass,
    Layers,
    LayoutGrid,
    ListOrdered,
    ListTree,
    Scale,
    TriangleAlert,
} from "lucide-react"

import type { LedgerMetaDto } from "@/apis-gen"
import { Alert, AlertTitle } from "@/components/ui/alert"
import { PageHeader, SectionCard } from "@/components/page/section"
import { LedgerMetaHeader } from "./LedgerMetaHeader"
import { LedgerTabs } from "./LedgerTabs"

/** 各账页标题图标，按账页标识取，缺省用通用账簿图标。 */
const PAGE_ICONS: Record<string, ComponentType<{ className?: string }>> = {
    "trial-balance": Scale,
    subsidiary: ListTree,
    general: Layers,
    journal: ListOrdered,
    "voucher-summary": Calculator,
    "multi-column": Columns2,
    "assist-balance": Briefcase,
    "assist-subsidiary": FolderTree,
    "assist-combine": LayoutGrid,
    ageing: Hourglass,
}

export interface LedgerPageShellProps {
    /** 当前账页标识，用于 Tab 高亮与互相切换。 */
    page: string
    title: string
    /** 一句话说明本页列示什么。 */
    hint?: string
    /** 查询面板。 */
    panel?: ReactNode
    meta?: LedgerMetaDto | null
    /** 接口返回的中文错误，直接展示。 */
    error?: string | null
    children: ReactNode
}

/**
 * 账页统一骨架：页头 + 账页 Tab + 查询面板 + 公共页头 + 正文。
 * 金额单位固定为元，故在正文下方统一标注。
 */
export function LedgerPageShell({
    page,
    title,
    hint,
    panel,
    meta,
    error,
    children,
}: LedgerPageShellProps) {
    return (
        <div className="flex flex-col gap-4">
            <PageHeader icon={PAGE_ICONS[page] ?? BookOpen} title={title} subtitle={hint} />

            <LedgerTabs current={page} />

            <SectionCard bodyClassName="flex flex-col gap-4">
                {panel}
                <LedgerMetaHeader meta={meta} />
                {error && (
                    <Alert
                        variant="destructive"
                        className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]"
                    >
                        <TriangleAlert />
                        <AlertTitle>{error}</AlertTitle>
                    </Alert>
                )}
                {children}
                <span className="text-[12px] text-slate-400">单位：元</span>
            </SectionCard>
        </div>
    )
}
