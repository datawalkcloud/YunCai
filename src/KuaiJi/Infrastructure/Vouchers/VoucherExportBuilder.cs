using System.Globalization;
using KuaiJi.Application.Vouchers;
using MiniExcelLibs;

namespace KuaiJi.Infrastructure.Vouchers
{
    /// <summary>
    /// 凭证导出生成器：用 MiniExcel 将扁平化的凭证分录行导出为 .xlsx。
    /// 导出列与 <see cref="XlsVoucherImportReader"/> 的解析列同构，导出文件可回传导入。
    /// </summary>
    public static class VoucherExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>生成导出文件的字节内容。</summary>
        public static byte[] Build(IReadOnlyList<VoucherExportRow> rows)
        {
            var sheetRows = rows
                .Select(r => new Dictionary<string, object?>
                {
                    ["日期"] = r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["凭证号"] = r.VoucherNo,
                    ["行号"] = r.LineNumber,
                    ["摘要"] = r.Summary,
                    ["科目编码"] = r.AccountCode,
                    ["科目名称"] = r.AccountName,
                    ["借方本币"] = r.Debit,
                    ["贷方本币"] = r.Credit,
                    ["制单人"] = r.Preparer,
                    ["审核人"] = r.Reviewer,
                    ["附件数"] = r.AttachmentCount,
                })
                .ToList();

            // 无数据时输出仅含表头的占位行，保证列名可被回传导入识别。
            if (sheetRows.Count == 0)
            {
                sheetRows.Add(new Dictionary<string, object?>
                {
                    ["日期"] = string.Empty,
                    ["凭证号"] = string.Empty,
                    ["行号"] = string.Empty,
                    ["摘要"] = string.Empty,
                    ["科目编码"] = string.Empty,
                    ["科目名称"] = string.Empty,
                    ["借方本币"] = string.Empty,
                    ["贷方本币"] = string.Empty,
                    ["制单人"] = string.Empty,
                    ["审核人"] = string.Empty,
                    ["附件数"] = string.Empty,
                });
            }

            var sheets = new Dictionary<string, object> { ["凭证"] = sheetRows };

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, sheets, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>生成含账套名的导出文件名。</summary>
        public static string BuildFileName(string accountSetName)
        {
            var safe = string.IsNullOrWhiteSpace(accountSetName) ? "账套" : accountSetName.Trim();
            return $"凭证_{safe}.xlsx";
        }
    }
}
