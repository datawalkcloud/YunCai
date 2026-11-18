namespace YunCai.Modules.Funds.Domain
{
    /// <summary>
    /// 收支类别：日记账记账时对流水进行归类的字典项（如「销售收入」「办公费」）。
    /// 归属于某一账套，分收入 / 支出两类。分录按类别名称关联，
    /// 删除类别不影响既有流水中已登记的类别文本。
    /// </summary>
    public class EntryCategory
    {
        /// <summary>类别名称最大长度（与分录 category 列保持一致）。</summary>
        public const int MaxNameLength = 50;

        /// <summary>类别编码最大长度。</summary>
        public const int MaxCodeLength = 32;

        // EF Core 物化实体所需的无参构造函数。
        private EntryCategory()
        {
        }

        private EntryCategory(Guid id, Guid accountSetId, EntryCategoryType type, string code, string name)
        {
            Id = id;
            AccountSetId = accountSetId;
            Type = type;
            Code = code;
            Name = name;
            IsEnabled = true;
        }

        /// <summary>类别标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。收支类别不能脱离账套独立存在。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>类别类型（收入 / 支出）。</summary>
        public EntryCategoryType Type { get; private set; }

        /// <summary>类别编码，同一账套内唯一（默认按序编号 1、2、3…）。</summary>
        public string Code { get; private set; } = null!;

        /// <summary>类别名称，同一账套同一类型内唯一。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>上级类别标识；顶级类别为 null。</summary>
        public Guid? ParentId { get; private set; }

        /// <summary>关联现金流项目（辅助核算档案中「现金流」类别的档案）；可为空。</summary>
        public Guid? CashFlowItemId { get; private set; }

        /// <summary>是否启用。停用后不再出现在记账的类别下拉中，但保留历史数据。</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>智能匹配关键字（逗号分隔）；流水导入时按摘要包含关系自动归类。</summary>
        public string? SmartKeywords { get; private set; }

        /// <summary>默认对方科目（由流水生成凭证时的默认对方科目），可为空。</summary>
        public Guid? VoucherSubjectId { get; private set; }

        /// <summary>智能匹配关键字最大长度。</summary>
        public const int MaxKeywordsLength = 200;

        /// <summary>新建收支类别，须归属于某一账套。</summary>
        public static EntryCategory Create(Guid accountSetId, EntryCategoryType type, string name, string? code = null)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。");
            }

            var normalizedCode = NormalizeCode(code);
            GuardName(name);
            return new EntryCategory(Guid.NewGuid(), accountSetId, type, normalizedCode ?? "0", name.Trim());
        }

        /// <summary>重命名类别。</summary>
        public void Rename(string name)
        {
            GuardName(name);
            Name = name.Trim();
        }

        /// <summary>调整类别编码（须符合长度限制）。</summary>
        public void ChangeCode(string code)
        {
            var normalized = NormalizeCode(code) ?? throw new ArgumentException("类别编码不能为空。");
            Code = normalized;
        }

        /// <summary>设置上级类别；传入 null 表示置为顶级类别。</summary>
        public void SetParent(Guid? parentId)
        {
            if (parentId == Id)
            {
                throw new ArgumentException("上级类别不能是自己。");
            }

            ParentId = parentId is null || parentId == Guid.Empty ? null : parentId;
        }

        /// <summary>关联现金流项目；传入 null 表示清除。</summary>
        public void SetCashFlow(Guid? itemId)
        {
            CashFlowItemId = itemId is null || itemId == Guid.Empty ? null : itemId;
        }

        /// <summary>启用类别。</summary>
        public void Enable() => IsEnabled = true;

        /// <summary>停用类别（不删除历史数据）。</summary>
        public void Disable() => IsEnabled = false;

        private static string? NormalizeCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            var value = code.Trim();
            if (value.Length > MaxCodeLength)
            {
                throw new ArgumentException($"类别编码不能超过 {MaxCodeLength} 个字符。", nameof(code));
            }

            return value;
        }

        /// <summary>设置智能匹配关键字；传入空白则清除。</summary>
        public void SetSmartKeywords(string? keywords)
        {
            var value = string.IsNullOrWhiteSpace(keywords) ? null : keywords.Trim();
            if (value is { Length: > MaxKeywordsLength })
            {
                throw new ArgumentException($"智能匹配关键字不能超过 {MaxKeywordsLength} 个字符。", nameof(keywords));
            }

            SmartKeywords = value;
        }

        /// <summary>设置默认对方科目；传入 null 表示清除。</summary>
        public void SetVoucherSubject(Guid? subjectId)
        {
            VoucherSubjectId = subjectId is null || subjectId == Guid.Empty ? null : subjectId;
        }

        /// <summary>解析后的关键字集合（按逗号/顿号/空格拆分，去空去重）。</summary>
        public IReadOnlyList<string> KeywordList()
        {
            if (string.IsNullOrWhiteSpace(SmartKeywords))
            {
                return [];
            }

            return SmartKeywords
                .Split([',', '，', '、', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
        }

        private static void GuardName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("类别名称不能为空。");
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"类别名称不能超过 {MaxNameLength} 个字符。", nameof(name));
            }
        }
    }
}
