import { useEffect, useMemo, useState } from "react"
import dayjs from "dayjs"
import { DownloadOutlined, PrinterOutlined } from "@ant-design/icons"
import { Alert, Button, Checkbox, Drawer, Input, InputNumber, Radio, Select, Spin, Tabs, message } from "antd"

import { VouchersApi, type VoucherDetailDto, type VoucherEntryDto } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { fmtAmount } from "@/lib/format"
import { amountToChineseUppercase } from "@/lib/amount-uppercase"
import { FormField } from "@/components/page/form-field"

/** 打印设置（对照柠檬云「凭证打印」，按账套保存在本机浏览器）。 */
interface PrintSettings {
    /** 打印选项：全部凭证 / 选中凭证。 */
    scope: "all" | "selected"
    /** 纸张：A4 两版（推荐）/ A4 一版 / 241×140 针式套打。 */
    paper: "A4x2" | "A4x1" | "V241"
    /** 打印方式：靠上 / 靠下。 */
    valign: "top" | "bottom"
    /** 每张凭证至少打印的行数。 */
    rows: number
    font: string
    fontSize: number
    orientation: "portrait" | "landscape"
    marginLeft: number
    marginRight: number
    marginTop: number
    marginBottom: number
    /** 其他选项：打印附件。 */
    printAttachment: boolean
    /* —— 更多设置 —— */
    signPreparer: boolean
    preparerText: string
    signReviewer: boolean
    reviewerText: string
    signTime: boolean
    signManager: boolean
    managerText: string
    signBookkeeper: boolean
    bookkeeperText: string
    signCashier: boolean
    cashierText: string
    showAccountCode: boolean
    zeroAttachment: boolean
    /** 汇总打印：多张凭证合并成一张。 */
    mergePrint: boolean
    showQtyPrice: boolean
    showFx: boolean
}

const DEFAULT_PRINT_SETTINGS: PrintSettings = {
    scope: "all",
    paper: "A4x2",
    valign: "top",
    rows: 5,
    font: "宋体",
    fontSize: 10,
    orientation: "portrait",
    marginLeft: 25,
    marginRight: 10,
    marginTop: 14,
    marginBottom: 15,
    printAttachment: false,
    signPreparer: true,
    preparerText: "",
    signReviewer: true,
    reviewerText: "",
    signTime: false,
    signManager: false,
    managerText: "",
    signBookkeeper: false,
    bookkeeperText: "",
    signCashier: false,
    cashierText: "",
    showAccountCode: true,
    zeroAttachment: false,
    mergePrint: false,
    showQtyPrice: false,
    showFx: false,
}

const PAPER_OPTIONS = [
    { value: "A4x2", label: "A4两版（推荐）" },
    { value: "A4x1", label: "A4一版" },
    { value: "V241", label: "241×140 针式套打" },
]

const FONT_OPTIONS = ["宋体", "黑体", "仿宋", "微软雅黑"].map((f) => ({ value: f, label: f }))
const FONT_SIZE_OPTIONS = [8, 9, 10, 11, 12, 14].map((s) => ({ value: s, label: String(s) }))

const FONT_FAMILY: Record<string, string> = {
    宋体: "'SimSun','宋体',serif",
    黑体: "'SimHei','黑体',sans-serif",
    仿宋: "'FangSong','仿宋',serif",
    微软雅黑: "'Microsoft YaHei','微软雅黑',sans-serif",
}

function storageKey(accountSetId: string) {
    return "kuaiji.closing.print." + accountSetId
}

function loadPrintSettings(accountSetId: string): PrintSettings {
    try {
        const raw = window.localStorage.getItem(storageKey(accountSetId))
        if (raw) {
            return { ...DEFAULT_PRINT_SETTINGS, ...(JSON.parse(raw) as Partial<PrintSettings>) }
        }
    } catch {
        /* 本机存储不可用时退回默认设置 */
    }
    return DEFAULT_PRINT_SETTINGS
}

function savePrintSettings(accountSetId: string, settings: PrintSettings) {
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

/** 一张纸的物理尺寸（mm）。 */
function paperSize(settings: PrintSettings): { w: number; h: number } {
    if (settings.paper === "V241") return { w: 241, h: 140 }
    return settings.orientation === "landscape" ? { w: 297, h: 210 } : { w: 210, h: 297 }
}

/** 每页放几张凭证。 */
function perPage(settings: PrintSettings): number {
    if (settings.mergePrint) return 1
    return settings.paper === "A4x2" ? 2 : 1
}

/** 生成打印窗口 HTML（自带样式与自动打印脚本）。 */
function buildPrintHtml(vouchers: VoucherDetailDto[], settings: PrintSettings, printedAt = dayjs()): string {
    const sheetCss = settings.paper === "V241" ? "241mm 140mm" : `A4 ${settings.orientation}`
    const size = paperSize(settings)
    const contentH = Math.max(40, size.h - settings.marginTop - settings.marginBottom)
    const family = FONT_FAMILY[settings.font] ?? FONT_FAMILY["宋体"]
    const per = perPage(settings)

    const pages: VoucherDetailDto[][] = []
    if (settings.mergePrint && vouchers.length > 1) {
        pages.push(vouchers)
    } else {
        for (let i = 0; i < vouchers.length; i += per) pages.push(vouchers.slice(i, i + per))
    }

    const dateText = (v: VoucherDetailDto) => (v.date ? dayjs(v.date).format("YYYY年MM月DD日") : "")
    const emptyCount = (v: VoucherDetailDto) => {
        const n = v.attachmentCount ?? 0
        return n === 0 && settings.zeroAttachment ? 0 : n
    }

    return (
        "<!DOCTYPE html><html lang='zh-CN'><head><meta charset='utf-8'><title>记账凭证</title><style>" +
        `@page { size: ${sheetCss}; margin: 0; }` +
        "* { box-sizing: border-box; }" +
        `body { margin: 0; padding: ${settings.marginTop}mm ${settings.marginRight}mm ${settings.marginBottom}mm ${settings.marginLeft}mm; font-family: ${family}; color: #000; font-size: ${settings.fontSize}pt; }` +
        `.page { height: ${contentH}mm; display: flex; flex-direction: column; justify-content: ${settings.valign === "top" ? "flex-start" : "flex-end"}; page-break-after: always; break-after: page; }` +
        ".page:last-child { page-break-after: auto; break-after: auto; }" +
        ".sheet { width: 100%; }" +
        ".sheet + .sheet { margin-top: 6mm; }" +
        `h1 { text-align: center; font-size: ${settings.fontSize + 6}pt; margin: 0 0 1.5mm; letter-spacing: 4px; }` +
        ".meta { display: flex; justify-content: space-between; margin-bottom: 1mm; }" +
        "table { width: 100%; border-collapse: collapse; table-layout: fixed; }" +
        "th, td { border: 0.4mm solid #000; padding: 0.6mm 1mm; height: 7mm; word-break: break-all; }" +
        "th { font-weight: normal; text-align: center; }" +
        ".n { text-align: right; }" +
        ".c { text-align: left; }" +
        ".total { font-weight: bold; }" +
        ".footer { display: flex; justify-content: space-between; gap: 4mm; margin-top: 2mm; }" +
        ".attach { margin-top: 2mm; }" +
        "</style></head><body>" +
        pages.map(buildPage).join("") +
        "<script>window.onload=function(){window.focus();window.print();}<" + "/script>" +
        "</body></html>"
    )

    function buildPage(list: VoucherDetailDto[]): string {
        return "<div class='page'>" + list.map(buildSheet).join("") + "</div>"
    }

    function accountText(e: VoucherEntryDto): string {
        const code = settings.showAccountCode ? (e.accountCode ?? "") : ""
        return (code ? code + " " : "") + (e.accountName ?? "")
    }

    function extraText(_e: VoucherEntryDto): string {
        // 数量/单价、原币/汇率需要分录上的数量核算与外币核算字段，当前凭证模型尚未支持。
        return ""
    }

    /** 单张凭证的版式：行数不足补空行，保证套打对齐。 */
    function buildSheet(voucher: VoucherDetailDto): string {
        const entries = voucher.entries ?? []
        const rowCount = Math.max(entries.length, settings.rows)
        const rows: string[] = []
        for (let i = 0; i < rowCount; i++) {
            const e = entries[i]
            rows.push(
                "<tr>" +
                    "<td class='c'>" + esc(e?.summary ?? "") + (e ? extraText(e) : "") + "</td>" +
                    "<td class='c'>" + (e ? esc(accountText(e)) : "") + "</td>" +
                    "<td class='n'>" + fmtAmount(e?.debit) + "</td>" +
                    "<td class='n'>" + fmtAmount(e?.credit) + "</td>" +
                    "</tr>",
            )
        }

        const signs: string[] = []
        if (settings.signPreparer) signs.push("制单人：" + esc(settings.preparerText || (voucher.preparer ?? "")))
        if (settings.signReviewer) signs.push("审核人：" + esc(settings.reviewerText || (voucher.reviewer ?? "")))
        if (settings.signManager) signs.push("主管：" + esc(settings.managerText))
        if (settings.signBookkeeper) signs.push("记账：" + esc(settings.bookkeeperText))
        if (settings.signCashier) signs.push("出纳：" + esc(settings.cashierText))
        if (settings.signTime) signs.push("打印时间：" + esc(printedAt.format("YYYY-MM-DD HH:mm")))

        return (
            "<div class='sheet'>" +
            "<h1>记账凭证</h1>" +
            "<div class='meta'><span>日期：" + dateText(voucher) + "</span><span>附单据 " + emptyCount(voucher) + " 张</span><span>凭证字号：" + esc((voucher.word ?? "") + " - " + (voucher.number ?? "")) + "</span></div>" +
            "<table><colgroup><col style='width:28%'><col style='width:32%'><col style='width:20%'><col style='width:20%'></colgroup>" +
            "<tr><th>摘要</th><th>会计科目</th><th>借方金额</th><th>贷方金额</th></tr>" +
            rows.join("") +
            "<tr class='total'><td colspan='2'>合计：人民币（大写）" + esc(amountToChineseUppercase(voucher.totalDebit ?? 0)) + "</td>" +
            "<td class='n'>" + fmtAmount(voucher.totalDebit, true) + "</td>" +
            "<td class='n'>" + fmtAmount(voucher.totalCredit, true) + "</td></tr>" +
            "</table>" +
            (settings.printAttachment && (voucher.attachmentCount ?? 0) > 0
                ? "<div class='attach'>附件：" + (voucher.attachmentCount ?? 0) + " 张（详见系统内「附件」）</div>"
                : "") +
            "<div class='footer'>" + signs.map((s) => "<span>" + s + "</span>").join("") + "</div>" +
            "</div>"
        )
    }
}

/**
 * 凭证打印：标准记账凭证版式，设置对照柠檬云「凭证打印」。
 * 打印设置按账套保存在本机浏览器；打印通过弹出独立窗口完成，不干扰应用界面。
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
    const [voucher, setVoucher] = useState<VoucherDetailDto | null>(null)
    /** 连续打印时的全部凭证；单张打印时长度为 1。 */
    const [sheets, setSheets] = useState<VoucherDetailDto[]>([])
    const [loading, setLoading] = useState(false)
    /** 服务端 PDF 导出进行中。 */
    const [exporting, setExporting] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [settings, setSettings] = useState<PrintSettings>(DEFAULT_PRINT_SETTINGS)

    useEffect(() => {
        if (!open || !accountSetId) return
        const ids = voucherIds && voucherIds.length > 0 ? voucherIds : voucherId ? [voucherId] : []
        if (ids.length === 0) return
        setSettings(loadPrintSettings(accountSetId))
        setLoading(true)
        setError(null)
        setVoucher(null)
        setSheets([])
        Promise.all(ids.map((id) => vouchersApi.accountsetsAccountSetIdVouchersIdGet({ accountSetId, id })))
            .then((details) => {
                setSheets(details)
                setVoucher(details[0] ?? null)
            })
            .catch(async (err) => setError(await apiErrorMessage(err, "加载凭证失败，请重试。")))
            .finally(() => setLoading(false))
    }, [open, voucherId, voucherIds, accountSetId, vouchersApi])

    function update(patch: Partial<PrintSettings>) {
        setSettings((current) => {
            const next = { ...current, ...patch }
            savePrintSettings(accountSetId, next)
            return next
        })
    }

    function doPrint() {
        const list = sheets.length > 0 ? sheets : voucher ? [voucher] : []
        if (list.length === 0) return
        const html = buildPrintHtml(list, settings)
        const win = window.open("", "_blank", "width=1180,height=720")
        if (!win) {
            window.alert("浏览器阻止了打印窗口，请允许本站弹出窗口后重试。")
            return
        }
        win.document.open()
        win.document.write(html)
        win.document.close()
    }

    /**
     * 导出 PDF：调后端 PaperSharp 渲染接口，按当前打印版式生成矢量 PDF 并下载。
     * 与「打印」共用同一套版式设置，区别是打印走浏览器弹出窗口，导出走服务端出文件。
     */
    async function exportPdf() {
        const ids = (voucherIds && voucherIds.length > 0 ? voucherIds : voucher ? [voucher.id ?? ""] : []).filter(Boolean)
        if (ids.length === 0) {
            message.warning("当前没有可导出的凭证")
            return
        }

        const body = {
            ids,
            paper: settings.paper,
            orientation: settings.orientation,
            valign: settings.valign,
            rows: settings.rows,
            fontSize: settings.fontSize,
            marginLeft: settings.marginLeft,
            marginRight: settings.marginRight,
            marginTop: settings.marginTop,
            marginBottom: settings.marginBottom,
            signPreparer: settings.signPreparer,
            preparerText: settings.preparerText || undefined,
            signReviewer: settings.signReviewer,
            reviewerText: settings.reviewerText || undefined,
            signManager: settings.signManager,
            managerText: settings.managerText || undefined,
            signBookkeeper: settings.signBookkeeper,
            bookkeeperText: settings.bookkeeperText || undefined,
            signCashier: settings.signCashier,
            cashierText: settings.cashierText || undefined,
            signTime: settings.signTime,
            showAccountCode: settings.showAccountCode,
            zeroAttachment: settings.zeroAttachment,
            mergePrint: settings.mergePrint,
        }

        setExporting(true)
        try {
            const res = await fetch(`/api/accountsets/${accountSetId}/Vouchers/pdf`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    ...(token ? { Authorization: "Bearer " + token } : {}),
                },
                body: JSON.stringify(body),
            })
            if (!res.ok) {
                const text = await res.text()
                throw new Error(text || `导出失败（HTTP ${res.status}）`)
            }

            const blob = await res.blob()
            const url = URL.createObjectURL(blob)
            const a = document.createElement("a")
            a.href = url
            a.download = fileNameFromDisposition(res.headers.get("Content-Disposition"), ids.length === 1 ? voucher?.voucherNo : undefined)
            document.body.appendChild(a)
            a.click()
            a.remove()
            URL.revokeObjectURL(url)
            message.success("PDF 已导出")
        } catch (err) {
            message.error(err instanceof Error ? err.message : "导出 PDF 失败，请稍后重试。")
        } finally {
            setExporting(false)
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

    const previewRows: (VoucherEntryDto | null)[] = [...(voucher?.entries ?? [])]
    while (previewRows.length < settings.rows) previewRows.push(null)

    const baseForm = (
        <div className="flex flex-col gap-3">
            <FormField label="打印选项">
                <Radio.Group
                    value={settings.scope}
                    onChange={(e) => update({ scope: e.target.value as PrintSettings["scope"] })}
                    options={[
                        { value: "all", label: "打印全部凭证" },
                        { value: "selected", label: "打印选中凭证" },
                    ]}
                />
            </FormField>
            <FormField label="选择打印纸张" htmlFor="print-paper">
                <Select
                    id="print-paper"
                    className="w-full"
                    value={settings.paper}
                    onChange={(v) => update({ paper: v as PrintSettings["paper"] })}
                    options={PAPER_OPTIONS}
                />
            </FormField>
            <FormField label="打印方式">
                <Radio.Group
                    value={settings.valign}
                    onChange={(e) => update({ valign: e.target.value as PrintSettings["valign"] })}
                    options={[
                        { value: "top", label: "靠上" },
                        { value: "bottom", label: "靠下" },
                    ]}
                />
            </FormField>
            <FormField label="打印凭证行" htmlFor="print-rows">
                <InputNumber
                    id="print-rows"
                    className="w-28!"
                    min={1}
                    max={20}
                    value={settings.rows}
                    onChange={(v) => update({ rows: Number(v) || 5 })}
                />
            </FormField>
            <FormField label="打印字体" htmlFor="print-font">
                <Select
                    id="print-font"
                    className="w-full"
                    value={settings.font}
                    onChange={(v) => update({ font: String(v) })}
                    options={FONT_OPTIONS}
                />
            </FormField>
            <FormField label="打印字体大小" htmlFor="print-font-size">
                <Select
                    id="print-font-size"
                    className="w-full"
                    value={settings.fontSize}
                    onChange={(v) => update({ fontSize: Number(v) })}
                    options={FONT_SIZE_OPTIONS}
                />
            </FormField>
            <FormField label="打印方向">
                <Radio.Group
                    value={settings.orientation}
                    disabled={settings.paper === "V241"}
                    onChange={(e) => update({ orientation: e.target.value as PrintSettings["orientation"] })}
                    options={[
                        { value: "portrait", label: "纵向" },
                        { value: "landscape", label: "横向" },
                    ]}
                />
            </FormField>
            <FormField label="页面边距（毫米）">
                <div className="grid grid-cols-4 gap-2">
                    <InputNumber addonBefore="左" min={0} max={60} value={settings.marginLeft} onChange={(v) => update({ marginLeft: Number(v) || 0 })} />
                    <InputNumber addonBefore="右" min={0} max={60} value={settings.marginRight} onChange={(v) => update({ marginRight: Number(v) || 0 })} />
                    <InputNumber addonBefore="上" min={0} max={60} value={settings.marginTop} onChange={(v) => update({ marginTop: Number(v) || 0 })} />
                    <InputNumber addonBefore="下" min={0} max={60} value={settings.marginBottom} onChange={(v) => update({ marginBottom: Number(v) || 0 })} />
                </div>
            </FormField>
            <FormField label="其他选项">
                <Checkbox checked={settings.printAttachment} onChange={(e) => update({ printAttachment: e.target.checked })}>
                    打印附件
                </Checkbox>
            </FormField>
        </div>
    )

    const moreForm = (
        <div className="flex flex-col gap-2">
            <div className="flex items-center gap-2">
                <Checkbox checked={settings.signPreparer} onChange={(e) => update({ signPreparer: e.target.checked })}>
                    打印制单人姓名
                </Checkbox>
                <Input
                    allowClear
                    className="w-40"
                    placeholder="默认取凭证制单人"
                    value={settings.preparerText}
                    onChange={(e) => update({ preparerText: e.target.value })}
                />
                <Checkbox checked={settings.signReviewer} onChange={(e) => update({ signReviewer: e.target.checked })}>
                    打印审核人姓名
                </Checkbox>
                <Input
                    allowClear
                    className="w-40"
                    placeholder="默认取凭证审核人"
                    value={settings.reviewerText}
                    onChange={(e) => update({ reviewerText: e.target.value })}
                />
                <Checkbox checked={settings.signTime} onChange={(e) => update({ signTime: e.target.checked })}>
                    打印时间
                </Checkbox>
            </div>
            <div className="flex items-center gap-2">
                <Checkbox checked={settings.signManager} onChange={(e) => update({ signManager: e.target.checked })}>
                    打印主管姓名
                </Checkbox>
                <Input allowClear className="w-52" placeholder="主管姓名" value={settings.managerText} onChange={(e) => update({ managerText: e.target.value })} />
            </div>
            <div className="flex items-center gap-2">
                <Checkbox checked={settings.signBookkeeper} onChange={(e) => update({ signBookkeeper: e.target.checked })}>
                    打印记账姓名
                </Checkbox>
                <Input allowClear className="w-52" placeholder="记账人姓名" value={settings.bookkeeperText} onChange={(e) => update({ bookkeeperText: e.target.value })} />
            </div>
            <div className="flex items-center gap-2">
                <Checkbox checked={settings.signCashier} onChange={(e) => update({ signCashier: e.target.checked })}>
                    打印出纳姓名
                </Checkbox>
                <Input allowClear className="w-52" placeholder="出纳姓名" value={settings.cashierText} onChange={(e) => update({ cashierText: e.target.value })} />
            </div>
            <Checkbox checked={settings.showFx} disabled onChange={(e) => update({ showFx: e.target.checked })}>
                在摘要中打印显示原币、汇率（需外币核算，暂未启用）
            </Checkbox>
            <Checkbox checked={settings.showQtyPrice} disabled onChange={(e) => update({ showQtyPrice: e.target.checked })}>
                在摘要中打印显示数量、单价（需数量核算，暂未启用）
            </Checkbox>
            <Checkbox checked={settings.showAccountCode} onChange={(e) => update({ showAccountCode: e.target.checked })}>
                打印显示科目编码
            </Checkbox>
            <Checkbox checked={settings.zeroAttachment} onChange={(e) => update({ zeroAttachment: e.target.checked })}>
                无附件时打印 “0”
            </Checkbox>
            <Checkbox checked={settings.mergePrint} onChange={(e) => update({ mergePrint: e.target.checked })}>
                汇总打印
            </Checkbox>
        </div>
    )

    return (
        <Drawer
            open={open}
            width={820}
            title={`凭证打印${
                sheets.length > 1 ? ` · 共 ${sheets.length} 张` : voucher ? ` · ${voucher.word}-${voucher.number}` : ""
            }`}
            onClose={() => onOpenChange(false)}
            styles={{ body: { display: "flex", flexDirection: "column", gap: 16, overflow: "auto" } }}
            footer={
                <div className="flex justify-end gap-2">
                    <Button onClick={() => onOpenChange(false)}>取消</Button>
                    <Button icon={<DownloadOutlined />} disabled={!voucher} loading={exporting} onClick={exportPdf}>
                        导出PDF
                    </Button>
                    <Button type="primary" icon={<PrinterOutlined />} disabled={!voucher} onClick={doPrint}>
                        打印
                    </Button>
                </div>
            }
        >
            {error && <Alert type="error" showIcon title={error} />}

            {loading && !voucher ? (
                <div className="flex justify-center py-10">
                    <Spin />
                </div>
            ) : voucher ? (
                <>
                    <Tabs
                        items={[
                            { key: "base", label: "基础设置", children: baseForm },
                            { key: "more", label: "更多设置", children: moreForm },
                        ]}
                    />

                    <div className="overflow-auto rounded-xl border border-(--border) bg-(--muted) p-3">
                        <div
                            className="mx-auto bg-(--card) shadow-[0_1px_4px_rgba(16,24,40,0.18)]"
                            style={{
                                width: settings.paper === "V241" ? "241mm" : "180mm",
                                padding: `${settings.marginTop}mm 6mm 6mm 6mm`,
                                fontFamily: FONT_FAMILY[settings.font] ?? FONT_FAMILY["宋体"],
                                fontSize: `${settings.fontSize}pt`,
                            }}
                        >
                            <div className="text-center font-bold tracking-[6px] text-black" style={{ fontSize: `${settings.fontSize + 6}pt` }}>
                                记账凭证
                            </div>
                            <div className="mt-1 flex items-center justify-between text-black">
                                <span>日期：{voucher.date ? dayjs(voucher.date).format("YYYY年MM月DD日") : ""}</span>
                                <span>附单据 {voucher.attachmentCount ?? 0} 张</span>
                                <span>
                                    凭证字号：{voucher.word} - {voucher.number}
                                </span>
                            </div>
                            <table className="mt-1 w-full border-collapse">
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
                                                {e ? `${settings.showAccountCode ? (e.accountCode ?? "") + " " : ""}${e.accountName ?? ""}` : ""}
                                            </td>
                                            <td className="border border-black px-2 text-right tabular-nums">{fmtAmount(e?.debit)}</td>
                                            <td className="border border-black px-2 text-right tabular-nums">{fmtAmount(e?.credit)}</td>
                                        </tr>
                                    ))}
                                    <tr className="font-semibold">
                                        <td className="border border-black px-2" colSpan={2}>
                                            合计：人民币（大写）{amountToChineseUppercase(voucher.totalDebit ?? 0)}
                                        </td>
                                        <td className="border border-black px-2 text-right tabular-nums">{fmtAmount(voucher.totalDebit, true)}</td>
                                        <td className="border border-black px-2 text-right tabular-nums">{fmtAmount(voucher.totalCredit, true)}</td>
                                    </tr>
                                </tbody>
                            </table>
                            <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-black">
                                {settings.signPreparer && <span>制单人：{settings.preparerText || (voucher.preparer ?? "")}</span>}
                                {settings.signReviewer && <span>审核人：{settings.reviewerText || (voucher.reviewer ?? "")}</span>}
                                {settings.signManager && <span>主管：{settings.managerText}</span>}
                                {settings.signBookkeeper && <span>记账：{settings.bookkeeperText}</span>}
                                {settings.signCashier && <span>出纳：{settings.cashierText}</span>}
                                {settings.signTime && <span>打印时间：{dayjs().format("YYYY-MM-DD HH:mm")}</span>}
                            </div>
                        </div>
                    </div>
                </>
            ) : null}
        </Drawer>
    )
}
