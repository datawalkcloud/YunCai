import type { ComponentType, ReactNode } from "react"
import {
    ApartmentOutlined,
    AuditOutlined,
    BookOutlined,
    CalculatorOutlined,
    ColumnWidthOutlined,
    FieldTimeOutlined,
    FolderOpenOutlined,
    OrderedListOutlined,
    ProfileOutlined,
    TableOutlined,
    AccountBookOutlined,
} from "@ant-design/icons"
import { Alert } from "antd"

import type { LedgerMetaDto } from "@/apis-gen"
import { PageHeader, SectionCard } from "@/components/page/section"
import { LedgerMetaHeader } from "./LedgerMetaHeader"
import { LedgerTabs } from "./LedgerTabs"

/** 各账页标题图标，按账页标识取，缺省用通用账簿图标。 */
const PAGE_ICONS: Record<string, ComponentType<{ className?: string }>> = {
    "trial-balance": AuditOutlined,
    subsidiary: ApartmentOutlined,
    general: ProfileOutlined,
    journal: OrderedListOutlined,
    "voucher-summary": CalculatorOutlined,
    "multi-column": ColumnWidthOutlined,
    "assist-balance": AccountBookOutlined,
    "assist-subsidiary": FolderOpenOutlined,
    "assist-combine": TableOutlined,
    ageing: FieldTimeOutlined,
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
    /** 页头右侧动作（打印 / 导出等）。 */
    actions?: ReactNode
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
    actions,
    children,
}: LedgerPageShellProps) {
    return (
        <div className="flex flex-col gap-4">
            <PageHeader icon={PAGE_ICONS[page] ?? BookOutlined} title={title} subtitle={hint} actions={actions} />

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
