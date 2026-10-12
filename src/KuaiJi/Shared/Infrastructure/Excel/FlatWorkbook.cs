using MiniExcelLibs;

namespace KuaiJi.SharedKernel.Infrastructure.Excel
{
    /// <summary>
    /// 通用扁平工作簿描述：页头信息表 + 单数据表（压平列名 + 行数据），由 <see cref="FlatWorkbookBuilder"/> 写成 .xlsx。
    /// 各模块的导出（账页/报表）统一落到此结构，避免每个模块复制 MiniExcel 书写逻辑。
    /// </summary>
    /// <param name="InfoSheetName">页头信息工作表名。</param>
    /// <param name="Info">页头键值对（账套、期间、口径说明等）。</param>
    /// <param name="DataSheetName">数据工作表名。</param>
    /// <param name="Columns">压平后的列名，顺序与 Rows 每行单元格一致。</param>
    /// <param name="Rows">表体行（含合计行）。</param>
    /// <param name="FileName">下载文件名。</param>
    public sealed record FlatWorkbook(
        string InfoSheetName,
        IReadOnlyList<(string Label, string Value)> Info,
        string DataSheetName,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<object?>> Rows,
        string FileName)
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }

    /// <summary>把 <see cref="FlatWorkbook"/> 写成 .xlsx 字节流。</summary>
    public static class FlatWorkbookBuilder
    {
        /// <summary>生成导出文件的字节内容。空数据集输出一行占位，否则 MiniExcel 写不出表头列名。</summary>
        public static byte[] Build(FlatWorkbook book)
        {
            var info = book.Info
                .Select(pair => new Dictionary<string, object?> { ["项目"] = pair.Label, ["内容"] = pair.Value })
                .ToList();

            var body = book.Rows.Count == 0
                ? new List<IReadOnlyList<object?>> { book.Columns.Select(_ => (object?)string.Empty).ToList() }
                : book.Rows;

            var sheets = new Dictionary<string, object>
            {
                [book.InfoSheetName] = info,
                [book.DataSheetName] = body.Select(row => ToRowDictionary(book.Columns, row)).ToList(),
            };

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, sheets, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        private static Dictionary<string, object?> ToRowDictionary(IReadOnlyList<string> columns, IReadOnlyList<object?> row)
        {
            var cells = new Dictionary<string, object?>(columns.Count);
            for (var i = 0; i < columns.Count; i++)
            {
                cells[columns[i]] = i < row.Count ? row[i] : null;
            }

            return cells;
        }
    }
}
