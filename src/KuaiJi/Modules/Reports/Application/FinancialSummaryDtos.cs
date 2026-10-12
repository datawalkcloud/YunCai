namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>财务概要的一个指标行：本年累计（含同比）与本期（含同比、环比）。</summary>
    public sealed class FinancialSummaryRowDto
    {
        /// <summary>指标名称（资产总额/负债总额/所有者权益/营业收入/…）。</summary>
        public string Item { get; init; } = null!;

        /// <summary>行次（从 1 开始）。</summary>
        public int Line { get; init; }

        /// <summary>本年累计金额。存量类指标（资产/负债/权益）为期末余额。</summary>
        public decimal YearToDateAmount { get; init; }

        /// <summary>本年累计同比变化（百分比；对比基数为 0 时为 null）。</summary>
        public decimal? YearToDateYoyPercent { get; init; }

        /// <summary>本期金额。存量类指标为本月末余额，流量类为本月发生额。</summary>
        public decimal CurrentAmount { get; init; }

        /// <summary>本期同比变化（百分比）。</summary>
        public decimal? CurrentYoyPercent { get; init; }

        /// <summary>本期环比变化（百分比，对比上月）。</summary>
        public decimal? CurrentMomPercent { get; init; }
    }

    /// <summary>财务概要信息：核心存量与流量指标的同比/环比速览。</summary>
    public sealed class FinancialSummaryDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间显示文本。</summary>
        public string PeriodText { get; init; } = null!;

        public IReadOnlyList<FinancialSummaryRowDto> Rows { get; init; } = [];
    }
}
