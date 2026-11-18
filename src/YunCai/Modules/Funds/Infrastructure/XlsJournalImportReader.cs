using System.Globalization;
using YunCai.Modules.Funds.Application;
using NPOI.SS.UserModel;

namespace YunCai.Modules.Funds.Infrastructure
{
    /// <summary>
    /// 基于 NPOI 的日记账导入读取器，解析「日记账」工作表（.xls / .xlsx）。
    /// 通过表头名称定位列，对列顺序变化具备容错能力；导出附带的只读列（序号/余额/关联凭证号）会被忽略。
    /// </summary>
    public sealed class XlsJournalImportReader : IJournalImportReader
    {
        private const string ColDate = "日期";
        private const string ColSummary = "摘要";
        private const string ColCategory = "类别";
        private const string ColCounterparty = "往来";
        private const string ColIncome = "收入";
        private const string ColExpense = "支出";

        private static readonly string[] RequiredColumns = [ColDate, ColSummary];

        public IReadOnlyList<ImportedJournalRow> Read(Stream stream)
        {
            IWorkbook workbook;
            try
            {
                workbook = WorkbookFactory.Create(stream);
            }
            catch (Exception ex)
            {
                throw new JournalImportException(
                    $"无法识别的表格文件，请上传由系统导出的 .xls/.xlsx 日记账文件。（{ex.Message}）");
            }

            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            if (sheet is null)
            {
                throw new JournalImportException("文件中未找到任何工作表。");
            }

            var headerRow = sheet.GetRow(sheet.FirstRowNum);
            if (headerRow is null)
            {
                throw new JournalImportException("文件缺少表头行，请使用系统导出的日记账模板。");
            }

            var columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var c = headerRow.FirstCellNum; c < headerRow.LastCellNum; c++)
            {
                var name = GetCellString(headerRow.GetCell(c));
                if (!string.IsNullOrEmpty(name) && !columnIndex.ContainsKey(name))
                {
                    columnIndex[name] = c;
                }
            }

            foreach (var required in RequiredColumns)
            {
                if (!columnIndex.ContainsKey(required))
                {
                    throw new JournalImportException(
                        $"表头缺少必要列「{required}」，请使用系统导出的日记账模板。");
                }
            }

            var result = new List<ImportedJournalRow>();
            var dataRowIndex = 0;

            for (var r = sheet.FirstRowNum + 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                var summary = GetString(row, columnIndex, ColSummary);
                var income = GetDecimal(row, columnIndex, ColIncome);
                var expense = GetDecimal(row, columnIndex, ColExpense);

                // 摘要为空且无金额的行视为空行跳过（避免尾部空行报错）。
                if (string.IsNullOrWhiteSpace(summary) && income == 0m && expense == 0m)
                {
                    continue;
                }

                dataRowIndex++;
                result.Add(new ImportedJournalRow
                {
                    RowNumber = dataRowIndex,
                    Date = GetDate(row, columnIndex, ColDate),
                    Summary = summary,
                    Category = NullIfEmpty(GetString(row, columnIndex, ColCategory)),
                    Counterparty = NullIfEmpty(GetString(row, columnIndex, ColCounterparty)),
                    Income = income,
                    Expense = expense,
                });
            }

            if (result.Count == 0)
            {
                throw new JournalImportException("文件中未找到任何日记账数据行。");
            }

            return result;
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static ICell? GetCell(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
            => columnIndex.TryGetValue(column, out var idx) ? row.GetCell(idx) : null;

        private static string GetString(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
            => GetCellString(GetCell(row, columnIndex, column));

        private static string GetCellString(ICell? cell)
        {
            if (cell is null)
            {
                return string.Empty;
            }

            switch (cell.CellType)
            {
                case CellType.String:
                    return cell.StringCellValue?.Trim() ?? string.Empty;
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(cell))
                    {
                        return cell.DateCellValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
                    }

                    return cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
                case CellType.Boolean:
                    return cell.BooleanCellValue ? "true" : "false";
                case CellType.Formula:
                    return GetFormulaString(cell);
                default:
                    return string.Empty;
            }
        }

        private static string GetFormulaString(ICell cell)
        {
            switch (cell.CachedFormulaResultType)
            {
                case CellType.String:
                    return cell.StringCellValue?.Trim() ?? string.Empty;
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(cell))
                    {
                        return cell.DateCellValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
                    }

                    return cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
                case CellType.Boolean:
                    return cell.BooleanCellValue ? "true" : "false";
                default:
                    return string.Empty;
            }
        }

        private static decimal GetDecimal(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
        {
            var cell = GetCell(row, columnIndex, column);
            if (cell is null)
            {
                return 0m;
            }

            if (cell.CellType == CellType.Numeric
                || (cell.CellType == CellType.Formula && cell.CachedFormulaResultType == CellType.Numeric))
            {
                if (DateUtil.IsCellDateFormatted(cell))
                {
                    return 0m;
                }

                return (decimal)cell.NumericCellValue;
            }

            var text = GetCellString(cell);
            return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        private static DateOnly GetDate(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
        {
            var cell = GetCell(row, columnIndex, column);
            if (cell is null)
            {
                return default;
            }

            if ((cell.CellType == CellType.Numeric
                 || (cell.CellType == CellType.Formula && cell.CachedFormulaResultType == CellType.Numeric))
                && DateUtil.IsCellDateFormatted(cell)
                && cell.DateCellValue is { } dt)
            {
                return DateOnly.FromDateTime(dt);
            }

            var text = GetCellString(cell);
            if (!string.IsNullOrEmpty(text)
                && (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    || DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed)))
            {
                return DateOnly.FromDateTime(parsed);
            }

            return default;
        }
    }
}
