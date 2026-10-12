using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Accounting.Domain
{
    /// <summary>
    /// 科目所设置的辅助核算维度（科目聚合内的实体）：一条记录表示该科目需要按某个维度辅助核算。
    /// <see cref="IsRequired"/> 为假时对应「非必录项」——制单可不填，但填了仍参与账簿聚合。
    /// 该配置只影响制单校验，不改变历史分录。
    /// </summary>
    public class AccountAssistType
    {
        // EF Core 物化实体所需的无参构造函数。
        private AccountAssistType()
        {
        }

        private AccountAssistType(Guid id, Guid accountId, AssistType type, bool isRequired)
        {
            Id = id;
            AccountId = accountId;
            Type = type;
            IsRequired = isRequired;
        }

        /// <summary>设置标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属科目标识。</summary>
        public Guid AccountId { get; private set; }

        /// <summary>辅助核算类别。</summary>
        public AssistType Type { get; private set; }

        /// <summary>该维度在制单时是否必录。</summary>
        public bool IsRequired { get; private set; }

        internal void SetRequired(bool isRequired) => IsRequired = isRequired;

        internal static AccountAssistType Create(Guid accountId, AssistType type, bool isRequired)
            => new(Guid.NewGuid(), accountId, type, isRequired);
    }
}
