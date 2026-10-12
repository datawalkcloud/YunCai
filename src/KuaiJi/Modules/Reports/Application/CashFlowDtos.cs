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
}
