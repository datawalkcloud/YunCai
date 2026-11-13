namespace YunCai.Modules.Funds.Domain
{
    /// <summary>
    /// 收支类别类型。与日记账流水的收入 / 支出方向对应。
    /// </summary>
    public enum EntryCategoryType
    {
        /// <summary>收入类别。</summary>
        Income = 1,

        /// <summary>支出类别。</summary>
        Expense = 2,
    }
}
