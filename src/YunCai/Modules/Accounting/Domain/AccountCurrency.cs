namespace YunCai.Modules.Accounting.Domain
{
    /// <summary>
    /// 外币核算科目允许使用的币别（科目聚合内的实体）：一条记录表示制单时该科目可选这一币别。
    /// 不设外键，币别档案被删除时残留的取值由写入校验兜住，读取侧按账套现有币别过滤。
    /// </summary>
    public class AccountCurrency
    {
        // EF Core 物化实体所需的无参构造函数。
        private AccountCurrency()
        {
        }

        private AccountCurrency(Guid id, Guid accountId, Guid currencyId)
        {
            Id = id;
            AccountId = accountId;
            CurrencyId = currencyId;
        }

        /// <summary>设置标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属科目标识。</summary>
        public Guid AccountId { get; private set; }

        /// <summary>币别标识（账套币别字典）。</summary>
        public Guid CurrencyId { get; private set; }

        internal static AccountCurrency Create(Guid accountId, Guid currencyId)
            => new(Guid.NewGuid(), accountId, currencyId);
    }
}
