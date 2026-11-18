/**
 * 下载后端导出的报表文件（/reports/export）。
 * 文件名优先取 Content-Disposition（后端带上账套与导出日期），取不到时用 fallbackName。
 */
export async function downloadReportExport(url: string, token: string, fallbackName: string): Promise<void> {
    const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } })
    if (!res.ok) return

    const disposition = res.headers.get("content-disposition") ?? ""
    const match = /filename\*=UTF-8''([^;]+)/i.exec(disposition) ?? /filename="?([^";]+)"?/i.exec(disposition)
    const fileName = match ? decodeURIComponent(match[1]) : fallbackName

    const blobUrl = URL.createObjectURL(await res.blob())
    const a = document.createElement("a")
    a.href = blobUrl
    a.download = fileName
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(blobUrl)
}
