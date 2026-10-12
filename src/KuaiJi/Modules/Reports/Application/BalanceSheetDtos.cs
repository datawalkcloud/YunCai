namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 资产负债表（会企01表）的一行金额。按标准报表行次归集，前端据行次填入对应位置。
    /// 金额已按该行的正常方向净额列示（如固定资产为原值扣减累计折旧、减值准备后的净额）。
    /// </summary>
    public sealed class BalanceSheetLineDto
    {
        /// <summary>报表行次（会企01表固定行号）。</summary>
        public int Line { get; init; }

        /// <summary>期末余额。</summary>
        public decimal Ending { get; init; }

        /// <summary>年初余额。</summary>
        public decimal Opening { get; init; }
    }

    /// <summary>资产负债表：选定会计期间及各标准行次的期末/年初金额。</summary>
    public sealed class BalanceSheetDto
    {
        /// <summary>会计期间——年份。</summary>
        public int Year { get; init; }

        /// <summary>会计期间——月份（1-12）。</summary>
        public int Month { get; init; }

        /// <summary>期末余额对应日期文本，形如 2026年07月31日。</summary>
        public string EndingDate { get; init; } = null!;

        /// <summary>年初余额对应日期文本，形如 2026年01月01日。</summary>
        public string OpeningDate { get; init; } = null!;

        /// <summary>各标准行次的金额（仅含明细行；合计行由前端按行次汇总）。</summary>
        public IReadOnlyList<BalanceSheetLineDto> Lines { get; init; } = [];
    }
}
