using System.Globalization;
using KuaiJi.Modules.Invoices.Domain;
using KuaiJi.SharedKernel.Infrastructure.Pdf;

namespace KuaiJi.Modules.Invoices.Application
{
    /// <summary>
    /// 发票打印 PDF 的表格投影：列与顺序按销项 / 进项发票列表页的屏幕表格 1:1 描述
    /// （进项多出认证日期、税款所属期与有效抵扣税额三列），末行合计与页面顶部统计条同口径。
    /// </summary>
    public static class InvoicePdfProjector
    {
        /// <summary>页头需要的账套信息：发票 DTO 不带编制单位，由控制器注入。</summary>
        public sealed record PrintContext(string AccountSetName, string? CompanyName = null);

        /// <summary>
        /// 发票清单。金额列按账页惯例 0 留白；<paramref name="summary"/> 即页面顶部的合计，为 null 时按列表求和。
        /// </summary>
        public static PrintSheet List(
            IReadOnlyList<InvoiceDto> invoices,
            InvoiceSummaryDto? summary,
            InvoiceDirection direction,
            PrintContext context,
            DateOnly? startDate,
            DateOnly? endDate)
        {
            var isPurchase = direction == InvoiceDirection.Purchase;
            var columns = new List<PrintColumn>
            {
                new("发票种类", Width: "7%"),
                new("录入日期", Width: "7%", Align: PdfAlign.Center),
                new("开票日期", Width: "7%", Align: PdfAlign.Center),
                new("发票号码", Width: "9%", Align: PdfAlign.Center),
                new(isPurchase ? "供应商名称" : "客户名称", Width: "10%"),
            };
            if (isPurchase)
            {
                columns.Add(new PrintColumn("认证日期", Width: "7%", Align: PdfAlign.Center));
                columns.Add(new PrintColumn("税款所属期", Width: "7%", Align: PdfAlign.Center));
            }

            columns.Add(new PrintColumn("业务类型", Width: "6%"));
            columns.Add(new PrintColumn("商品名称", Width: "10%"));
            columns.Add(new PrintColumn("合计金额", Width: "8%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("税率", Width: "5%", Align: PdfAlign.Center));
            columns.Add(new PrintColumn("合计税额", Width: "8%", Align: PdfAlign.Right));
            if (isPurchase) columns.Add(new PrintColumn("有效抵扣税额", Width: "8%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("价税合计", Width: "8%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("关联凭证", Width: "6%", Align: PdfAlign.Center));
            columns.Add(new PrintColumn("发票来源", Width: "6%"));
            columns.Add(new PrintColumn("状态", Width: "5%", Align: PdfAlign.Center));

            var rows = invoices
                .Select(invoice => new PrintRow(LineCells(invoice, isPurchase)))
                .ToList();

            if (rows.Count > 0)
            {
                // 页面把三项合计放在表格上方的统计条里，打印折成表尾合计行。
                rows.Add(TotalRow(invoices, summary, isPurchase, columns.Count));
            }

            var company = string.IsNullOrWhiteSpace(context.CompanyName) ? context.AccountSetName : context.CompanyName!;
            var withTax = summary?.TotalWithTax ?? invoices.Sum(i => i.TotalWithTax);

            return new PrintSheet
            {
                Title = (isPurchase ? "进项发票" : "销项发票") + "清单",
                Meta =
                [
                    new PrintMeta($"编制单位：{company}"),
                    new PrintMeta($"账套：{context.AccountSetName}", PdfAlign.Center),
                    new PrintMeta($"期间：{RangeText(startDate, endDate)}", PdfAlign.Right),
                ],
                Columns = columns,
                Rows = rows,
                Notes =
                [
                    "单位：元",
                    $"共 {invoices.Count} 张发票，价税合计 {PdfText.AmountAlways(withTax)} 元。",
                    "商品名称与税率取每张发票的首条明细，全部明细请见「商品明细」页签。",
                ],
                Landscape = true,
                PrintedAt = DateTimeOffset.Now,
                EmptyText = "所选期间没有发票，请确认开票日期区间与入账状态筛选。",
            };
        }

        /// <summary>一张发票一行：明细只取首条（与页面同口径），有效抵扣税额只落在已认证的进项发票上。</summary>
        private static IReadOnlyList<PrintCell> LineCells(InvoiceDto invoice, bool isPurchase)
        {
            var first = invoice.Lines.Count > 0 ? invoice.Lines[0] : null;
            // 页面按状态文案「已认证」判定可否抵扣，后端认证只回填认证日期而不改入账状态，故以认证日期为准。
            var deductible = isPurchase && invoice.AuthDate is not null ? invoice.TotalTax : (decimal?)null;

            var cells = new List<PrintCell>
            {
                new(invoice.InvoiceType),
                new(invoice.InputDate, Align: PdfAlign.Center),
                new(invoice.IssueDate, Align: PdfAlign.Center),
                new(invoice.InvoiceNo, Align: PdfAlign.Center),
                new(invoice.PartnerName),
            };
            if (isPurchase)
            {
                cells.Add(new PrintCell(invoice.AuthDate, Align: PdfAlign.Center));
                cells.Add(new PrintCell(TaxPeriodText(invoice.TaxPeriod), Align: PdfAlign.Center));
            }

            cells.Add(new PrintCell(invoice.BizType));
            cells.Add(new PrintCell(first?.GoodsName));
            cells.Add(new PrintCell(invoice.TotalAmount));
            cells.Add(new PrintCell(RateText(first?.TaxRate), Align: PdfAlign.Center));
            cells.Add(new PrintCell(invoice.TotalTax));
            if (isPurchase) cells.Add(new PrintCell(deductible));
            cells.Add(new PrintCell(invoice.TotalWithTax));
            cells.Add(new PrintCell(invoice.VoucherText, Align: PdfAlign.Center));
            cells.Add(new PrintCell(invoice.Source));
            cells.Add(new PrintCell(invoice.StatusText, Align: PdfAlign.Center));
            return cells;
        }

        /// <summary>表尾合计：整格跨到「合计金额」之前，再依次给出金额 / 税额 /（抵扣税额）/ 价税合计。</summary>
        private static PrintRow TotalRow(
            IReadOnlyList<InvoiceDto> invoices,
            InvoiceSummaryDto? summary,
            bool isPurchase,
            int columnCount)
        {
            var amount = summary?.TotalAmount ?? invoices.Sum(i => i.TotalAmount);
            var tax = summary?.TotalTax ?? invoices.Sum(i => i.TotalTax);
            var withTax = summary?.TotalWithTax ?? invoices.Sum(i => i.TotalWithTax);
            var deductibleTotal = invoices.Where(i => i.AuthDate is not null).Sum(i => i.TotalTax);

            // 「合计金额」之前的列数：销项 7 列（种类…商品名称），进项 9 列（另含认证日期、税款所属期）。
            var cells = new List<PrintCell>
            {
                new("合计", ColSpan: isPurchase ? 9 : 7, Css: PdfCss.Strong),
                Strong(amount),
                new(Value: null, Align: PdfAlign.Center),
                Strong(tax),
            };
            if (isPurchase) cells.Add(Strong(deductibleTotal));
            cells.Add(Strong(withTax));

            var used = cells.Sum(c => Math.Max(1, c.ColSpan));
            if (used < columnCount) cells.Add(new PrintCell(null, ColSpan: columnCount - used));
            return new PrintRow(cells, PdfCss.Strong);
        }

        private static PrintCell Strong(decimal value)
            => new(PdfText.AmountAlways(value), Align: PdfAlign.Right, Css: PdfCss.Strong);

        /// <summary>税率兼容小数（0.13）与百分数（13）两种口径，与页面一致。</summary>
        private static string? RateText(decimal? rate)
        {
            if (rate is not { } value) return null;
            var percent = value is > 0 and < 1 ? value * 100m : value;
            return percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>税款所属期 202601 → 「2026-01」。</summary>
        private static string? TaxPeriodText(int? period)
        {
            if (period is not { } value) return null;
            return value >= 100_000
                ? string.Create(CultureInfo.InvariantCulture, $"{value / 100:D4}-{value % 100:D2}")
                : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string RangeText(DateOnly? startDate, DateOnly? endDate)
        {
            if (startDate is { } from && endDate is { } to) return $"{from:yyyy-MM-dd} 至 {to:yyyy-MM-dd}";
            if (endDate is { } until) return $"截至 {until:yyyy-MM-dd}";
            if (startDate is { } since) return $"{since:yyyy-MM-dd} 起";
            return "全部";
        }
    }
}
