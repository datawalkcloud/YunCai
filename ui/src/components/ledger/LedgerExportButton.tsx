import { useState } from "react"
import { DownloadOutlined, PrinterOutlined } from "@ant-design/icons"
import { Button } from "antd"

import { apiErrorMessage } from "@/lib/api-client"
import { printPdf } from "@/lib/print-pdf"
import { toISODate } from "@/lib/format"
import type { LedgerConditions } from "./types"

/** 导出接口认识的账页标识，与后端 LedgerExportKindText 的解析结果一一对应。 */
export type LedgerExportKind =
    | "subsidiary"
    | "general"
    | "journal"
    | "voucher-summary"
    | "multi-column"
    | "assist-balance"
    | "assist-subsidiary"
    | "assist-combine"
    | "ageing"

export interface LedgerExportButtonProps {
    kind: LedgerExportKind
    /** 导出按钮显示用的一句话，如「明细账」。 */
    label?: string
    accountSetId: string
    token?: string | null
    /** 当前已提交的条件，导出与页面取数保持同一口径。 */
    conditions: LedgerConditions
}

/** 把条件拼成导出接口的 query；日期已是 yyyy-MM-dd，直接透传。 */
function buildQuery(conditions: LedgerConditions): string {
    const params = new URLSearchParams()
    const start = toISODate(conditions.startDate)
    const end = toISODate(conditions.endDate)
    if (start) params.set("startDate", start)
    if (end) params.set("endDate", end)
    const text = (value?: string) => (value && value.trim() ? value.trim() : undefined)
    for (const [key, value] of Object.entries({
        accountFrom: text(conditions.accountFrom),
        accountTo: text(conditions.accountTo),
        summaryKeyword: text(conditions.summaryKeyword),
    })) {
        if (value) params.set(key, value)
    }
    for (const [key, value] of Object.entries({
        maxLevel: conditions.maxLevel,
        minLevel: conditions.minLevel,
        sortBy: conditions.sortBy,
        assistType: conditions.assistType,
        secondaryAssistType: conditions.secondaryAssistType,
    })) {
        if (value != null) params.set(key, String(value))
    }
    for (const [key, value] of Object.entries({
        showCounterAccount: conditions.showCounterAccount,
        showAssist: conditions.showAssist,
        showOnlyLeaf: conditions.showOnlyLeaf,
        hideZeroBalance: conditions.hideZeroBalance,
        hideNoActivity: conditions.hideNoActivity,
        hideTotalsNoActivity: conditions.hideTotalsNoActivity,
        includeIdleAccounts: conditions.includeIdleAccounts,
    })) {
        if (value != null) params.set(key, value ? "true" : "false")
    }
    for (const [key, value] of Object.entries({
        assistItemId: conditions.assistItemId,
        accountId: conditions.accountId,
    })) {
        if (value) params.set(key, value)
    }
    const query = params.toString()
    return query ? `?${query}` : ""
}

/**
 * 账页导出：按当前条件向后端要一份 xlsx。
 * 后端返回的是文件流，生成的客户端只把它当成 void，故沿用日记账页的 fetch + Content-Disposition 下载写法。
 */
export function LedgerExportButton({
    kind,
    label = "导出",
    accountSetId,
    token,
    conditions,
}: LedgerExportButtonProps) {
    const [busy, setBusy] = useState(false)
    const [error, setError] = useState<string | null>(null)

    async function download() {
        if (!accountSetId || busy) return
        setBusy(true)
        setError(null)
        try {
            const response = await fetch(`/api/accountsets/${accountSetId}/Ledgers/${kind}/export${buildQuery(conditions)}`, {
                headers: token ? { Authorization: `Bearer ${token}` } : undefined,
            })
            if (!response.ok) {
                const message = await response.json().catch(() => null)
                throw new Error(message?.message ?? "导出失败，请重试。")
            }
            const disposition = response.headers.get("Content-Disposition") ?? ""
            const match = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
            const url = URL.createObjectURL(await response.blob())
            const a = document.createElement("a")
            a.href = url
            a.download = match ? decodeURIComponent(match[1]) : `${kind}.xlsx`
            a.click()
            URL.revokeObjectURL(url)
        } catch (err) {
            setError(await apiErrorMessage(err, "导出失败，请重试。"))
        } finally {
            setBusy(false)
        }
    }

    return (
        <div className="flex items-center gap-2">
            <Button
                loading={busy}
                disabled={!accountSetId}
                onClick={() => void download()}
                icon={<DownloadOutlined />}
            >
                {label}
            </Button>
            {error && <span className="text-[12.5px] text-(--danger)">{error}</span>}
        </div>
    )
}

export interface LedgerPrintButtonProps extends Omit<LedgerExportButtonProps, "label"> {
    /** 打印按钮显示用的一句话，如「明细账」。 */
    label?: string
}

/**
 * 账页打印：按当前条件让后端渲染 PDF，在新窗口打开预览。
 * 版式（A4、页码、打印时间）由后端 PrintSheetPdfBuilder 统一负责，前端不再有打印设置。
 */
export function LedgerPrintButton({ kind, label = "打印", accountSetId, token, conditions }: LedgerPrintButtonProps) {
    const [busy, setBusy] = useState(false)
    const [error, setError] = useState<string | null>(null)

    async function print() {
        if (!accountSetId || busy) return
        setBusy(true)
        setError(null)
        try {
            await printPdf(`/api/accountsets/${accountSetId}/Ledgers/${kind}/pdf${buildQuery(conditions)}`, token)
        } catch (err) {
            setError(err instanceof Error ? err.message : "生成打印稿失败，请重试。")
        } finally {
            setBusy(false)
        }
    }

    return (
        <div className="flex items-center gap-2">
            <Button loading={busy} disabled={!accountSetId} onClick={() => void print()} icon={<PrinterOutlined />}>
                {label}
            </Button>
            {error && <span className="text-[12.5px] text-(--danger)">{error}</span>}
        </div>
    )
}
