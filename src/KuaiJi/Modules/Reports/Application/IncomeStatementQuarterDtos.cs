namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>利润表季报：选定季度各标准行次的本季金额与本年累计金额。</summary>
    public sealed class IncomeStatementQuarterDto
    {
        /// <summary>会计年度。</summary>
        public int Year { get; init; }

        /// <summary>季度（1-4）。</summary>
        public int Quarter { get; init; }

        /// <summary>期间显示文本，形如 2026年第3季度。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>各标准行次的金额（明细行；合计行由前端汇总）。</summary>
        public IReadOnlyList<IncomeStatementLineDto> Lines { get; init; } = [];
    }
}
