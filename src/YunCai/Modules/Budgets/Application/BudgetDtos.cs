namespace YunCai.Modules.Budgets.Application
{
    /// <summary>新增 / 修改预算单的请求；Lines 为整单覆盖。</summary>
    public sealed class SaveBudgetRequest
    {
        public int Year { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Note { get; init; }

        public IReadOnlyList<SaveBudgetLineRequest> Lines { get; init; } = [];
    }

    /// <summary>预算行请求：科目 + 12 个月金额（不足 12 个按 0 补齐，交由服务层校验）。</summary>
    public sealed class SaveBudgetLineRequest
    {
        public Guid AccountId { get; init; }

        public IReadOnlyList<decimal> Months { get; init; } = [];
    }

    /// <summary>预算单列表项（不含逐月明细，便于年列表快速渲染）。</summary>
    public sealed class BudgetSummaryDto
    {
        public Guid Id { get; init; }
        public int Year { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Note { get; init; }
        public int LineCount { get; init; }
        public decimal Total { get; init; }
        public string CreatedBy { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    public sealed class BudgetLineDto
    {
        public Guid AccountId { get; init; }
        public string AccountCode { get; init; } = string.Empty;
        public string AccountName { get; init; } = string.Empty;

        /// <summary>是否明细科目（非末级科目预算只作汇总展示，账簿取数仍按末级）。</summary>
        public bool IsLeaf { get; init; }

        public IReadOnlyList<decimal> Months { get; init; } = [];
        public decimal Total { get; init; }
    }

    public sealed class BudgetDto
    {
        public Guid Id { get; init; }
        public int Year { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Note { get; init; }
        public string CreatedBy { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public IReadOnlyList<BudgetLineDto> Lines { get; init; } = [];
        public decimal Total { get; init; }
    }

    /// <summary>预算执行对比的一行：按月预算 / 实际与全年差异。</summary>
    public sealed class BudgetExecutionRowDto
    {
        public Guid AccountId { get; init; }
        public string AccountCode { get; init; } = string.Empty;
        public string AccountName { get; init; } = string.Empty;

        /// <summary>科目余额方向（1 借 / 2 贷），前端据此展示「超支 / 未达」。</summary>
        public int Direction { get; init; }

        public IReadOnlyList<decimal> BudgetMonths { get; init; } = [];
        public IReadOnlyList<decimal> ActualMonths { get; init; } = [];

        /// <summary>截至指定月份（默认全年）的预算与执行累计。</summary>
        public decimal BudgetToDate { get; init; }
        public decimal ActualToDate { get; init; }

        /// <summary>执行差异 = 实际 - 预算（正数表示超预算，收入类为超额完成）。</summary>
        public decimal Variance { get; init; }

        /// <summary>执行率（实际 / 预算，百分比）；预算为 0 时无意义，返回 null。</summary>
        public decimal? Rate { get; init; }
    }

    /// <summary>预算执行对比表：指定截至月份（缺省全年）。</summary>
    public sealed class BudgetExecutionDto
    {
        public Guid BudgetId { get; init; }
        public int Year { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Note { get; init; }

        /// <summary>截至月份（1-12）。</summary>
        public int ThroughMonth { get; init; }

        public IReadOnlyList<BudgetExecutionRowDto> Rows { get; init; } = [];
        public IReadOnlyList<decimal> BudgetMonths { get; init; } = [];
        public IReadOnlyList<decimal> ActualMonths { get; init; } = [];
        public decimal BudgetToDate { get; init; }
        public decimal ActualToDate { get; init; }
        public decimal Variance { get; init; }
        public decimal? Rate { get; init; }
    }
}
