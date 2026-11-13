using System.Globalization;
using System.Text;
using PaperSharp;

namespace YunCai.SharedKernel.Infrastructure.Pdf
{
    /// <summary>
    /// 通用账页/报表 PDF 生成器：把 <see cref="PrintSheet"/> 渲染成分页矢量 PDF。
    /// 版式全站统一（A4、9pt、全边框、表头跨页重复、页脚页码与打印时间），
    /// 表格结构（多级表头、跨列小计、缩进、加粗）由各模块的 builder 按页面 1:1 描述。
    /// </summary>
    public static class PrintSheetPdfBuilder
    {
        public const string ContentType = "application/pdf";

        public static byte[] Build(PrintSheet sheet)
        {
            var (html, css) = BuildDocument(sheet);
            return Renderer.RenderPdf(html, css, PageSize.A4);
        }

        /// <summary>生成含账套名的文件名，如「明细账_杭州数_202601-202603.pdf」。</summary>
        public static string BuildFileName(string title, string accountSetName, string? suffix = null)
        {
            var safe = string.IsNullOrWhiteSpace(accountSetName) ? "账套" : accountSetName.Trim();
            var tail = string.IsNullOrWhiteSpace(suffix) ? string.Empty : $"_{suffix.Trim()}";
            return $"{title}_{safe}{tail}.pdf";
        }

        internal static (string Html, string Css) BuildDocument(PrintSheet sheet)
        {
            var size = sheet.Landscape ? "297mm 210mm" : "210mm 297mm";
            var footerLeft = sheet.PrintedAt is { } at
                ? "@bottom-left { content: \"打印时间：" + at.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\"; font-size: 8pt; } "
                : string.Empty;

            var css = new StringBuilder()
                .Append("@page { size: ").Append(size).Append("; margin: 12mm 10mm 14mm; ")
                .Append(footerLeft)
                .Append("@bottom-right { content: \"第 \" counter(page) \" 页 / 共 \" counter(pages) \" 页\"; font-size: 8pt; } }")
                .Append("body { margin: 0; color: #000; font-size: 9pt; font-family: \"Noto Sans SC\", \"Microsoft YaHei\", sans-serif; }")
                .Append(".title { text-align: center; font-size: 15pt; font-weight: bold; letter-spacing: 2pt; margin: 0 0 2.5mm; }")
                .Append("table.meta { width: 100%; border-collapse: collapse; margin-bottom: 1.5mm; }")
                .Append("table.meta td { border: 0; padding: 0 1mm; }")
                .Append("table.meta .l { text-align: left; } table.meta .c { text-align: center; } table.meta .r { text-align: right; }")
                .Append("table.grid { width: 100%; border-collapse: collapse; table-layout: fixed; }")
                .Append("table.grid th, table.grid td { border: 0.5pt solid #000; padding: 0.6mm 1mm; line-height: 1.3; vertical-align: middle; font-weight: normal; overflow-wrap: break-word; }")
                .Append("table.grid th { text-align: center; font-weight: bold; }")
                .Append("table.grid tr.strong td { font-weight: bold; }")
                .Append("table.grid tr { break-inside: avoid; }")
                .Append(".t-left { text-align: left; } .t-center { text-align: center; } .t-right { text-align: right; }")
                // 加粗与缩进挂在 td 上：`table.grid td` 的规则优先级更高，选择器必须同样带上 td 才压得过。
                .Append("table.grid td.strong, table.grid th.strong, table.grid tr.strong td { font-weight: bold; }")
                .Append("table.grid td.i1 { padding-left: 3mm; } table.grid td.i2 { padding-left: 6mm; }")
                .Append("table.grid td.nw, table.grid th.nw { white-space: nowrap; }")
                .Append("table.grid td.empty { padding: 4mm 0; text-align: center; }")
                .Append(".notes { margin-top: 2mm; } .notes p { margin: 0 0 0.8mm; font-size: 8pt; }")
                .ToString();

            var html = new StringBuilder("<html><head><title>")
                .Append(PdfText.Esc(sheet.Title))
                .Append("</title></head><body>");

            html.Append("<div class='title'>").Append(PdfText.Esc(sheet.Title)).Append("</div>");
            AppendMeta(html, sheet.Meta);
            AppendTable(html, sheet);
            AppendNotes(html, sheet.Notes);

            html.Append("</body></html>");
            return (html.ToString(), css.ToString());
        }

        private static void AppendMeta(StringBuilder html, IReadOnlyList<PrintMeta> meta)
        {
            if (meta.Count == 0) return;

            html.Append("<table class='meta'><tr>");
            AppendMetaSide(html, meta, PdfAlign.Left, "l");
            AppendMetaSide(html, meta, PdfAlign.Center, "c");
            AppendMetaSide(html, meta, PdfAlign.Right, "r");
            html.Append("</tr></table>");
        }

        private static void AppendMetaSide(StringBuilder html, IReadOnlyList<PrintMeta> meta, string align, string cls)
        {
            var texts = meta.Where(m => m.Align == align).Select(m => PdfText.Esc(m.Text));
            html.Append("<td class='").Append(cls).Append("'>").Append(string.Join("<br/>", texts)).Append("</td>");
        }

        private static void AppendTable(StringBuilder html, PrintSheet sheet)
        {
            var columns = sheet.Columns;
            var grouped = columns.Any(c => c.Group is not null);

            html.Append("<table class='grid'>");
            if (columns.Any(c => !string.IsNullOrWhiteSpace(c.Width)))
            {
                html.Append("<colgroup>");
                foreach (var column in columns)
                {
                    html.Append(string.IsNullOrWhiteSpace(column.Width) ? "<col>" : $"<col style='width:{column.Width}'>");
                }

                html.Append("</colgroup>");
            }

            html.Append("<thead>");
            if (grouped)
            {
                // 第一行：无分组列跨两行，相邻同组列合并为一格。
                html.Append("<tr>");
                foreach (var segment in Segments(columns))
                {
                    if (segment.Group is null)
                    {
                        html.Append("<th rowspan='2' class='").Append(HeaderClass(segment.Columns[0])).Append("'>")
                            .Append(PdfText.Esc(segment.Columns[0].Text)).Append("</th>");
                    }
                    else
                    {
                        html.Append("<th colspan='").Append(segment.Columns.Count).Append("'>")
                            .Append(PdfText.Esc(segment.Group)).Append("</th>");
                    }
                }

                html.Append("</tr><tr>");
                foreach (var segment in Segments(columns).Where(s => s.Group is not null))
                {
                    foreach (var column in segment.Columns)
                    {
                        html.Append("<th class='").Append(HeaderClass(column)).Append("'>")
                            .Append(PdfText.Esc(column.Text)).Append("</th>");
                    }
                }

                html.Append("</tr>");
            }
            else
            {
                html.Append("<tr>");
                foreach (var column in columns)
                {
                    html.Append("<th class='").Append(HeaderClass(column)).Append("'>")
                        .Append(PdfText.Esc(column.Text)).Append("</th>");
                }

                html.Append("</tr>");
            }

            html.Append("</thead><tbody>");

            if (sheet.Rows.Count == 0 && !string.IsNullOrWhiteSpace(sheet.EmptyText))
            {
                html.Append("<tr><td class='empty' colspan='").Append(columns.Count).Append("'>")
                    .Append(PdfText.Esc(sheet.EmptyText)).Append("</td></tr>");
            }

            for (var r = 0; r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                html.Append("<tr");
                if (!string.IsNullOrWhiteSpace(row.Css)) html.Append(" class='").Append(row.Css).Append("'");
                html.Append('>');

                var start = 0;
                for (var c = 0; c < row.Cells.Count; c++)
                {
                    var cell = row.Cells[c];
                    var span = Math.Max(1, cell.ColSpan);
                    html.Append("<td");
                    if (span > 1) html.Append(" colspan='").Append(span).Append('\'');
                    html.Append(" class='").Append(CellClass(cell, columns, start)).Append("'>")
                        .Append(CellText(cell.Value)).Append("</td>");
                    start += span;
                }

                // 单元格少于列数时补空，避免整行错位、边框缺失。
                for (; start < columns.Count; start++) html.Append("<td>").Append(PdfText.Nbsp).Append("</td>");
                html.Append("</tr>");
            }

            html.Append("</tbody></table>");
        }

        private static void AppendNotes(StringBuilder html, IReadOnlyList<string> notes)
        {
            if (notes.Count == 0) return;

            html.Append("<div class='notes'>");
            foreach (var note in notes) html.Append("<p>").Append(PdfText.Esc(note)).Append("</p>");
            html.Append("</div>");
        }

        private sealed record Segment(string? Group, IReadOnlyList<PrintColumn> Columns);

        /// <summary>把列按相邻同组折叠：无分组列为单列段，同组相邻列合成一段。</summary>
        private static IReadOnlyList<Segment> Segments(IReadOnlyList<PrintColumn> columns)
        {
            var segments = new List<Segment>();
            for (var i = 0; i < columns.Count;)
            {
                var column = columns[i];
                if (column.Group is null)
                {
                    segments.Add(new Segment(null, [column]));
                    i++;
                    continue;
                }

                var group = column.Group;
                var run = new List<PrintColumn> { column };
                i++;
                while (i < columns.Count && columns[i].Group == group)
                {
                    run.Add(columns[i]);
                    i++;
                }

                segments.Add(new Segment(group, run));
            }

            return segments;
        }

        private static string AlignClass(string align) => align switch
        {
            PdfAlign.Center => "t-center",
            PdfAlign.Right => "t-right",
            _ => "t-left",
        };

        /// <summary>表头单元格样式：列对齐 + 列上附加的样式类。</summary>
        private static string HeaderClass(PrintColumn column)
            => string.IsNullOrWhiteSpace(column.Css)
                ? AlignClass(column.Align)
                : $"{AlignClass(column.Align)} {column.Css}";

        /// <summary>单元格样式：对齐（单元格覆盖列）、缩进与加粗。</summary>
        private static string CellClass(PrintCell cell, IReadOnlyList<PrintColumn> columns, int startIndex)
        {
            var column = startIndex >= 0 && startIndex < columns.Count ? columns[startIndex] : null;
            var parts = new List<string> { AlignClass(cell.Align ?? column?.Align ?? PdfAlign.Left) };
            if (!string.IsNullOrWhiteSpace(cell.Css)) parts.Add(cell.Css);
            return string.Join(' ', parts);
        }

        /// <summary>单元格文本：null 与 0 金额留白，decimal 走金额格式（装箱后的 decimal? 命中 decimal 分支）。</summary>
        private static string CellText(object? value) => value switch
        {
            null => PdfText.Nbsp,
            decimal amount => PdfText.Amount(amount),
            _ => PdfText.Esc(value),
        };
    }
}
