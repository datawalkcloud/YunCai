namespace YunCai.Modules.Archives.Domain
{
    /// <summary>
    /// 会计电子档案分类树节点：按账套隔离，ParentId 自引用构成任意层级目录。
    /// 「所有文件 / 内置七类 / 数据备份」由应用服务首次读取时播种，标记 IsSystem，不允许改名或删除，
    /// 但所有节点（含内置节点）下面都可以新增子节点。
    /// 档案的 Category 存从根节点拼接的路径（分隔符 <see cref="PathSeparator"/>），便于按子树过滤。
    /// </summary>
    public class ArchiveCategory
    {
        /// <summary>节点名称最大长度。</summary>
        public const int MaxNameLength = 50;

        /// <summary>档案类别路径的分隔符（与列表「路径」列展示一致）。</summary>
        public const char PathSeparator = '>';

        private ArchiveCategory()
        {
        }

        private ArchiveCategory(Guid id, Guid accountSetId, Guid? parentId, string name, int sortOrder, bool isSystem)
        {
            Id = id;
            AccountSetId = accountSetId;
            ParentId = parentId;
            Name = name;
            SortOrder = sortOrder;
            IsSystem = isSystem;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>节点标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>上级节点标识；空表示根节点。</summary>
        public Guid? ParentId { get; private set; }

        /// <summary>节点名称（单段，不含路径分隔符）。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>同级显示顺序。</summary>
        public int SortOrder { get; private set; }

        /// <summary>是否系统内置节点（不可改名 / 删除）。</summary>
        public bool IsSystem { get; private set; }

        /// <summary>创建时间。</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>新建分类节点。</summary>
        public static ArchiveCategory Create(Guid accountSetId, Guid? parentId, string name, int sortOrder, bool isSystem = false)
        {
            ValidateName(name);
            return new ArchiveCategory(Guid.NewGuid(), accountSetId, parentId, name.Trim(), sortOrder, isSystem);
        }

        /// <summary>重命名自建节点。</summary>
        public void Rename(string name)
        {
            if (IsSystem)
            {
                throw new InvalidOperationException("内置分类不能重命名。");
            }

            ValidateName(name);
            Name = name.Trim();
        }

        private static void ValidateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("分类名称不能为空。");
            }

            var trimmed = name.Trim();
            if (trimmed.Length > MaxNameLength)
            {
                throw new ArgumentException($"分类名称不能超过 {MaxNameLength} 个字符。");
            }

            if (trimmed.Contains(PathSeparator) || trimmed.Contains('/') || trimmed.Contains('\\'))
            {
                throw new ArgumentException("分类名称不能包含路径分隔符。");
            }
        }
    }
}
