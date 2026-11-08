using System.Globalization;
using KuaiJi.Modules.Funds.Application;
using MiniExcelLibs;

namespace KuaiJi.Modules.Funds.Infrastructure
{
    /// <summary>
    /// 日记账导出生成器：用 MiniExcel 将一本日记账的分录导出为 .xlsx。
    /// 导出列包含导入可回传列（日期/摘要/类别/往来/收入/支出）与只读列
    /// （序号/余额/关联凭证号/凭证日期/结算方式/票据号/项目/部门/备注/流水来源）；
    /// 只读列在回传导入时会被 <see cref="XlsJournalImportReader"/> 忽略。
    /// </summary>
    public static class JournalExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>生成导出文件的字节内容。</summary>
        public static byte[] Build(JournalDetailDto journal)
        {
            var rows = journal.Entries
                .OrderBy(e => e.Sequence)
                .Select(e => new Dictionary<string, object?>
                {
                    ["序号"] = e.Sequence,
                    ["日期"] = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["摘要"] = e.Summary,
                    ["类别"] = e.Category ?? string.Empty,
                    ["往来"] = e.Counterparty ?? string.Empty,
                    ["收入"] = e.Income,
                    ["支出"] = e.Expense,
                    ["余额"] = e.Balance,
                    ["关联凭证号"] = e.VoucherNo ?? string.Empty,
                    ["凭证日期"] = e.VoucherDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    ["结算方式"] = e.SettlementMethod ?? string.Empty,
                    ["票据号"] = e.BillNo ?? string.Empty,
                    ["项目"] = e.Project ?? string.Empty,
                    ["部门"] = e.Department ?? string.Empty,
                    ["备注"] = e.Remark ?? string.Empty,
                    ["流水来源"] = e.Source ?? "手工登记",
                })
                .ToList();

            // 无分录时输出仅含表头的占位行，保证列名可被回传导入识别。
            if (rows.Count == 0)
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["序号"] = string.Empty,
                    ["日期"] = string.Empty,
                    ["摘要"] = string.Empty,
                    ["类别"] = string.Empty,
                    ["往来"] = string.Empty,
                    ["收入"] = string.Empty,
                    ["支出"] = string.Empty,
                    ["余额"] = string.Empty,
                    ["关联凭证号"] = string.Empty,
                    ["凭证日期"] = string.Empty,
                    ["结算方式"] = string.Empty,
                    ["票据号"] = string.Empty,
                    ["项目"] = string.Empty,
                    ["部门"] = string.Empty,
                    ["备注"] = string.Empty,
                    ["流水来源"] = string.Empty,
                });
            }

            var sheets = new Dictionary<string, object> { ["日记账"] = rows };

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, sheets, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>
        /// 生成「所有账户全部数据」导出：把多本日记账的分录合到一张表，首列补「账户」。
        /// 与单本导出共用列定义，因此同样可回传导入（账户列在导入时被忽略）。
        /// </summary>
        public static byte[] BuildAll(IReadOnlyList<JournalDetailDto> journals)
        {
            var rows = new List<Dictionary<string, object?>>();
            foreach (var journal in journals)
            {
                var accountLabel = string.IsNullOrWhiteSpace(journal.AccountCode)
                    ? journal.Name
                    : $"{journal.AccountCode} {journal.AccountName}";

                foreach (var entry in journal.Entries.OrderBy(e => e.Date).ThenBy(e => e.Sequence))
                {
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["账户"] = accountLabel,
                        ["序号"] = entry.Sequence,
                        ["日期"] = entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["摘要"] = entry.Summary,
                        ["类别"] = entry.Category ?? string.Empty,
                        ["往来"] = entry.Counterparty ?? string.Empty,
                        ["收入"] = entry.Income,
                        ["支出"] = entry.Expense,
                        ["余额"] = entry.Balance,
                        ["关联凭证号"] = entry.VoucherNo ?? string.Empty,
                        ["凭证日期"] = entry.VoucherDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                        ["结算方式"] = entry.SettlementMethod ?? string.Empty,
                        ["票据号"] = entry.BillNo ?? string.Empty,
                        ["项目"] = entry.Project ?? string.Empty,
                        ["部门"] = entry.Department ?? string.Empty,
                        ["备注"] = entry.Remark ?? string.Empty,
                        ["流水来源"] = entry.Source ?? "手工登记",
                    });
                }
            }

            if (rows.Count == 0)
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["账户"] = string.Empty,
                    ["序号"] = string.Empty,
                    ["日期"] = string.Empty,
                    ["摘要"] = string.Empty,
                    ["类别"] = string.Empty,
                    ["往来"] = string.Empty,
                    ["收入"] = string.Empty,
                    ["支出"] = string.Empty,
                    ["余额"] = string.Empty,
                    ["关联凭证号"] = string.Empty,
                    ["凭证日期"] = string.Empty,
                    ["结算方式"] = string.Empty,
                    ["票据号"] = string.Empty,
                    ["项目"] = string.Empty,
                    ["部门"] = string.Empty,
                    ["备注"] = string.Empty,
                    ["流水来源"] = string.Empty,
                });
            }

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { ["日记账"] = rows }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>生成含账簿名的导出文件名。</summary>
        public static string BuildFileName(string journalName)
        {
            var safe = string.IsNullOrWhiteSpace(journalName) ? "日记账" : journalName.Trim();
            return $"{safe}.xlsx";
        }
    }
}
