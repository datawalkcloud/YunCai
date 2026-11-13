using System.Globalization;
using YunCai.Modules.Funds.Domain;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>票据导入的可预期错误（格式不符等）。</summary>
    public sealed class BillImportException(string message) : Exception(message);

    /// <summary>
    /// 票据导入服务：解析「票据台账」工作表（与导出表头一致），逐行登记票据。
    /// 票据号重复、必填缺失、金额非法、日期非法的行跳过并告警，不阻断整体导入。
    /// </summary>
    public sealed class BillImportService
    {
        private const string ColCategory = "收支类别";
        private const string ColIssueDate = "出票日";
        private const string ColBillNo = "票据号";
        private const string ColAmount = "票据金额";
        private const string ColKind = "票据种类";
        private const string ColDueDate = "汇票到期日";
        private const string ColDrawer = "出票人";
        private const string ColAcceptor = "承兑人";
        private const string ColStatus = "票据状态";
        private const string ColTransferable = "能否转让";
        private const string ColPayee = "收票人";
        private const string ColDrawerAccountNo = "出票人账号";
        private const string ColPayeeAccountNo = "收票人账号";
        private const string ColAcceptorAccountNo = "承兑人账号/行号";
        private const string ColDrawerBank = "出票人开户银行";
        private const string ColPayeeBank = "收票人开户银行";
        private const string ColAcceptorBank = "承兑人开户银行";
        private const string ColRemark = "备注";

        private readonly BillService _billService;

        public BillImportService(BillService billService)
        {
            _billService = billService;
        }

        /// <summary>读取并导入票据。</summary>
        public async Task<BillImportResult> ImportAsync(Guid accountSetId, Stream stream, CancellationToken cancellationToken = default)
        {
            var rows = ReadRows(stream);
            var warnings = new List<string>();
            var imported = 0;
            var skipped = 0;

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.BillNo))
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：票据号为空，已跳过。");
                    continue;
                }

                if (row.IssueDate == default)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：出票日缺失或格式不正确，已跳过。");
                    continue;
                }

                if (row.Amount <= 0m)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：票据金额须大于 0，已跳过。");
                    continue;
                }

                try
                {
                    await _billService.CreateAsync(accountSetId, new SaveBillRequest
                    {
                        Category = row.Category,
                        IssueDate = row.IssueDate,
                        BillNo = row.BillNo,
                        Amount = row.Amount,
                        Kind = row.Kind,
                        DueDate = row.DueDate,
                        Drawer = row.Drawer,
                        Payee = row.Payee,
                        Acceptor = row.Acceptor,
                        DrawerAccountNo = row.DrawerAccountNo,
                        PayeeAccountNo = row.PayeeAccountNo,
                        AcceptorAccountNo = row.AcceptorAccountNo,
                        DrawerBank = row.DrawerBank,
                        PayeeBank = row.PayeeBank,
                        AcceptorBank = row.AcceptorBank,
                        Transferable = row.Transferable,
                        Status = row.Status,
                        Remark = row.Remark,
                    }, cancellationToken);
                    imported++;
                }
                catch (BillException ex)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：{ex.Message}已跳过。");
                }
            }

            return new BillImportResult
            {
                TotalRows = rows.Count,
                Imported = imported,
                Skipped = skipped,
                Warnings = warnings,
            };
        }

        /// <summary>生成导入模板（表头 + 一行示例）。</summary>
        public static byte[] BuildTemplate()
        {
            using var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("票据台账");
            var header = sheet.CreateRow(0);
            var columns = new[]
            {
                ColCategory, ColIssueDate, ColBillNo, ColAmount, ColKind, ColDueDate,
                ColDrawer, ColAcceptor, ColStatus, ColTransferable, ColPayee,
                ColDrawerAccountNo, ColPayeeAccountNo, ColAcceptorAccountNo,
                ColDrawerBank, ColPayeeBank, ColAcceptorBank, ColRemark,
            };

            var style = workbook.CreateCellStyle();
            var font = workbook.CreateFont();
            font.IsBold = true;
            style.SetFont(font);

            for (var i = 0; i < columns.Length; i++)
            {
                var cell = header.CreateCell(i);
                cell.SetCellValue(columns[i]);
                cell.CellStyle = style;
                sheet.SetColumnWidth(i, 18 * 256);
            }

            var sample = sheet.CreateRow(1);
            var today = DateTime.Today;
            sample.CreateCell(0).SetCellValue("销售收款");
            sample.CreateCell(1).SetCellValue(today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            sample.CreateCell(2).SetCellValue("3100000001");
            sample.CreateCell(3).SetCellValue(100000);
            sample.CreateCell(4).SetCellValue("银行汇票");
            sample.CreateCell(5).SetCellValue(today.AddMonths(6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            sample.CreateCell(6).SetCellValue("杭州示例出票有限公司");
            sample.CreateCell(7).SetCellValue("中国工商银行杭州分行");
            sample.CreateCell(8).SetCellValue("提示收票已签收");
            sample.CreateCell(9).SetCellValue("可转让");
            sample.CreateCell(10).SetCellValue("杭州数从科技有限公司");
            sample.CreateCell(11).SetCellValue("1202020109900012345");
            sample.CreateCell(12).SetCellValue("1202020109900067890");
            sample.CreateCell(13).SetCellValue("102331000123");
            sample.CreateCell(14).SetCellValue("工商银行杭州城西支行");
            sample.CreateCell(15).SetCellValue("工商银行杭州城西支行");
            sample.CreateCell(16).SetCellValue("工商银行杭州分行");
            sample.CreateCell(17).SetCellValue("");

            using var stream = new MemoryStream();
            workbook.Write(stream, leaveOpen: true);
            return stream.ToArray();
        }

        private static List<ImportRow> ReadRows(Stream stream)
        {
            IWorkbook workbook;
            try
            {
                workbook = WorkbookFactory.Create(stream);
            }
            catch (Exception ex)
            {
                throw new BillImportException($"无法识别的表格文件，请上传 .xls/.xlsx 格式。（{ex.Message}）");
            }

            using (workbook)
            {
                var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
                var headerRow = sheet?.GetRow(sheet.FirstRowNum);
                if (sheet is null || headerRow is null)
                {
                    throw new BillImportException("文件中未找到工作表或表头行，请使用系统导出的模板。");
                }

                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                for (var c = headerRow.FirstCellNum; c < headerRow.LastCellNum; c++)
                {
                    var name = headerRow.GetCell(c)?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(name) && !index.ContainsKey(name))
                    {
                        index[name] = c;
                    }
                }

                foreach (var required in new[] { ColBillNo, ColIssueDate, ColAmount })
                {
                    if (!index.ContainsKey(required))
                    {
                        throw new BillImportException($"表头缺少必要列「{required}」，请使用系统导出的模板。");
                    }
                }

                var rows = new List<ImportRow>();
                for (var r = sheet.FirstRowNum + 1; r <= sheet.LastRowNum; r++)
                {
                    var row = sheet.GetRow(r);
                    if (row is null)
                    {
                        continue;
                    }

                    var billNo = GetString(row, index, ColBillNo);
                    if (string.IsNullOrWhiteSpace(billNo))
                    {
                        continue;
                    }

                    var transferableText = GetString(row, index, ColTransferable);
                    rows.Add(new ImportRow
                    {
                        RowNumber = r + 1,
                        Category = GetString(row, index, ColCategory),
                        IssueDate = GetDate(row, index, ColIssueDate),
                        BillNo = billNo,
                        Amount = GetDecimal(row, index, ColAmount),
                        Kind = ParseKind(GetString(row, index, ColKind)),
                        DueDate = GetNullableDate(row, index, ColDueDate),
                        Drawer = NullIfEmpty(GetString(row, index, ColDrawer)),
                        Payee = NullIfEmpty(GetString(row, index, ColPayee)),
                        Acceptor = NullIfEmpty(GetString(row, index, ColAcceptor)),
                        DrawerAccountNo = NullIfEmpty(GetString(row, index, ColDrawerAccountNo)),
                        PayeeAccountNo = NullIfEmpty(GetString(row, index, ColPayeeAccountNo)),
                        AcceptorAccountNo = NullIfEmpty(GetString(row, index, ColAcceptorAccountNo)),
                        DrawerBank = NullIfEmpty(GetString(row, index, ColDrawerBank)),
                        PayeeBank = NullIfEmpty(GetString(row, index, ColPayeeBank)),
                        AcceptorBank = NullIfEmpty(GetString(row, index, ColAcceptorBank)),
                        Transferable = !transferableText.Contains("不可", StringComparison.Ordinal),
                        Status = ParseStatus(GetString(row, index, ColStatus)),
                        Remark = NullIfEmpty(GetString(row, index, ColRemark)),
                    });
                }

                return rows;
            }
        }

        private static int ParseKind(string text) => text.Trim() switch
        {
            "商业汇票" or "商业承兑汇票" => (int)BillKind.CommercialAcceptance,
            "支票" => (int)BillKind.Cheque,
            "其他" => (int)BillKind.Other,
            _ => (int)BillKind.BankAcceptance,
        };

        private static int? ParseStatus(string text) => text.Trim() switch
        {
            "提示收票已签收" => (int)BillStatus.Holding,
            "背书待签收" => (int)BillStatus.Endorsing,
            "背书已签收" => (int)BillStatus.Endorsed,
            "买断式贴现已签收" or "已贴现" => (int)BillStatus.Discounted,
            "票据已结清" or "已兑现" => (int)BillStatus.Cashed,
            "已托收" => (int)BillStatus.Collected,
            "已作废" => (int)BillStatus.Voided,
            _ => null,
        };

        private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static string GetString(IRow row, Dictionary<string, int> index, string column)
        {
            if (!index.TryGetValue(column, out var c))
            {
                return string.Empty;
            }

            var cell = row.GetCell(c);
            if (cell is null)
            {
                return string.Empty;
            }

            if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
            {
                return cell.DateCellValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return cell.ToString()?.Trim() ?? string.Empty;
        }

        private static decimal GetDecimal(IRow row, Dictionary<string, int> index, string column)
        {
            if (!index.TryGetValue(column, out var c))
            {
                return 0m;
            }

            var cell = row.GetCell(c);
            if (cell is null)
            {
                return 0m;
            }

            if (cell.CellType == CellType.Numeric)
            {
                return (decimal)cell.NumericCellValue;
            }

            var text = cell.ToString()?.Replace(",", string.Empty).Replace("￥", string.Empty).Trim();
            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m;
        }

        private static DateOnly GetDate(IRow row, Dictionary<string, int> index, string column)
        {
            var parsed = GetNullableDate(row, index, column);
            return parsed ?? default;
        }

        private static DateOnly? GetNullableDate(IRow row, Dictionary<string, int> index, string column)
        {
            if (!index.TryGetValue(column, out var c))
            {
                return null;
            }

            var cell = row.GetCell(c);
            if (cell is null)
            {
                return null;
            }

            if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
            {
                var dt = cell.DateCellValue;
                return dt is null ? null : DateOnly.FromDateTime(dt.Value);
            }

            var text = cell.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return DateOnly.FromDateTime(parsed);
            }

            foreach (var format in new[] { "yyyy-MM-dd", "yyyy/M/d", "yyyy.MM.dd", "yyyyMMdd" })
            {
                if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                {
                    return DateOnly.FromDateTime(parsed);
                }
            }

            return null;
        }

        private sealed class ImportRow
        {
            public int RowNumber { get; init; }

            public string Category { get; init; } = string.Empty;

            public DateOnly IssueDate { get; init; }

            public string BillNo { get; init; } = string.Empty;

            public decimal Amount { get; init; }

            public int Kind { get; init; } = (int)BillKind.BankAcceptance;

            public DateOnly? DueDate { get; init; }

            public string? Drawer { get; init; }

            public string? Payee { get; init; }

            public string? Acceptor { get; init; }

            public string? DrawerAccountNo { get; init; }

            public string? PayeeAccountNo { get; init; }

            public string? AcceptorAccountNo { get; init; }

            public string? DrawerBank { get; init; }

            public string? PayeeBank { get; init; }

            public string? AcceptorBank { get; init; }

            public bool Transferable { get; init; } = true;

            public int? Status { get; init; }

            public string? Remark { get; init; }
        }
    }
}
