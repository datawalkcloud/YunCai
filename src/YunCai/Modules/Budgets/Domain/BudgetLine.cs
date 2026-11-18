namespace YunCai.Modules.Budgets.Domain
{
    /// <summary>
    /// 预算行：一个科目 1-12 月的预算金额。金额按月拆列存储（而不是拼接字符串），
    /// 便于按月份直接求和与导出。
    /// </summary>
    public class BudgetLine
    {
        /// <summary>一年 12 个月。</summary>
        public const int MonthCount = 12;

        /// <summary>单月金额上限（与原金额字段精度保持一致）。</summary>
        public const decimal MaxAmount = 999_999_999.99m;

        private BudgetLine()
        {
        }

        private BudgetLine(Guid id, Guid budgetId, Guid accountSetId, Guid accountId, decimal[] months)
        {
            Id = id;
            BudgetId = budgetId;
            AccountSetId = accountSetId;
            AccountId = accountId;
            M01 = months[0];
            M02 = months[1];
            M03 = months[2];
            M04 = months[3];
            M05 = months[4];
            M06 = months[5];
            M07 = months[6];
            M08 = months[7];
            M09 = months[8];
            M10 = months[9];
            M11 = months[10];
            M12 = months[11];
        }

        public Guid Id { get; private set; }

        public Guid BudgetId { get; private set; }

        public Guid AccountSetId { get; private set; }

        public Guid AccountId { get; private set; }

        public decimal M01 { get; private set; }
        public decimal M02 { get; private set; }
        public decimal M03 { get; private set; }
        public decimal M04 { get; private set; }
        public decimal M05 { get; private set; }
        public decimal M06 { get; private set; }
        public decimal M07 { get; private set; }
        public decimal M08 { get; private set; }
        public decimal M09 { get; private set; }
        public decimal M10 { get; private set; }
        public decimal M11 { get; private set; }
        public decimal M12 { get; private set; }

        internal static BudgetLine Create(Guid budgetId, Guid accountSetId, Guid accountId, decimal[] months)
            => new(Guid.NewGuid(), budgetId, accountSetId, accountId, months);

        /// <summary>按月份顺序返回 12 个金额。</summary>
        public decimal[] ToMonths()
            => [M01, M02, M03, M04, M05, M06, M07, M08, M09, M10, M11, M12];

        /// <summary>全年预算合计。</summary>
        public decimal Total => M01 + M02 + M03 + M04 + M05 + M06 + M07 + M08 + M09 + M10 + M11 + M12;

        /// <summary>取指定月份（1-12）的预算额。</summary>
        public decimal AmountOf(int month) => month switch
        {
            1 => M01,
            2 => M02,
            3 => M03,
            4 => M04,
            5 => M05,
            6 => M06,
            7 => M07,
            8 => M08,
            9 => M09,
            10 => M10,
            11 => M11,
            12 => M12,
            _ => 0m,
        };
    }
}
