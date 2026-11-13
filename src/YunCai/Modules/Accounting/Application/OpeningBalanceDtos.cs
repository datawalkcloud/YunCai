using YunCai.Modules.Accounting.Domain;

namespace YunCai.Modules.Accounting.Application
{
    /// <summary>期初余额表单行：末级科目可录入，非末级科目由下级汇总（只读）。</summary>
    public sealed class OpeningBalanceRowDto
    {
        public Guid AccountId { get; init; }

        public string Code { get; init; } = null!;

        public string Name { get; init; } = null!;

        public AccountCategory Category { get; init; }

        public BalanceDirection Direction { get; init; }

        public int Level { get; init; }

        public bool IsLeaf { get; init; }

        /// <summary>期初余额（按科目余额方向为正，反方向为负）。非末级科目为下级汇总值。</summary>
        public decimal OpeningBalance { get; init; }

        /// <summary>借方列示金额（净额为借方时）。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方列示金额（净额为贷方时）。</summary>
        public decimal Credit { get; init; }

        /// <summary>年初余额（与柠檬云期初页的「年初余额」列一致）。</summary>
        public decimal YearOpeningBalance { get; init; }
    }

    /// <summary>期初余额总表：含全部科目行与试算平衡合计。</summary>
    public sealed class OpeningBalancesDto
    {
        public IReadOnlyList<OpeningBalanceRowDto> Rows { get; init; } = Array.Empty<OpeningBalanceRowDto>();

        /// <summary>借方期初合计（按末级科目汇总）。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>贷方期初合计（按末级科目汇总）。</summary>
        public decimal TotalCredit { get; init; }

        /// <summary>是否试算平衡（借方合计 = 贷方合计）。</summary>
        public bool Balanced { get; init; }
    }

    /// <summary>批量保存期初余额请求。</summary>
    public sealed class SaveOpeningBalancesRequest
    {
        public IReadOnlyList<OpeningBalanceItem> Items { get; init; } = Array.Empty<OpeningBalanceItem>();
    }

    /// <summary>单个科目的期初余额（按科目余额方向为正，反方向余额用负数）。</summary>
    public sealed class OpeningBalanceItem
    {
        public Guid AccountId { get; init; }

        public decimal Amount { get; init; }
    }
}
