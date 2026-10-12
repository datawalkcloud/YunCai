using KuaiJi.Modules.Invoices.Domain;

namespace KuaiJi.Modules.Invoices.Application
{
    /// <summary>发票明细行 DTO。</summary>
    public sealed class InvoiceLineDto
    {
        public Guid Id { get; init; }

        public int LineNumber { get; init; }

        public string GoodsName { get; init; } = null!;

        public decimal Amount { get; init; }

        public decimal TaxRate { get; init; }

        public decimal TaxAmount { get; init; }

        /// <summary>价税合计。</summary>
        public decimal TotalWithTax => Amount + TaxAmount;
    }

    /// <summary>发票 DTO（含明细行）。</summary>
    public sealed class InvoiceDto
    {
        public Guid Id { get; init; }

        public string InvoiceType { get; init; } = null!;

        public DateOnly InputDate { get; init; }

        public DateOnly IssueDate { get; init; }

        public string InvoiceNo { get; init; } = null!;

        public string PartnerName { get; init; } = null!;

        /// <summary>业务类型。</summary>
        public string? BizType { get; init; }

        /// <summary>认证日期（进项）。</summary>
        public DateOnly? AuthDate { get; init; }

        /// <summary>税款所属期（进项，yyyyMM）。</summary>
        public int? TaxPeriod { get; init; }

        /// <summary>合计金额（不含税）。</summary>
        public decimal TotalAmount { get; init; }

        /// <summary>合计税额。</summary>
        public decimal TotalTax { get; init; }

        /// <summary>价税合计。</summary>
        public decimal TotalWithTax { get; init; }

        /// <summary>关联凭证文本。</summary>
        public string? VoucherText { get; init; }

        /// <summary>发票来源。</summary>
        public string? Source { get; init; }

        public string StatusText { get; init; } = null!;

        public IReadOnlyList<InvoiceLineDto> Lines { get; init; } = [];

        public static string StatusTextOf(InvoiceStatus status) => status switch
        {
            InvoiceStatus.Pending => "未入账",
            InvoiceStatus.Posted => "已入账",
            _ => "暂不入账",
        };
    }

    /// <summary>发票汇总（列表页合计行）。</summary>
    public sealed class InvoiceSummaryDto
    {
        public int Count { get; init; }

        public decimal TotalAmount { get; init; }

        public decimal TotalTax { get; init; }

        public decimal TotalWithTax { get; init; }
    }

    /// <summary>新建 / 修改发票请求。</summary>
    public sealed class SaveInvoiceRequest
    {
        public string InvoiceType { get; init; } = null!;

        public DateOnly InputDate { get; init; }

        public DateOnly IssueDate { get; init; }

        public string InvoiceNo { get; init; } = null!;

        public string PartnerName { get; init; } = null!;

        public string? BizType { get; init; }

        public string? Remark { get; init; }

        public IReadOnlyList<SaveInvoiceLineRequest> Lines { get; init; } = [];
    }

    /// <summary>发票明细行请求。</summary>
    public sealed class SaveInvoiceLineRequest
    {
        public string GoodsName { get; init; } = null!;

        public decimal Amount { get; init; }

        public decimal TaxRate { get; init; }

        public decimal TaxAmount { get; init; }
    }

    /// <summary>发票认证请求（仅进项）。</summary>
    public sealed class AuthenticateInvoiceRequest
    {
        public DateOnly AuthDate { get; init; }

        /// <summary>税款所属期（yyyyMM），可为空。</summary>
        public int? TaxPeriod { get; init; }
    }

    /// <summary>发票入账状态变更请求。</summary>
    public sealed class ChangeInvoiceStatusRequest
    {
        /// <summary>目标状态（1 未入账 / 2 已入账 / 3 暂不入账）。</summary>
        public int Status { get; init; }
    }

    /// <summary>发票资金一览表行 DTO。</summary>
    public sealed class InvoiceFundOverviewRowDto
    {
        /// <summary>往来单位名称。</summary>
        public string PartnerName { get; init; } = null!;

        /// <summary>销项发票金额（含税）。</summary>
        public decimal SalesWithTax { get; init; }

        /// <summary>收款金额。</summary>
        public decimal Received { get; init; }

        /// <summary>未开票金额（含税）= max(0, 收款 − 销项)。</summary>
        public decimal NotInvoiced => Math.Max(0m, Received - SalesWithTax);

        /// <summary>未收款金额 = max(0, 销项 − 收款)。</summary>
        public decimal NotReceived => Math.Max(0m, SalesWithTax - Received);
    }
}
