namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>资金报表可用的汇总维度（与前端页签对应）。</summary>
    public static class FundReportDimensions
    {
        public const string Account = "account";
        public const string Category = "category";
        public const string Counterparty = "counterparty";
    }

    /// <summary>资金报表行：按维度汇总的收支数据。</summary>
    public sealed class FundReportRowDto
    {
        /// <summary>维度值名称（账户名 / 类别名 / 往来单位名）。</summary>
        public string Name { get; init; } = "";

        /// <summary>账户维度为科目编码；其他维度为空。</summary>
        public string? Code { get; init; }

        /// <summary>账户维度的期初余额（截至区间起始日之前）；其他维度为空。</summary>
        public decimal? OpeningBalance { get; init; }

        public decimal TotalIncome { get; init; }

        public decimal TotalExpense { get; init; }

        /// <summary>账户维度为期末余额；其他维度为净额（收入 − 支出）。</summary>
        public decimal Balance { get; init; }

        public int IncomeCount { get; init; }

        public int ExpenseCount { get; init; }
    }

    /// <summary>资金报表结果。</summary>
    public sealed class FundReportDto
    {
        public string Dimension { get; init; } = FundReportDimensions.Account;

        public DateOnly StartDate { get; init; }

        public DateOnly EndDate { get; init; }

        public IReadOnlyList<FundReportRowDto> Rows { get; init; } = [];

        /// <summary>合计行。</summary>
        public FundReportRowDto Total { get; init; } = new();
    }

    /// <summary>核对总账行：出纳账（日记账余额）与总账科目余额的对照。</summary>
    public sealed class FundReconciliationRowDto
    {
        public Guid JournalId { get; init; }

        public string JournalName { get; init; } = "";

        /// <summary>账户类型（1=现金，2=银行存款）。</summary>
        public int JournalType { get; init; }

        public string AccountCode { get; init; } = "";

        public string AccountName { get; init; } = "";

        /// <summary>出纳余额（期初 + 收入 − 支出，截至核对日）。</summary>
        public decimal JournalBalance { get; init; }

        /// <summary>账面余额（总账科目余额，截至核对日，仅已过账凭证 + 科目期初）。</summary>
        public decimal LedgerBalance { get; init; }

        /// <summary>差异 = 出纳余额 − 账面余额。</summary>
        public decimal Difference { get; init; }

        public bool Balanced { get; init; }

        /// <summary>提示说明（如绑定科目缺失）。</summary>
        public string? Note { get; init; }
    }

    /// <summary>核对总账结果。</summary>
    public sealed class FundReconciliationDto
    {
        public DateOnly Date { get; init; }

        public bool AllBalanced { get; init; }

        public IReadOnlyList<FundReconciliationRowDto> Rows { get; init; } = [];
    }
}
