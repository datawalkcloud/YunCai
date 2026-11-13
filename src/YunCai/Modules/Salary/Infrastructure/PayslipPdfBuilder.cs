using System.Globalization;
using System.Text;
using YunCai.Modules.Salary.Application;
using YunCai.Shared.Infrastructure;
using PaperSharp;

namespace YunCai.Modules.Salary.Infrastructure
{
    /// <summary>
    /// 工资条 PDF 生成器（PaperSharp）：A4 纵向，一页排 3 条，
    /// 条与条之间画裁切线，便于打印后裁开分发。
    /// </summary>
    public static class PayslipPdfBuilder
    {
        public const string ContentType = "application/pdf";

        /// <summary>每页工资条数。</summary>
        private const int PerPage = 3;

        public static byte[] Build(IReadOnlyList<SalaryBillDto> bills, int period, string companyName)
        {
            var periodText = $"{(period / 100):D4}年{(period % 100):D2}月";
            var company = string.IsNullOrWhiteSpace(companyName) ? "工资条" : companyName.Trim();

            var css = new StringBuilder()
                .Append("@page { size: A4; margin: 0; }")
                .Append("body { margin: 0; padding: 12mm 10mm; color: #000; font-size: 9pt; }")
                .Append(".slip { width: 100%; margin-bottom: 4mm; }")
                .Append(".slip-head { display: flex; justify-content: space-between; align-items: baseline; }")
                .Append(".slip-title { font-size: 12pt; font-weight: bold; letter-spacing: 1px; }")
                .Append(".slip-meta { font-size: 8.5pt; }")
                .Append("table.grid { width: 100%; border-collapse: collapse; table-layout: fixed; margin-top: 1.5mm; }")
                .Append("table.grid th, table.grid td { border: 0.3mm solid #000; padding: 1mm; }")
                .Append("table.grid th { font-weight: normal; background: #f2f2f2; text-align: center; font-size: 8.5pt; }")
                .Append(".n { text-align: right; }")
                .Append(".big { font-weight: bold; }")
                .Append(".cut { border-top: 0.3mm dashed #666; margin: 2mm 0 3mm; font-size: 7pt; color: #666; text-align: right; }")
                .ToString();

            var html = new StringBuilder("<html><head><title>工资条</title></head><body>");

            for (var i = 0; i < bills.Count; i++)
            {
                var b = bills[i];
                html.Append("<div class='slip'>")
                    .Append("<div class='slip-head'><span class='slip-title'>")
                    .Append(Esc(company)).Append("　").Append(periodText).Append(" 工资条</span>")
                    .Append("<span class='slip-meta'>编号：").Append(Esc(b.EmployeeCode))
                    .Append("　姓名：").Append(Esc(b.EmployeeName))
                    .Append("　部门：").Append(Esc(b.Dept)).Append("</span></div>");

                html.Append("<table class='grid'><colgroup>")
                    .Append("<col style='width:16.6%'><col style='width:16.6%'><col style='width:16.6%'>")
                    .Append("<col style='width:16.6%'><col style='width:16.6%'><col style='width:17%'>")
                    .Append("</colgroup>")
                    .Append("<tr><th>工资总额</th><th>应扣工资</th><th>应发工资</th>")
                    .Append("<th>代扣个人款项</th><th>本月应缴个税</th><th>实发工资</th></tr>")
                    .Append("<tr><td class='n'>").Append(Amount(b.Gross))
                    .Append("</td><td class='n'>").Append(Amount(b.Deductible))
                    .Append("</td><td class='n'>").Append(Amount(b.Payable))
                    .Append("</td><td class='n'>").Append(Amount(b.PersonalDeduction))
                    .Append("</td><td class='n'>").Append(Amount(b.Tax))
                    .Append("</td><td class='n big'>").Append(Amount(b.Net)).Append("</td></tr>")
                    .Append("</table>");

                html.Append("<table class='grid'><colgroup>")
                    .Append("<col style='width:25%'><col style='width:25%'><col style='width:25%'><col style='width:25%'>")
                    .Append("</colgroup>")
                    .Append("<tr><th>实发工资（大写）</th><td colspan='3'>").Append(Esc(ChineseAmount.ToUppercase(b.Net))).Append("</td></tr>")
                    .Append("<tr><th>公司承担款项</th><td class='n'>").Append(Amount(b.CompanyPart))
                    .Append("</td><th>员工成本</th><td class='n'>").Append(Amount(b.Cost)).Append("</td></tr>")
                    .Append("<tr><th>员工签字</th><td></td><th>发放日期</th><td></td></tr>")
                    .Append("</table>");

                if (i < bills.Count - 1)
                {
                    html.Append("<div class='cut'>✂ - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -</div>");
                }

                html.Append("</div>");

                if ((i + 1) % PerPage == 0 && i < bills.Count - 1)
                {
                    html.Append("<div style='page-break-after:always; break-after:page;'></div>");
                }
            }

            html.Append("</body></html>");
            return Renderer.RenderPdf(html.ToString(), css, PageSize.A4);
        }

        public static string BuildFileName(string accountSetName, int period)
        {
            var safe = string.IsNullOrWhiteSpace(accountSetName) ? "账套" : accountSetName.Trim();
            return $"工资条_{safe}_{period / 100:D4}{period % 100:D2}.pdf";
        }

        private static string Amount(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

        private static string Esc(string? value) =>
            (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }
}
