import { Table } from "antd"
import type { TableColumnsType } from "antd"
import { EditOutlined } from "@ant-design/icons"

import { amountToChineseUppercase } from "@/lib/amount-uppercase"
import { AmountCells, AmountHeaderCell } from "@/components/voucher/amount-grid"

/** 纸样尺寸：表头 60，分录行与合计行 70，与凭证纸面比例一致。 */
export const VOUCHER_HEAD_H = 60
export const VOUCHER_ROW_H = 70

/** 借 / 贷金额栏：11 个分位格，每格 20px。 */
export const VOUCHER_AMOUNT_WIDTH = 220

/** 分录行（只读）：余额为该行过账后的科目余额，不传则不显示。 */
export interface VoucherPaperLine {
    key: string
    summary: string
    accountCode: string
    accountName: string
    debit: number
    credit: number
    balance?: number
}

export interface VoucherPaperProps {
    word: string
    number?: number | null
    /** 已格式化的日期（yyyy-MM-dd）。 */
    date: string
    lines: VoucherPaperLine[]
    totalDebit: number
    totalCredit: number
    preparer?: string
    loading?: boolean
}

/** 纸样表头标题：整格居中，与金额列的两段式表头等高。 */
function PaperHeadTitle({ text }: { text: string }) {
    return (
        <div className="flex items-center justify-center" style={{ height: VOUCHER_HEAD_H }}>
            {text}
        </div>
    )
}

const columns: TableColumnsType<VoucherPaperLine> = [
    {
        title: <PaperHeadTitle text="序号" />,
        width: 33,
        onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
        onCell: () => ({ className: "pk-sheet-cell", style: { height: VOUCHER_ROW_H } }),
        render: (_, _row, index) => (
            <div className="flex h-full items-center justify-center text-[13px] tabular-nums text-(--muted-foreground)">
                {index + 1}
            </div>
        ),
    },
    {
        title: <PaperHeadTitle text="摘要" />,
        width: 260,
        onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
        onCell: () => ({ className: "pk-sheet-cell", style: { height: VOUCHER_ROW_H } }),
        render: (_, row) => (
            <div className="flex h-full items-center px-3">
                <span className="truncate" title={row.summary}>
                    {row.summary}
                </span>
            </div>
        ),
    },
    {
        title: <PaperHeadTitle text="会计科目" />,
        onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
        onCell: () => ({ className: "pk-sheet-cell", style: { height: VOUCHER_ROW_H } }),
        render: (_, row) => (
            <div className="flex h-full flex-col justify-center gap-0.5 px-3">
                <span className="truncate" title={`${row.accountCode} ${row.accountName}`}>
                    {row.accountCode} {row.accountName}
                </span>
                {row.balance != null && (
                    <span className="text-[12px] leading-4 tabular-nums text-(--muted-foreground)">
                        余额：{row.balance.toFixed(2)}
                    </span>
                )}
            </div>
        ),
    },
    {
        title: <AmountHeaderCell title="借方金额" idPrefix="paper-debit" height={VOUCHER_HEAD_H} divider />,
        width: VOUCHER_AMOUNT_WIDTH,
        onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
        onCell: () => ({ className: "pk-sheet-cell" }),
        render: (_, row) => <AmountCells value={row.debit} height={VOUCHER_ROW_H} />,
    },
    {
        title: <AmountHeaderCell title="贷方金额" idPrefix="paper-credit" height={VOUCHER_HEAD_H} divider />,
        width: VOUCHER_AMOUNT_WIDTH,
        onHeaderCell: () => ({ className: "pk-sheet-head-cell" }),
        onCell: () => ({ className: "pk-sheet-cell" }),
        render: (_, row) => <AmountCells value={row.credit} height={VOUCHER_ROW_H} />,
    },
]

/**
 * 凭证纸样（只读）：与凭证录入页共用同一套纸面版式（`.voucher-sheet` 格线、分位金额栏、合计大写）。
 * 用于展示系统生成或已存凭证——录入交互仍走 VoucherSheet。
 */
export function VoucherPaper({
    word,
    number,
    date,
    lines,
    totalDebit,
    totalCredit,
    preparer,
    loading,
}: VoucherPaperProps) {
    const headerCell = "min-w-10 rounded border border-(--border) bg-(--card) px-2 py-0.5 text-center font-semibold text-(--foreground)"

    return (
        <div className="overflow-hidden rounded-lg border border-(--border)">
            <div className="flex flex-wrap items-center gap-x-6 gap-y-2 border-b border-(--border) bg-(--muted) px-3 py-2.5 text-[13px] text-(--muted-foreground)">
                <span className="flex items-center gap-1.5">
                    凭证字号
                    <span className={headerCell}>{word}</span>
                    {number != null && <span className={headerCell}>{number}</span>}号
                </span>
                <span className="flex items-center gap-1.5">
                    日期
                    <span className={headerCell}>{date}</span>
                </span>
            </div>
            <Table<VoucherPaperLine>
                size="small"
                pagination={false}
                tableLayout="fixed"
                className="voucher-sheet"
                // 窄屏保底宽度：科目列不压缩，改为整表横向滚动（抽屉 980 内仍能一屏放下）。
                scroll={{ x: 900 }}
                loading={loading}
                rowKey={(row) => row.key}
                rowClassName={() => "bg-(--card)"}
                columns={columns}
                dataSource={lines}
                summary={() => (
                    <Table.Summary.Row>
                        <Table.Summary.Cell index={0} colSpan={3} className="pk-sheet-label" align="left">
                            合计：{amountToChineseUppercase(totalDebit)}
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={3}>
                            <AmountCells value={totalDebit} height={VOUCHER_ROW_H} />
                        </Table.Summary.Cell>
                        <Table.Summary.Cell index={4}>
                            <AmountCells value={totalCredit} height={VOUCHER_ROW_H} />
                        </Table.Summary.Cell>
                    </Table.Summary.Row>
                )}
            />
            <div className="flex items-center gap-1.5 px-3 py-2 text-[13px] text-(--foreground)">
                <EditOutlined className="text-(--brand)" />
                制单人：{preparer || "—"}
            </div>
        </div>
    )
}
