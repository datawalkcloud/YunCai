namespace YunCai.Modules.Reports.Application
{
    /// <summary>利润表的一个报表行次：本期金额与本年累计金额（均已换算为该行的正常方向）。</summary>
    public sealed class IncomeStatementLineDto
    {
        /// <summary>报表行次（会企02表固定行号）。</summary>
        public int Line { get; init; }

        /// <summary>本期金额（选定月份的发生净额）。</summary>
        public decimal Current { get; init; }

        /// <summary>本年累计金额（本年 1 月 1 日至选定月末的发生净额）。</summary>
        public decimal YearToDate { get; init; }

        /// <summary>上年同期累计金额（上年 1 月 1 日至上年同月末的发生净额）。</summary>
        public decimal LastYear { get; init; }
    }

    /// <summary>利润表：选定会计期间及各标准行次的本期金额与本年累计金额。</summary>
    public sealed class IncomeStatementDto
    {
        /// <summary>会计期间——年份。</summary>
        public int Year { get; init; }

        /// <summary>会计期间——月份（1-12）。</summary>
        public int Month { get; init; }

        /// <summary>期间显示文本，形如 2026年07月。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>各标准行次的金额（仅含明细行；营业利润、利润总额、净利润等合计行由前端汇总）。</summary>
        public IReadOnlyList<IncomeStatementLineDto> Lines { get; init; } = [];
    }
}
