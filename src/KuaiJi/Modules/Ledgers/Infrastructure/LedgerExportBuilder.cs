using KuaiJi.Modules.Ledgers.Application;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace KuaiJi.Modules.Ledgers.Infrastructure
{
    /// <summary>
    /// 账页导出器：按柠檬云打印版式写成单张 .xlsx
    /// —— 标题 / 页头（编制单位、科目、期间、单位）/ 表头 / 表体。
    /// </summary>
    public static class LedgerExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>金额列的显示格式。</summary>
        private const string AmountFormat = "#,##0.00";

        /// <summary>生成导出文件的字节内容。</summary>
        public static byte[] Build(LedgerExportSheet sheet)
        {
            var columnCount = Math.Max(1, sheet.Columns.Count);
            var workbook = new XSSFWorkbook();
            var ws = workbook.CreateSheet(SanitizeSheetName(sheet.Name));
            var styles = new Styles(workbook);
            var rowIndex = 0;

            var titleRow = ws.CreateRow(rowIndex++);
            titleRow.HeightInPoints = 26f;
            SetText(titleRow.CreateCell(0), sheet.Title ?? sheet.Name, styles.Title);
            MergeRow(ws, titleRow.RowNum, columnCount);

            var metaRow = ws.CreateRow(rowIndex++);
            metaRow.HeightInPoints = 20f;
            var items = new List<string>
            {
                $"编制单位：{CompanyOf(sheet.Meta)}",
            };
            if (!string.IsNullOrWhiteSpace(sheet.Subject))
            {
                items.Add($"科目：{sheet.Subject}");
            }

            items.Add(PeriodLabel(sheet.Meta.StartDate, sheet.Meta.EndDate));
            items.Add("单位：元");
            WriteMetaRow(ws, metaRow, columnCount, items, styles.Meta);
            foreach (var pair in sheet.ExtraInfo)
            {
                var row = ws.CreateRow(rowIndex++);
                SetText(row.CreateCell(0), $"{pair.Key}：{pair.Value}", styles.Note);
                MergeRow(ws, row.RowNum, columnCount);
            }

            var headerRow = ws.CreateRow(rowIndex++);
            headerRow.HeightInPoints = 22f;
            for (var i = 0; i < columnCount; i++)
            {
                SetText(headerRow.CreateCell(i), sheet.Columns[i], styles.Header);
            }

            foreach (var values in sheet.Rows)
            {
                var row = ws.CreateRow(rowIndex++);
                for (var i = 0; i < columnCount; i++)
                {
                    WriteCell(row.CreateCell(i), i < values.Count ? values[i] : null, sheet.Columns[i], styles);
                }
            }

            for (var i = 0; i < columnCount; i++)
            {
                ws.SetColumnWidth(i, ColumnWidth(sheet.Columns[i]) * 256);
            }

            using var stream = new MemoryStream();
            workbook.Write(stream, leaveOpen: true);
            return stream.ToArray();
        }

        /// <summary>生成「账页_科目_期间_单位_导出日期」的文件名，与柠檬云导出一致。</summary>
        public static string BuildFileName(LedgerExportSheet sheet)
        {
            var parts = new List<string> { sheet.Name };
            if (!string.IsNullOrWhiteSpace(sheet.Subject) && sheet.Subject != "所有")
            {
                parts.Add(Sanitize(sheet.Subject.Replace(" ", string.Empty)));
            }

            parts.Add($"{CompactMonth(sheet.Meta.StartDate)}-{CompactMonth(sheet.Meta.EndDate)}");
            var company = CompanyOf(sheet.Meta);
            if (!string.IsNullOrWhiteSpace(company))
            {
                parts.Add(Sanitize(company));
            }

            parts.Add(DateTime.Now.ToString("yyyyMMdd"));
            return string.Join("_", parts) + ".xlsx";
        }

        private static string CompanyOf(LedgerMetaDto meta) =>
            string.IsNullOrWhiteSpace(meta.CompanyName) ? meta.AccountSetName : meta.CompanyName;

        private static void WriteMetaRow(ISheet sheet, IRow row, int columnCount, IReadOnlyList<string> items, ICellStyle style)
        {
            if (columnCount < items.Count)
            {
                SetText(row.CreateCell(0), string.Join("　", items), style);
                MergeRow(sheet, row.RowNum, columnCount);
                return;
            }

            var start = 0;
            for (var i = 0; i < items.Count; i++)
            {
                // 页头各项按列数均分，最后一项吃掉剩余列。
                var end = i == items.Count - 1
                    ? columnCount - 1
                    : Math.Max(start, (i + 1) * columnCount / items.Count - 1);
                var cell = row.CreateCell(start);
                SetText(cell, items[i], style);
                if (end > start)
                {
                    sheet.AddMergedRegion(new CellRangeAddress(row.RowNum, row.RowNum, start, end));
                }

                start = end + 1;
            }
        }

        private static void MergeRow(ISheet sheet, int rowIndex, int columnCount)
        {
            if (columnCount > 1)
            {
                sheet.AddMergedRegion(new CellRangeAddress(rowIndex, rowIndex, 0, columnCount - 1));
            }
        }

        private static void SetText(ICell cell, string? text, ICellStyle style)
        {
            cell.SetCellValue(text ?? string.Empty);
            cell.CellStyle = style;
        }

        private static void WriteCell(ICell cell, object? value, string header, Styles styles)
        {
            cell.CellStyle = IsCentered(header) ? styles.Center : IsAmount(header) ? styles.Amount : styles.Text;
            switch (value)
            {
                case null:
                    break;
                case string text:
                    cell.SetCellValue(text);
                    break;
                case decimal number:
                    cell.SetCellValue((double)number);
                    break;
                case double number:
                    cell.SetCellValue(number);
                    break;
                case int number:
                    cell.SetCellValue(number);
                    break;
                case long number:
                    cell.SetCellValue(number);
                    break;
                case DateTime date:
                    cell.SetCellValue(date);
                    break;
                default:
                    cell.SetCellValue(value.ToString() ?? string.Empty);
                    break;
            }
        }

        private static bool IsAmount(string header) =>
            header.Contains("借") || header.Contains("贷") || header.Contains("余额") || header.Contains("金额") || header.Contains("张数");

        private static bool IsCentered(string header) => header == "方向";

        private static int ColumnWidth(string header) => header switch
        {
            "日期" => 12,
            "凭证字号" or "凭证号" => 10,
            "科目编码" => 12,
            "科目名称" => 18,
            "摘要" => 40,
            "方向" => 7,
            "对方科目" => 22,
            "期间" => 12,
            _ => IsAmount(header) ? 16 : 14,
        };

        /// <summary>2025-10-01 → 2025年10月；两端同月时只显示一次。</summary>
        private static string PeriodLabel(string start, string end)
        {
            var startLabel = MonthLabel(start);
            var endLabel = MonthLabel(end);
            return startLabel == endLabel ? startLabel : $"{startLabel}至{endLabel}";
        }

        private static string MonthLabel(string date) =>
            DateOnly.TryParse(date, out var value) ? $"{value.Year}年{value.Month}月" : date;

        private static string CompactMonth(string date) =>
            DateOnly.TryParse(date, out var value) ? value.ToString("yyyyMM") : Sanitize(date);

        private static string SanitizeSheetName(string name)
        {
            var cleaned = new string(name.Where(c => c is not ('\\' or '/' or '?' or '*' or '[' or ']' or ':')).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "账页" : cleaned[..Math.Min(cleaned.Length, 31)];
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }

        /// <summary>表头与表体共用的少量样式，避免每个单元格重复创建。</summary>
        private sealed class Styles
        {
            public ICellStyle Title { get; }

            public ICellStyle Meta { get; }

            public ICellStyle Note { get; }

            public ICellStyle Header { get; }

            public ICellStyle Text { get; }

            public ICellStyle Amount { get; }

            public ICellStyle Center { get; }

            public Styles(IWorkbook workbook)
            {
                Title = CreateBase(workbook);
                Title.Alignment = HorizontalAlignment.Center;
                Title.VerticalAlignment = VerticalAlignment.Center;
                var titleFont = workbook.CreateFont();
                titleFont.IsBold = true;
                titleFont.FontHeightInPoints = 16;
                Title.SetFont(titleFont);

                Meta = CreateBase(workbook);
                Meta.VerticalAlignment = VerticalAlignment.Center;
                var metaFont = workbook.CreateFont();
                metaFont.FontHeightInPoints = 10;
                Meta.SetFont(metaFont);

                Note = CreateBase(workbook);
                var noteFont = workbook.CreateFont();
                noteFont.FontHeightInPoints = 9;
                noteFont.Color = IndexedColors.Grey50Percent.Index;
                Note.SetFont(noteFont);

                Header = CreateDataStyle(workbook, HorizontalAlignment.Center);
                Header.FillForegroundColor = IndexedColors.Grey25Percent.Index;
                Header.FillPattern = FillPattern.SolidForeground;
                var headerFont = workbook.CreateFont();
                headerFont.IsBold = true;
                Header.SetFont(headerFont);

                Text = CreateDataStyle(workbook, HorizontalAlignment.Left);
                Amount = CreateDataStyle(workbook, HorizontalAlignment.Right);
                Amount.DataFormat = workbook.CreateDataFormat().GetFormat(AmountFormat);
                Center = CreateDataStyle(workbook, HorizontalAlignment.Center);
            }

            private static ICellStyle CreateBase(IWorkbook workbook) => workbook.CreateCellStyle();

            private static ICellStyle CreateDataStyle(IWorkbook workbook, HorizontalAlignment alignment)
            {
                var style = workbook.CreateCellStyle();
                style.Alignment = alignment;
                style.VerticalAlignment = VerticalAlignment.Center;
                style.BorderTop = BorderStyle.Thin;
                style.BorderBottom = BorderStyle.Thin;
                style.BorderLeft = BorderStyle.Thin;
                style.BorderRight = BorderStyle.Thin;
                return style;
            }
        }
    }
}
