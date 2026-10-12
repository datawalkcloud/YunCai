import type { ReactNode } from "react"
import { type RowData, useTable } from "@tanstack/react-table"

import { cn } from "@/lib/utils"
import { Spinner } from "@/components/ui/spinner"
import { amountCls } from "./format"
import { type LedgerColumnDefs, ledgerFeatures } from "./types"
import {
    Table,
    TableBody,
    TableCell,
    TableFooter,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table"

export interface LedgerTableProps<D extends RowData> {    columns: LedgerColumnDefs<D>
    data: D[]
    /** 请求进行中且无数据时展示加载占位。 */
    loading?: boolean
    /** 无数据时的提示文案。 */
    emptyText?: string
    /** 行键；缺省时按顺序号取，账页有稳定标识时应传入。 */
    getRowId?: (row: D, index: number) => string
    /** 行点击（下钻）；未提供时行不可点击。 */
    onRowClick?: (row: D, index: number) => void
    /** 判断某列是否为金额列（决定右对齐等宽数字）。 */
    isAmountColumn?: (columnId: string) => boolean
    /** 行级附加样式，用于区分期初 / 合计行。 */
    rowClassName?: (row: D, index: number) => string | undefined
    /** 合计行等自定义页脚。 */
    footer?: ReactNode
}

/**
 * 账页通用表格外壳：只负责多级表头、空态、加载态与金额列对齐，不含任何账页业务字段。
 */
export function LedgerTable<D extends RowData>({
    columns,
    data,
    loading,
    emptyText = "暂无数据。",
    getRowId,
    onRowClick,
    isAmountColumn,
    rowClassName,
    footer,
}: LedgerTableProps<D>) {
    const table = useTable({ features: ledgerFeatures, columns, data, getRowId })
    const leafCount = table.getLeafHeaders().length
    const cellCls = (columnId: string) => cn(isAmountColumn?.(columnId) && amountCls)

    return (
        <Table>
            <TableHeader>
                {table.getHeaderGroups().map((headerGroup) => (
                    <TableRow key={headerGroup.id}>
                        {headerGroup.headers.map((header) =>
                            // rowSpan 为 0 表示被上层跨行表头覆盖，跳过该占位表头。
                            header.rowSpan === 0 ? null : (
                                <TableHead
                                    key={header.id}
                                    colSpan={header.colSpan}
                                    rowSpan={header.rowSpan}
                                    className={cellCls(header.column.id)}
                                >
                                    <table.FlexRender header={header} />
                                </TableHead>
                            ),
                        )}
                    </TableRow>
                ))}
            </TableHeader>
            <TableBody>
                {loading && data.length === 0 ? (
                    <TableRow className="hover:bg-transparent">
                        <TableCell colSpan={leafCount} className="py-8 text-center">
                            <Spinner className="mx-auto size-5 text-slate-300" />
                        </TableCell>
                    </TableRow>
                ) : data.length === 0 ? (
                    <TableRow className="hover:bg-transparent">
                        <TableCell colSpan={leafCount} className="py-8 text-center text-slate-400">
                            {emptyText}
                        </TableCell>
                    </TableRow>
                ) : (
                    table.getRowModel().rows.map((row, index) => (
                        <TableRow
                            key={row.id}
                            className={cn(
                                rowClassName?.(row.original, index),
                                onRowClick && "cursor-pointer hover:bg-[#f2f7ff]",
                            )}
                            onClick={onRowClick ? () => onRowClick(row.original, index) : undefined}
                        >
                            {row.getAllCells().map((cell) => (
                                <TableCell key={cell.id} className={cellCls(cell.column.id)}>
                                    <table.FlexRender cell={cell} />
                                </TableCell>
                            ))}
                        </TableRow>
                    ))
                )}
            </TableBody>
            {data.length > 0 && footer != null && <TableFooter>{footer}</TableFooter>}
        </Table>
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
            <div className="flex flex-wrap items-baseline justify-between gap-2 border-b border-slate-200 bg-muted px-4 py-2.5">
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
