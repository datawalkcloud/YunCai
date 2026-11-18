namespace YunCai.Modules.Reports.Application
{
    /// <summary>费用明细表的一行：一个费用类科目（或上级科目汇总）。</summary>
    public sealed class ExpenseDetailRowDto
    {
        /// <summary>科目 id。</summary>
        public Guid Id { get; init; }

        /// <summary>科目编码。</summary>
        public string Code { get; init; } = null!;

        /// <summary>科目名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>科目层级（1 为一级）。</summary>
        public int Level { get; init; }

        /// <summary>上级科目 id（一级科目为空）。</summary>
        public Guid? ParentId { get; init; }

        /// <summary>是否有下级科目（非叶行金额由下级汇总）。</summary>
        public bool HasChildren { get; init; }

        /// <summary>本期金额（选定月份借-贷净发生额）。</summary>
        public decimal Current { get; init; }

        /// <summary>合计（本年累计借-贷净发生额）。</summary>
        public decimal Total { get; init; }
    }

    /// <summary>费用明细表：成本类与损益类（借方向）科目按期间的本期发生与本年累计。</summary>
    public sealed class ExpenseDetailDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间显示文本。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>科目行（按编码排序，含非叶级次以便前端展开）。</summary>
        public IReadOnlyList<ExpenseDetailRowDto> Rows { get; init; } = [];
    }
}
