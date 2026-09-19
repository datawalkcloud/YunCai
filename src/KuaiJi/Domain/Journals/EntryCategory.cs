namespace KuaiJi.Domain.Journals
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
