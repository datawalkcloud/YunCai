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
    /** 自定义行事件；与 onRowClick 二选一，分组头等非下钻行用它。 */
    onRow?: TableProps<D>["onRow"]
    /** 行级附加样式，用于区分期初 / 合计行。 */
    rowClassName?: (row: D, index: number) => string | undefined
    /** 展开配置；科目余额表等树形账页用它控制级次展开。 */
    expandable?: TableProps<D>["expandable"]
    /** 合计行等自定义页脚，内部用 Table.Summary.Row / Cell。 */
    footer?: ReactNode
    /** 分页配置；不传时表格不分页，页脚只展示总条数。 */
    pagination?: TableProps<D>["pagination"]
    /** 表头吸顶（跟随内容区滚动），宽表默认开启；嵌在弹窗里的窄表可关闭。 */
    sticky?: boolean
    /** 表格下方「共 N 条」；树形账页与自带上报头的页面可关闭。 */
    showCount?: boolean
}

/** 树形数据按可见行计数：data.length 只是根节点数，会少报下级科目。 */
function countRows<D>(rows: D[]): number {
    let count = 0
    for (const row of rows) {
        count += 1
        const children = (row as { children?: D[] }).children
        if (children?.length) count += countRows(children)
    }
    return count
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
    onRow,
    rowClassName,
    expandable,
    footer,
    pagination,
    sticky = true,
    showCount = true,
}: LedgerTableProps<D>) {
    const showSpinner = Boolean(loading) && data.length === 0

    return (
        <div className="flex flex-col">
            <Table<D>
                columns={columns}
                dataSource={data}
                rowKey={rowKey}
                pagination={pagination ?? false}
                expandable={expandable}
                sticky={sticky}
                scroll={{ x: "max-content" }}
                rowClassName={(row, index) =>
                    cn(rowClassName?.(row, index), onRowClick && "cursor-pointer")
                }
                onRow={
                    onRow ??
                    (onRowClick
                        ? (row, index) => ({ onClick: () => onRowClick(row, index ?? 0) })
                        : undefined)
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
            {/* 不分页时只报总条数；此前固定显示「1 到 20」并画了翻不动的页码，
                50~500 行的账页会给出与实际不符的页脚。 */}
            {!pagination && showCount && (
                <div className="flex items-center px-4 py-2.5 text-[12.5px] text-(--muted-foreground)">
                    共 {countRows(data)} 条
                </div>
            )}
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
        <section className="flex flex-col overflow-hidden rounded-xl border border-(--border)">
            {/* 段标题条不画下边框：它紧贴表格上沿，再加一条线会被读成表格的外框上沿 */}
            <div className="flex flex-wrap items-baseline justify-between gap-2 bg-muted px-4 py-2.5">
                <h2 className="flex items-center gap-2 text-[13px] font-semibold text-(--foreground)">
                    <span className="h-3 w-1 rounded-full bg-(--brand-fill)" />
                    {heading}
                </h2>
                {extra && <span className="text-[12px] text-(--muted-foreground)">{extra}</span>}
            </div>
            <div className="bg-(--card)">{children}</div>
        </section>
    )
}
