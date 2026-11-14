import { useEffect, useState } from "react"
import { Alert, Drawer, Empty, Table } from "antd"
import type { TableColumnsType } from "antd"

import {
    getReportLineDrilldown,
    type ReportDrilldownAccountDto,
    type ReportDrilldownDto,
    type ReportDrilldownKind,
} from "@/api/report-drilldown"
import { apiErrorMessage } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { AMOUNT_COL } from "@/components/page/styles"

interface Props {
    open: boolean
    onClose: () => void
    accountSetId: string
    token?: string | null
    kind: ReportDrilldownKind
    year: number
    month: number
    line: number | null
    /** 报表行名称，用作抽屉标题。 */
    title: string
    /** 主金额列标题（资产负债表「期末余额」/ 利润表「本期金额」）。 */
    currentLabel: string
    /** 参照金额列标题（资产负债表「年初余额」/ 利润表「本年累计」）。 */
    yearToDateLabel: string
    /** 点科目行进入该科目的明细账。 */
    onOpenLedger: (code: string) => void
}

/**
 * 报表行下钻浮层：列出该行次由哪些科目组成，点科目继续进明细账。
 * 取数与报表同源（后端同一套逐科目净额与归集），合计与报表行金额天然一致。
 */
export function ReportDrilldownDrawer({
    open,
    onClose,
    accountSetId,
    token,
    kind,
    year,
    month,
    line,
    title,
    currentLabel,
    yearToDateLabel,
    onOpenLedger,
}: Props) {
    const [data, setData] = useState<ReportDrilldownDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        if (!open || !accountSetId || line == null) return
        let cancelled = false
        setLoading(true)
        setError(null)
        setData(null)
        getReportLineDrilldown(accountSetId, kind, { year, month, line }, token)
            .then((res) => {
                if (!cancelled) setData(res)
            })
            .catch(async (err) => {
                if (!cancelled) setError(await apiErrorMessage(err, "加载科目明细失败，请重试。"))
            })
            .finally(() => {
                if (!cancelled) setLoading(false)
            })
        return () => {
            cancelled = true
        }
    }, [open, accountSetId, token, kind, year, month, line])

    const columns: TableColumnsType<ReportDrilldownAccountDto> = [
        {
            title: "科目编码",
            dataIndex: "code",
            width: 110,
            render: (code: string) => <span className="tabular-nums text-(--brand)">{code}</span>,
        },
        {
            title: "科目名称",
            dataIndex: "name",
            render: (name: string, row) => (
                <span className="cursor-pointer text-(--brand) hover:underline" onClick={() => onOpenLedger(row.code)}>
                    {name}
                </span>
            ),
        },
        {
            title: currentLabel,
            dataIndex: "current",
            width: 130,
            ...AMOUNT_COL,
            render: (value: number) => fmtAmount(value, true),
        },
        {
            title: yearToDateLabel,
            dataIndex: "yearToDate",
            width: 130,
            ...AMOUNT_COL,
            render: (value: number) => fmtAmount(value, true),
        },
    ]

    return (
        <Drawer
            open={open}
            onClose={onClose}
            size={640}
            title={title}
            styles={{ body: { display: "flex", flexDirection: "column", gap: 12 } }}
        >
            <div className="text-[12.5px] text-(--muted-foreground)">
                {data ? `${data.periodText} · 点科目名称进入该科目明细账` : "加载中…"}
            </div>
            {error && <Alert type="error" showIcon title={error} />}
            <Table<ReportDrilldownAccountDto>
                columns={columns}
                dataSource={data?.accounts ?? []}
                rowKey="accountId"
                loading={loading}
                pagination={false}
                size="small"
                locale={{ emptyText: <Empty description="该行次暂无科目发生额。" /> }}
                onRow={(row) => ({
                    onClick: () => onOpenLedger(row.code),
                    className: "cursor-pointer",
                })}
                summary={() =>
                    data && data.accounts.length > 0 ? (
                        <Table.Summary.Row>
                            <Table.Summary.Cell index={0} colSpan={2}>
                                合计
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={2} {...AMOUNT_COL}>
                                {fmtAmount(data.totalCurrent, true)}
                            </Table.Summary.Cell>
                            <Table.Summary.Cell index={3} {...AMOUNT_COL}>
                                {fmtAmount(data.totalYearToDate, true)}
                            </Table.Summary.Cell>
                        </Table.Summary.Row>
                    ) : null
                }
            />
        </Drawer>
    )
}
