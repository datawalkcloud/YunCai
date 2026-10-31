using System.Globalization;
using System.Text;
using KuaiJi.Modules.Vouchers.Application;
using KuaiJi.Shared.Infrastructure;
using PaperSharp;

namespace KuaiJi.Modules.Vouchers.Infrastructure
{
    /// <summary>凭证 PDF 版式选项，字段与前端「凭证打印」设置一一对应。</summary>
    public sealed class VoucherPdfOptions
    {
        /// <summary>纸张：A4 两版 / A4 一版 / 241×140 针式套打。</summary>
        public string Paper { get; init; } = "A4x2";

        /// <summary>方向：portrait / landscape。</summary>
        public string Orientation { get; init; } = "portrait";

        /// <summary>打印方式：top 靠上 / bottom 靠下。</summary>
        public string Valign { get; init; } = "top";

        /// <summary>每张凭证至少打印的行数（不足补空行，保证套打对齐）。</summary>
        public int Rows { get; init; } = 5;

        /// <summary>正文字号（pt）。</summary>
        public int FontSize { get; init; } = 10;

        /// <summary>页边距（mm）。</summary>
        public int MarginLeft { get; init; } = 25;
        public int MarginRight { get; init; } = 10;
        public int MarginTop { get; init; } = 14;
        public int MarginBottom { get; init; } = 15;

        /// <summary>签名栏开关与自定义文本（自定义文本为空时回落到凭证上的制单人/审核人）。</summary>
        public bool SignPreparer { get; init; } = true;
        public string? PreparerText { get; init; }
        public bool SignReviewer { get; init; } = true;
        public string? ReviewerText { get; init; }
        public bool SignManager { get; init; }
        public string? ManagerText { get; init; }
        public bool SignBookkeeper { get; init; }
        public string? BookkeeperText { get; init; }
        public bool SignCashier { get; init; }
        public string? CashierText { get; init; }
        public bool SignTime { get; init; }

        public bool ShowAccountCode { get; init; } = true;
        public bool ZeroAttachment { get; init; }

        /// <summary>汇总打印：多张凭证合并到同一页。</summary>
        public bool MergePrint { get; init; }

        /// <summary>打印时间（签名栏开启「打印时间」时使用）。</summary>
        public DateTimeOffset? PrintedAt { get; init; }
    }

    /// <summary>
    /// 凭证 PDF 生成器：按标准记账凭证版式拼出 HTML/CSS，再交给 PaperSharp 渲染为矢量 PDF。
    /// 版式与前端「凭证打印」（ui/src/pages/closing/print-sheet.tsx）保持一致，
    /// 前端用于浏览器打印，本类用于服务端出 PDF 文件（下载 / 归档）。
    /// </summary>
    public static class VoucherPdfBuilder
    {
        public const string ContentType = "application/pdf";

        public static byte[] Build(IReadOnlyList<VoucherDetailDto> vouchers, VoucherPdfOptions? options = null)
        {
            var opt = options ?? new VoucherPdfOptions();
            var (html, css) = BuildDocument(vouchers, opt);
            return Renderer.RenderPdf(html, css, PageSize.A4);
        }

        /// <summary>生成含账套名的文件名。</summary>
        public static string BuildFileName(string accountSetName, VoucherDetailDto? single)
        {
            var safe = string.IsNullOrWhiteSpace(accountSetName) ? "账套" : accountSetName.Trim();
            return single is null ? $"记账凭证_{safe}.pdf" : $"记账凭证_{safe}_{single.VoucherNo}.pdf";
        }

        /// <summary>纸张物理尺寸（mm）。</summary>
        private static (double W, double H) PaperSize(VoucherPdfOptions opt)
        {
            if (opt.Paper == "V241") return (241, 140);
            return opt.Orientation == "landscape" ? (297, 210) : (210, 297);
        }

        /// <summary>每页放几张凭证。</summary>
        private static int PerPage(VoucherPdfOptions opt)
        {
            if (opt.MergePrint) return int.MaxValue;
            return opt.Paper == "A4x2" ? 2 : 1;
        }

        private static (string Html, string Css) BuildDocument(IReadOnlyList<VoucherDetailDto> vouchers, VoucherPdfOptions opt)
        {
            var size = PaperSize(opt);
            var contentH = Math.Max(40, size.H - opt.MarginTop - opt.MarginBottom);
            var per = PerPage(opt);

            // @page 由 PaperSharp 解析，覆盖默认 A4 尺寸，支持 241×140 针式套打与横向
            var sheetSize = opt.Paper == "V241" ? "241mm 140mm" : $"{size.W}mm {size.H}mm";

            var css = new StringBuilder()
                .Append("@page { size: ").Append(sheetSize).Append("; margin: 0; }")
                .Append("body { margin: 0; padding: ").Append(opt.MarginTop).Append("mm ")
                .Append(opt.MarginRight).Append("mm ").Append(opt.MarginBottom).Append("mm ")
                .Append(opt.MarginLeft).Append("mm; color: #000; font-size: ").Append(opt.FontSize).Append("pt; }")
                // 同时给出 page-break-after 与 break-after：PaperSharp 解析后者，浏览器打印兼容前者
                .Append(".page { min-height: ").Append(contentH.ToString("0.##", CultureInfo.InvariantCulture))
                .Append("mm; page-break-after: always; break-after: page; }")
                .Append(".page-last { page-break-after: auto; break-after: auto; }")
                .Append(".sheet { width: 100%; }")
                .Append(".sheet-sep { height: 6mm; }")
                .Append("h1 { text-align: center; font-size: ").Append(opt.FontSize + 6)
                .Append("pt; margin: 0 0 2mm; letter-spacing: 4px; }")
                .Append("table.meta { width: 100%; border-collapse: collapse; margin-bottom: 1mm; }")
                .Append("table.meta td { border: 0; padding: 0; }")
                .Append("table.meta td.r { text-align: right; }")
                .Append("table.grid { width: 100%; border-collapse: collapse; table-layout: fixed; }")
                .Append("table.grid th, table.grid td { border: 0.4mm solid #000; padding: 0.8mm 1mm; }")
                .Append("table.grid th { font-weight: normal; text-align: center; }")
                .Append(".c { text-align: left; }")
                .Append(".n { text-align: right; }")
                .Append(".total { font-weight: bold; }")
                .Append("table.sign { width: 100%; border-collapse: collapse; margin-top: 2mm; }")
                .Append("table.sign td { border: 0; padding: 0 1mm; }")
                .Append(".attach { margin-top: 2mm; }")
                .ToString();

            var html = new StringBuilder("<html><head><title>记账凭证</title></head><body>");

            var pages = new List<List<VoucherDetailDto>>();
            if (opt.MergePrint)
            {
                pages.Add([.. vouchers]);
            }
            else
            {
                for (var i = 0; i < vouchers.Count; i += per)
                {
                    pages.Add([.. vouchers.Skip(i).Take(per)]);
                }
            }

            for (var p = 0; p < pages.Count; p++)
            {
                var cls = p == pages.Count - 1 ? "page page-last" : "page";
                html.Append("<div class='").Append(cls).Append("'>");
                for (var s = 0; s < pages[p].Count; s++)
                {
                    if (s > 0) html.Append("<div class='sheet-sep'></div>");
                    html.Append(BuildSheet(pages[p][s], opt));
                }

                html.Append("</div>");
            }

            html.Append("</body></html>");
            return (html.ToString(), css.ToString());
        }

        private static string BuildSheet(VoucherDetailDto voucher, VoucherPdfOptions opt)
        {
            var entries = voucher.Entries ?? [];
            var rowCount = Math.Max(entries.Count, Math.Max(1, opt.Rows));

            var sb = new StringBuilder("<div class='sheet'>");
            sb.Append("<h1>记账凭证</h1>");

            var attach = voucher.AttachmentCount;
            if (attach == 0 && opt.ZeroAttachment) attach = 0;

            sb.Append("<table class='meta'><tr>")
              .Append("<td>日期：").Append(Esc(voucher.Date.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture))).Append("</td>")
              .Append("<td style='text-align:center'>附单据 ").Append(attach).Append(" 张</td>")
              .Append("<td class='r'>凭证字号：").Append(Esc(voucher.VoucherNo)).Append("</td>")
              .Append("</tr></table>");

            sb.Append("<table class='grid'><colgroup>")
              .Append("<col style='width:28%'><col style='width:32%'><col style='width:20%'><col style='width:20%'>")
              .Append("</colgroup>")
              .Append("<tr><th>摘要</th><th>会计科目</th><th>借方金额</th><th>贷方金额</th></tr>");

            for (var i = 0; i < rowCount; i++)
            {
                var e = i < entries.Count ? entries[i] : null;
                sb.Append("<tr>")
                  .Append("<td class='c'>").Append(e is null ? string.Empty : Esc(e.Summary)).Append("</td>")
                  .Append("<td class='c'>").Append(e is null ? string.Empty : Esc(AccountText(e, opt))).Append("</td>")
                  .Append("<td class='n'>").Append(Amount(e?.Debit ?? 0m)).Append("</td>")
                  .Append("<td class='n'>").Append(Amount(e?.Credit ?? 0m)).Append("</td>")
                  .Append("</tr>");
            }

            sb.Append("<tr class='total'><td colspan='2'>合计：人民币（大写）")
              .Append(Esc(ChineseAmount.ToUppercase(voucher.TotalDebit))).Append("</td>")
              .Append("<td class='n'>").Append(Amount(voucher.TotalDebit, true)).Append("</td>")
              .Append("<td class='n'>").Append(Amount(voucher.TotalCredit, true)).Append("</td></tr>");
            sb.Append("</table>");

            if (voucher.AttachmentCount > 0)
            {
                sb.Append("<div class='attach'>附件：").Append(voucher.AttachmentCount).Append(" 张</div>");
            }

            var signs = new List<string>();
            if (opt.SignPreparer) signs.Add("制单人：" + (opt.PreparerText ?? voucher.Preparer));
            if (opt.SignReviewer) signs.Add("审核人：" + (opt.ReviewerText ?? voucher.Reviewer ?? string.Empty));
            if (opt.SignManager) signs.Add("主管：" + (opt.ManagerText ?? string.Empty));
            if (opt.SignBookkeeper) signs.Add("记账：" + (opt.BookkeeperText ?? string.Empty));
            if (opt.SignCashier) signs.Add("出纳：" + (opt.CashierText ?? string.Empty));
            if (opt.SignTime) signs.Add("打印时间：" + (opt.PrintedAt ?? DateTimeOffset.Now).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));

            if (signs.Count > 0)
            {
                sb.Append("<table class='sign'><tr>");
                foreach (var s in signs) sb.Append("<td>").Append(Esc(s)).Append("</td>");
                sb.Append("</tr></table>");
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        private static string AccountText(VoucherEntryDto entry, VoucherPdfOptions opt) =>
            opt.ShowAccountCode ? $"{entry.AccountCode} {entry.AccountName}" : entry.AccountName;

        /// <summary>金额格式化：0 显示为空（套打留白），force 时始终显示两位小数。</summary>
        private static string Amount(decimal value, bool force = false)
        {
            if (!force && value == 0m) return string.Empty;
            return value.ToString("#,##0.00", CultureInfo.InvariantCulture);
        }

        private static string Esc(string? value) =>
            (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
    }
}
