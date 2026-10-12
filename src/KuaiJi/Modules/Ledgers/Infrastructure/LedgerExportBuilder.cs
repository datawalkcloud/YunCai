using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.SharedKernel.Infrastructure.Excel;

namespace KuaiJi.Modules.Ledgers.Infrastructure
{
    /// <summary>
    /// 账页导出成器：把 <see cref="LedgerExportSheet"/> 适配成通用 <see cref="FlatWorkbook"/> 后写出 .xlsx
    /// （第一个工作表是页头信息，第二个工作表是压平多级表头后的表体，含合计行）。
    /// </summary>
    public static class LedgerExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = FlatWorkbook.XlsxContentType;

        /// <summary>页头工作表名。</summary>
        private const string InfoSheetName = "账页信息";

        /// <summary>生成导出文件的字节内容。</summary>
        public static byte[] Build(LedgerExportSheet sheet)
        {
            var meta = sheet.Meta;
            var info = new List<(string Label, string Value)>
            {
                ("账套", meta.AccountSetName ?? "账套"),
                ("记账单位", meta.CompanyName ?? string.Empty),
                ("期间", meta.PeriodText ?? string.Empty),
                ("已结账期间", meta.ClosedPeriodText ?? "尚未结账"),
                ("未过账凭证", $"{meta.UnpostedVoucherCount} 张"),
            };
            info.AddRange(sheet.ExtraInfo.Select(pair => (pair.Key, pair.Value ?? string.Empty)));

            return FlatWorkbookBuilder.Build(new FlatWorkbook(
                InfoSheetName,
                info,
                sheet.Name,
                sheet.Columns,
                sheet.Rows,
                BuildFileName(sheet)));
        }

        /// <summary>生成含账页名、账套名与区间的导出文件名。</summary>
        public static string BuildFileName(LedgerExportSheet sheet)
        {
            var accountSet = string.IsNullOrWhiteSpace(sheet.Meta.AccountSetName)
                ? "账套"
                : Sanitize(sheet.Meta.AccountSetName);

            return $"{sheet.Name}_{accountSet}_{sheet.Meta.StartDate}至{sheet.Meta.EndDate}.xlsx";
        }

        private static string Sanitize(string name)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }
    }
}
