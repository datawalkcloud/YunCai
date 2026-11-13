using System.Globalization;
using System.Text;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Vouchers.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using PaperSharp;

namespace YunCai.Modules.Reports.Infrastructure
{
    /// <summary>账套归档 PDF 生成器：封面 + 科目余额表 + 凭证分录总表，PaperSharp 渲染。</summary>
    public static class ArchivePdfBuilder
    {
        public const string ContentType = "application/pdf";

        public static byte[] Build(AccountSet accountSet, IReadOnlyList<BalanceRow> balances, IReadOnlyList<EntryRow> entries, string generatedAt)
        {
            var html = new StringBuilder();
            html.Append("<div class='cover'>");
            html.Append($"<h1>{Escape(accountSet.CompanyName.Length > 0 ? accountSet.CompanyName : accountSet.Name)}</h1>");
            html.Append("<div class='sub'>账套归档资料</div>");
            html.Append($"<div class='meta'>会计准则：{accountSet.Standard}　|　本位币：{accountSet.BaseCurrency}　|　启用期间：{accountSet.StartYear}年{accountSet.StartMonth:00}月</div>");
            html.Append($"<div class='meta'>导出时间：{Escape(generatedAt)}</div>");
            html.Append("</div>");

            html.Append("<h2>一、科目余额表（期初至本期末累计）</h2>");
            html.Append("<table><thead><tr><th>科目编码</th><th>科目名称</th><th>类别</th><th class='r'>期初余额</th><th class='r'>累计借方</th><th class='r'>累计贷方</th><th class='r'>期末余额</th></tr></thead><tbody>");
            foreach (var b in balances)
            {
                html.Append($"<tr><td>{Escape(b.Code)}</td><td>{Escape(b.Name)}</td><td>{b.Category}</td>");
                html.Append($"<td class='r'>{b.Opening:N2}</td><td class='r'>{b.Debit:N2}</td><td class='r'>{b.Credit:N2}</td><td class='r'>{b.Closing:N2}</td></tr>");
            }

            html.Append("</tbody></table>");

            html.Append("<h2>二、凭证分录总表</h2>");
            html.Append("<table><thead><tr><th>日期</th><th>凭证字号</th><th>摘要</th><th>科目编码</th><th>科目名称</th><th class='r'>借方</th><th class='r'>贷方</th></tr></thead><tbody>");
            foreach (var e in entries)
            {
                html.Append($"<tr><td>{e.Date:yyyy-MM-dd}</td><td>{Escape(e.Word)}-{e.Number}</td><td>{Escape(e.Summary)}</td>");
                html.Append($"<td>{Escape(e.AccountCode)}</td><td>{Escape(e.AccountName)}</td><td class='r'>{e.Debit:N2}</td><td class='r'>{e.Credit:N2}</td></tr>");
            }

            html.Append("</tbody></table>");
            html.Append($"<div class='foot'>本资料由系统按账套全部已登记凭证生成，共 {entries.Count} 条分录；金额单位为元。</div>");

            const string css = """
                body { font-family: "Microsoft YaHei", sans-serif; font-size: 9pt; color: #1f2937; }
                .cover { text-align: center; padding-top: 120px; page-break-after: always; }
                .cover h1 { font-size: 22pt; margin-bottom: 12px; }
                .cover .sub { font-size: 14pt; color: #2f6bff; margin-bottom: 40px; }
                .cover .meta { font-size: 10pt; color: #4b5563; margin: 6px 0; }
                h2 { font-size: 12pt; margin: 18px 0 8px; }
                table { width: 100%; border-collapse: collapse; margin-bottom: 10px; }
                th, td { border: 1px solid #9ca3af; padding: 3px 5px; }
                th { background: #eef3ff; }
                td.r, th.r { text-align: right; }
                .foot { margin-top: 10px; font-size: 8pt; color: #6b7280; }
                """;

            return Renderer.RenderPdf(html.ToString(), css, PageSize.A4);
        }

        /// <summary>科目余额行（归档用）。</summary>
        public sealed record BalanceRow(string Code, string Name, string Category, decimal Opening, decimal Debit, decimal Credit, decimal Closing);

        /// <summary>凭证分录行（归档用）。</summary>
        public sealed record EntryRow(DateOnly Date, string Word, int Number, string Summary, string AccountCode, string AccountName, decimal Debit, decimal Credit);

        private static string Escape(string? text)
            => (text ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
