namespace YunCai.Modules.Funds.Domain
{
    /// <summary>
    /// 日记账类型。日记账按序时逐日登记，最常见为现金与银行存款两类。
    /// </summary>
    public enum JournalType
    {
        /// <summary>现金日记账（库存现金）。</summary>
        Cash = 1,

        /// <summary>银行存款日记账。</summary>
        BankDeposit = 2,
    }
}
