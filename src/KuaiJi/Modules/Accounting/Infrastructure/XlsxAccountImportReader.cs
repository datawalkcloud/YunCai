using System.Globalization;
using KuaiJi.Modules.Accounting.Application;
using NPOI.SS.UserModel;

namespace KuaiJi.Modules.Accounting.Infrastructure
{
    /// <summary>
    /// 基于 NPOI 的科目导入读取器，解析「科目导入模板」首个工作表。
    /// NPOI 的 <see cref="WorkbookFactory"/> 可自动识别 .xls 与 .xlsx，兼容两种格式。
    /// 通过表头名称定位列，对列顺序变化具备一定容错能力。
    /// </summary>
    public sealed class XlsxAccountImportReader : IAccountImportReader
    {
        // 表头名称，与导出的模板列保持一致。
        private const string ColCode = "科目编码";
        private const string ColName = "科目名称";
        private const string ColCategory = "类别";
        private const string ColDirection = "余额方向";
        private const string ColParentCode = "上级科目编码";
        private const string ColMnemonic = "助记码";
        private const string ColEnabled = "是否启用";

        public IReadOnlyList<ImportedAccountRow> Read(Stream stream)
        {
            IWorkbook workbook;
            try
            {
                workbook = WorkbookFactory.Create(stream);
            }
            catch (Exception ex)
            {
                throw new AccountImportException($"无法识别的表格文件，请上传由系统导出的 .xlsx 科目导入模板。（{ex.Message}）");
            }

            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            if (sheet is null)
                throw new AccountImportException("文件中未找到任何工作表。");

            var headerRow = sheet.GetRow(sheet.FirstRowNum);
            if (headerRow is null)
                throw new AccountImportException("文件缺少表头行，请使用系统导出的科目导入模板。");

            // 以表头名称建立「列名 → 列索引」映射。
            var columnIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var c = headerRow.FirstCellNum; c < headerRow.LastCellNum; c++)
            {
                var name = GetCellString(headerRow.GetCell(c));
                if (!string.IsNullOrEmpty(name) && !columnIndex.ContainsKey(name))
                    columnIndex[name] = c;
            }

            foreach (var required in new[] { ColCode, ColName })
            {
                if (!columnIndex.ContainsKey(required))
                    throw new AccountImportException($"表头缺少必要列「{required}」，请使用系统导出的科目导入模板。");
            }

            var rows = new List<ImportedAccountRow>();
            var dataRowIndex = 0;

            for (var r = sheet.FirstRowNum + 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null) continue;

                var code = GetString(row, columnIndex, ColCode);
                var name = GetString(row, columnIndex, ColName);

                // 编码与名称均为空的行视为空行跳过。
                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
                    continue;

                dataRowIndex++;
                rows.Add(new ImportedAccountRow
                {
                    RowNumber = dataRowIndex,
                    Code = code,
                    Name = name,
                    Category = GetString(row, columnIndex, ColCategory),
                    Direction = GetString(row, columnIndex, ColDirection),
                    ParentCode = GetString(row, columnIndex, ColParentCode),
                    Mnemonic = GetString(row, columnIndex, ColMnemonic),
                    IsEnabled = ParseEnabled(GetString(row, columnIndex, ColEnabled)),
                });
            }

            if (rows.Count == 0)
                throw new AccountImportException("文件中未找到任何科目数据行。");

            return rows;
        }

        private static ICell? GetCell(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
            => columnIndex.TryGetValue(column, out var idx) ? row.GetCell(idx) : null;

        private static string GetString(IRow row, IReadOnlyDictionary<string, int> columnIndex, string column)
            => GetCellString(GetCell(row, columnIndex, column));

        /// <summary>将单元格统一转为去空白的字符串，兼顾数值/日期/公式/布尔等类型。</summary>
        private static string GetCellString(ICell? cell)
        {
            if (cell is null) return string.Empty;

            switch (cell.CellType)
            {
                case CellType.String:
                    return cell.StringCellValue?.Trim() ?? string.Empty;
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(cell))
                        return cell.DateCellValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
                    return cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
                case CellType.Boolean:
                    return cell.BooleanCellValue ? "是" : "否";
                case CellType.Formula:
                    return cell.CachedFormulaResultType switch
                    {
                        CellType.String => cell.StringCellValue?.Trim() ?? string.Empty,
                        CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                            ? cell.DateCellValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty
                            : cell.NumericCellValue.ToString(CultureInfo.InvariantCulture),
                        CellType.Boolean => cell.BooleanCellValue ? "是" : "否",
                        _ => string.Empty,
                    };
                default:
                    return string.Empty;
            }
        }

        /// <summary>解析「是否启用」列：空、是、启用、true、1 视为启用；其余视为停用。</summary>
        private static bool ParseEnabled(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            var t = text.Trim();
            if (t is "否" or "停用" or "禁用" or "0") return false;
            if (bool.TryParse(t, out var b)) return b;
            return t is not ("false" or "no" or "n" or "N" or "No" or "FALSE" or "False");
        }
    }
}
