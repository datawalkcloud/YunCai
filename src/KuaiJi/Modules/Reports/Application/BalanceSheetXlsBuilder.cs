using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace KuaiJi.Modules.Reports.Application
{
    public sealed record BalanceSheetXlsModel(
        string CompanyName,
        string DateText,
        string TemplateTitle,
        IReadOnlyList<BalanceSheetExportRow> Left,
        IReadOnlyList<BalanceSheetExportRow> Right);

    /// <summary>
    /// 资产负债表 .xls 导出：两栏八列版式（左资产、右负债和所有者权益），
    /// 抬头含报表名、版式角标、编制单位/日期/金额单位。使用项目已有的 NPOI（HSSF），不依赖 Office。
    /// </summary>
    public static class BalanceSheetXlsBuilder
    {
        public const string ContentType = "application/vnd.ms-excel";

        private const int ColumnCount = 8;
        private const int HeaderRowIndex = 3;
        private const int FirstBodyRowIndex = 4;

        public static byte[] Build(BalanceSheetXlsModel model)
        {
            var workbook = new HSSFWorkbook();
            var sheet = workbook.CreateSheet("资产负债表");
            var styles = new Styles(workbook);

            for (var column = 0; column < ColumnCount; column++)
            {
                sheet.SetColumnWidth(column, (column is 0 or 4 ? 30 : column is 1 or 5 ? 7 : 15) * 256);
            }

            WriteTitle(sheet, styles, model);
            WriteHeader(sheet, styles);
            WriteBody(sheet, styles, model);

            sheet.PrintSetup.Landscape = true;
            sheet.FitToPage = true;
            sheet.PrintSetup.FitWidth = 1;
            sheet.PrintSetup.FitHeight = 0;

            using var stream = new MemoryStream();
            workbook.Write(stream, true);
            return stream.ToArray();
        }

        private static void WriteTitle(ISheet sheet, Styles styles, BalanceSheetXlsModel model)
        {
            var titleRow = sheet.CreateRow(0);
            titleRow.HeightInPoints = 30;
            var title = titleRow.CreateCell(0);
            title.SetCellValue("资产负债表");
            title.CellStyle = styles.Title;
            sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, ColumnCount - 1));

            var templateRow = sheet.CreateRow(1);
            templateRow.HeightInPoints = 18;
            var template = templateRow.CreateCell(0);
            template.SetCellValue(model.TemplateTitle);
            template.CellStyle = styles.Template;
            sheet.AddMergedRegion(new CellRangeAddress(1, 1, 0, ColumnCount - 1));

            var metaRow = sheet.CreateRow(2);
            metaRow.HeightInPoints = 18;
            var company = metaRow.CreateCell(0);
            company.SetCellValue($"编制单位：{model.CompanyName}");
            company.CellStyle = styles.MetaLeft;
            sheet.AddMergedRegion(new CellRangeAddress(2, 2, 0, 2));

            var date = metaRow.CreateCell(3);
            date.SetCellValue(model.DateText);
            date.CellStyle = styles.MetaCenter;
            sheet.AddMergedRegion(new CellRangeAddress(2, 2, 3, 4));

            var unit = metaRow.CreateCell(5);
            unit.SetCellValue("单位：元");
            unit.CellStyle = styles.MetaRight;
            sheet.AddMergedRegion(new CellRangeAddress(2, 2, 5, ColumnCount - 1));
        }

        private static void WriteHeader(ISheet sheet, Styles styles)
        {
            var row = sheet.CreateRow(HeaderRowIndex);
            row.HeightInPoints = 22;
            string[] headers = ["资产", "行次", "期末余额", "年初余额", "负债和所有者权益", "行次", "期末余额", "年初余额"];
            for (var column = 0; column < ColumnCount; column++)
            {
                var cell = row.CreateCell(column);
                cell.SetCellValue(headers[column]);
                cell.CellStyle = styles.Header;
            }
        }

        private static void WriteBody(ISheet sheet, Styles styles, BalanceSheetXlsModel model)
        {
            var rowCount = Math.Max(model.Left.Count, model.Right.Count);
            for (var index = 0; index < rowCount; index++)
            {
                var row = sheet.CreateRow(FirstBodyRowIndex + index);
                row.HeightInPoints = 19;

                var left = index < model.Left.Count ? model.Left[index] : null;
                var right = index < model.Right.Count ? model.Right[index] : null;
                WriteSide(row, 0, left, styles);
                WriteSide(row, 4, right, styles);
            }
        }

        private static void WriteSide(IRow row, int firstColumn, BalanceSheetExportRow? source, Styles styles)
        {
            if (source is null)
            {
                for (var column = firstColumn; column < firstColumn + 4; column++)
                {
                    row.CreateCell(column).CellStyle = styles.Blank;
                }

                return;
            }

            var isTotal = source.Kind == BalanceSheetRowKind.Total;
            var isSection = source.Kind == BalanceSheetRowKind.Section;
            var isDetail = source.Kind == BalanceSheetRowKind.Detail;

            var label = row.CreateCell(firstColumn);
            label.SetCellValue(source.Label);
            label.CellStyle = isTotal
                ? styles.LabelBold
                : isSection
                    ? styles.Section
                    : isDetail
                        ? styles.Detail
                        : styles.Label;

            var line = row.CreateCell(firstColumn + 1);
            if (source.Line is { } number)
            {
                line.SetCellValue(number);
            }

            line.CellStyle = isTotal ? styles.LineBold : styles.Line;

            WriteAmount(row.CreateCell(firstColumn + 2), source.Ending, isSection, isTotal, styles);
            WriteAmount(row.CreateCell(firstColumn + 3), source.Opening, isSection, isTotal, styles);
        }

        private static void WriteAmount(ICell cell, decimal? value, bool isSection, bool isTotal, Styles styles)
        {
            if (!isSection && value is { } amount)
            {
                cell.SetCellValue((double)amount);
            }

            cell.CellStyle = isTotal ? styles.AmountBold : styles.Amount;
        }

        private sealed class Styles
        {
            public Styles(IWorkbook workbook)
            {
                Title = CreateBase(workbook, 16, bold: true);
                Template = CreateBase(workbook, 11, bold: false);

                MetaLeft = CreateBase(workbook, 10, bold: false);
                MetaLeft.Alignment = HorizontalAlignment.Left;

                var metaCenter = CreateBase(workbook, 10, bold: false);
                metaCenter.Alignment = HorizontalAlignment.Center;
                MetaCenter = metaCenter;

                var metaRight = CreateBase(workbook, 10, bold: false);
                metaRight.Alignment = HorizontalAlignment.Right;
                MetaRight = metaRight;

                var header = CreateBase(workbook, 10, bold: true);
                header.FillForegroundColor = IndexedColors.Grey25Percent.Index;
                header.FillPattern = FillPattern.SolidForeground;
                Header = header;

                Label = CreateBordered(workbook, 10, bold: false, HorizontalAlignment.Left);
                Label.Indention = 1;

                LabelBold = CreateBordered(workbook, 10, bold: true, HorizontalAlignment.Left);
                LabelBold.Indention = 1;

                Section = CreateBordered(workbook, 10, bold: false, HorizontalAlignment.Left);
                Detail = CreateBordered(workbook, 10, bold: false, HorizontalAlignment.Left);
                Detail.Indention = 2;

                Line = CreateBordered(workbook, 10, bold: false, HorizontalAlignment.Center);
                LineBold = CreateBordered(workbook, 10, bold: true, HorizontalAlignment.Center);

                var amount = CreateBordered(workbook, 10, bold: false, HorizontalAlignment.Right);
                amount.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");
                Amount = amount;

                var amountBold = CreateBordered(workbook, 10, bold: true, HorizontalAlignment.Right);
                amountBold.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");
                AmountBold = amountBold;

                Blank = CreateBordered(workbook, 10, bold: false, HorizontalAlignment.Center);
            }

            public ICellStyle Title { get; }
            public ICellStyle Template { get; }
            public ICellStyle MetaLeft { get; }
            public ICellStyle MetaCenter { get; }
            public ICellStyle MetaRight { get; }
            public ICellStyle Header { get; }
            public ICellStyle Label { get; }
            public ICellStyle LabelBold { get; }
            public ICellStyle Section { get; }
            public ICellStyle Detail { get; }
            public ICellStyle Line { get; }
            public ICellStyle LineBold { get; }
            public ICellStyle Amount { get; }
            public ICellStyle AmountBold { get; }
            public ICellStyle Blank { get; }

            private static ICellStyle CreateBase(IWorkbook workbook, short fontSize, bool bold)
            {
                var style = workbook.CreateCellStyle();
                var font = workbook.CreateFont();
                font.FontHeightInPoints = fontSize;
                font.IsBold = bold;
                style.SetFont(font);
                style.Alignment = HorizontalAlignment.Center;
                style.VerticalAlignment = VerticalAlignment.Center;
                return style;
            }

            private static ICellStyle CreateBordered(
                IWorkbook workbook, short fontSize, bool bold, HorizontalAlignment alignment)
            {
                var style = CreateBase(workbook, fontSize, bold);
                style.Alignment = alignment;
                style.BorderTop = BorderStyle.Thin;
                style.BorderBottom = BorderStyle.Thin;
                style.BorderLeft = BorderStyle.Thin;
                style.BorderRight = BorderStyle.Thin;
                return style;
            }
        }
    }
}
