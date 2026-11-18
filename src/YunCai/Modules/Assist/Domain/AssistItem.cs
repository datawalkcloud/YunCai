namespace YunCai.Modules.Assist.Domain
{
    /// <summary>
    /// 辅助核算档案（核算项目）聚合根：某一维度下的具体档案，如「客户 / 某某科技有限公司」。
    /// 与会计科目同构——支持层级编码（上级编码为前缀）、末级才能记账、可停用而保留历史数据。
    /// 仅层级型类别（部门/项目/存货/现金流）允许挂下级档案。
    /// </summary>
    public class AssistItem
    {
        /// <summary>档案编码最大长度，与科目编码保持一致。</summary>
        public const int MaxCodeLength = 32;

        private readonly List<AssistItem> _children = new();

        // EF Core 物化实体所需的无参构造函数。
        private AssistItem()
        {
        }

        private AssistItem(
            Guid id,
            Guid accountSetId,
            AssistType type,
            string code,
            string name,
            Guid? parentId,
            int level)
        {
            Id = id;
            AccountSetId = accountSetId;
            Type = type;
            Code = code;
            Name = name;
            ParentId = parentId;
            Level = level;
            IsLeaf = true;
            IsEnabled = true;
        }

        /// <summary>档案标识（代理主键）。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。档案不能脱离账套独立存在。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>所属辅助核算类别。一经创建不可变更，否则历史分录的维度会错位。</summary>
        public AssistType Type { get; private set; }

        /// <summary>档案编码，同一账套同一类别内唯一；明细档案以上级编码为前缀。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>档案名称。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>上级档案标识；顶级档案为 null。</summary>
        public Guid? ParentId { get; private set; }

        /// <summary>档案级次，从 1 开始。</summary>
        public int Level { get; private set; }

        /// <summary>是否末级档案。仅末级档案允许在凭证分录中使用。</summary>
        public bool IsLeaf { get; private set; }

        /// <summary>是否启用。停用后不可在新凭证中选用，但保留历史数据与账簿列示。</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>联系人/证件号等资料，可为空。</summary>
        public string? Contact { get; private set; }

        /// <summary>备注，可为空。</summary>
        public string? Remark { get; private set; }

        /// <summary>上级档案导航属性。</summary>
        public AssistItem? Parent { get; private set; }

        /// <summary>下级档案集合。</summary>
        public IReadOnlyCollection<AssistItem> Children => _children;

        /// <summary>创建一个顶级档案。</summary>
        public static AssistItem Create(Guid accountSetId, AssistType type, string code, string name)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。");
            }

            Guard(code, name);
            return new AssistItem(Guid.NewGuid(), accountSetId, type, code.Trim(), name.Trim(), parentId: null, level: 1);
        }

        /// <summary>
        /// 在当前档案下创建一个下级档案。仅层级型类别允许；编码须以当前编码为前缀，级次自动加一，
        /// 创建后当前档案不再是末级档案。
        /// </summary>
        public AssistItem AddChild(string code, string name)
        {
            if (!AssistTypeText.SupportsHierarchy(Type))
            {
                throw new InvalidOperationException($"{AssistTypeText.Of(Type)}类档案不支持层级，不能添加下级。");
            }

            Guard(code, name);

            var childCode = code.Trim();
            if (!childCode.StartsWith(Code, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"下级档案编码「{childCode}」必须以上级档案编码「{Code}」为前缀。", nameof(code));
            }

            var child = new AssistItem(
                Guid.NewGuid(),
                AccountSetId,
                Type,
                childCode,
                name.Trim(),
                parentId: Id,
                level: Level + 1)
            {
                Parent = this,
            };

            _children.Add(child);
            IsLeaf = false; // 一旦挂上下级，自身即为非末级档案。
            return child;
        }

        /// <summary>重命名档案。</summary>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("档案名称不能为空。");
            }

            Name = name.Trim();
        }

        /// <summary>设置联系资料；传入空白则清除。</summary>
        public void SetContact(string? contact)
        {
            Contact = string.IsNullOrWhiteSpace(contact) ? null : contact.Trim();
        }

        /// <summary>设置备注；传入空白则清除。</summary>
        public void SetRemark(string? remark)
        {
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
        }

        /// <summary>启用档案。</summary>
        public void Enable() => IsEnabled = true;

        /// <summary>停用档案（不删除历史数据）。</summary>
        public void Disable() => IsEnabled = false;

        /// <summary>编码方案变更时，用新编码、新上级和新级次重建此档案。</summary>
        public void RebaseCode(string newCode, Guid? newParentId, int newLevel)
        {
            Code = newCode;
            ParentId = newParentId;
            Level = newLevel;
        }

        private static void Guard(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("档案编码不能为空。");
            }

            if (code.Trim().Length > MaxCodeLength)
            {
                throw new ArgumentException($"档案编码不能超过 {MaxCodeLength} 个字符。", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("档案名称不能为空。");
            }
        }
    }
}
