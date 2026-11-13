using System.Globalization;
using YunCai.Modules.Vouchers.Application;
using NPOI.SS.UserModel;

namespace YunCai.Modules.Vouchers.Infrastructure
{
    /// <summary>
    /// 基于 NPOI 的凭证导入读取器，解析外部平台导出的「凭证列表」工作表。
    /// NPOI 的 <see cref="WorkbookFactory"/> 可自动识别旧版 .xls（BIFF）与新版 .xlsx（OOXML），
    /// 因此同时支持两种格式。通过表头名称定位列，对列顺序变化具备一定容错能力。
    /// </summary>
    public sealed class XlsVoucherImportReader : IVoucherImportReader
    {
        // 表头名称 → 内部字段。与外部平台导出的列名保持一致。
        private const string ColDate = "日期";
        private const string ColVoucherNo = "凭证号";
        private const string ColLineNumber = "行号";
        private const string ColSummary = "摘要";
        private const string ColAccountCode = "科目编码";
        private const string ColAccountName = "科目名称";
        private const string ColDebit = "借方本币";
        private const string ColCredit = "贷方本币";
        private const string ColPreparer = "制单人";
        private const string ColReviewer = "审核人";
        private const string ColAttachment = "附件数";

        private static readonly string[] RequiredColumns =
            [ColDate, ColVoucherNo, ColAccountCode, ColDebit, ColCredit];

        public IReadOnlyList<ImportedVoucherRow> Read(Stream stream)
        {
            IWorkbook workbook;
            try
            {
                // WorkbookFactory 依据文件头自动选择 HSSF(.xls) / XSSF(.xlsx) 实现。
                workbook = WorkbookFactory.Create(stream);
            }
            catch (Exception ex)
            {
                throw new VoucherImportException(
                    $"无法识别的表格文件，请上传由平台导出的 .xls/.xlsx 凭证文件。（{ex.Message}）");
            }

            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            if (sheet is null)
            {
                throw new VoucherImportException("文件中未找到任何工作表。");
            }

            var headerRow = sheet.GetRow(sheet.FirstRowNum);
            if (headerRow is null)
            {
                throw new VoucherImportException("文件缺少表头行，请确认使用平台导出的凭证格式。");
            }

            // 以表头名称建立「列名 → 列索引」映射（后出现的同名列不覆盖）。
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
                    throw new VoucherImportException(
                        $"表头缺少必要列「{required}」，请确认使用平台导出的凭证格式。");
                }
            }

            var result = new List<ImportedVoucherRow>();
            var dataRowIndex = 0;

            for (var r = sheet.FirstRowNum + 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                var voucherNo = GetString(row, columnIndex, ColVoucherNo);
                var accountCode = GetString(row, columnIndex, ColAccountCode);

                // 完全空白 / 无凭证号且无科目的行视为空行跳过（避免尾部空行报错）。
                if (string.IsNullOrWhiteSpace(voucherNo) && string.IsNullOrWhiteSpace(accountCode))
                {
                    continue;
                }

                dataRowIndex++;
                result.Add(new ImportedVoucherRow
                {
                    RowNumber = dataRowIndex,
                    Date = GetDate(row, columnIndex, ColDate),
                    VoucherNo = voucherNo,
                    LineNumber = GetInt(row, columnIndex, ColLineNumber),
                    Summary = GetString(row, columnIndex, ColSummary),
                    AccountCode = accountCode,
                    AccountName = GetString(row, columnIndex, ColAccountName),
                    Debit = GetDecimal(row, columnIndex, ColDebit),
                    Credit = GetDecimal(row, columnIndex, ColCredit),
                    Preparer = GetString(row, columnIndex, ColPreparer),
                    Reviewer = GetString(row, columnIndex, ColReviewer),
                    AttachmentCount = GetInt(row, columnIndex, ColAttachment),
                });
            }

            if (result.Count == 0)
            {
                throw new VoucherImportException("文件中未找到任何凭证数据行。");
            }

            return result;
        }

        /// <summary>取列对应单元格；列不存在或行短于列索引时返回 null。</summary>
        private static ICell? GetCell(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
        {
            return columnIndex.TryGetValue(column, out var idx) ? row.GetCell(idx) : null;
        }

        private static string GetString(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
            => GetCellString(GetCell(row, columnIndex, column));

        /// <summary>将单元格统一转为去空白的字符串，兼顾数值/日期/公式/布尔等类型。</summary>
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

        /// <summary>读取公式单元格的缓存计算结果。</summary>
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

        private static int GetInt(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
        {
            var d = GetDecimal(row, columnIndex, column);
            return (int)Math.Round(d, MidpointRounding.AwayFromZero);
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
