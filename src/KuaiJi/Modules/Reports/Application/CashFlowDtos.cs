using KuaiJi.Modules.Reports.Domain.CashFlow;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>现金流量表行次金额。current 与 ytd 分别为期间金额与本年累计金额。</summary>
    public sealed class CashFlowLineDto
    {
        /// <summary>报表行次（一般企业现金流量表主表行号）。</summary>
        public int Line { get; init; }

        /// <summary>行次名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>所属活动分类（小计之后的汇总行可能为空）。</summary>
        public CashFlowSection? Section { get; init; }

        /// <summary>行次性质（流入/流出/小计/净额等）。</summary>
        public CashFlowRowKind Kind { get; init; }

        /// <summary>本期金额。</summary>
        public decimal Current { get; init; }

        /// <summary>本年累计金额。</summary>
        public decimal YearToDate { get; init; }

        /// <summary>上年同期累计金额（上年 1 月 1 日至上年同月末）。</summary>
        public decimal LastYear { get; init; }
    }

    /// <summary>现金流量表（月度口径：本期金额 + 本年累计金额）。</summary>
    public sealed class CashFlowDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间显示文本，形如 2026年09月。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>各标准行次金额（小计/净额/期初期末已由服务端汇总）。</summary>
        public IReadOnlyList<CashFlowLineDto> Lines { get; init; } = [];
    }

    /// <summary>现金流量表季报（本季金额 + 本年累计金额）。</summary>
    public sealed class CashFlowQuarterDto
    {
        public int Year { get; init; }

        /// <summary>季度（1-4）。</summary>
        public int Quarter { get; init; }

        /// <summary>期间显示文本，形如 2026年第3季度。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>各标准行次金额。</summary>
        public IReadOnlyList<CashFlowLineDto> Lines { get; init; } = [];
    }

    /// <summary>现金流量表底稿行：某张凭证的现金收付分摊到某个对手方科目后归入的标准行次。</summary>
    public sealed class CashFlowWorkpaperRowDto
    {
        public DateOnly Date { get; init; }

        /// <summary>凭证字号，形如 记-0001。</summary>
        public string VoucherText { get; init; } = null!;

        /// <summary>对手方分录摘要。</summary>
        public string Summary { get; init; } = null!;

        public string AccountCode { get; init; } = null!;

        public string AccountName { get; init; } = null!;

        /// <summary>归入的主表行次。</summary>
        public int Line { get; init; }

        public string LineName { get; init; } = null!;

        /// <summary>true=现金流入，false=现金流出。</summary>
        public bool Inflow { get; init; }

        /// <summary>分摊到该对手方的金额（流入/流出均为正数）。</summary>
        public decimal Amount { get; init; }
    }

    /// <summary>现金流量表底稿（主表金额的来源明细）。</summary>
    public sealed class CashFlowWorkpaperDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        public string PeriodText { get; init; } = null!;

        public IReadOnlyList<CashFlowWorkpaperRowDto> Rows { get; init; } = [];

        public decimal InflowTotal { get; init; }

        public decimal OutflowTotal { get; init; }
    }

    /// <summary>期初/期末现金余额按现金科目拆解的一行。</summary>
    public sealed class CashFlowOpeningRowDto
    {
        public string AccountCode { get; init; } = null!;

        public string AccountName { get; init; } = null!;

        /// <summary>期间起始日前的余额（借方为正，含科目启用期初）。</summary>
        public decimal Opening { get; init; }

        /// <summary>期间截止日的余额（借方为正）。</summary>
        public decimal Closing { get; init; }
    }

    /// <summary>期初现金余额明细（主表期初/期末行按科目展开）。</summary>
    public sealed class CashFlowOpeningDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        public string PeriodText { get; init; } = null!;

        public IReadOnlyList<CashFlowOpeningRowDto> Rows { get; init; } = [];

        public decimal OpeningTotal { get; init; }

        public decimal ClosingTotal { get; init; }
    }

    /// <summary>补充资料段落。</summary>
    public enum CashFlowSupplementGroup
    {
        /// <summary>1.将净利润调节为经营活动现金流量。</summary>
        Adjustment = 1,

        /// <summary>2.不涉及现金收支的重大投资和筹资活动。</summary>
        NonCashActivities = 2,

        /// <summary>3.现金及现金等价物净增加情况。</summary>
        CashChanges = 3,
    }

    /// <summary>现金流量表补充资料（间接法）行次。</summary>
    public sealed class CashFlowSupplementLineDto
    {
        /// <summary>补充资料行次（1-15 调节项、16-18 非现金投融资、19-23 现金净增加）。</summary>
        public int Line { get; init; }

        public string Name { get; init; } = null!;

        public CashFlowSupplementGroup Group { get; init; }

        /// <summary>是否为该段的合计/净额行。</summary>
        public bool IsTotal { get; init; }

        /// <summary>本期金额（第 19-23 行为期末时点数）。</summary>
        public decimal Current { get; init; }

        /// <summary>本年累计金额（第 19-23 行的期初数以年初为基准）。</summary>
        public decimal YearToDate { get; init; }
    }

    /// <summary>
    /// 现金流量表补充资料：净利润经调节项还原为经营活动现金流量净额，并核对现金及现金等价物净增加额。
    /// 调节结果与主表（直接法）经营活动现金流量净额的差额一并返回，供使用者判断归类口径的差异。
    /// </summary>
    public sealed class CashFlowSupplementDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        public string PeriodText { get; init; } = null!;

        public IReadOnlyList<CashFlowSupplementLineDto> Lines { get; init; } = [];

        /// <summary>主表（直接法）经营活动产生的现金流量净额，用于与补充资料勾稽。</summary>
        public decimal MainTableOperatingNet { get; init; }

        /// <summary>补充资料调节净额与主表经营活动净额的差额（0 表示完全勾稽）。</summary>
        public decimal Difference { get; init; }
    }
}
