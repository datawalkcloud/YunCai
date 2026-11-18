namespace YunCai.Modules.AccountSets.Application
{
    /// <summary>账套角色列表行。</summary>
    public sealed class AccountSetRoleDto
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = null!;

        /// <summary>是否预置角色（不可删除）。</summary>
        public bool IsSystem { get; init; }

        /// <summary>是否账套管理员（权限不可修改）。</summary>
        public bool IsAdmin { get; init; }

        /// <summary>权限键集合（模块.操作）。</summary>
        public IReadOnlyList<string> Permissions { get; init; } = [];

        /// <summary>权限汇总文案（如「查看资金、账簿和报表 | 新增账套」）。</summary>
        public string PermissionText { get; init; } = string.Empty;

        public int SortOrder { get; init; }
    }

    /// <summary>新增 / 修改账套角色的请求。</summary>
    public sealed class SaveAccountSetRoleRequest
    {
        public string Name { get; init; } = string.Empty;

        /// <summary>权限键集合（模块.操作）。</summary>
        public IReadOnlyList<string> Permissions { get; init; } = [];
    }

    /// <summary>权限目录里的一个操作。</summary>
    public sealed class PermissionActionDto
    {
        public string Key { get; init; } = null!;

        public string Name { get; init; } = null!;
    }

    /// <summary>权限目录里的一个功能模块。</summary>
    public sealed class PermissionModuleDto
    {
        public string Key { get; init; } = null!;

        public string Name { get; init; } = null!;

        public IReadOnlyList<PermissionActionDto> Actions { get; init; } = [];
    }

    /// <summary>角色业务异常（消息直接展示给用户）。</summary>
    public sealed class RoleException : Exception
    {
        public RoleException(string message)
            : base(message)
        {
        }
    }
}
