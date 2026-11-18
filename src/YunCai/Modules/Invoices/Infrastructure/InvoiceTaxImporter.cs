using YunCai.Modules.Invoices.Domain;

namespace YunCai.Modules.Invoices.Infrastructure
{
    /// <summary>
    /// 把税局取票结果写进发票池。发票号在「账套 + 方向」内唯一，重复同步只跳过不覆盖，
    /// 避免冲掉用户后续补的认证、入账状态或凭证关联。
    /// </summary>
    public sealed class InvoiceTaxImporter : IInvoiceTaxImporter
    {
        /// <summary>税局没有返回商品明细时，用这一行承载票头的合计金额与税额。</summary>
        private const string FallbackGoodsName = "（税局取票）";

        private readonly IInvoiceRepository _invoices;

        public InvoiceTaxImporter(IInvoiceRepository invoices)
        {
            _invoices = invoices;
        }

        public async Task<TaxInvoiceImportOutcome> ImportAsync(
            Guid accountSetId,
            IReadOnlyList<TaxInvoiceRecord> records,
            CancellationToken cancellationToken = default)
        {
            if (records.Count == 0)
            {
                return new TaxInvoiceImportOutcome();
            }

            var existing = new Dictionary<InvoiceDirection, HashSet<string>>
            {
                [InvoiceDirection.Sales] = await InvoiceNumbersAsync(accountSetId, InvoiceDirection.Sales, cancellationToken),
                [InvoiceDirection.Purchase] = await InvoiceNumbersAsync(accountSetId, InvoiceDirection.Purchase, cancellationToken),
            };

            var warnings = new List<string>();
            var imported = 0;
            var skipped = 0;

            foreach (var record in records)
            {
                var invoiceNo = Truncate(record.InvoiceNo?.Trim(), Invoice.MaxInvoiceNoLength);
                var partner = Truncate(record.PartnerName?.Trim(), Invoice.MaxPartnerLength);
                if (string.IsNullOrWhiteSpace(invoiceNo) || string.IsNullOrWhiteSpace(partner))
                {
                    skipped++;
                    warnings.Add("税局返回的发票缺少发票号码或对方名称，已跳过。");
                    continue;
                }

                if (record.Amount == 0m)
                {
                    skipped++;
                    warnings.Add($"发票「{invoiceNo}」金额为零，已跳过。");
                    continue;
                }

                if (existing[record.Direction].Contains(invoiceNo))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var invoiceType = Truncate(
                        string.IsNullOrWhiteSpace(record.InvoiceType) ? "其他发票" : record.InvoiceType.Trim(),
                        50);
                    var invoice = Invoice.Create(
                        accountSetId,
                        record.Direction,
                        invoiceType,
                        record.IssueDate,
                        record.IssueDate,
                        invoiceNo,
                        partner,
                        null,
                        "一键取票",
                        null);

                    var goodsName = string.IsNullOrWhiteSpace(record.GoodsName)
                        ? FallbackGoodsName
                        : record.GoodsName.Trim();
                    var taxRate = record.Amount == 0m
                        ? 0m
                        : Math.Round(Math.Abs(record.Tax / record.Amount) * 100m, 2, MidpointRounding.AwayFromZero);
                    taxRate = Math.Clamp(taxRate, 0m, 100m);

                    invoice.ReplaceLines(
                        [(goodsName, record.Amount, taxRate, record.Tax)],
                        allowNegative: true);

                    await _invoices.AddAsync(invoice, cancellationToken);
                    existing[record.Direction].Add(invoiceNo);
                    imported++;
                }
                catch (ArgumentException ex)
                {
                    skipped++;
                    warnings.Add($"发票「{invoiceNo}」写入失败：{ex.Message}");
                }
            }

            if (imported > 0)
            {
                await _invoices.SaveChangesAsync(cancellationToken);
            }

            return new TaxInvoiceImportOutcome
            {
                Imported = imported,
                Skipped = skipped,
                Warnings = warnings.Distinct().Take(10).ToList(),
            };
        }

        private async Task<HashSet<string>> InvoiceNumbersAsync(
            Guid accountSetId,
            InvoiceDirection direction,
            CancellationToken cancellationToken)
        {
            var list = await _invoices.GetRangeAsync(
                accountSetId,
                direction,
                cancellationToken: cancellationToken);
            return new HashSet<string>(list.Select(item => item.InvoiceNo), StringComparer.OrdinalIgnoreCase);
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
