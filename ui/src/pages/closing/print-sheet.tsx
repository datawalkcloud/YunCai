import { useEffect, useMemo, useState } from "react"
import { DownloadOutlined, PrinterOutlined } from "@ant-design/icons"
import { Alert, Button, Drawer, Spin, message } from "antd"

import { VouchersApi, type VoucherDetailDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount, toISODate } from "@/lib/format"
import { printPdf } from "@/lib/print-pdf"

/**
 * 凭证打印：版式（纸张、边距、行数、表尾签章）全部由后端 VoucherPdfBuilder 按 VoucherPdfRequest 的默认值渲染，
 * 前端只决定打印哪些凭证——「打印」在新窗口打开 PDF 预览，「导出」下载同一份 PDF。
 */
export function PrintVoucherSheet({
    accountSetId,
    token,
    voucherId,
    voucherIds,
    open,
    onOpenChange,
}: {
    accountSetId: string
    token?: string | null
    voucherId: string | null
    /** 连续打印：给定时按顺序打印这些凭证，忽略 voucherId。 */
    voucherIds?: string[]
    open: boolean
    onOpenChange: (open: boolean) => void
}) {
    const vouchersApi = useMemo(() => new VouchersApi(apiConfig(token)), [token])
    /** 待打印的凭证清单，只用于展示确认信息，出片一律由后端按 id 取数。 */
    const [sheets, setSheets] = useState<VoucherDetailDto[]>([])
    const [loading, setLoading] = useState(false)
    /** 正在出片的动作，避免重复点击开出多个窗口。 */
    const [busy, setBusy] = useState<"print" | "export" | null>(null)
    const [error, setError] = useState<string | null>(null)

    const ids = useMemo(() => {
        const list = voucherIds && voucherIds.length > 0 ? voucherIds : voucherId ? [voucherId] : []
        return list.filter(Boolean)
    }, [voucherId, voucherIds])

    useEffect(() => {
        if (!open || !accountSetId || ids.length === 0) return
        setLoading(true)
        setError(null)
        setSheets([])
        Promise.all(ids.map((id) => vouchersApi.accountsetsAccountSetIdVouchersIdGet({ accountSetId, id })))
            .then(setSheets)
            .catch(async (err) => setError(await apiErrorMessage(err, "加载凭证失败，请重试。")))
            .finally(() => setLoading(false))
    }, [open, accountSetId, ids, vouchersApi])

    const pdfUrl = `/api/accountsets/${accountSetId}/Vouchers/pdf`
    /** 请求体只带凭证清单，其余版式字段一律吃后端 VoucherPdfRequest 的默认值。 */
    const pdfBody = JSON.stringify({ ids })
    const pdfHeaders = { "Content-Type": "application/json" }

    /** 打印：新窗口打开后端 PDF，用户在 PDF 查看器里点打印。 */
    async function doPrint() {
        if (ids.length === 0) {
            message.warning("当前没有可打印的凭证")
            return
        }
        setBusy("print")
        setError(null)
        try {
            await printPdf(pdfUrl, token, { method: "POST", headers: pdfHeaders, body: pdfBody })
        } catch (err) {
            setError(err instanceof Error ? err.message : "生成打印稿失败，请重试。")
        } finally {
            setBusy(null)
        }
    }

    /** 导出：与打印同一份后端 PDF，区别是落盘下载而不是开预览窗口。 */
    async function exportPdf() {
        if (ids.length === 0) {
            message.warning("当前没有可导出的凭证")
            return
        }
        setBusy("export")
        setError(null)
        try {
            const res = await fetch(pdfUrl, {
                method: "POST",
                headers: { ...pdfHeaders, ...(token ? { Authorization: "Bearer " + token } : {}) },
                body: pdfBody,
            })
            if (!res.ok) throw new Error(await apiErrorMessage({ response: res }, "导出 PDF 失败，请稍后重试。"))

            const url = URL.createObjectURL(await res.blob())
            const a = document.createElement("a")
            a.href = url
            a.download = fileNameFromDisposition(res.headers.get("Content-Disposition"), ids.length === 1 ? sheets[0]?.voucherNo : undefined)
            document.body.appendChild(a)
            a.click()
            a.remove()
            // 留出下载时间再回收，过早 revoke 会让浏览器拿到空文件。
            window.setTimeout(() => URL.revokeObjectURL(url), 30_000)
            message.success("PDF 已导出")
        } catch (err) {
            setError(err instanceof Error ? err.message : "导出 PDF 失败，请稍后重试。")
        } finally {
            setBusy(null)
        }
    }

    /** 从 Content-Disposition 里取服务端下发的文件名，取不到时本地兜底。 */
    function fileNameFromDisposition(header: string | null, voucherNo?: string): string {
        const match = header?.match(/filename\*=UTF-8''([^;]+)/)
        if (match?.[1]) return decodeURIComponent(match[1])
        const plain = header?.match(/filename="?([^";]+)"?/)
        if (plain?.[1]) return plain[1]
        return voucherNo ? `记账凭证_${voucherNo}.pdf` : "记账凭证.pdf"
    }

    return (
        <Drawer
            open={open}
            width={640}
            title={`凭证打印${ids.length > 1 ? ` · 共 ${ids.length} 张` : sheets[0] ? ` · ${sheets[0].voucherNo}` : ""}`}
            onClose={() => onOpenChange(false)}
            styles={{ body: { display: "flex", flexDirection: "column", gap: 16, overflow: "auto" } }}
            footer={
                <div className="flex justify-end gap-2">
                    <Button onClick={() => onOpenChange(false)}>取消</Button>
                    <Button icon={<DownloadOutlined />} disabled={ids.length === 0} loading={busy === "export"} onClick={exportPdf}>
                        导出PDF
                    </Button>
                    <Button type="primary" icon={<PrinterOutlined />} disabled={ids.length === 0} loading={busy === "print"} onClick={doPrint}>
                        打印
                    </Button>
                </div>
            }
        >
            {error && <Alert type="error" showIcon title={error} />}
            <Alert type="info" showIcon title="打印稿由后端统一生成（A4 两版套打），页边距、行数与表尾签章栏位均为系统默认版式。" />

            {loading ? (
                <div className="flex justify-center py-10">
                    <Spin />
                </div>
            ) : sheets.length > 0 ? (
                <div className="overflow-auto rounded-xl border border-(--border)">
                    <table className="w-full text-[13px]">
                        <thead className="bg-(--muted)">
                            <tr>
                                {["凭证字号", "日期", "附单据", "分录数", "借方合计", "贷方合计"].map((h) => (
                                    <th key={h} className="px-3 py-2 text-left font-normal">
                                        {h}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {sheets.map((v) => (
                                <tr key={v.id} className="border-t border-(--border)">
                                    <td className="px-3 py-2">{v.voucherNo}</td>
                                    <td className="px-3 py-2 tabular-nums">{toISODate(v.date)}</td>
                                    <td className="px-3 py-2 tabular-nums">{v.attachmentCount ?? 0}</td>
                                    <td className="px-3 py-2 tabular-nums">{(v.entries ?? []).length}</td>
                                    <td className="px-3 py-2 text-right tabular-nums">{fmtAmount(v.totalDebit, true)}</td>
                                    <td className="px-3 py-2 text-right tabular-nums">{fmtAmount(v.totalCredit, true)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            ) : ids.length === 0 ? (
                <div className="py-10 text-center text-(--muted-foreground)">没有可打印的凭证。</div>
            ) : null}
        </Drawer>
    )
}
