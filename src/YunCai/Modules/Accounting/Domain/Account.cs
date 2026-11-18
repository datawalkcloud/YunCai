using YunCai.Modules.Assist.Domain;

namespace YunCai.Modules.Accounting.Domain
{
    /// <summary>
    /// 会计科目（Chart of Accounts）聚合根。
    /// 支持多级科目：一级科目下可挂任意层级的明细科目；明细科目的类别继承自上级，
    /// 编码以上级编码为前缀（如 1002 银行存款 → 100201 工商银行）。仅末级科目可用于凭证记账。
    /// </summary>
    public class Account
    {
        /// <summary>计量单位最大长度（如 件、公斤、平方米）。</summary>
        public const int MaxUnitLength = 20;

        /// <summary>
        /// 现金类科目编码前缀：1001（库存现金/现金）、1002（银行存款）、1009 与 1012（其他货币资金）。
        /// 只按前四位匹配——新旧两套体系的现金与银行存款均以 1001/1002 开头，
        /// 其他货币资金在两套体系下编号不同（1009 与 1012），一并纳入。
        /// </summary>
        private static readonly string[] CashCodePrefixes = ["1001", "1002", "1009", "1012"];

        private readonly List<Account> _children = new();
        private readonly List<AccountAssistType> _assistTypes = new();
        private readonly List<AccountCurrency> _currencies = new();

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

        /// <summary>科目二级类别（流动资产/非流动资产…），明细科目创建时继承上级；为空表示尚未指定。只作维护与展示，不参与报表取数。</summary>
        public AccountSubCategory? SubCategory { get; private set; }

        /// <summary>是否为现金及现金等价物科目（现金流量表口径），预置科目按现金类编码预置。</summary>
        public bool IsCashAccount { get; private set; }

        /// <summary>是否数量核算。开启后须填计量单位，凭证分录可录数量与单价。</summary>
        public bool IsQuantityAccounted { get; private set; }

        /// <summary>数量核算的计量单位；未开启数量核算时为空。</summary>
        public string? Unit { get; private set; }

        /// <summary>是否外币核算。开启后凭证分录可选币别取自 <see cref="Currencies"/>。</summary>
        public bool IsForeignCurrencyAccounted { get; private set; }

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

        /// <summary>该科目设置的辅助核算维度（未设置则为空集合，表示不做辅助核算）。</summary>
        public IReadOnlyList<AccountAssistType> AssistTypes => _assistTypes;

        /// <summary>该科目允许使用的币别（未开启外币核算时为空集合）。</summary>
        public IReadOnlyList<AccountCurrency> Currencies => _currencies;

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
                throw new ArgumentException("必须指定所属账套。");
            }

            Guard(code, name);
            return new Account(Guid.NewGuid(), accountSetId, code.Trim(), name.Trim(), category, direction, parentId: null, level: 1, isPreset);
        }

        /// <summary>
        /// 在当前科目下创建一个明细科目：类别继承自上级，方向默认继承（可显式覆盖），
        /// 级次自动加一。二级类别与现金/数量/外币核算属性同样继承，创建后当前科目不再是末级科目。
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
                SubCategory = SubCategory,
                IsCashAccount = IsCashAccount,
                IsQuantityAccounted = IsQuantityAccounted,
                Unit = Unit,
                IsForeignCurrencyAccounted = IsForeignCurrencyAccounted,
            };

            foreach (var currency in _currencies)
            {
                child._currencies.Add(AccountCurrency.Create(child.Id, currency.CurrencyId));
            }

            _children.Add(child);
            IsLeaf = false; // 一旦挂上下级，自身即为非末级科目。
            return child;
        }

        /// <summary>重命名科目。</summary>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("科目名称不能为空。");
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

        /// <summary>
        /// 修改科目编码。仅末级科目允许——非末级的编码是下级编码的前缀，
        /// 改动会让整棵子树的编码不再连续。长度与层级由调用方按编码方案校验。
        /// </summary>
        public void ChangeCode(string code)
        {
            if (!IsLeaf)
            {
                throw new InvalidOperationException("非末级科目不允许修改科目编码，否则下级科目编码的前缀会失效。");
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("科目编码不能为空。");
            }

            Code = code.Trim();
        }

        /// <summary>设置科目二级类别；传入 null 表示清除。类别下不存在的取值视为无效。</summary>
        public void SetSubCategory(AccountSubCategory? subCategory)
        {
            if (subCategory is { } value && !AccountSubCategoryCatalog.IsValid(Category, value))
            {
                throw new ArgumentException(
                    $"「{AccountCategoryCatalog.LabelOf(Category)}」类科目没有二级类别「{AccountSubCategoryCatalog.LabelOf(value)}」。",
                    nameof(subCategory));
            }

            SubCategory = subCategory;
        }

        /// <summary>标记或取消「是否为现金及现金等价物科目」（现金流量表口径）。</summary>
        public void SetCashAccount(bool isCashAccount) => IsCashAccount = isCashAccount;

        /// <summary>
        /// 设置数量核算。开启时必须给出计量单位；关闭则清空单位，
        /// 已有分录的数量不再参与账簿汇总（历史数量值保留在分录上）。
        /// </summary>
        public void SetQuantityAccounting(bool isEnabled, string? unit = null)
        {
            if (!isEnabled)
            {
                IsQuantityAccounted = false;
                Unit = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(unit))
            {
                throw new ArgumentException("开启数量核算的科目必须填写计量单位。", nameof(unit));
            }

            var trimmed = unit.Trim();
            if (trimmed.Length > MaxUnitLength)
            {
                throw new ArgumentException($"计量单位不能超过 {MaxUnitLength} 个字符。", nameof(unit));
            }

            IsQuantityAccounted = true;
            Unit = trimmed;
        }

        /// <summary>
        /// 设置外币核算并整体覆盖可选币别。开启时至少要选一个币别；关闭则清空列表。
        /// 币别是否属于本账套由应用层校验（领域内拿不到币别档案）。
        /// </summary>
        public void SetForeignCurrencyAccounting(bool isEnabled, IEnumerable<Guid>? currencyIds = null)
        {
            // 关闭只需清列表，不要求传币别；判空必须放在开启分支之后，否则 SetForeignCurrencyAccounting(false) 直接抛 ArgumentNullException。
            if (!isEnabled)
            {
                IsForeignCurrencyAccounted = false;
                _currencies.Clear();
                return;
            }

            ArgumentNullException.ThrowIfNull(currencyIds);

            var distinct = currencyIds.Distinct().ToList();
            if (distinct.Count == 0)
            {
                throw new ArgumentException("开启外币核算的科目至少要选择一个币别。", nameof(currencyIds));
            }

            IsForeignCurrencyAccounted = true;
            _currencies.Clear();
            foreach (var currencyId in distinct)
            {
                _currencies.Add(AccountCurrency.Create(Id, currencyId));
            }
        }

        /// <summary>
        /// 整体覆盖该科目的辅助核算维度设置（同一类别只保留一条，后者覆盖前者）。
        /// 仅影响后续制单校验，已入账分录的辅助核算取值不变。
        /// </summary>
        public void SetAssistTypes(IEnumerable<(AssistType Type, bool IsRequired)> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            _assistTypes.Clear();
            foreach (var setting in settings.DistinctBy(s => s.Type))
            {
                _assistTypes.Add(AccountAssistType.Create(Id, setting.Type, setting.IsRequired));
            }
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

        /// <summary>
        /// 下级科目全部删除后恢复末级标记。IsLeaf 是持久化标记（挂下级时置 false），
        /// 删除下级若不同步回置，上级会永久停在非末级状态被「非末级不可记账/不可录期初」挡住。
        /// </summary>
        public void RestoreLeaf() => IsLeaf = true;

        /// <summary>编码方案变更时，用新编码、新上级和新级次重建此科目。</summary>
        public void RebaseCode(string newCode, Guid? newParentId, int newLevel)
        {
            Code = newCode;
            ParentId = newParentId;
            Level = newLevel;
        }

        /// <summary>
        /// 按编码判断是否属于现金类科目。建账预置与存量数据打标用它决定「是否为现金科目」初值；
        /// 明细科目因父编码为前缀而自然命中。
        /// </summary>
        public static bool LooksLikeCashAccount(string code) =>
            CashCodePrefixes.Any(prefix => code.StartsWith(prefix, StringComparison.Ordinal));

        private static void Guard(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("科目编码不能为空。");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("科目名称不能为空。");
            }
        }
    }
}
