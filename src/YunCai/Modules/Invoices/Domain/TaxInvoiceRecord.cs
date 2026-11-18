namespace YunCai.Modules.Invoices.Domain
{
    /// <summary>
    /// 从电子税务局「全量发票查询」取到的一张发票（票头口径）。
    /// 只有合计金额 / 税额，没有商品明细时由导入方补一行合计行；红冲发票金额为负数。
    /// </summary>
    public sealed record TaxInvoiceRecord(
        InvoiceDirection Direction,
        string InvoiceType,
        DateOnly IssueDate,
        string InvoiceNo,
        string PartnerName,
        decimal Amount,
        decimal Tax,
        string? GoodsName,
        string? Raw);

    /// <summary>税局取票落库的结果；Warnings 只留前若干条给页面展示。</summary>
    public sealed class TaxInvoiceImportOutcome
    {
        public int Imported { get; init; }

        public int Skipped { get; init; }

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// 税局取票结果的落库入口。取票在税局无头浏览器里完成，属于 Tax 模块；
    /// 发票池的写入规则留在 Invoices 模块，两边只通过这个领域契约衔接。
    /// </summary>
    public interface IInvoiceTaxImporter
    {
        Task<TaxInvoiceImportOutcome> ImportAsync(
            Guid accountSetId,
            IReadOnlyList<TaxInvoiceRecord> records,
            CancellationToken cancellationToken = default);
    }
}
