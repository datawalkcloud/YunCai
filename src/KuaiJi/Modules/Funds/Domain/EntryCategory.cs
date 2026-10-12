namespace KuaiJi.Modules.Funds.Domain
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

        // EF Core 物化实体所需的无参构造函数。
        private EntryCategory()
        {
        }

        private EntryCategory(Guid id, Guid accountSetId, EntryCategoryType type, string name)
        {
            Id = id;
            AccountSetId = accountSetId;
            Type = type;
            Name = name;
        }

        /// <summary>类别标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。收支类别不能脱离账套独立存在。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>类别类型（收入 / 支出）。</summary>
        public EntryCategoryType Type { get; private set; }

        /// <summary>类别名称，同一账套同一类型内唯一。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>智能匹配关键字（逗号分隔）；流水导入时按摘要包含关系自动归类。</summary>
        public string? SmartKeywords { get; private set; }

        /// <summary>默认对方科目（由流水生成凭证时的默认对方科目），可为空。</summary>
        public Guid? VoucherSubjectId { get; private set; }

        /// <summary>智能匹配关键字最大长度。</summary>
        public const int MaxKeywordsLength = 200;

        /// <summary>新建收支类别，须归属于某一账套。</summary>
        public static EntryCategory Create(Guid accountSetId, EntryCategoryType type, string name)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。", nameof(accountSetId));
            }

            GuardName(name);
            return new EntryCategory(Guid.NewGuid(), accountSetId, type, name.Trim());
        }

        /// <summary>重命名类别。</summary>
        public void Rename(string name)
        {
            GuardName(name);
            Name = name.Trim();
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
                throw new ArgumentException("类别名称不能为空。", nameof(name));
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"类别名称不能超过 {MaxNameLength} 个字符。", nameof(name));
            }
        }
    }
}
