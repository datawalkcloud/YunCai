using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Vouchers.Domain
{
    /// <summary>
    /// 凭证分录上的一条辅助核算取值（凭证聚合内的实体）。
    /// 冗余存储 <see cref="AccountId"/> 与 <see cref="Type"/>：账簿按「科目 × 档案」聚合时
    /// 无需回查科目表与档案表，且可建覆盖索引 (account_id, assist_item_id)。
    /// </summary>
    public class VoucherEntryAssist
    {
        // EF Core 物化实体所需的无参构造函数。
        private VoucherEntryAssist()
        {
        }

        private VoucherEntryAssist(Guid id, Guid voucherEntryId, Guid accountId, AssistType type, Guid assistItemId)
        {
            Id = id;
            VoucherEntryId = voucherEntryId;
            AccountId = accountId;
            Type = type;
            AssistItemId = assistItemId;
        }

        /// <summary>取值标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属分录标识。</summary>
        public Guid VoucherEntryId { get; private set; }

        /// <summary>所属分录的科目标识（冗余）。</summary>
        public Guid AccountId { get; private set; }

        /// <summary>辅助核算类别（冗余，用于校验档案类型匹配与账簿分维度聚合）。</summary>
        public AssistType Type { get; private set; }

        /// <summary>辅助核算档案标识。</summary>
        public Guid AssistItemId { get; private set; }

        internal static VoucherEntryAssist Create(Guid voucherEntryId, Guid accountId, AssistRef reference)
            => new(Guid.NewGuid(), voucherEntryId, accountId, reference.Type, reference.ItemId);
    }
}
