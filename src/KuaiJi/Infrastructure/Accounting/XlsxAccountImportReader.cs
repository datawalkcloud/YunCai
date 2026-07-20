using System.Globalization;
using KuaiJi.Application.Accounts;
using MiniExcelLibs;

namespace KuaiJi.Infrastructure.Accounting
{
    /// <summary>
    /// 基于 MiniExcel 的科目导入读取器，解析「科目导入模板」工作表（.xlsx）。
    /// 通过表头名称定位列，对列顺序变化具备一定容错能力（MiniExcel 不支持旧版 .xls）。
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
            // 拷贝到内存，避免上游转发流不可多次读取。
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;

            List<IDictionary<string, object>> records;
            try
            {
                records = buffer.Query(useHeaderRow: true)
                    .Cast<IDictionary<string, object>>()
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new AccountImportException($"无法识别的表格文件，请上传由系统导出的 .xlsx 科目导入模板。（{ex.Message}）");
            }

            if (records.Count == 0)
            {
                throw new AccountImportException("文件中未找到任何科目数据行。");
            }

            // 依据首行的键构建「期望列名 → 实际键」映射（对表头空白具备容错）。
            var keyMap = MapColumns(records[0].Keys);
            foreach (var required in new[] { ColCode, ColName })
            {
                if (!keyMap.ContainsKey(required))
                {
                    throw new AccountImportException($"表头缺少必要列「{required}」，请使用系统导出的科目导入模板。");
                }
            }

            var rows = new List<ImportedAccountRow>();
            var dataRowIndex = 0;
            foreach (var record in records)
            {
                var code = GetString(record, keyMap, ColCode);
                var name = GetString(record, keyMap, ColName);

                // 编码与名称均为空的行视为空行跳过（避免尾部空行报错）。
                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                dataRowIndex++;
                rows.Add(new ImportedAccountRow
                {
                    RowNumber = dataRowIndex,
                    Code = code,
                    Name = name,
                    Category = GetString(record, keyMap, ColCategory),
                    Direction = GetString(record, keyMap, ColDirection),
                    ParentCode = GetString(record, keyMap, ColParentCode),
                    Mnemonic = GetString(record, keyMap, ColMnemonic),
                    IsEnabled = ParseEnabled(GetString(record, keyMap, ColEnabled)),
                });
            }

            if (rows.Count == 0)
            {
                throw new AccountImportException("文件中未找到任何科目数据行。");
            }

            return rows;
        }

        private static Dictionary<string, string> MapColumns(IEnumerable<string> headerKeys)
        {
            // 期望列名 → 实际键（按去除首尾空白后的名称匹配，首个命中优先）。
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in headerKeys)
            {
                var name = key?.Trim();
                if (!string.IsNullOrEmpty(name) && !map.ContainsKey(name))
                {
                    map[name] = key!;
                }
            }

            return map;
        }

        private static string GetString(IDictionary<string, object> record, IReadOnlyDictionary<string, string> keyMap, string column)
        {
            if (!keyMap.TryGetValue(column, out var key) || !record.TryGetValue(key, out var value) || value is null)
            {
                return string.Empty;
            }

            return value switch
            {
                string s => s.Trim(),
                double d => d.ToString(CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                bool b => b ? "是" : "否",
                _ => value.ToString()?.Trim() ?? string.Empty,
            };
        }

        /// <summary>解析「是否启用」列：空、是、启用、true、1、y、yes 视为启用；其余视为停用。</summary>
        private static bool ParseEnabled(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var t = text.Trim();
            if (t is "否" or "停用" or "禁用" or "0")
            {
                return false;
            }

            if (bool.TryParse(t, out var b))
            {
                return b;
            }

            return t is not ("false" or "no" or "n" or "N" or "No" or "FALSE" or "False");
        }
    }
}
