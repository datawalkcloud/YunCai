namespace YunCai.Modules.Reports.Application
{
    /// <summary>
    /// 报表行下钻后的一个科目：金额与该行在报表上的方向一致
    /// （资产负债表为「借-贷」按科目类别正负号换算，利润表收入类取贷-借、费用类取借-贷）。
    /// </summary>
    public sealed class ReportDrilldownAccountDto
    {
        public Guid AccountId { get; init; }

        /// <summary>科目编码（末级科目；账簿下钻按它定位）。</summary>
        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        /// <summary>主金额：资产负债表为期末余额、利润表为本期金额。</summary>
        public decimal Current { get; init; }

        /// <summary>参照金额：资产负债表为年初余额、利润表为本年累计金额。</summary>
        public decimal YearToDate { get; init; }
    }

    /// <summary>
    /// 报表某一行次的科目级拆解。合计值由科目行相加得出，与报表行金额同源同口径，
    /// 用于前端「点行 → 看科目 → 再点科目进明细账」的两级下钻。
    /// </summary>
    public sealed class ReportDrilldownDto
    {
        /// <summary>报表行次。</summary>
        public int Line { get; init; }

        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间显示文本，形如 2026年07月。</summary>
        public string PeriodText { get; init; } = null!;

        public IReadOnlyList<ReportDrilldownAccountDto> Accounts { get; init; } = [];

        public decimal TotalCurrent { get; init; }

        public decimal TotalYearToDate { get; init; }
    }
}
