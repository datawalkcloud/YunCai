/** 报表导出下载：带认证头拉取 .xlsx 二进制并触发浏览器保存。 */
export async function downloadReportExport(
    token: string,
    accountSetId: string,
    kind: string,
    params: { year?: number; month?: number; quarter?: number },
): Promise<void> {
    const qs = new URLSearchParams({ kind });
    if (params.year) {
        qs.set("year", String(params.year));
    }
    if (params.month) {
        qs.set("month", String(params.month));
    }
    if (params.quarter) {
        qs.set("quarter", String(params.quarter));
    }

    const res = await fetch(`/api/accountsets/${accountSetId}/reports/export?${qs.toString()}`, {
        headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) {
        let message = "导出失败";
        try {
            const body = (await res.json()) as { message?: string };
            message = body.message ?? message;
        } catch {
            // 非 JSON 错误体时保持默认提示
        }
        throw new Error(message);
    }

    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    // 优先用 Content-Disposition 里的服务端文件名，取不到回退 kind。
    const disposition = res.headers.get("Content-Disposition") ?? "";
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    anchor.download = match ? decodeURIComponent(match[1]) : `${kind}.xlsx`;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    URL.revokeObjectURL(url);
}
