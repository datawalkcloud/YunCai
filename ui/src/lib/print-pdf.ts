import { apiErrorMessage } from "@/lib/api-client"

/**
 * 打印：请求后端生成 PDF，并在新窗口打开预览（用户在 PDF 查看器里点打印）。
 * 先同步开窗再取数，否则取数完成后的 window.open 会被浏览器判定为非用户操作而拦截。
 * 失败时关掉空白窗口并抛出可展示的中文文案，提示方式由调用方决定。
 */
export async function printPdf(url: string, token?: string | null, init?: RequestInit): Promise<void> {
    const win = window.open("", "_blank")
    try {
        const res = await fetch(url, {
            ...init,
            headers: { ...(init?.headers ?? {}), ...(token ? { Authorization: `Bearer ${token}` } : {}) },
        })
        if (!res.ok) {
            throw new Error(await apiErrorMessage({ response: res }, "生成打印稿失败，请重试。"))
        }

        const blobUrl = URL.createObjectURL(await res.blob())
        if (win) {
            win.location.href = blobUrl
        } else {
            window.open(blobUrl, "_blank")
        }
        // 留出加载时间再回收，过早 revoke 会让查看器读到空白。
        window.setTimeout(() => URL.revokeObjectURL(blobUrl), 60_000)
    } catch (err) {
        win?.close()
        throw err instanceof Error ? err : new Error("生成打印稿失败，请重试。")
    }
}
