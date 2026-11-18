namespace YunCai.Modules.Reports.Application
{
    /// <summary>纳税统计表的一行：一个税费科目的计提净额与同比变化。</summary>
    public sealed class TaxStatisticsRowDto
    {
        /// <summary>科目编码。</summary>
        public string Code { get; init; } = null!;

        /// <summary>税费项目名称（科目名称）。</summary>
        public string Name { get; init; } = null!;

        /// <summary>行次（从 1 开始）。</summary>
        public int Line { get; init; }

        /// <summary>本期金额（选定月份计提净额 = 贷方发生 - 借方缴纳）。</summary>
        public decimal CurrentAmount { get; init; }

        /// <summary>本期已缴金额（借方发生额：缴纳/抵扣）。</summary>
        public decimal CurrentPaidAmount { get; init; }

        /// <summary>本期同比变化（百分比，相对上年同期；上年同期为 0 时为 null）。</summary>
        public decimal? CurrentYoyPercent { get; init; }

        /// <summary>本年累计金额（年初至选定月末计提净额）。</summary>
        public decimal YearToDateAmount { get; init; }

        /// <summary>本年累计已缴金额。</summary>
        public decimal YearToDatePaidAmount { get; init; }

        /// <summary>本年累计同比变化（百分比，相对上年同期累计）。</summary>
        public decimal? YearToDateYoyPercent { get; init; }
    }

    /// <summary>纳税统计表：按应交税费类科目统计计提净额、同比与累计。</summary>
    public sealed class TaxStatisticsDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间显示文本。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>税费科目行（按编码排序）。</summary>
        public IReadOnlyList<TaxStatisticsRowDto> Rows { get; init; } = [];
    }
}
