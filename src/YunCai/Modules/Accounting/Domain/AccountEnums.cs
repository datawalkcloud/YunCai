namespace YunCai.Modules.Accounting.Domain
{
    /// <summary>
    /// 会计科目类别。对应《企业会计准则》的科目大类，决定科目在报表中的归属。
    /// </summary>
    public enum AccountCategory
    {
        /// <summary>资产。</summary>
        Asset = 1,

        /// <summary>负债。</summary>
        Liability = 2,

        /// <summary>权益（所有者权益）。</summary>
        Equity = 3,

        /// <summary>成本。</summary>
        Cost = 4,

        /// <summary>损益（收入、费用）。</summary>
        ProfitAndLoss = 5,
    }

    /// <summary>
    /// 科目余额方向（记账方向）。资产、成本类通常为借方；负债、权益类通常为贷方。
    /// </summary>
    public enum BalanceDirection
    {
        /// <summary>借方。</summary>
        Debit = 1,

        /// <summary>贷方。</summary>
        Credit = 2,
    }
}
