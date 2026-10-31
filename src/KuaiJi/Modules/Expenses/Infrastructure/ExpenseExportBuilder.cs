using System.Globalization;
using KuaiJi.Modules.Expenses.Application;
using MiniExcelLibs;

namespace KuaiJi.Modules.Expenses.Infrastructure
{
    /// <summary>
    /// 费用单据导出生成器（MiniExcel）：
    /// 费用单据清单与导入模板，列名与导入解析一一对应，导出后可直接改完再导回。
    /// </summary>
    public static class ExpenseExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>费用单据导出（列与导入模板一致，可回填后原样导回）。</summary>
        public static byte[] BuildBills(IReadOnlyList<ExpenseBillDto> bills)
        {
            var rows = bills.Select(b => new Dictionary<string, object?>
            {
                ["单据编号"] = b.BillNo,
                ["单据日期"] = b.BillDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["费用事由"] = b.Reason,
                ["费用类型"] = b.ExpenseType,
                ["收款人"] = b.Payee ?? string.Empty,
                ["结算方式"] = b.SettleMethod ?? string.Empty,
                ["可抵扣税额"] = b.DeductibleTax,
                ["单据金额（含税）"] = b.Amount,
                ["状态"] = b.StatusText,
                ["关联凭证"] = b.VoucherText ?? string.Empty,
            }).ToList();

            if (rows.Count == 0)
            {
                rows.Add(EmptyRow());
            }

            return Save(rows, "费用单据");
        }

        /// <summary>导入模板（仅表头 + 空行）。</summary>
        public static byte[] BuildTemplate() => Save([EmptyRow()], "费用单据");

        private static Dictionary<string, object?> EmptyRow() => new()
        {
            ["单据编号"] = string.Empty,
            ["单据日期"] = string.Empty,
            ["费用事由"] = string.Empty,
            ["费用类型"] = string.Empty,
            ["收款人"] = string.Empty,
            ["结算方式"] = string.Empty,
            ["可抵扣税额"] = string.Empty,
            ["单据金额（含税）"] = string.Empty,
            ["状态"] = string.Empty,
            ["关联凭证"] = string.Empty,
        };

        private static byte[] Save(List<Dictionary<string, object?>> rows, string sheetName)
        {
            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { [sheetName] = rows }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }
    }
}
