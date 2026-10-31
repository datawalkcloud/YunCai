using System.Globalization;
using KuaiJi.Modules.Invoices.Application;
using KuaiJi.Modules.Invoices.Domain;
using MiniExcelLibs;

namespace KuaiJi.Modules.Invoices.Infrastructure
{
    /// <summary>
    /// 发票导出生成器：用 MiniExcel 把发票导出为 .xlsx。
    /// 按「商品明细行」展开（一张发票有多行商品时占多行、发票号码重复），
    /// 保证税率 / 金额信息无损；导入时再按发票号码聚合成一张发票。
    /// </summary>
    public static class InvoiceExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>
        /// 生成发票导出文件。方向与页签一致：销项不含认证日期 / 税款所属期两列。
        /// </summary>
        public static byte[] Build(IReadOnlyList<InvoiceDto> invoices, InvoiceDirection direction)
        {
            var isSales = direction == InvoiceDirection.Sales;
            var rows = new List<Dictionary<string, object?>>();

            foreach (var invoice in invoices)
            {
                // 无明细行的发票（老数据）也须导出：用表头占位保证不丢发票。
                var lines = invoice.Lines is { Count: > 0 } lines2
                    ? lines2
                    : [new InvoiceLineDto { GoodsName = string.Empty, Amount = invoice.TotalAmount, TaxRate = 0m, TaxAmount = invoice.TotalTax }];

                foreach (var line in lines)
                {
                    var row = new Dictionary<string, object?>
                    {
                        ["发票种类"] = invoice.InvoiceType,
                        ["录入日期"] = invoice.InputDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["开票日期"] = invoice.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["发票号码"] = invoice.InvoiceNo,
                        [isSales ? "客户名称" : "供应商名称"] = invoice.PartnerName,
                        ["业务类型"] = invoice.BizType ?? string.Empty,
                        ["商品名称"] = line.GoodsName,
                        ["不含税金额"] = line.Amount,
                        ["税率(%)"] = line.TaxRate,
                        ["税额"] = line.TaxAmount,
                        ["价税合计"] = line.Amount + line.TaxAmount,
                    };

                    if (!isSales)
                    {
                        row["认证日期"] = invoice.AuthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
                        row["税款所属期"] = invoice.TaxPeriod?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                    }

                    row["关联凭证"] = invoice.VoucherText ?? string.Empty;
                    row["发票来源"] = invoice.Source ?? string.Empty;
                    row["状态"] = invoice.StatusText;
                    rows.Add(row);
                }
            }

            if (rows.Count == 0)
            {
                rows.Add(EmptyRow(isSales));
            }

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { ["发票"] = rows }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>生成「发票资金一览表」导出（按往来单位的票款对账）。</summary>
        public static byte[] BuildFundOverview(IReadOnlyList<InvoiceFundOverviewRowDto> rows)
        {
            var body = rows
                .Select(r => new Dictionary<string, object?>
                {
                    ["往来单位"] = r.PartnerName,
                    ["销项发票金额（含税）"] = r.SalesWithTax,
                    ["收款金额"] = r.Received,
                    ["未开票金额（含税）"] = r.NotInvoiced,
                    ["未收款金额"] = r.NotReceived,
                })
                .ToList();

            if (body.Count == 0)
            {
                body.Add(new Dictionary<string, object?>
                {
                    ["往来单位"] = string.Empty,
                    ["销项发票金额（含税）"] = string.Empty,
                    ["收款金额"] = string.Empty,
                    ["未开票金额（含税）"] = string.Empty,
                    ["未收款金额"] = string.Empty,
                });
            }

            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { ["发票资金一览表"] = body }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }

        /// <summary>生成导出文件名，形如「销项发票_2025-10.xlsx」。</summary>
        public static string BuildFileName(InvoiceDirection direction, string periodLabel)
        {
            var name = direction == InvoiceDirection.Sales ? "销项发票" : "进项发票";
            return string.IsNullOrWhiteSpace(periodLabel) ? $"{name}.xlsx" : $"{name}_{periodLabel}.xlsx";
        }

        private static Dictionary<string, object?> EmptyRow(bool isSales)
        {
            var row = new Dictionary<string, object?>
            {
                ["发票种类"] = string.Empty,
                ["录入日期"] = string.Empty,
                ["开票日期"] = string.Empty,
                ["发票号码"] = string.Empty,
                [isSales ? "客户名称" : "供应商名称"] = string.Empty,
                ["业务类型"] = string.Empty,
                ["商品名称"] = string.Empty,
                ["不含税金额"] = string.Empty,
                ["税率(%)"] = string.Empty,
                ["税额"] = string.Empty,
                ["价税合计"] = string.Empty,
            };

            if (!isSales)
            {
                row["认证日期"] = string.Empty;
                row["税款所属期"] = string.Empty;
            }

            row["关联凭证"] = string.Empty;
            row["发票来源"] = string.Empty;
            row["状态"] = string.Empty;
            return row;
        }
    }
}
