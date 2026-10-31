using System.Globalization;
using KuaiJi.Modules.Funds.Application;
using MiniExcelLibs;

namespace KuaiJi.Modules.Funds.Infrastructure
{
    /// <summary>
    /// 内部转账导出生成器：用 MiniExcel 把转账台账导出为 .xlsx，
    /// 列与柠檬云「内部转账」列表一致（日期 / 摘要 / 转出账户 / 转入账户 / 金额 / 备注 / 关联凭证）。
    /// </summary>
    public static class TransferExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>生成导出文件的字节内容。</summary>
        public static byte[] Build(IReadOnlyList<TransferDto> transfers)
        {
            var rows = transfers
                .OrderBy(t => t.Date)
                .Select(t => new Dictionary<string, object?>
                {
                    ["日期"] = t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["摘要"] = t.Summary,
                    ["转出账户"] = t.FromJournalName,
                    ["转入账户"] = t.ToJournalName,
                    ["金额"] = t.Amount,
                    ["备注"] = t.Remark ?? string.Empty,
                    ["关联凭证"] = t.VoucherNo ?? string.Empty,
                })
                .ToList();

            if (rows.Count == 0)
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["日期"] = string.Empty,
                    ["摘要"] = string.Empty,
                    ["转出账户"] = string.Empty,
                    ["转入账户"] = string.Empty,
                    ["金额"] = string.Empty,
                    ["备注"] = string.Empty,
                    ["关联凭证"] = string.Empty,
                });
            }

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { ["内部转账"] = rows }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }
    }
}
