namespace YunCai.Modules.AccountSets.Application
{
    /// <summary>账套成员列表行。</summary>
    public sealed class AccountSetMemberDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

        public string Phone { get; init; } = string.Empty;

        public string? Email { get; init; }

        /// <summary>角色标识。</summary>
        public Guid RoleId { get; init; }

        /// <summary>角色名称。</summary>
        public string RoleName { get; init; } = string.Empty;

        /// <summary>该角色的权限说明（用于列表「权限」列）。</summary>
        public string PermissionText { get; init; } = string.Empty;

        public bool Enabled { get; init; }

        /// <summary>是否为账套创建者。</summary>
        public bool IsOwner { get; init; }

        public DateTime CreatedAt { get; init; }
    }

    /// <summary>新增 / 修改账套成员的请求。</summary>
    public sealed class SaveAccountSetMemberRequest
    {
        public string Name { get; init; } = string.Empty;

        public string Phone { get; init; } = string.Empty;

        public string? Email { get; init; }

        /// <summary>角色标识；必须属于当前账套。</summary>
        public Guid RoleId { get; init; }
    }

    /// <summary>成员启用状态切换请求。</summary>
    public sealed class ToggleMemberRequest
    {
        public bool Enabled { get; init; }
    }

    /// <summary>成员业务异常（消息直接展示给用户）。</summary>
    public sealed class MemberException : Exception
    {
        public MemberException(string message)
            : base(message)
        {
        }
    }
}
