using System.Globalization;
using KuaiJi.Modules.Funds.Domain;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>内部转账导入的可预期错误（格式不符、账户不存在等）。</summary>
    public sealed class TransferImportException(string message) : Exception(message);

    /// <summary>内部转账导入结果。</summary>
    public sealed class TransferImportResult
    {
        /// <summary>文件中的数据行总数（不含表头）。</summary>
        public int TotalRows { get; init; }

        /// <summary>成功导入的转账笔数。</summary>
        public int Imported { get; init; }

        /// <summary>被跳过的行数。</summary>
        public int Skipped { get; init; }

        /// <summary>逐行告警（跳过原因）。</summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    /// <summary>
    /// 内部转账导入服务：解析「内部转账」工作表（日期 / 摘要 / 转出账户 / 转入账户 / 金额 / 备注），
    /// 按账户名称匹配资金账户后逐行登记转账。非法行跳过并告警，不阻断整体导入。
    /// 表头可由本页「导出」产生，也可用模板填写。
    /// </summary>
    public sealed class TransferImportService
    {
        private const string ColDate = "日期";
        private const string ColSummary = "摘要";
        private const string ColFrom = "转出账户";
        private const string ColTo = "转入账户";
        private const string ColAmount = "金额";
        private const string ColRemark = "备注";

        private readonly TransferService _transferService;
        private readonly IJournalRepository _journalRepository;

        public TransferImportService(TransferService transferService, IJournalRepository journalRepository)
        {
            _transferService = transferService;
            _journalRepository = journalRepository;
        }

        /// <summary>读取并导入内部转账。账户按名称（或「编码 名称」）匹配，找不到则跳过该行。</summary>
        public async Task<TransferImportResult> ImportAsync(Guid accountSetId, Stream stream, CancellationToken cancellationToken = default)
        {
            var rows = ReadRows(stream);
            var journals = await _journalRepository.GetAllAsync(accountSetId, cancellationToken);
            var byName = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (var journal in journals)
            {
                if (journal.IsEnabled)
                {
                    byName[journal.Name.Trim()] = journal.Id;
                }
            }

            var warnings = new List<string>();
            var imported = 0;
            var skipped = 0;

            foreach (var row in rows)
            {
                if (row.Date == default)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：日期缺失或格式不正确，已跳过。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Summary))
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：摘要为空，已跳过。");
                    continue;
                }

                if (row.Amount <= 0)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：金额须大于 0，已跳过。");
                    continue;
                }

                if (!byName.TryGetValue(row.FromName.Trim(), out var fromId))
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：转出账户「{row.FromName}」不存在或已停用，已跳过。");
                    continue;
                }

                if (!byName.TryGetValue(row.ToName.Trim(), out var toId))
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：转入账户「{row.ToName}」不存在或已停用，已跳过。");
                    continue;
                }

                if (fromId == toId)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：转出与转入账户相同，已跳过。");
                    continue;
                }

                try
                {
                    await _transferService.CreateAsync(accountSetId, new SaveTransferRequest
                    {
                        Date = row.Date,
                        Summary = row.Summary.Trim(),
                        FromJournalId = fromId,
                        ToJournalId = toId,
                        Amount = row.Amount,
                        Remark = string.IsNullOrWhiteSpace(row.Remark) ? null : row.Remark.Trim(),
                    }, cancellationToken);
                    imported++;
                }
                catch (JournalException ex)
                {
                    skipped++;
                    warnings.Add($"第 {row.RowNumber} 行：{ex.Message}");
                }
            }

            return new TransferImportResult
            {
                TotalRows = rows.Count,
                Imported = imported,
                Skipped = skipped,
                Warnings = warnings,
            };
        }

        /// <summary>生成导入模板（.xlsx），含表头与一行示例。</summary>
        public static byte[] BuildTemplate()
        {
            using var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("内部转账");
            var header = sheet.CreateRow(0);
            var columns = new[] { ColDate, ColSummary, ColFrom, ColTo, ColAmount, ColRemark };
            for (var i = 0; i < columns.Length; i++)
            {
                header.CreateCell(i).SetCellValue(columns[i]);
                sheet.SetColumnWidth(i, 18 * 256);
            }

            var sample = sheet.CreateRow(1);
            sample.CreateCell(0).SetCellValue(DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            sample.CreateCell(1).SetCellValue("资金内部调拨");
            sample.CreateCell(2).SetCellValue("现金");
            sample.CreateCell(3).SetCellValue("工行");
            sample.CreateCell(4).SetCellValue(1000);
            sample.CreateCell(5).SetCellValue("从现金存入银行");

            using var stream = new MemoryStream();
            workbook.Write(stream);
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
                throw new TransferImportException($"无法识别的表格文件，请上传 .xls/.xlsx 格式。（{ex.Message}）");
            }

            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            var headerRow = sheet?.GetRow(sheet.FirstRowNum);
            if (sheet is null || headerRow is null)
            {
                throw new TransferImportException("文件中未找到工作表或表头行，请使用系统导出的模板。");
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

            foreach (var required in new[] { ColDate, ColFrom, ColTo, ColAmount })
            {
                if (!index.ContainsKey(required))
                {
                    throw new TransferImportException($"表头缺少必要列「{required}」，请使用系统导出的模板。");
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

                var summary = GetString(row, index, ColSummary);
                var from = GetString(row, index, ColFrom);
                var to = GetString(row, index, ColTo);
                var amount = GetDecimal(row, index, ColAmount);
                if (string.IsNullOrWhiteSpace(summary) && string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to))
                {
                    continue;
                }

                rows.Add(new ImportRow
                {
                    RowNumber = r + 1,
                    Date = GetDate(row, index, ColDate),
                    Summary = summary,
                    FromName = from,
                    ToName = to,
                    Amount = amount,
                    Remark = GetString(row, index, ColRemark),
                });
            }

            return rows;
        }

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
            if (!index.TryGetValue(column, out var c))
            {
                return default;
            }

            var cell = row.GetCell(c);
            if (cell is null)
            {
                return default;
            }

            if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
            {
                var dt = cell.DateCellValue;
                return dt is null ? default : DateOnly.FromDateTime(dt.Value);
            }

            var text = cell.ToString()?.Trim() ?? string.Empty;
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

            return default;
        }

        private sealed class ImportRow
        {
            public int RowNumber { get; init; }

            public DateOnly Date { get; init; }

            public string Summary { get; init; } = string.Empty;

            public string FromName { get; init; } = string.Empty;

            public string ToName { get; init; } = string.Empty;

            public decimal Amount { get; init; }

            public string Remark { get; init; } = string.Empty;
        }
    }
}
