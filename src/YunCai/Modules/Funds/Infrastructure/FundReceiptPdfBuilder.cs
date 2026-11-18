using System.Globalization;
using System.Text;
using YunCai.Modules.Funds.Application;
using YunCai.Shared.Infrastructure;
using YunCai.SharedKernel.Infrastructure.Pdf;
using PaperSharp;

namespace YunCai.Modules.Funds.Infrastructure
{
    /// <summary>
    /// 收 / 付款凭据 PDF 生成器：A5 纵向，一笔流水一张凭据，字段与日记账账页「打印」链接的卡片一致
    /// （日期、账户、摘要、收支类别、往来单位、金额、大写金额、签章行）。
    /// 版式不走 <see cref="PrintSheetPdfBuilder"/>（那是表格账页），与凭证套打一样手写 HTML/CSS 以保留单据版式。
    /// </summary>
    public static class FundReceiptPdfBuilder
    {
        public const string ContentType = "application/pdf";

        /// <summary>生成凭据 PDF。<paramref name="entries"/> 为空时输出账簿全部分录。</summary>
        public static byte[] Build(JournalDetailDto journal, IReadOnlyList<JournalEntryDto> entries, DateTimeOffset? printedAt = null)
        {
            var css = Css(printedAt ?? DateTimeOffset.Now);
            var html = new StringBuilder("<html><head><title>记账凭据</title></head><body>");

            var accountName = $"{journal.AccountCode} - {journal.AccountName}".TrimStart(' ');
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var amount = FundsPdfProjector.ReceiptAmount(entry);
                html.Append("<section class='receipt'>")
                    .Append("<h2>").Append(entry.Income > 0m ? "收 款 凭 据" : "付 款 凭 据").Append("</h2>")
                    .Append("<div class='line'><span>日期：").Append(entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                    .Append("</span><span>账户：").Append(PdfText.Esc(accountName)).Append("</span>")
                    .Append("<span>摘要：").Append(PdfText.Esc(entry.Summary)).Append("</span></div>")
                    .Append("<table class='grid'><colgroup>")
                    .Append("<col style='width:16%'><col style='width:34%'><col style='width:16%'><col style='width:34%'>")
                    .Append("</colgroup>")
                    .Append("<tr><th>往来单位</th><td>").Append(PdfText.Esc(entry.Counterparty))
                    .Append("</td><th>收支类别</th><td>").Append(PdfText.Esc(entry.Category)).Append("</td></tr>")
                    .Append("<tr><th>结算方式</th><td>").Append(PdfText.Esc(entry.SettlementMethod))
                    .Append("</td><th>票据号</th><td>").Append(PdfText.Esc(entry.BillNo)).Append("</td></tr>")
                    .Append("<tr><th>金额</th><td class='num'>").Append("￥").Append(Amount(amount))
                    .Append("</td><th>关联凭证</th><td>").Append(PdfText.Esc(entry.VoucherNo)).Append("</td></tr>")
                    .Append("<tr><th>大写金额</th><td colspan='3'>人民币").Append(PdfText.Esc(ChineseAmount.ToUppercase(amount))).Append("</td></tr>")
                    .Append("</table>")
                    .Append("<div class='sign'><span>记账：____________</span><span>出纳：____________</span><span>经办人：____________</span></div>")
                    .Append("</section>");

                // 凭据之间画裁切线，与前端旧打印稿的分割线一致；最后一张不留多余线。
                if (i < entries.Count - 1) html.Append("<div class='cut'></div>");
            }

            if (entries.Count == 0)
            {
                html.Append("<div class='empty'>该账户暂无流水，无法生成收付款凭据。</div>");
            }

            html.Append("</body></html>");
            return Renderer.RenderPdf(html.ToString(), css, PageSize.A4);
        }

        /// <summary>生成含账套名与账户名的文件名，如「记账凭据_杭州数_库存现金.pdf」。</summary>
        public static string BuildFileName(string accountSetName, string journalName)
        {
            var safe = string.IsNullOrWhiteSpace(accountSetName) ? "账套" : accountSetName.Trim();
            var journal = string.IsNullOrWhiteSpace(journalName) ? "资金账户" : journalName.Trim();
            return $"记账凭据_{safe}_{journal}.pdf";
        }

        private static string Css(DateTimeOffset printedAt) => new StringBuilder()
            // PaperSharp 始终以 A4 为渲染基准，物理纸张由 @page 的 size 决定（A5 纵向）。
            .Append("@page { size: 148mm 210mm; margin: 12mm 12mm 16mm; ")
            .Append("@bottom-left { content: \"打印时间：").Append(printedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .Append("\"; font-size: 8pt; } ")
            .Append("@bottom-right { content: \"第 \" counter(page) \" 页 / 共 \" counter(pages) \" 页\"; font-size: 8pt; } }")
            .Append("body { margin: 0; color: #000; font-size: 9.5pt; font-family: \"Noto Sans SC\", \"Microsoft YaHei\", sans-serif; }")
            .Append(".receipt { break-inside: avoid; }")
            .Append("h2 { margin: 0 0 6px; font-size: 15pt; text-align: center; font-weight: bold; letter-spacing: 4px; }")
            .Append(".line { display: flex; justify-content: space-between; gap: 4mm; font-size: 9pt; margin-bottom: 4px; }")
            .Append("table.grid { width: 100%; border-collapse: collapse; table-layout: fixed; }")
            .Append("table.grid th, table.grid td { border: 0.5pt solid #000; padding: 1.2mm 1.5mm; font-weight: normal; }")
            .Append("table.grid th { background: #f6f6f6; text-align: left; white-space: nowrap; }")
            .Append(".num { text-align: right; font-variant-numeric: tabular-nums; }")
            .Append(".sign { display: flex; justify-content: space-between; margin-top: 6mm; font-size: 9pt; }")
            .Append(".cut { border-top: 0.4mm dashed #666; margin: 5mm 0; }")
            .Append(".empty { padding: 10mm 0; text-align: center; color: #444; }")
            .ToString();

        private static string Amount(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);
    }
}
