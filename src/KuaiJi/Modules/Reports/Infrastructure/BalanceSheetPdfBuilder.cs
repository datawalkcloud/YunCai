using System.Globalization;
using System.Text;
using KuaiJi.Modules.Reports.Application;
using PaperSharp;

namespace KuaiJi.Modules.Reports.Infrastructure
{
    /// <summary>资产负债表 PDF 模型：抬头信息 + 左右两半版式 + 底部平衡结论（与 .xls 导出共用同一版式布局）。</summary>
    public sealed record BalanceSheetPdfModel(
        string CompanyName,
        string DateText,
        string TemplateTitle,
        IReadOnlyList<BalanceSheetExportRow> Left,
        IReadOnlyList<BalanceSheetExportRow> Right,
        BalanceSheetExportBalance Balance);

    /// <summary>
    /// 资产负债表 PDF 生成器：A4 纵向、左右两栏八列版式，PaperSharp 渲染为矢量 PDF。
    /// 版式对齐柠檬云打印稿（宋体风格、全边框、金额右对齐、合计加粗），不依赖浏览器。
    /// </summary>
    public static class BalanceSheetPdfBuilder
    {
        public const string ContentType = "application/pdf";

        public static byte[] Build(BalanceSheetPdfModel model)
        {
            var html = new StringBuilder("<html><head><title>资产负债表</title></head><body>");
            html.Append("<div class='title'>资产负债表</div>");

            html.Append("<table class='meta'><tr>")
                .Append("<td class='l'>编制单位：").Append(Escape(model.CompanyName)).Append("</td>")
                .Append("<td class='c'>").Append(Escape(model.DateText)).Append("</td>")
                .Append("<td class='r'>").Append(Escape(model.TemplateTitle)).Append("<br/>单位：元</td>")
                .Append("</tr></table>");

            html.Append("<table class='grid'><colgroup>")
                .Append("<col style='width:20%'><col style='width:6%'><col style='width:12%'><col style='width:12%'>")
                .Append("<col style='width:20%'><col style='width:6%'><col style='width:12%'><col style='width:12%'>")
                .Append("</colgroup><thead><tr>")
                .Append("<th>资产</th><th>行次</th><th>期末余额</th><th>年初余额</th>")
                .Append("<th>负债和所有者权益</th><th>行次</th><th>期末余额</th><th>年初余额</th>")
                .Append("</tr></thead><tbody>");

            var rowCount = Math.Max(model.Left.Count, model.Right.Count);
            for (var index = 0; index < rowCount; index++)
            {
                html.Append("<tr>");
                AppendSide(html, index < model.Left.Count ? model.Left[index] : null);
                AppendSide(html, index < model.Right.Count ? model.Right[index] : null);
                html.Append("</tr>");
            }

            html.Append("</tbody></table>");
            html.Append("<div class='balance").Append(model.Balance.Balanced ? "" : " warn").Append("'>")
                .Append(Escape(model.Balance.Conclusion)).Append("</div>");
            html.Append("</body></html>");

            const string css = """
                @page { size: 210mm 297mm; margin: 0; }
                body { margin: 0; padding: 10mm 10mm 8mm; color: #000; font-size: 9pt; font-family: "Microsoft YaHei", "Noto Sans SC", sans-serif; }
                .title { text-align: center; font-size: 16pt; font-weight: bold; letter-spacing: 3pt; margin-bottom: 2.5mm; }
                table.meta { width: 100%; border-collapse: collapse; margin-bottom: 1.5mm; font-size: 9.5pt; }
                table.meta td { border: 0; padding: 0; vertical-align: top; }
                table.meta .l { text-align: left; }
                table.meta .c { text-align: center; }
                table.meta .r { text-align: right; line-height: 1.35; }
                table.grid { width: 100%; border-collapse: collapse; table-layout: fixed; }
                table.grid th, table.grid td { border: 0.5pt solid #000; padding: 0.5mm 1mm; height: 5.9mm; line-height: 1.2; vertical-align: middle; font-weight: normal; }
                table.grid th { text-align: center; font-weight: bold; height: 6.5mm; }
                .item { padding-left: 4mm; }
                .detail { padding-left: 8mm; }
                .total { font-weight: bold; }
                .line { text-align: center; }
                .amount { text-align: right; }
                .balance { margin-top: 2.5mm; font-size: 9pt; }
                .balance.warn { color: #c00000; }
                """;

            return Renderer.RenderPdf(html.ToString(), css, PageSize.A4);
        }

        private static void AppendSide(StringBuilder html, BalanceSheetExportRow? row)
        {
            if (row is null)
            {
                // 空单元格也要有内容，否则 PaperSharp 不画它的边框。
                html.Append("<td>&nbsp;</td><td>&nbsp;</td><td>&nbsp;</td><td>&nbsp;</td>");
                return;
            }

            var labelClass = row.Kind switch
            {
                BalanceSheetRowKind.Total => "label total",
                BalanceSheetRowKind.Detail => "label detail",
                BalanceSheetRowKind.Item => "label item",
                _ => "label",
            };
            var amountClass = row.Kind == BalanceSheetRowKind.Total ? "amount total" : "amount";

            html.Append("<td class='").Append(labelClass).Append("'>").Append(OrNbsp(Escape(row.Label))).Append("</td>");
            html.Append("<td class='line'>").Append(OrNbsp(row.Line?.ToString(CultureInfo.InvariantCulture))).Append("</td>");
            html.Append("<td class='").Append(amountClass).Append("'>").Append(OrNbsp(Amount(row.Ending))).Append("</td>");
            html.Append("<td class='").Append(amountClass).Append("'>").Append(OrNbsp(Amount(row.Opening))).Append("</td>");
        }

        /// <summary>金额格式化：0 留白，与页面打印稿一致。</summary>
        private static string Amount(decimal? value)
            => value is { } amount && amount != 0m
                ? amount.ToString("#,##0.00", CultureInfo.InvariantCulture)
                : string.Empty;

        private static string OrNbsp(string? text)
            => string.IsNullOrEmpty(text) ? "&nbsp;" : text;

        private static string Escape(string? text)
            => (text ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }
}
