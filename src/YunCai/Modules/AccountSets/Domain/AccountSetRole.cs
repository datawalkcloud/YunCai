namespace YunCai.Modules.AccountSets.Domain
{
    /// <summary>
    /// 账套角色：权限以「模块.操作」键的逗号分隔文本保存（见 <see cref="RolePermissionCatalog"/>）。
    /// 预置角色由应用服务首次读取时播种，标记 IsSystem；账套管理员额外标记 IsAdmin，权限不可修改。
    /// </summary>
    public class AccountSetRole
    {
        /// <summary>角色名称最大长度。</summary>
        public const int MaxNameLength = 20;

        /// <summary>权限键之间的分隔符。</summary>
        public const char PermissionSeparator = ',';

        // EF Core 物化实体所需的无参构造函数。
        private AccountSetRole()
        {
        }

        private AccountSetRole(Guid id, Guid accountSetId, string name, bool isSystem, bool isAdmin, string permissions, int sortOrder)
        {
            Id = id;
            AccountSetId = accountSetId;
            Name = name;
            IsSystem = isSystem;
            IsAdmin = isAdmin;
            Permissions = permissions;
            SortOrder = sortOrder;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        /// <summary>角色标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>角色名称，账套内唯一。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>是否预置角色（不可删除；仅账套管理员不可改权限）。</summary>
        public bool IsSystem { get; private set; }

        /// <summary>是否账套管理员（固定全部权限，不可修改）。</summary>
        public bool IsAdmin { get; private set; }

        /// <summary>权限键文本（逗号分隔）。</summary>
        public string Permissions { get; private set; } = string.Empty;

        /// <summary>显示顺序。</summary>
        public int SortOrder { get; private set; }

        /// <summary>创建时间（UTC）。</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>最后修改时间（UTC）。</summary>
        public DateTime UpdatedAt { get; private set; }

        /// <summary>权限键集合。</summary>
        public IReadOnlyList<string> PermissionKeys => Parse(Permissions);

        /// <summary>新建角色。</summary>
        public static AccountSetRole Create(Guid accountSetId, string name, IReadOnlyList<string> permissions,
            bool isSystem = false, bool isAdmin = false, int sortOrder = 0)
        {
            ValidateName(name);
            return new AccountSetRole(
                Guid.NewGuid(),
                accountSetId,
                name.Trim(),
                isSystem,
                isAdmin,
                Serialize(permissions),
                sortOrder);
        }

        /// <summary>重命名自建角色；预置角色名称固定。</summary>
        public void Rename(string name)
        {
            if (IsSystem)
            {
                throw new InvalidOperationException("预置角色不能重命名。");
            }

            ValidateName(name);
            Name = name.Trim();
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>覆盖角色权限；账套管理员固定为全部权限。</summary>
        public void SetPermissions(IReadOnlyList<string> permissions)
        {
            if (IsAdmin)
            {
                throw new InvalidOperationException("账套管理员的权限不能修改。");
            }

            Permissions = Serialize(permissions);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>权限键集合 → 入库文本。</summary>
        public static string Serialize(IEnumerable<string> permissions)
            => string.Join(PermissionSeparator, RolePermissionCatalog.Normalize(permissions));

        /// <summary>入库文本 → 权限键集合。</summary>
        public static IReadOnlyList<string> Parse(string? permissions)
            => string.IsNullOrWhiteSpace(permissions)
                ? []
                : permissions.Split(PermissionSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static void ValidateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("角色名称不能为空。");
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"角色名称不能超过 {MaxNameLength} 个字符。");
            }
        }
    }
}
