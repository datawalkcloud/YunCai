namespace KuaiJi.Application.Reports
{
    /// <summary>科目余额表的一行：某科目在选定会计期间的期初、本期发生额与期末余额（各含借/贷）。</summary>
    public sealed class TrialBalanceRowDto
    {
        /// <summary>科目编码。</summary>
        public string Code { get; init; } = null!;

        /// <summary>科目名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>期初余额（借方）。</summary>
        public decimal OpeningDebit { get; init; }

        /// <summary>期初余额（贷方）。</summary>
        public decimal OpeningCredit { get; init; }

        /// <summary>本期发生额（借方）。</summary>
        public decimal PeriodDebit { get; init; }

        /// <summary>本期发生额（贷方）。</summary>
        public decimal PeriodCredit { get; init; }

        /// <summary>期末余额（借方）。</summary>
        public decimal EndingDebit { get; init; }

        /// <summary>期末余额（贷方）。</summary>
        public decimal EndingCredit { get; init; }
    }

    /// <summary>科目余额表：选定会计期间及其各科目余额行。</summary>
    public sealed class TrialBalanceDto
    {
        /// <summary>会计期间——年份。</summary>
        public int Year { get; init; }

        /// <summary>会计期间——月份（1-12）。</summary>
        public int Month { get; init; }

        /// <summary>会计期间显示文本，形如 2026年07月。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>各科目余额行（按科目编码排序，仅含有余额或本期发生额的科目）。</summary>
        public IReadOnlyList<TrialBalanceRowDto> Rows { get; init; } = [];
    }
}
