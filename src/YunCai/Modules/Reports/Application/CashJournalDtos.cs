namespace YunCai.Modules.Reports.Application
{
    /// <summary>现金日记账的一条流水（由涉及库存现金科目的凭证分录派生）。</summary>
    public sealed class CashJournalRowDto
    {
        /// <summary>业务日期（凭证日期），形如 2026-07-03。</summary>
        public string Date { get; init; } = null!;

        /// <summary>关联凭证号，形如 记-002。</summary>
        public string VoucherNo { get; init; } = null!;

        /// <summary>摘要。</summary>
        public string Summary { get; init; } = null!;

        /// <summary>对方科目（同一凭证中除库存现金外的其他科目名称，多个以顿号连接）。</summary>
        public string ContraAccount { get; init; } = null!;

        /// <summary>收入金额（现金借方发生额）。</summary>
        public decimal Income { get; init; }

        /// <summary>支出金额（现金贷方发生额）。</summary>
        public decimal Expense { get; init; }
    }

    /// <summary>现金日记账：选定日期范围、期初余额与按日期排序的流水明细。</summary>
    public sealed class CashJournalDto
    {
        /// <summary>起始日期（含），形如 2026-07-01。</summary>
        public string StartDate { get; init; } = null!;

        /// <summary>截止日期（含），形如 2026-07-31。</summary>
        public string EndDate { get; init; } = null!;

        /// <summary>期间显示文本，形如 2026-07-01 至 2026-07-31。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>期初余额（期间起始日之前全部现金分录的净额）。</summary>
        public decimal OpeningBalance { get; init; }

        /// <summary>本期流水明细（按日期、凭证号排序；逐行余额由前端累计得出）。</summary>
        public IReadOnlyList<CashJournalRowDto> Rows { get; init; } = [];
    }
}
