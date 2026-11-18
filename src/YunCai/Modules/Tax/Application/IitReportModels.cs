namespace YunCai.Modules.Tax.Application
{
    /// <summary>
    /// 个税「报表填写」的一行申报表数据，列口径与税局《个人所得税扣缴申报表》综合所得一致。
    /// 只包含本系统能算出的列：险种明细、专项附加扣除、捐赠与减免等需要登录电子税务局取回，
    /// 由前端按「未接入」渲染成空列。
    /// </summary>
    public sealed class TaxIitReportRowDto
    {
        public string Key { get; init; } = string.Empty;

        /// <summary>员工编号。</summary>
        public string Code { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string IdNo { get; init; } = string.Empty;

        /// <summary>本期收入 = 应发工资。</summary>
        public decimal CurrentIncome { get; init; }

        /// <summary>减除费用标准（取工资单的个税免征额，默认 5000）。</summary>
        public decimal StandardDeduction { get; init; }

        /// <summary>累计收入额 = 本年 1 月至所属期的本期收入合计。</summary>
        public decimal CumulativeIncome { get; init; }

        /// <summary>累计减除费用 = 减除费用标准 × 本年截至本月的任职受雇月份数。</summary>
        public decimal CumulativeStandardDeduction { get; init; }

        /// <summary>
        /// 累计专项扣除 = 本年 1 月至所属期的「代扣个人款项」合计。
        /// 工资表只登记合计、不拆分险种，所以本期专项扣除各明细列为空。
        /// </summary>
        public decimal CumulativeSpecialDeduction { get; init; }

        public string Remark { get; init; } = string.Empty;

        /// <summary>累计应纳税所得额。</summary>
        public decimal TaxableIncome { get; init; }

        /// <summary>预扣率。</summary>
        public decimal Rate { get; init; }

        public decimal QuickDeduction { get; init; }

        /// <summary>累计应纳税额 = 累计应纳税所得额 × 预扣率 − 速算扣除数。</summary>
        public decimal CumulativeTax { get; init; }

        /// <summary>已缴税额 = 本年 1 月至上月的本月应缴个税合计。</summary>
        public decimal PaidTax { get; init; }

        /// <summary>应补（退）税额 = 累计应纳税额 − 已缴税额。</summary>
        public decimal Refundable { get; init; }
    }

    /// <summary>个税报表填写的取数结果（含表头的三项汇总）。</summary>
    public sealed class TaxIitReportResultDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        public IReadOnlyList<TaxIitReportRowDto> Rows { get; init; } = Array.Empty<TaxIitReportRowDto>();

        /// <summary>申报总人数。</summary>
        public int PersonCount { get; init; }

        /// <summary>收入总额（本期）。</summary>
        public decimal TotalIncome { get; init; }

        /// <summary>应补（退）税额合计（本期）。</summary>
        public decimal TotalRefundable { get; init; }

        /// <summary>该期间还没有工资表数据时的提示，前端据此给出取数出口。</summary>
        public string? Message { get; init; }
    }
}
