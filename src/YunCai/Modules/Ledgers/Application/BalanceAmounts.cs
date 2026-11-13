using YunCai.Modules.Assist.Domain;

namespace YunCai.Modules.Ledgers.Application
{
    /// <summary>
    /// 余额聚合主键：科目，或「科目 × 某类别下的单个档案」。
    /// 科目余额表用 <c>(accountId, null, null)</c>，核算项目三表用 <c>(accountId, type, itemId)</c>，
    /// 两级共用同一个累加器，因此「核算项目余额表按科目合计 == 科目余额表该科目」恒成立（前提：该维度必录）。
    /// </summary>
    public readonly record struct BalanceKey(Guid AccountId, AssistType? AssistType, Guid? AssistItemId);

    /// <summary>
    /// 一个聚合键在选定期间内的余额三要素。内部一律使用「净额 = 借 - 贷」口径，
    /// 只在输出层（DTO）按正负拆回借/贷两栏。
    /// </summary>
    public readonly record struct BalanceAmounts(decimal OpeningNet, decimal PeriodDebit, decimal PeriodCredit)
    {
        /// <summary>期末净额（借 - 贷）。</summary>
        public decimal EndingNet => OpeningNet + PeriodDebit - PeriodCredit;

        /// <summary>本期净额（借 - 贷）。</summary>
        public decimal PeriodNet => PeriodDebit - PeriodCredit;

        /// <summary>是否整期间无发生额且期初期末皆零（用于「无发生额且余额为 0 不显示」）。</summary>
        public bool IsEmpty => OpeningNet == 0m && PeriodDebit == 0m && PeriodCredit == 0m;

        /// <summary>把净额按正负拆入借/贷方：非负记借方，负数取绝对值记贷方。</summary>
        public static (decimal Debit, decimal Credit) SplitBySign(decimal net)
            => net >= 0m ? (net, 0m) : (0m, -net);
    }

    /// <summary>
    /// 一次聚合的产物：科目级与科目×档案级两级余额。
    /// 阶段二的物化表 <c>account_period_balances</c> 主键即 <see cref="BalanceKey"/>，列即本结构的三个字段。
    /// </summary>
    public sealed class LedgerBalances
    {
        /// <summary>科目级余额（键为 <c>(accountId, null, null)</c>）。</summary>
        public required IReadOnlyDictionary<Guid, BalanceAmounts> ByAccount { get; init; }

        /// <summary>科目 × 档案级余额。</summary>
        public required IReadOnlyDictionary<BalanceKey, BalanceAmounts> ByAccountAssist { get; init; }
    }
}
