using System.Globalization;
using YunCai.Modules.Funds.Application;
using MiniExcelLibs;

namespace YunCai.Modules.Funds.Infrastructure
{
    /// <summary>
    /// 票据导出生成器：用 MiniExcel 把票据台账导出为 .xlsx。
    /// 前 10 列与柠檬云「票据台账」列表一致，其后的列用于导入时回填票据当事人与银行信息，
    /// 因此「导出 → 导入」可以原样往返。
    /// </summary>
    public static class BillExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>导出列（顺序即表头顺序）。</summary>
        public static readonly string[] Columns =
        [
            "收支类别",
            "出票日",
            "票据号",
            "票据金额",
            "票据种类",
            "汇票到期日",
            "出票人",
            "承兑人",
            "票据状态",
            "能否转让",
            "收票人",
            "出票人账号",
            "收票人账号",
            "承兑人账号/行号",
            "出票人开户银行",
            "收票人开户银行",
            "承兑人开户银行",
            "备注",
        ];

        /// <summary>生成导出文件的字节内容。</summary>
        public static byte[] Build(IReadOnlyList<BillDto> bills)
        {
            var rows = bills
                .OrderBy(b => b.IssueDate)
                .ThenBy(b => b.BillNo, StringComparer.Ordinal)
                .Select(b => new Dictionary<string, object?>
                {
                    ["收支类别"] = b.Category,
                    ["出票日"] = b.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["票据号"] = b.BillNo,
                    ["票据金额"] = b.Amount,
                    ["票据种类"] = b.KindText,
                    ["汇票到期日"] = b.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    ["出票人"] = b.Drawer ?? string.Empty,
                    ["承兑人"] = b.Acceptor ?? string.Empty,
                    ["票据状态"] = b.StatusText,
                    ["能否转让"] = b.Transferable ? "可转让" : "不可转让",
                    ["收票人"] = b.Payee ?? string.Empty,
                    ["出票人账号"] = b.DrawerAccountNo ?? string.Empty,
                    ["收票人账号"] = b.PayeeAccountNo ?? string.Empty,
                    ["承兑人账号/行号"] = b.AcceptorAccountNo ?? string.Empty,
                    ["出票人开户银行"] = b.DrawerBank ?? string.Empty,
                    ["收票人开户银行"] = b.PayeeBank ?? string.Empty,
                    ["承兑人开户银行"] = b.AcceptorBank ?? string.Empty,
                    ["备注"] = b.Remark ?? string.Empty,
                })
                .ToList();

            if (rows.Count == 0)
            {
                rows.Add(Columns.ToDictionary(c => c, _ => (object?)string.Empty));
            }

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { ["票据台账"] = rows }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }
    }
}
