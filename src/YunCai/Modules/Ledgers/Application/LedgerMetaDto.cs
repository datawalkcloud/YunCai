namespace YunCai.Modules.Ledgers.Application
{
    /// <summary>
    /// 账页公共页头信息：所有账页共用，前端一张表头组件即可渲染。
    /// </summary>
    public sealed class LedgerMetaDto
    {
        /// <summary>账套名称。</summary>
        public string AccountSetName { get; init; } = null!;

        /// <summary>记账单位名称。</summary>
        public string CompanyName { get; init; } = null!;

        /// <summary>起始日期（已归一到月初），形如 2026-07-01。</summary>
        public string StartDate { get; init; } = null!;

        /// <summary>截止日期（已归一到月末），形如 2026-07-31。</summary>
        public string EndDate { get; init; } = null!;

        /// <summary>期间显示文本，形如 2026-07-01 至 2026-07-31。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>账套已结账到的期间（如 2026-06），未结账为 null。</summary>
        public string? ClosedPeriodText { get; init; }

        /// <summary>查询截止期间是否已结账。</summary>
        public bool IsEndPeriodClosed { get; init; }

        /// <summary>区间内未过账凭证张数；大于 0 时页头须提示这些凭证未纳入账簿。</summary>
        public int UnpostedVoucherCount { get; init; }

        /// <summary>从装载结果投影页头。</summary>
        public static LedgerMetaDto From(LedgerDataSet dataSet) => From(dataSet, dataSet.StartDate, dataSet.EndDate);

        /// <summary>
        /// 投影页头，但用指定的显示区间覆盖装载区间。
        /// 明细类账页为算「本年累计」会把装载窗口向前扩到本年 1 月 1 日，页头仍应显示用户选定的月份区间。
        /// </summary>
        public static LedgerMetaDto From(LedgerDataSet dataSet, DateOnly displayStart, DateOnly displayEnd)
        {
            return new LedgerMetaDto
            {
                AccountSetName = dataSet.AccountSet.Name,
                CompanyName = dataSet.AccountSet.CompanyName,
                StartDate = displayStart.ToString("yyyy-MM-dd"),
                EndDate = displayEnd.ToString("yyyy-MM-dd"),
                PeriodText = $"{displayStart:yyyy-MM-dd} 至 {displayEnd:yyyy-MM-dd}",
                ClosedPeriodText = dataSet.ClosedPeriodText,
                IsEndPeriodClosed = dataSet.IsEndPeriodClosed,
                UnpostedVoucherCount = dataSet.UnpostedVoucherCount,
            };
        }
    }

    /// <summary>账簿行通用方向文本：借 / 贷 / 平。</summary>
    public static class LedgerDirectionText
    {
        /// <summary>按净额（借 - 贷）的正负给出余额方向文本。</summary>
        public static string Of(decimal net) => net > 0m ? "借" : net < 0m ? "贷" : "平";
    }
}
