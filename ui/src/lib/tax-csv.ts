/** 智能报税各页面共用的前端 CSV 导出：带 BOM，Excel 打开中文不乱码。 */
export function downloadCsv(filename: string, rows: Array<Array<string | number>>): void {
    const csv = rows
        .map((line) => line.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(","))
        .join("\r\n");
    const blob = new Blob(["\ufeff", csv], { type: "text/csv;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    link.click();
    URL.revokeObjectURL(url);
}
