namespace YunCai.Modules.Expenses.Domain
{
    /// <summary>费用单据状态。</summary>
    public enum ExpenseBillStatus
    {
        /// <summary>已登记（未生成凭证）。</summary>
        Registered = 1,

        /// <summary>已入账（已生成凭证）。</summary>
        Posted = 2,
    }

    /// <summary>费用单据结算方式（展示用文本，不做强约束）。</summary>
    public static class ExpenseSettleMethods
    {
        public static readonly string[] All = ["银行转账", "库存现金", "支付宝", "微信", "挂应付账款"];
    }
}
