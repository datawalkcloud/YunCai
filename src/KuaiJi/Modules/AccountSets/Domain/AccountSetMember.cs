namespace KuaiJi.Modules.AccountSets.Domain
{
    /// <summary>账套成员在账套内的角色，决定其可使用的功能范围。</summary>
    public enum AccountSetMemberRole
    {
        /// <summary>账套管理员：全部功能，含成员与设置管理。</summary>
        Admin = 0,

        /// <summary>会计：凭证、账簿、报表、结账等日常核算。</summary>
        Accountant = 1,

        /// <summary>出纳：资金日记账、银行对账与收付款单据。</summary>
        Cashier = 2,

        /// <summary>查看者：只读，不能新增或修改任何单据。</summary>
        Viewer = 3,
    }

    /// <summary>
    /// 账套成员：一个账套下可协作的自然人及其角色。
    /// 账套创建者自动成为唯一的管理员成员（IsOwner = true），不可删除或停用。
    /// </summary>
    public class AccountSetMember
    {
        // EF Core 物化实体所需的无参构造函数。
        private AccountSetMember()
        {
        }

        private AccountSetMember(Guid id, Guid accountSetId, string name, string phone, string? email,
            AccountSetMemberRole role, bool isOwner)
        {
            Id = id;
            AccountSetId = accountSetId;
            Name = name;
            Phone = phone;
            Email = email;
            Role = role;
            IsOwner = isOwner;
            Enabled = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        /// <summary>成员标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>成员姓名（显示名）。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>手机号，账套内唯一（可留空表示仅站内使用）。</summary>
        public string Phone { get; private set; } = string.Empty;

        /// <summary>邮箱，可空。</summary>
        public string? Email { get; private set; }

        /// <summary>账套内角色。</summary>
        public AccountSetMemberRole Role { get; private set; }

        /// <summary>是否启用；停用后不可登录该账套。</summary>
        public bool Enabled { get; private set; }

        /// <summary>是否为账套创建者（管理员，不可删除/停用）。</summary>
        public bool IsOwner { get; private set; }

        /// <summary>创建时间（UTC）。</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>最后修改时间（UTC）。</summary>
        public DateTime UpdatedAt { get; private set; }

        /// <summary>新建普通成员。</summary>
        public static AccountSetMember Create(Guid accountSetId, string name, string phone, string? email, AccountSetMemberRole role)
            => new(Guid.NewGuid(), accountSetId, name.Trim(), phone.Trim(), string.IsNullOrWhiteSpace(email) ? null : email.Trim(), role, false);

        /// <summary>新建账套创建者成员（管理员）。</summary>
        public static AccountSetMember CreateOwner(Guid accountSetId, string name)
            => new(Guid.NewGuid(), accountSetId, name.Trim(), string.Empty, null, AccountSetMemberRole.Admin, true);

        /// <summary>修改成员资料与角色；创建者角色固定为管理员。</summary>
        public void Update(string name, string phone, string? email, AccountSetMemberRole role)
        {
            Name = name.Trim();
            Phone = phone.Trim();
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
            Role = IsOwner ? AccountSetMemberRole.Admin : role;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>启用或停用成员；创建者始终启用。</summary>
        public void SetEnabled(bool enabled)
        {
            Enabled = IsOwner ? true : enabled;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
