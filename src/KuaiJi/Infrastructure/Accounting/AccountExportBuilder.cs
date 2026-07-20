using KuaiJi.Domain.Accounting;
using MiniExcelLibs;

namespace KuaiJi.Infrastructure.Accounting
{
    /// <summary>
    /// 科目导出生成器：用 MiniExcel 将账套科目导出为 .xlsx。
    /// 导出列与 <see cref="AccountImportTemplateBuilder"/> / <see cref="XlsxAccountImportReader"/> 完全一致，
    /// 因此导出文件可原样回传导入。上级科目编码由 <see cref="Account.ParentId"/> 反查。
    /// </summary>
    public static class AccountExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>生成导出文件的字节内容。科目按编码升序排列。</summary>
        public static byte[] Build(IReadOnlyList<Account> accounts)
        {
            var byId = accounts.ToDictionary(a => a.Id, a => a);

            var rows = accounts
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .Select(a => new Dictionary<string, object?>
                {
                    ["科目编码"] = a.Code,
                    ["科目名称"] = a.Name,
                    ["类别"] = CategoryText(a.Category),
                    ["余额方向"] = DirectionText(a.Direction),
                    ["上级科目编码"] = a.ParentId is { } pid && byId.TryGetValue(pid, out var parent) ? parent.Code : string.Empty,
                    ["助记码"] = a.Mnemonic ?? string.Empty,
                    ["是否启用"] = a.IsEnabled ? "是" : "否",
                })
                .ToList();

            // 至少保留表头行：无数据时输出一行空白列名占位。
            if (rows.Count == 0)
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["科目编码"] = string.Empty,
                    ["科目名称"] = string.Empty,
                    ["类别"] = string.Empty,
                    ["余额方向"] = string.Empty,
                    ["上级科目编码"] = string.Empty,
                    ["助记码"] = string.Empty,
                    ["是否启用"] = string.Empty,
                });
            }

            var sheets = new Dictionary<string, object> { ["科目"] = rows };

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, sheets, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>生成含账套名的导出文件名。</summary>
        public static string BuildFileName(string accountSetName)
        {
            var safe = string.IsNullOrWhiteSpace(accountSetName) ? "账套" : accountSetName.Trim();
            return $"科目表_{safe}.xlsx";
        }

        private static string CategoryText(AccountCategory category) => category switch
        {
            AccountCategory.Asset => "资产",
            AccountCategory.Liability => "负债",
            AccountCategory.Equity => "权益",
            AccountCategory.Cost => "成本",
            AccountCategory.ProfitAndLoss => "损益",
            _ => string.Empty,
        };

        private static string DirectionText(BalanceDirection direction) =>
            direction == BalanceDirection.Credit ? "贷" : "借";
    }
}
