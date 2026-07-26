namespace KuaiJi.Domain.Accounting
{
    /// <summary>
    /// 会计科目（Chart of Accounts）聚合根。
    /// 支持多级科目：一级科目下可挂任意层级的明细科目；明细科目的类别继承自上级，
    /// 编码以上级编码为前缀（如 1002 银行存款 → 100201 工商银行）。仅末级科目可用于凭证记账。
    /// </summary>
    public class Account
    {
        private readonly List<Account> _children = new();

        // EF Core 物化实体所需的无参构造函数。
        private Account()
        {
        }

        private Account(
            Guid id,
            Guid accountSetId,
            string code,
            string name,
            AccountCategory category,
            BalanceDirection direction,
            Guid? parentId,
            int level,
            bool isPreset)
        {
            Id = id;
            AccountSetId = accountSetId;
            Code = code;
            Name = name;
            Category = category;
            Direction = direction;
            ParentId = parentId;
            Level = level;
            IsLeaf = true;
            IsEnabled = true;
            IsPreset = isPreset;
        }

        /// <summary>科目标识（代理主键）。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。科目不能脱离账套独立存在。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>科目编码，账套内唯一；明细科目以上级编码为前缀。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>科目名称。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>科目类别（资产/负债/权益/成本/损益），明细科目继承自上级。</summary>
        public AccountCategory Category { get; private set; }

        /// <summary>余额方向（借/贷）。</summary>
        public BalanceDirection Direction { get; private set; }

        /// <summary>上级科目标识；顶级科目为 null。</summary>
        public Guid? ParentId { get; private set; }

        /// <summary>科目级次，从 1 开始（顶级为 1）。</summary>
        public int Level { get; private set; }

        /// <summary>是否末级科目。仅末级科目允许在凭证分录中使用。</summary>
        public bool IsLeaf { get; private set; }

        /// <summary>是否启用。停用后不可在新凭证中选用，但保留历史数据。</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>是否系统预置科目。预置科目不允许删除。</summary>
        public bool IsPreset { get; private set; }

        /// <summary>助记码（拼音首字母等），用于快速检索，可为空。</summary>
        public string? Mnemonic { get; private set; }

        /// <summary>期初余额（账套启用期间的期初，按科目余额方向为正）。仅末级科目持有，上级由下级汇总。</summary>
        public decimal OpeningBalance { get; private set; }

        /// <summary>上级科目导航属性。</summary>
        public Account? Parent { get; private set; }

        /// <summary>下级明细科目集合。</summary>
        public IReadOnlyCollection<Account> Children => _children;

        /// <summary>创建一个顶级（一级）科目，须归属于某一账套。</summary>
        public static Account CreateRoot(
            Guid accountSetId,
            string code,
            string name,
            AccountCategory category,
            BalanceDirection direction,
            bool isPreset = false)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。", nameof(accountSetId));
            }

            Guard(code, name);
            return new Account(Guid.NewGuid(), accountSetId, code.Trim(), name.Trim(), category, direction, parentId: null, level: 1, isPreset);
        }

        /// <summary>
        /// 在当前科目下创建一个明细科目：类别继承自上级，方向默认继承（可显式覆盖），
        /// 级次自动加一。创建后当前科目不再是末级科目。
        /// </summary>
        public Account AddChild(string code, string name, BalanceDirection? direction = null, bool isPreset = false)
        {
            Guard(code, name);

            var childCode = code.Trim();
            if (!childCode.StartsWith(Code, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"明细科目编码「{childCode}」必须以上级科目编码「{Code}」为前缀。", nameof(code));
            }

            var child = new Account(
                Guid.NewGuid(),
                AccountSetId,
                childCode,
                name.Trim(),
                Category,
                direction ?? Direction,
                parentId: Id,
                level: Level + 1,
                isPreset)
            {
                Parent = this,
            };

            _children.Add(child);
            IsLeaf = false; // 一旦挂上下级，自身即为非末级科目。
            return child;
        }

        /// <summary>重命名科目。</summary>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("科目名称不能为空。", nameof(name));
            }

            Name = name.Trim();
        }

        /// <summary>修改余额方向。仅末级科目允许修改（非末级的方向由下级决定，无实际意义）。</summary>
        public void ChangeDirection(BalanceDirection direction)
        {
            if (!IsLeaf)
            {
                throw new InvalidOperationException("非末级科目不允许修改余额方向。");
            }

            Direction = direction;
        }

        /// <summary>设置助记码；传入空白则清除。</summary>
        public void SetMnemonic(string? mnemonic)
        {
            Mnemonic = string.IsNullOrWhiteSpace(mnemonic) ? null : mnemonic.Trim();
        }

        /// <summary>设置期初余额（按科目余额方向为正，反方向余额用负数）。仅末级科目允许录入。</summary>
        public void SetOpeningBalance(decimal amount)
        {
            if (!IsLeaf && amount != 0m)
            {
                throw new InvalidOperationException($"科目「{Code} {Name}」非末级科目，期初余额须录在末级科目上。");
            }

            OpeningBalance = amount;
        }

        /// <summary>启用科目。</summary>
        public void Enable() => IsEnabled = true;

        /// <summary>停用科目。</summary>
        public void Disable() => IsEnabled = false;

        /// <summary>编码方案变更时，用新编码、新上级和新级次重建此科目。</summary>
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
                throw new ArgumentException("科目编码不能为空。", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("科目名称不能为空。", nameof(name));
            }
        }
    }
}
