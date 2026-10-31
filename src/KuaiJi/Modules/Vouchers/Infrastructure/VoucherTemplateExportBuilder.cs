using System.Globalization;
using KuaiJi.Modules.Vouchers.Application;
using MiniExcelLibs;

namespace KuaiJi.Modules.Vouchers.Infrastructure
{
    /// <summary>
    /// 凭证模板导出生成器（MiniExcel）：
    /// 模板清单（模板名称 / 模板类型 / 摘要 / 科目编码 / 方向），
    /// 列名与导入解析一一对应，导出后可直接改完再导回。
    /// </summary>
    public static class VoucherTemplateExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private static readonly string[] Columns = ["模板名称", "模板类型", "摘要", "科目编码", "方向"];

        /// <summary>凭证模板导出（一模板多行，导回时按模板名称分组）。</summary>
        public static byte[] BuildTemplates(IReadOnlyList<VoucherTemplateDto> templates)
        {
            var rows = new List<Dictionary<string, object?>>();
            foreach (var template in templates)
            {
                foreach (var line in template.Lines)
                {
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["模板名称"] = template.Name,
                        ["模板类型"] = template.TypeText,
                        ["摘要"] = line.Summary,
                        ["科目编码"] = line.AccountCode,
                        ["方向"] = line.IsDebit ? "借" : "贷",
                    });
                }
            }

            if (rows.Count == 0)
            {
                rows.Add(EmptyRow());
            }

            return Save(rows, "凭证模板");
        }

        /// <summary>导入模板（仅表头 + 空行）。</summary>
        public static byte[] BuildTemplate() => Save([EmptyRow()], "凭证模板");

        private static Dictionary<string, object?> EmptyRow() => new()
        {
            ["模板名称"] = string.Empty,
            ["模板类型"] = string.Empty,
            ["摘要"] = string.Empty,
            ["科目编码"] = string.Empty,
            ["方向"] = string.Empty,
        };

        private static byte[] Save(List<Dictionary<string, object?>> rows, string sheetName)
        {
            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { [sheetName] = rows }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }
    }
}
