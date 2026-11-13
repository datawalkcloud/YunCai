using YunCai.Modules.Accounting.Domain;

namespace YunCai.Modules.Closing.Domain
{
    /// <summary>结转模板行的取数方式。</summary>
    public enum ClosingTemplateSourceType
    {
        /// <summary>固定金额。</summary>
        Fixed = 1,

        /// <summary>取来源科目本期借方发生额（仅已过账凭证）。</summary>
        PeriodDebit = 2,

        /// <summary>取来源科目本期贷方发生额（仅已过账凭证）。</summary>
        PeriodCredit = 3,

        /// <summary>取来源科目期末余额（借 - 贷净额的绝对值，含启用期初）。</summary>
        Balance = 4,
    }

    /// <summary>
    /// 自定义结转模板聚合根：月末结转时可复用生成一张凭证的一组分录（如计提工资、结转成本）。
    /// 每行 = 摘要 + 科目 + 借贷方向 + 取数规则（固定金额 / 取来源科目本期借方或贷方发生额 / 取来源科目期末余额）。
    /// 模板只描述「怎么记」，实际金额在执行结转或预览时按当月数据计算。
    /// </summary>
    public class ClosingTemplate
    {
        /// <summary>模板名称最大长度。</summary>
        public const int MaxNameLength = 50;

        private readonly List<ClosingTemplateLine> _lines = [];

        // EF Core 物化实体所需的无参构造函数。
        private ClosingTemplate()
        {
        }

        private ClosingTemplate(Guid id, Guid accountSetId, string name, bool isEnabled)
        {
            Id = id;
            AccountSetId = accountSetId;
            Name = name;
            IsEnabled = isEnabled;
        }

        /// <summary>模板标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。模板不能脱离账套独立存在。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>模板名称（同一账套内唯一）。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>是否启用。启用后出现在结转向导的「结转模板」列表并默认勾选；停用仅表示不再默认执行。</summary>
        public bool IsEnabled { get; private set; }

        /// <summary>模板行集合（按行号顺序）。</summary>
        public IReadOnlyList<ClosingTemplateLine> Lines => _lines;

        /// <summary>新建结转模板。</summary>
        public static ClosingTemplate Create(Guid accountSetId, string name, bool isEnabled)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。");
            }

            GuardName(name);
            return new ClosingTemplate(Guid.NewGuid(), accountSetId, name.Trim(), isEnabled);
        }

        /// <summary>重命名模板。</summary>
        public void Rename(string name)
        {
            GuardName(name);
            Name = name.Trim();
        }

        /// <summary>启用/停用模板。</summary>
        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        /// <summary>整体替换模板行（编辑时使用）：清空后按传入顺序重建并重新编号。</summary>
        public void ReplaceLines(IEnumerable<ClosingTemplateLine> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);
            _lines.Clear();
            var index = 0;
            foreach (var line in lines)
            {
                index++;
                line.Attach(Id, index);
                _lines.Add(line);
            }
        }

        private static void GuardName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("模板名称不能为空。");
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"模板名称不能超过 {MaxNameLength} 个字符。", nameof(name));
            }
        }
    }

    /// <summary>结转模板行：执行模板时将生成的一条分录。</summary>
    public class ClosingTemplateLine
    {
        /// <summary>摘要最大长度。</summary>
        public const int MaxSummaryLength = 100;

        // EF Core 物化实体所需的无参构造函数。
        private ClosingTemplateLine()
        {
        }

        private ClosingTemplateLine(
            Guid id,
            string summary,
            Guid accountId,
            BalanceDirection direction,
            ClosingTemplateSourceType sourceType,
            Guid? sourceAccountId,
            decimal amount)
        {
            Id = id;
            Summary = summary;
            AccountId = accountId;
            Direction = direction;
            SourceType = sourceType;
            SourceAccountId = sourceAccountId;
            Amount = amount;
        }

        /// <summary>行标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属模板标识。</summary>
        public Guid TemplateId { get; private set; }

        /// <summary>行号（从 1 开始，决定分录顺序）。</summary>
        public int LineNumber { get; private set; }

        /// <summary>摘要。</summary>
        public string Summary { get; private set; } = null!;

        /// <summary>记账科目标识（末级科目）。</summary>
        public Guid AccountId { get; private set; }

        /// <summary>借贷方向。</summary>
        public BalanceDirection Direction { get; private set; }

        /// <summary>取数方式。</summary>
        public ClosingTemplateSourceType SourceType { get; private set; }

        /// <summary>取数来源科目（仅非固定金额时使用）。</summary>
        public Guid? SourceAccountId { get; private set; }

        /// <summary>固定金额（仅 SourceType = Fixed 时使用）。</summary>
        public decimal Amount { get; private set; }

        /// <summary>创建模板行并校验取数配置。</summary>
        public static ClosingTemplateLine Create(
            string summary,
            Guid accountId,
            BalanceDirection direction,
            ClosingTemplateSourceType sourceType,
            Guid? sourceAccountId,
            decimal amount)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                throw new ArgumentException("摘要不能为空。");
            }

            if (summary.Trim().Length > MaxSummaryLength)
            {
                throw new ArgumentException($"摘要不能超过 {MaxSummaryLength} 个字符。", nameof(summary));
            }

            if (accountId == Guid.Empty)
            {
                throw new ArgumentException("必须选择会计科目。");
            }

            if (!Enum.IsDefined(direction))
            {
                throw new ArgumentException("借贷方向不合法。");
            }

            Guid? source = null;
            switch (sourceType)
            {
                case ClosingTemplateSourceType.Fixed:
                    if (amount <= 0m)
                    {
                        throw new ArgumentException("固定金额必须大于 0。");
                    }

                    break;

                case ClosingTemplateSourceType.PeriodDebit:
                case ClosingTemplateSourceType.PeriodCredit:
                case ClosingTemplateSourceType.Balance:
                    if (sourceAccountId is null || sourceAccountId == Guid.Empty)
                    {
                        throw new ArgumentException("取数方式需要选择来源科目。");
                    }

                    source = sourceAccountId;
                    amount = 0m;
                    break;

                default:
                    throw new ArgumentException("取数方式不合法。");
            }

            return new ClosingTemplateLine(Guid.NewGuid(), summary.Trim(), accountId, direction, sourceType, source, amount);
        }

        /// <summary>挂到模板并写入行号（由 <see cref="ClosingTemplate.ReplaceLines"/> 调用）。</summary>
        internal void Attach(Guid templateId, int lineNumber)
        {
            TemplateId = templateId;
            LineNumber = lineNumber;
        }
    }
}
