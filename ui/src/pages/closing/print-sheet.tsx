import { useEffect, useMemo, useState } from "react"
import dayjs from "dayjs"
import { PrinterOutlined } from "@ant-design/icons"
import { Alert, Button, Drawer, Select, Spin } from "antd"

import { VouchersApi, type VoucherDetailDto, type VoucherEntryDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { amountToChineseUppercase } from "@/lib/amount-uppercase"
import { FormField } from "@/components/page/form-field"
import { NumberField } from "@/components/fields/number-field"

/** 打印设置（按账套保存在本机浏览器）。 */
interface PrintSettings {
    /** 纸型：A4 横向 / 241×140 针式套打。 */
    paper: "A4" | "V241"
    offsetX: number
    offsetY: number
}

const DEFAULT_SETTINGS: PrintSettings = { paper: "A4", offsetX: 0, offsetY: 0 }

function storageKey(accountSetId: string) {
    return "kuaiji.closing.print." + accountSetId
}

function loadSettings(accountSetId: string): PrintSettings {
    try {
        const raw = window.localStorage.getItem(storageKey(accountSetId))
        if (raw) {
            return { ...DEFAULT_SETTINGS, ...(JSON.parse(raw) as Partial<PrintSettings>) }
        }
    } catch {
        /* 本机存储不可用时退回默认设置 */
    }
    return DEFAULT_SETTINGS
}

function saveSettings(accountSetId: string, settings: PrintSettings) {
    try {
        window.localStorage.setItem(storageKey(accountSetId), JSON.stringify(settings))
    } catch {
        /* 忽略本机存储异常 */
    }
}

/** HTML 转义（凭证内容来自账套数据，需防注入）。 */
function esc(value: string): string {
    return value
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
}

/** 生成打印窗口 HTML（自带样式与自动打印脚本）。 */
function buildPrintHtml(voucher: VoucherDetailDto, settings: PrintSettings): string {
    const isA4 = settings.paper === "A4"
    const entries = voucher.entries ?? []
    const rowCount = Math.max(entries.length, 5)

    const rows: string[] = []
    for (let i = 0; i < rowCount; i++) {
        const e = entries[i]
        rows.push(
            "<tr>" +
                "<td class='c'>" + esc(e?.summary ?? "") + "</td>" +
                "<td class='c'>" + (e ? esc((e.accountCode ?? "") + " " + (e.accountName ?? "")) : "") + "</td>" +
                "<td class='n'>" + fmtAmount(e?.debit) + "</td>" +
                "<td class='n'>" + fmtAmount(e?.credit) + "</td>" +
                "</tr>",
        )
    }

    const dateText = voucher.date ? dayjs(voucher.date).format("YYYY年MM月DD日") : ""
    const pageCss = isA4
        ? "@page { size: A4 landscape; margin: 8mm; }"
        : "@page { size: 241mm 140mm; margin: 0; }"
    const contentWidth = isA4 ? "265mm" : "241mm"
    const fontSize = isA4 ? "10.5pt" : "9.5pt"

    return (
        "<!DOCTYPE html><html lang='zh-CN'><head><meta charset='utf-8'>" +
        "<title>记账凭证 " + esc((voucher.word ?? "") + "-" + (voucher.number ?? "")) + "</title><style>" +
        pageCss +
        "* { box-sizing: border-box; }" +
        "body { margin: 0; font-family: 'SimSun','宋体',serif; color: #000; }" +
        ".sheet { width: " + contentWidth + "; padding: " + settings.offsetY + "mm 0 0 " + settings.offsetX + "mm; font-size: " + fontSize + "; }" +
        "h1 { text-align: center; font-size: " + (isA4 ? "16pt" : "14pt") + "; margin: 0 0 2mm; letter-spacing: 4px; font-family: 'SimHei','黑体',sans-serif; }" +
        ".meta { display: flex; justify-content: space-between; margin-bottom: 1mm; }" +
        "table { width: 100%; border-collapse: collapse; table-layout: fixed; }" +
        "th, td { border: 0.4mm solid #000; padding: 0.6mm 1mm; height: 7mm; word-break: break-all; }" +
        "th { font-weight: normal; text-align: center; }" +
        ".n { text-align: right; }" +
        ".c { text-align: left; }" +
        ".total { font-weight: bold; }" +
        ".footer { display: flex; justify-content: space-between; margin-top: 2mm; }" +
        "</style></head><body>" +
        "<div class='sheet'>" +
        "<h1>记账凭证</h1>" +
        "<div class='meta'><span>日期：" + dateText + "</span><span>附单据 " + (voucher.attachmentCount ?? 0) + " 张</span><span>凭证字号：" + esc((voucher.word ?? "") + " - " + (voucher.number ?? "")) + "</span></div>" +
        "<table><colgroup><col style='width:28%'><col style='width:32%'><col style='width:20%'><col style='width:20%'></colgroup>" +
        "<tr><th>摘要</th><th>会计科目</th><th>借方金额</th><th>贷方金额</th></tr>" +
        rows.join("") +
        "<tr class='total'><td colspan='2'>合计：人民币（大写）" + esc(amountToChineseUppercase(voucher.totalDebit ?? 0)) + "</td>" +
        "<td class='n'>" + fmtAmount(voucher.totalDebit, true) + "</td>" +
        "<td class='n'>" + fmtAmount(voucher.totalCredit, true) + "</td></tr>" +
        "</table>" +
        "<div class='footer'><span>制单人：" + esc(voucher.preparer ?? "") + "</span><span>审核人：" + esc(voucher.reviewer ?? "") + "</span><span>状态：" + esc(voucher.statusText ?? "") + "</span></div>" +
        "</div>" +
        "<script>window.onload=function(){window.focus();window.print();}<" + "/script>" +
        "</body></html>"
    )
}

/**
 * 结转凭证打印：标准记账凭证版式 + 套打偏移调整。
 * 打印设置（纸型与偏移）按账套保存在本机浏览器；打印通过弹出独立窗口完成，不干扰应用界面。
 */
export function PrintVoucherSheet({
    accountSetId,
    token,
    voucherId,
    open,
    onOpenChange,
}: {
    accountSetId: string
    token?: string | null
    voucherId: string | null
    open: boolean
    onOpenChange: (open: boolean) => void
}) {
    const vouchersApi = useMemo(() => new VouchersApi(apiConfig(token)), [token])
    const [voucher, setVoucher] = useState<VoucherDetailDto | null>(null)
    const [loading, setLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [settings, setSettings] = useState<PrintSettings>(DEFAULT_SETTINGS)

    useEffect(() => {
        if (!open || !voucherId || !accountSetId) return
        setSettings(loadSettings(accountSetId))
        setLoading(true)
        setError(null)
        setVoucher(null)
        vouchersApi
            .accountsetsAccountSetIdVouchersIdGet({ accountSetId, id: voucherId })
            .then((detail) => setVoucher(detail))
            .catch(async (err) => setError(await apiErrorMessage(err, "加载凭证失败，请重试。")))
            .finally(() => setLoading(false))
    }, [open, voucherId, accountSetId, vouchersApi])

    function update(patch: Partial<PrintSettings>) {
        setSettings((current) => {
            const next = { ...current, ...patch }
            saveSettings(accountSetId, next)
            return next
        })
    }

    function doPrint() {
        if (!voucher) return
        const html = buildPrintHtml(voucher, settings)
        const win = window.open("", "_blank", "width=1180,height=720")
        if (!win) {
            window.alert("浏览器阻止了打印窗口，请允许本站弹出窗口后重试。")
            return
        }
        win.document.open()
        win.document.write(html)
        win.document.close()
    }

    const isA4 = settings.paper === "A4"
    const previewRows: (VoucherEntryDto | null)[] = [...(voucher?.entries ?? [])]
    while (previewRows.length < 5) previewRows.push(null)

    return (
        <Drawer
            open={open}
            size={768}
            title={`凭证打印${voucher ? ` · ${voucher.word}-${voucher.number}` : ""}`}
            onClose={() => onOpenChange(false)}
            styles={{ body: { display: "flex", flexDirection: "column", gap: 16, overflow: "auto" } }}
            footer={
                <div className="flex justify-end gap-2">
                    <Button onClick={() => onOpenChange(false)}>关闭</Button>
                    <Button type="primary" icon={<PrinterOutlined />} disabled={!voucher} onClick={doPrint}>
                        打印
                    </Button>
                </div>
            }
        >
            <p className="text-[12.5px] text-slate-400">
                标准记账凭证版式；可选 241×140 针式套打纸并微调偏移（打印设置按账套保存在本机浏览器）。
            </p>

            {error && <Alert type="error" showIcon title={error} />}

            {loading && !voucher ? (
                <div className="flex justify-center py-10">
                    <Spin />
                </div>
            ) : voucher ? (
                <>
                    <div className="grid grid-cols-3 items-end gap-3">
                        <FormField label="纸型" htmlFor="print-paper">
                            <Select
                                id="print-paper"
                                className="w-full"
                                size="small"
                                value={settings.paper}
                                onChange={(v) => update({ paper: v === "V241" ? "V241" : "A4" })}
                                options={[
                                    { value: "A4", label: "A4 横向" },
                                    { value: "V241", label: "241×140 针式套打" },
                                ]}
                            />
                        </FormField>
                        <FormField label="左偏移（mm）" htmlFor="print-offset-x">
                            <NumberField
                                id="print-offset-x"
                                value={settings.offsetX}
                                onChange={(v) => update({ offsetX: v ?? 0 })}
                                min={-15}
                                max={15}
                                precision={1}
                            />
                        </FormField>
                        <FormField label="上偏移（mm）" htmlFor="print-offset-y">
                            <NumberField
                                id="print-offset-y"
                                value={settings.offsetY}
                                onChange={(v) => update({ offsetY: v ?? 0 })}
                                min={-15}
                                max={15}
                                precision={1}
                            />
                        </FormField>
                    </div>

                    <div className="overflow-auto rounded-xl border border-slate-200 bg-[#f1f5f9] p-3">
                        <div
                            className="mx-auto bg-white shadow-[0_1px_4px_rgba(16,24,40,0.18)]"
                            style={{
                                width: isA4 ? "265mm" : "241mm",
                                padding: `${settings.offsetY}mm 6mm 6mm ${settings.offsetX}mm`,
                                fontFamily: "'SimSun','宋体',serif",
                            }}
                        >
                            <div
                                className="text-center text-[19px] font-bold tracking-[6px] text-black"
                                style={{ fontFamily: "'SimHei','黑体',sans-serif" }}
                            >
                                记账凭证
                            </div>
                            <div className="mt-1 flex items-center justify-between text-[12.5px] text-black">
                                <span>日期：{voucher.date ? dayjs(voucher.date).format("YYYY年MM月DD日") : ""}</span>
                                <span>附单据 {voucher.attachmentCount ?? 0} 张</span>
                                <span>凭证字号：{voucher.word} - {voucher.number}</span>
                            </div>
                            <table className="mt-1 w-full border-collapse text-[12.5px]">
                                <colgroup>
                                    <col style={{ width: "28%" }} />
                                    <col style={{ width: "32%" }} />
                                    <col style={{ width: "20%" }} />
                                    <col style={{ width: "20%" }} />
                                </colgroup>
                                <thead>
                                    <tr>
                                        {["摘要", "会计科目", "借方金额", "贷方金额"].map((h) => (
                                            <th key={h} className="border border-black px-2 py-1 text-center font-normal">
                                                {h}
                                            </th>
                                        ))}
                                    </tr>
                                </thead>
                                <tbody>
                                    {previewRows.map((e, i) => (
                                        <tr key={i} className="h-8">
                                            <td className="border border-black px-2">{e?.summary ?? ""}</td>
                                            <td className="border border-black px-2">
                                                {e ? `${e.accountCode ?? ""} ${e.accountName ?? ""}` : ""}
                                            </td>
                                            <td className="border border-black px-2 text-right tabular-nums">
                                                {fmtAmount(e?.debit)}
                                            </td>
                                            <td className="border border-black px-2 text-right tabular-nums">
                                                {fmtAmount(e?.credit)}
                                            </td>
                                        </tr>
                                    ))}
                                    <tr className="font-semibold">
                                        <td className="border border-black px-2" colSpan={2}>
                                            合计：人民币（大写）{amountToChineseUppercase(voucher.totalDebit ?? 0)}
                                        </td>
                                        <td className="border border-black px-2 text-right tabular-nums">
                                            {fmtAmount(voucher.totalDebit, true)}
                                        </td>
                                        <td className="border border-black px-2 text-right tabular-nums">
                                            {fmtAmount(voucher.totalCredit, true)}
                                        </td>
                                    </tr>
                                </tbody>
                            </table>
                            <div className="mt-2 flex items-center justify-between text-[12.5px] text-black">
                                <span>制单人：{voucher.preparer ?? ""}</span>
                                <span>审核人：{voucher.reviewer ?? ""}</span>
                                <span>状态：{voucher.statusText ?? ""}</span>
                            </div>
                        </div>
                    </div>
                </>
            ) : null}
        </Drawer>
    )
}
