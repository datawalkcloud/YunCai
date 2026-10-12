namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>所有者权益变动表的一行：某权益项目的期初/期末余额（本年与上年）。</summary>
    public sealed class EquityChangeRowDto
    {
        /// <summary>权益项目名称（实收资本/资本公积/盈余公积/未分配利润/所有者权益合计）。</summary>
        public string Item { get; init; } = null!;

        /// <summary>行次（从 1 开始）。</summary>
        public int Line { get; init; }

        /// <summary>本年期初余额。</summary>
        public decimal CurrentBegin { get; init; }

        /// <summary>本年期末余额（即「本年金额」）。</summary>
        public decimal CurrentEnd { get; init; }

        /// <summary>上年期初余额。</summary>
        public decimal PriorBegin { get; init; }

        /// <summary>上年期末余额（即「上年金额」）。</summary>
        public decimal PriorEnd { get; init; }
    }

    /// <summary>所有者权益变动表：按年度对比各权益项目的期初与期末余额（贷方为正）。</summary>
    public sealed class EquityChangeDto
    {
        /// <summary>会计年度。</summary>
        public int Year { get; init; }

        /// <summary>期间显示文本，形如 2026年度。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>权益项目行（末行为合计）。</summary>
        public IReadOnlyList<EquityChangeRowDto> Rows { get; init; } = [];
    }
}
