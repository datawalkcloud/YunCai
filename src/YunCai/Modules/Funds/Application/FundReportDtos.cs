namespace YunCai.Modules.Funds.Application
{
    /// <summary>资金报表可用的汇总维度（与前端六个页签一一对应）。</summary>
    public static class FundReportDimensions
    {
        public const string Account = "account";
        public const string Category = "category";
        public const string Counterparty = "counterparty";
        public const string Project = "project";
        public const string Department = "department";
        public const string Combo = "combo";

        /// <summary>全部维度（按页签顺序）。</summary>
        public static readonly string[] All =
        [
            Account,
            Category,
            Counterparty,
            Project,
            Department,
            Combo,
        ];
    }

    /// <summary>资金报表行：按维度汇总的收支数据。</summary>
    public sealed class FundReportRowDto
    {
        /// <summary>维度值名称（账户名 / 类别名 / 往来单位名 / 项目名 / 部门名；组合维度为主类别值）。</summary>
        public string Name { get; init; } = "";

        /// <summary>编码（账户维度为科目编码，类别维度为序号，其余维度为空）。</summary>
        public string? Code { get; init; }

        /// <summary>账户维度的期初余额（截至区间起始日之前）；其他维度为空。</summary>
        public decimal? OpeningBalance { get; init; }

        public decimal TotalIncome { get; init; }

        public decimal TotalExpense { get; init; }

        /// <summary>账户维度为期末余额；其他维度为净额（收入 − 支出）。</summary>
        public decimal Balance { get; init; }

        public int IncomeCount { get; init; }

        public int ExpenseCount { get; init; }

        /// <summary>组合维度的次类别编码；其余维度为空。</summary>
        public string? SecondaryCode { get; init; }

        /// <summary>组合维度的次类别值（项目名 / 部门名 / 往来单位名，取决于页面选择的次类别）；其余维度为空。</summary>
        public string? SecondaryName { get; init; }
    }

    /// <summary>报表分段：一张子表（如「现金账户」「收入类别」），自带小计行。</summary>
    public sealed class FundReportSectionDto
    {
        /// <summary>分段名称，用作该子表的第二列表头（现金账户 / 银行账户 / 收入类别 …）。</summary>
        public string Name { get; init; } = "";

        /// <summary>小计行标签（人民币合计 / 收入合计 / 往来单位合计 …）。</summary>
        public string SubtotalLabel { get; init; } = "合计";

        public IReadOnlyList<FundReportRowDto> Rows { get; init; } = [];

        /// <summary>分段小计行。</summary>
        public FundReportRowDto Subtotal { get; init; } = new();
    }

    /// <summary>资金报表结果。</summary>
    public sealed class FundReportDto
    {
        public string Dimension { get; init; } = FundReportDimensions.Account;

        public DateOnly StartDate { get; init; }

        public DateOnly EndDate { get; init; }

        /// <summary>分段集合；2 段以上时页面额外渲染「合计」行。</summary>
        public IReadOnlyList<FundReportSectionDto> Sections { get; init; } = [];

        /// <summary>总计行（跨分段合计）。</summary>
        public FundReportRowDto Total { get; init; } = new();

        /// <summary>页面底部提示（如「内部转账，不计入收支」）；无提示为 null。</summary>
        public string? Note { get; init; }

        /// <summary>外币换算说明（仅账户维度）。</summary>
        public string? CurrencyNote { get; init; }

        /// <summary>该维度是否支持按账户筛选。</summary>
        public bool SupportsAccountFilter { get; init; }

        /// <summary>组合维度的主类别名称（如「收支类别」）；非组合维度为空。</summary>
        public string? PrimaryText { get; init; }

        /// <summary>组合维度的次类别名称（如「项目」「部门」「往来单位」）；非组合维度为空。</summary>
        public string? SecondaryText { get; init; }
    }

    /// <summary>核对总账行：出纳账（日记账）与总账科目余额的对照，按会计期间列示。</summary>
    public sealed class FundReconciliationRowDto
    {
        public Guid JournalId { get; init; }

        public string JournalName { get; init; } = "";

        /// <summary>账户类型（1=现金，2=银行存款）。</summary>
        public int JournalType { get; init; }

        /// <summary>「项目」列文案：现金 / 银行存款。</summary>
        public string ProjectText { get; init; } = "";

        public string AccountCode { get; init; } = "";

        public string AccountName { get; init; } = "";

        /// <summary>币别（当前账套仅本币，固定「人民币」）。</summary>
        public string Currency { get; init; } = "人民币";

        /// <summary>期初余额（会计期间起始日之前：账户期初 + 以前流水）。</summary>
        public decimal OpeningBalance { get; init; }

        /// <summary>借方（收入）：期间起始日 ~ 核对日的收入合计。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方（支出）：期间起始日 ~ 核对日的支出合计。</summary>
        public decimal Credit { get; init; }

        /// <summary>余额 = 期初余额 + 借方 − 贷方（出纳口径）。</summary>
        public decimal Balance { get; init; }

        /// <summary>账面余额（总账科目余额，截至核对日，仅已过账凭证 + 科目期初）。</summary>
        public decimal LedgerBalance { get; init; }

        /// <summary>差异 = 余额 − 账面余额。</summary>
        public decimal Difference { get; init; }

        public bool Balanced { get; init; }

        /// <summary>提示说明（如绑定科目缺失）。</summary>
        public string? Note { get; init; }
    }

    /// <summary>核对总账结果。</summary>
    public sealed class FundReconciliationDto
    {
        /// <summary>会计期间起始日（核对日所在月的 1 日）。</summary>
        public DateOnly StartDate { get; init; }

        /// <summary>核对日（期间截止日）。</summary>
        public DateOnly Date { get; init; }

        public bool AllBalanced { get; init; }

        /// <summary>是否存在未关联会计科目的资金账户（会在页面顶部给出提示）。</summary>
        public bool HasUnlinkedAccounts { get; init; }

        public IReadOnlyList<FundReconciliationRowDto> Rows { get; init; } = [];
    }
}
