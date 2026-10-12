import { useState } from "react"
import { Download } from "lucide-react"

import { apiErrorMessage } from "@/lib/api-client"
import { Button } from "@/components/ui/button"
import { PAGE_BTN_GHOST } from "@/components/page/styles"
import { Spinner } from "@/components/ui/spinner"
import type { LedgerConditions } from "./types"
import { asDate } from "./types"

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

/** 把条件拼成导出接口的 query；日期要还原成后端可解析的 yyyy-MM-dd。 */
function buildQuery(conditions: LedgerConditions): string {
    const params = new URLSearchParams()
    const start = asDate(conditions.startDate)
    const end = asDate(conditions.endDate)
    if (start) params.set("startDate", start.toISOString().slice(0, 10))
    if (end) params.set("endDate", end.toISOString().slice(0, 10))
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
        sortBy: conditions.sortBy,
        assistType: conditions.assistType,
        secondaryAssistType: conditions.secondaryAssistType,
    })) {
        if (value != null) params.set(key, String(value))
    }
    for (const [key, value] of Object.entries({
        showCounterAccount: conditions.showCounterAccount,
        showOnlyLeaf: conditions.showOnlyLeaf,
        hideZeroBalance: conditions.hideZeroBalance,
        hideNoActivity: conditions.hideNoActivity,
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
                type="button"
                variant="outline"
                disabled={!accountSetId || busy}
                onClick={() => void download()}
                className={PAGE_BTN_GHOST}
            >
                {busy ? <Spinner className="size-3.5" /> : <Download data-icon="inline-start" />}
                {label}
            </Button>
            {error && <span className="text-[12.5px] text-[#ef4444]">{error}</span>}
        </div>
    )
}
