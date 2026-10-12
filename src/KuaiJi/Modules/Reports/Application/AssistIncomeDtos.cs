namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>部门利润表的一个部门列（列头）。</summary>
    public sealed class IncomeDepartmentDto
    {
        /// <summary>辅助核算档案 id。</summary>
        public Guid Id { get; init; }

        /// <summary>部门名称。</summary>
        public string Name { get; init; } = null!;
    }

    /// <summary>部门利润表的一个项目行：各部门的本期金额与本年累计金额（与列头顺序对齐）。</summary>
    public sealed class IncomeDepartmentRowDto
    {
        /// <summary>利润表项目名称（营业收入/营业成本/…/净利润）。</summary>
        public string Item { get; init; } = null!;

        /// <summary>行次（部门利润表内部顺序，从 1 开始）。</summary>
        public int Line { get; init; }

        /// <summary>各部门金额（与 Departments 顺序对齐）。</summary>
        public IReadOnlyList<IncomeDepartmentAmountDto> Amounts { get; init; } = [];
    }

    /// <summary>某部门在某行的金额。</summary>
    public sealed class IncomeDepartmentAmountDto
    {
        /// <summary>本期金额（选定月份）。</summary>
        public decimal Current { get; init; }

        /// <summary>本年累计金额。</summary>
        public decimal YearToDate { get; init; }
    }

    /// <summary>部门利润表：按辅助核算「部门」维度拆解收入、成本费用与利润。</summary>
    public sealed class DepartmentIncomeDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间显示文本，形如 2026年09月。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>部门列头（仅列示本年有损益发生的部门；无数据时为空，前端提示「暂无部门信息」）。</summary>
        public IReadOnlyList<IncomeDepartmentDto> Departments { get; init; } = [];

        /// <summary>项目行（含合计行 营业利润/利润总额/净利润）。</summary>
        public IReadOnlyList<IncomeDepartmentRowDto> Rows { get; init; } = [];
    }

    /// <summary>项目利润表的一个项目行：该项目收入抵减成本费用后的利润额。</summary>
    public sealed class IncomeProjectRowDto
    {
        /// <summary>辅助核算档案 id。</summary>
        public Guid Id { get; init; }

        /// <summary>项目名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>行次（从 1 开始）。</summary>
        public int Line { get; init; }

        /// <summary>本期利润额（收入-成本费用）。</summary>
        public decimal Current { get; init; }

        /// <summary>本年累计利润额。</summary>
        public decimal YearToDate { get; init; }
    }

    /// <summary>项目利润表：按辅助核算「项目」维度拆解损益。</summary>
    public sealed class ProjectIncomeDto
    {
        public int Year { get; init; }

        public int Month { get; init; }

        /// <summary>期间显示文本。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>项目行（仅列示有损益发生的项目）。</summary>
        public IReadOnlyList<IncomeProjectRowDto> Rows { get; init; } = [];
    }
}
