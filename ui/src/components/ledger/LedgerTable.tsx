import type { ReactNode } from "react"
import { Spin, Table } from "antd"
import type { TableColumnsType, TableProps } from "antd"

import { cn } from "@/lib/utils"

export interface LedgerTableProps<D> {
    columns: TableColumnsType<D>
    data: D[]
    /** 请求进行中且无数据时，在表体展示加载占位。 */
    loading?: boolean
    /** 无数据时的提示文案。 */
    emptyText?: string
    /** 行键；账页行类型一般自带 rowKey。 */
    rowKey: (row: D) => string
    /** 行点击（下钻）；未提供时行不显示手型光标。 */
    onRowClick?: (row: D, index: number) => void
    /** 行级附加样式，用于区分期初 / 合计行。 */
    rowClassName?: (row: D, index: number) => string | undefined
    /** 展开配置；科目余额表等树形账页用它控制级次展开。 */
    expandable?: TableProps<D>["expandable"]
    /** 合计行等自定义页脚，内部用 Table.Summary.Row / Cell。 */
    footer?: ReactNode
}

/**
 * 账页通用表格外壳：只负责多级表头、空态、加载态与行点击，不含任何账页业务字段。
 */
export function LedgerTable<D>({
    columns,
    data,
    loading,
    emptyText = "暂无数据。",
    rowKey,
    onRowClick,
    rowClassName,
    expandable,
    footer,
}: LedgerTableProps<D>) {
    const showSpinner = Boolean(loading) && data.length === 0

    return (
        <div className="flex flex-col">
            <Table<D>
                columns={columns}
                dataSource={data}
                rowKey={rowKey}
                pagination={false}
                expandable={expandable}
                rowClassName={(row, index) =>
                    cn(rowClassName?.(row, index), onRowClick && "cursor-pointer")
                }
                onRow={
                    onRowClick
                        ? (row, index) => ({ onClick: () => onRowClick(row, index ?? 0) })
                        : undefined
                }
                locale={{
                    emptyText: showSpinner ? (
                        <div className="flex justify-center py-6">
                            <Spin />
                        </div>
                    ) : (
                        emptyText
                    ),
                }}
                summary={
                    footer == null || data.length === 0
                        ? undefined
                        : () => <Table.Summary>{footer}</Table.Summary>
                }
            />
            {/* 页脚：与柠檬云一致的「显示 x 到 y，共 z 条」；不画上边框 —— 表格末行已有分隔线，再加一条会叠成双线 */}
            <div className="flex items-center justify-between px-4 py-2.5 text-[12.5px] text-slate-500">
                <span>
                    显示 {data.length ? 1 : 0} 到 {Math.min(data.length, 20)}，共 {data.length} 条
                </span>
                <span className="flex items-center gap-2">
                    <span className="inline-flex h-7 items-center rounded border border-slate-200 px-2">
                        20
                    </span>
                    <span className="inline-flex h-7 items-center rounded border border-slate-200 px-2 text-slate-400">
                        ‹
                    </span>
                    <span className="inline-flex h-7 w-8 items-center justify-center rounded border border-[#2f6bff] text-[#2f6bff]">
                        1
                    </span>
                    <span className="inline-flex h-7 items-center rounded border border-slate-200 px-2 text-slate-400">
                        ›
                    </span>
                </span>
            </div>
        </div>
    )
}

export interface LedgerSectionProps {
    /** 段标题，如「1001 库存现金」。 */
    heading: ReactNode
    /** 标题右侧的补充信息，如借贷发生额合计。 */
    extra?: ReactNode
    children: ReactNode
}

/** 账页分段容器：明细账、总账等按科目分段的账页用它包住每一段。 */
export function LedgerSection({ heading, extra, children }: LedgerSectionProps) {
    return (
        <section className="flex flex-col overflow-hidden rounded-xl border border-slate-100">
            {/* 段标题条不画下边框：它紧贴表格上沿，再加一条线会被读成表格的外框上沿 */}
            <div className="flex flex-wrap items-baseline justify-between gap-2 bg-muted px-4 py-2.5">
                <h2 className="flex items-center gap-2 text-[13px] font-semibold text-slate-800">
                    <span className="h-3 w-1 rounded-full bg-[#2f6bff]" />
                    {heading}
                </h2>
                {extra && <span className="text-[12px] text-slate-400">{extra}</span>}
            </div>
            <div className="bg-white">{children}</div>
        </section>
    )
}
