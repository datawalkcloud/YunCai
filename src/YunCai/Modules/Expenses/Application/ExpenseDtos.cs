using YunCai.Modules.Expenses.Domain;

namespace YunCai.Modules.Expenses.Application
{
    /// <summary>费用单据业务错误。</summary>
    public sealed class ExpenseException : Exception
    {
        public ExpenseException(string message) : base(message)
        {
        }
    }

    /// <summary>费用单据 DTO。</summary>
    public sealed class ExpenseBillDto
    {
        public Guid Id { get; init; }

        public string BillNo { get; init; } = null!;

        public DateOnly BillDate { get; init; }

        public string Reason { get; init; } = null!;

        public string ExpenseType { get; init; } = null!;

        public string? Payee { get; init; }

        public string? SettleMethod { get; init; }

        /// <summary>单据金额（含税）。</summary>
        public decimal Amount { get; init; }

        /// <summary>可抵扣税额。</summary>
        public decimal DeductibleTax { get; init; }

        /// <summary>不含税金额 = 单据金额 − 可抵扣税额。</summary>
        public decimal NetAmount => Amount - DeductibleTax;

        public string StatusText => Status == ExpenseBillStatus.Posted ? "已入账" : "已登记";

        public ExpenseBillStatus Status { get; init; }

        public Guid? VoucherId { get; init; }

        public string? VoucherText { get; init; }
    }

    /// <summary>费用单据列表合计。</summary>
    public sealed class ExpenseBillSummaryDto
    {
        public int Count { get; init; }

        /// <summary>单据金额合计（含税）。</summary>
        public decimal TotalAmount { get; init; }

        /// <summary>可抵扣税额合计。</summary>
        public decimal TotalDeductibleTax { get; init; }
    }

    /// <summary>新建 / 修改费用单据请求。</summary>
    public sealed class SaveExpenseBillRequest
    {
        /// <summary>单据编号；留空时自动生成。</summary>
        public string? BillNo { get; init; }

        public DateOnly BillDate { get; init; }

        public string Reason { get; init; } = null!;

        public string ExpenseType { get; init; } = null!;

        public string? Payee { get; init; }

        public string? SettleMethod { get; init; }

        public decimal Amount { get; init; }

        public decimal DeductibleTax { get; init; }
    }

    /// <summary>费用单据生成凭证请求。</summary>
    public sealed class GenerateExpenseVoucherRequest
    {
        /// <summary>费用科目（借方，按不含税金额）。</summary>
        public Guid ExpenseAccountId { get; init; }

        /// <summary>对方科目（贷方，按含税金额）。</summary>
        public Guid CounterAccountId { get; init; }

        /// <summary>进项税额科目（有可抵扣税额时必填，自动推断 2221 下「进项税额」）。</summary>
        public Guid? TaxAccountId { get; init; }

        public string? Word { get; init; }

        public string Preparer { get; init; } = "系统";
    }

    /// <summary>费用单据批量生成凭证请求。</summary>
    public sealed class GenerateExpenseVouchersRequest
    {
        public IReadOnlyList<Guid> BillIds { get; init; } = [];

        public Guid ExpenseAccountId { get; init; }

        public Guid CounterAccountId { get; init; }

        public Guid? TaxAccountId { get; init; }

        public string? Word { get; init; }

        public string Preparer { get; init; } = "系统";
    }

    /// <summary>批量生成结果（逐张报错，不整批失败）。</summary>
    public sealed class ExpenseVoucherBatchResultDto
    {
        public int Generated { get; init; }

        public int Skipped { get; init; }

        public IReadOnlyList<ExpenseVoucherFailureDto> Failures { get; init; } = [];
    }

    /// <summary>批量生成失败明细。</summary>
    public sealed class ExpenseVoucherFailureDto
    {
        public Guid BillId { get; init; }

        public string BillNo { get; init; } = null!;

        public string Reason { get; init; } = null!;
    }

    /// <summary>费用单据导入结果。</summary>
    public sealed class ExpenseImportResultDto
    {
        public int Imported { get; init; }

        public int Updated { get; init; }

        public IReadOnlyList<ExpenseImportFailureDto> Failures { get; init; } = [];
    }

    /// <summary>费用单据导入失败明细。</summary>
    public sealed class ExpenseImportFailureDto
    {
        public int Row { get; init; }

        public string Reason { get; init; } = null!;
    }

    /// <summary>费用单据导入行。</summary>
    public sealed class ExpenseImportRow
    {
        public int Row { get; init; }

        public string? BillNo { get; init; }

        public string? BillDate { get; init; }

        public string? Reason { get; init; }

        public string? ExpenseType { get; init; }

        public string? Payee { get; init; }

        public string? SettleMethod { get; init; }

        public string? Amount { get; init; }

        public string? DeductibleTax { get; init; }
    }

    /// <summary>费用单据批量操作请求。</summary>
    public sealed class ExpenseBulkIdsRequest
    {
        public IReadOnlyList<Guid> Ids { get; init; } = [];
    }

    /// <summary>费用单据批量操作结果。</summary>
    public sealed class ExpenseBulkResultDto
    {
        /// <summary>成功处理条数。</summary>
        public int Affected { get; init; }

        /// <summary>跳过条数。</summary>
        public int Skipped { get; init; }

        /// <summary>跳过原因（逐条）。</summary>
        public IReadOnlyList<string> Messages { get; init; } = [];
    }

    /// <summary>单张生成凭证结果。</summary>
    public sealed class ExpenseVoucherResultDto
    {
        /// <summary>生成的凭证字号，如「记-12」。</summary>
        public string VoucherText { get; init; } = string.Empty;
    }
}
