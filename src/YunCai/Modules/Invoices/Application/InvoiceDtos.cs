using YunCai.Modules.Invoices.Domain;

namespace YunCai.Modules.Invoices.Application
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

    /// <summary>
    /// 由发票生成凭证的请求。
    /// 销项：借 对方科目 价税合计 / 贷 收入科目 金额、贷 销项税额科目 税额；
    /// 进项：借 商品科目 金额、借 进项税额科目 税额 / 贷 对方科目 价税合计。
    /// 三个科目均可省略，省略时按科目编码取系统默认（销项 1122/5001/222100105，进项 2202/1405/222100101）。
    /// </summary>
    public sealed class GenerateInvoiceVoucherRequest
    {
        /// <summary>对方科目（销项记应收、进项记应付）；省略时按编码取默认科目。</summary>
        public Guid? CounterAccountId { get; init; }

        /// <summary>收入（销项）/ 商品或费用（进项）科目；省略时按编码取默认科目。</summary>
        public Guid? SubjectAccountId { get; init; }

        /// <summary>税额科目（销项税额 / 进项税额）；省略时按编码取默认科目。</summary>
        public Guid? TaxAccountId { get; init; }

        /// <summary>凭证字，省略为「记」。</summary>
        public string? Word { get; init; }

        /// <summary>凭证日期，省略取发票开票日期。</summary>
        public DateOnly? Date { get; init; }
    }

    /// <summary>由发票生成凭证的结果。</summary>
    public sealed class InvoiceVoucherResultDto
    {
        /// <summary>发票标识。</summary>
        public Guid InvoiceId { get; init; }

        /// <summary>新生成的凭证标识。</summary>
        public Guid VoucherId { get; init; }

        /// <summary>凭证字号，形如「记-12」。</summary>
        public string VoucherNo { get; init; } = null!;
    }

    /// <summary>批量生成发票凭证的请求。</summary>
    public sealed class GenerateInvoiceVouchersRequest
    {
        /// <summary>待生成凭证的发票标识集合。</summary>
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        /// <summary>科目与凭证字 / 日期设置，对整批生效；各项省略时按方向取默认科目。</summary>
        public GenerateInvoiceVoucherRequest Options { get; init; } = new();
    }

    /// <summary>批量生成发票凭证的结果。</summary>
    public sealed class InvoiceVoucherBatchResultDto
    {
        /// <summary>成功生成的张数。</summary>
        public int Created { get; init; }

        /// <summary>跳过的张数（已入账 / 暂不入账 / 不存在）。</summary>
        public int Skipped { get; init; }

        /// <summary>失败明细：发票标识 → 原因。</summary>
        public IReadOnlyList<InvoiceVoucherFailureDto> Failures { get; init; } = [];
    }

    /// <summary>批量生成凭证的单条失败明细。</summary>
    public sealed class InvoiceVoucherFailureDto
    {
        public Guid InvoiceId { get; init; }

        /// <summary>发票号码，便于前端提示定位。</summary>
        public string? InvoiceNo { get; init; }

        public string Reason { get; init; } = null!;
    }

    /// <summary>发票导入的一行（对应导出的一个商品明细行）。</summary>
    public sealed class InvoiceImportRow
    {
        public string? InvoiceType { get; init; }
        public DateOnly? InputDate { get; init; }
        public DateOnly? IssueDate { get; init; }
        public string? InvoiceNo { get; init; }
        public string? PartnerName { get; init; }
        public string? BizType { get; init; }
        public string? GoodsName { get; init; }
        public decimal Amount { get; init; }
        public decimal TaxRate { get; init; }
        public decimal? TaxAmount { get; init; }
        public DateOnly? AuthDate { get; init; }
        public int? TaxPeriod { get; init; }
    }

    /// <summary>发票导入结果。</summary>
    public sealed class InvoiceImportResult
    {
        /// <summary>解析出的数据行数。</summary>
        public int TotalRows { get; init; }

        /// <summary>成功导入的发票张数（多行商品按发票号聚合后计 1 张）。</summary>
        public int Imported { get; init; }

        /// <summary>跳过的数据行数。</summary>
        public int Skipped { get; init; }

        /// <summary>告警信息（重复发票号、金额非法等）。</summary>
        public IReadOnlyList<string> Warnings { get; init; } = [];
    }

    /// <summary>按标识批量操作发票的请求。</summary>
    public sealed class BulkInvoiceIdsRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];
    }

    /// <summary>批量变更发票入账状态的请求。</summary>
    public sealed class BulkInvoiceStatusRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        /// <summary>目标状态（1 未入账 / 2 已入账 / 3 暂不入账）。</summary>
        public int Status { get; init; }
    }

    /// <summary>批量认证进项发票的请求。</summary>
    public sealed class BulkAuthenticateRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];

        public DateOnly AuthDate { get; init; }

        /// <summary>税款所属期（yyyyMM），可为空。</summary>
        public int? TaxPeriod { get; init; }
    }

    /// <summary>批量操作结果。</summary>
    public sealed class BulkInvoiceResultDto
    {
        /// <summary>实际变更 / 删除的张数。</summary>
        public int Affected { get; init; }

        /// <summary>跳过的张数（不存在 / 方向不符等）。</summary>
        public int Skipped { get; init; }
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
