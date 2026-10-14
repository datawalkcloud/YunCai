import { useEffect, useState } from "react"

import { type VoucherDetailDto, VouchersApi } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { cn } from "@/lib/utils"
import { Alert, AlertTitle } from "@/components/ui/alert"
import { Chip } from "@/components/page/chip"
import { StatusPill } from "@/components/page/status-pill"
import { voucherStatusTone } from "@/components/page/styles"
import { Spinner } from "@/components/ui/spinner"
import {
    Table,
    TableBody,
    TableCell,
    TableFooter,
    TableHead,
    TableHeader,
    TableRow,
} from "@/components/ui/table"
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet"
import { amountCls } from "./format"
import { dayKey } from "./useVoucherLookup"

export interface VoucherDetailSheetProps {
    accountSetId: string
    token?: string | null
    /** 待查看的凭证 id；为空表示抽屉关闭。 */
    voucherId: string | null
    onOpenChange: (open: boolean) => void
}

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
        <Sheet open={Boolean(voucherId)} onOpenChange={onOpenChange}>
            <SheetContent className="sm:max-w-4xl" aria-describedby={undefined}>
                <SheetHeader className="border-b border-slate-100">
                    <SheetTitle className="text-[15px] font-semibold text-slate-800">
                        凭证 {detail?.voucherNo ?? ""}
                    </SheetTitle>
                </SheetHeader>
                <div className="flex min-h-0 flex-1 flex-col gap-4 overflow-auto px-4 pb-4">
                    {loading && (
                        <div className="flex justify-center py-8">
                            <Spinner className="size-5 text-slate-300" />
                        </div>
                    )}
                    {!loading && error && (
                        <Alert
                            variant="destructive"
                            className="rounded-xl border-[#fecaca] bg-[#fef2f2] text-[#ef4444]"
                        >
                            <AlertTitle>{error}</AlertTitle>
                        </Alert>
                    )}
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

                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead className="w-12">行次</TableHead>
                                        <TableHead>摘要</TableHead>
                                        <TableHead>科目</TableHead>
                                        <TableHead>辅助核算</TableHead>
                                        <TableHead className={amountCls}>借方</TableHead>
                                        <TableHead className={amountCls}>贷方</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {entries.map((entry) => (
                                        <TableRow key={entry.id ?? entry.lineNumber}>
                                            <TableCell>{entry.lineNumber}</TableCell>
                                            <TableCell>{entry.summary}</TableCell>
                                            <TableCell>
                                                <span className="text-slate-400">
                                                    {entry.accountCode}{" "}
                                                </span>
                                                {entry.accountName}
                                            </TableCell>
                                            <TableCell className="text-slate-400">
                                                {entry.assists?.length
                                                    ? entry.assists
                                                          .map((a) => `${a.typeText}：${a.itemName}`)
                                                          .join("、")
                                                    : ""}
                                            </TableCell>
                                            <TableCell className={amountCls}>
                                                {fmtAmount(entry.debit)}
                                            </TableCell>
                                            <TableCell className={amountCls}>
                                                {fmtAmount(entry.credit)}
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                                <TableFooter>
                                    <TableRow>
                                        <TableCell colSpan={4}>合计</TableCell>
                                        <TableCell className={amountCls}>
                                            {fmtAmount(detail.totalDebit, true)}
                                        </TableCell>
                                        <TableCell className={amountCls}>
                                            {fmtAmount(detail.totalCredit, true)}
                                        </TableCell>
                                    </TableRow>
                                </TableFooter>
                            </Table>

                            <span
                                className={cn(
                                    "text-[12.5px] text-slate-400",
                                    !detail.isBalanced && "text-[#ef4444]",
                                )}
                            >
                                {detail.isBalanced
                                    ? "借贷平衡"
                                    : "借贷不平衡，请核对凭证分录。"}
                            </span>
                        </>
                    )}
                </div>
            </SheetContent>
        </Sheet>
    )
}
