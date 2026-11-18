import { useEffect, useState } from "react"

import { Alert, Drawer, Spin, Table } from "antd"
import type { ColumnsType } from "antd/es/table"

import { type VoucherDetailDto, type VoucherEntryDto, VouchersApi } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { cn } from "@/lib/utils"
import { Chip } from "@/components/page/chip"
import { StatusPill } from "@/components/page/status-pill"
import { voucherStatusTone } from "@/components/page/styles"
import { dayKey } from "./useVoucherLookup"

export interface VoucherDetailSheetProps {
    accountSetId: string
    token?: string | null
    /** 待查看的凭证 id；为空表示抽屉关闭。 */
    voucherId: string | null
    onOpenChange: (open: boolean) => void
}

const amountCol = { align: "right", className: "tabular-nums" } as const

const ENTRY_COLUMNS: ColumnsType<VoucherEntryDto> = [
    { title: "行次", dataIndex: "lineNumber", width: 56 },
    { title: "摘要", dataIndex: "summary" },
    {
        title: "科目",
        render: (_, entry) => (
            <>
                <span className="text-(--muted-foreground)">{entry.accountCode} </span>
                {entry.accountName}
            </>
        ),
    },
    {
        title: "辅助核算",
        render: (_, entry) => (
            <span className="text-(--muted-foreground)">
                {entry.assists?.map((a) => `${a.typeText}：${a.itemName}`).join("、") ?? ""}
            </span>
        ),
    },
    { title: "借方", dataIndex: "debit", ...amountCol, render: (v) => fmtAmount(v) },
    { title: "贷方", dataIndex: "credit", ...amountCol, render: (v) => fmtAmount(v) },
]

/**
 * 凭证详情抽屉：正文即整张凭证（凭证头 + 全部分录 + 辅助核算 + 合计），
 * 供明细账、序时账、多栏账等账页行点击下钻复用。
 */
export function VoucherDetailSheet({
    accountSetId,
    token,
    voucherId,
    onOpenChange,
}: VoucherDetailSheetProps) {
    const [detail, setDetail] = useState<VoucherDetailDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        if (!voucherId) {
            setDetail(null)
            setError(null)
            return
        }
        let cancelled = false
        setLoading(true)
        setError(null)
        new VouchersApi(apiConfig(token))
            .accountsetsAccountSetIdVouchersIdGet({ accountSetId, id: voucherId })
            .then((res) => {
                if (!cancelled) setDetail(res)
            })
            .catch(async (err) => {
                if (cancelled) return
                setDetail(null)
                setError(await apiErrorMessage(err, "加载凭证详情失败，请重试。"))
            })
            .finally(() => {
                if (!cancelled) setLoading(false)
            })
        return () => {
            cancelled = true
        }
    }, [accountSetId, token, voucherId])

    const entries = detail?.entries ?? []

    return (
        <Drawer
            open={Boolean(voucherId)}
            onClose={() => onOpenChange(false)}
            size={896}
            title={`凭证 ${detail?.voucherNo ?? ""}`}
            styles={{ body: { display: "flex", flexDirection: "column", gap: 16 } }}
        >
            {loading && (
                <div className="flex justify-center py-8">
                    <Spin />
                </div>
            )}
            {!loading && error && <Alert type="error" showIcon title={error} />}
            {!loading && !error && detail && (
                <>
                    <div className="flex flex-wrap items-center gap-1.5">
                        <Chip label="日期" value={dayKey(detail.date)} />
                        <Chip label="附件" value={`${detail.attachmentCount ?? 0} 张`} />
                        <Chip label="制单人" value={detail.preparer} />
                        <Chip label="审核人" value={detail.reviewer} />
                        <StatusPill tone={voucherStatusTone(detail.status, detail.isBalanced)}>
                            {detail.statusText}
                        </StatusPill>
                    </div>

                    <Table
                        columns={ENTRY_COLUMNS}
                        dataSource={entries}
                        rowKey={(row) => row.id ?? String(row.lineNumber)}
                        pagination={false}
                        summary={() => (
                            <Table.Summary.Row>
                                <Table.Summary.Cell index={0} colSpan={4}>
                                    合计
                                </Table.Summary.Cell>
                                <Table.Summary.Cell index={4} align="right" className="tabular-nums">
                                    {fmtAmount(detail.totalDebit, true)}
                                </Table.Summary.Cell>
                                <Table.Summary.Cell index={5} align="right" className="tabular-nums">
                                    {fmtAmount(detail.totalCredit, true)}
                                </Table.Summary.Cell>
                            </Table.Summary.Row>
                        )}
                    />

                    <span
                        className={cn(
                            "text-[12.5px] text-(--muted-foreground)",
                            !detail.isBalanced && "text-(--danger)",
                        )}
                    >
                        {detail.isBalanced ? "借贷平衡" : "借贷不平衡，请核对凭证分录。"}
                    </span>
                </>
            )}
        </Drawer>
    )
}
