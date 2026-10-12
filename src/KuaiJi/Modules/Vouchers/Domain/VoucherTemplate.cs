namespace KuaiJi.Modules.Vouchers.Domain
{
    /// <summary>
    /// 凭证模板：预先定义好的分录骨架（摘要 + 科目 + 借贷方向），录凭证时可整体套用。
    /// 模板行金额留空（按当次业务填写），保存时校验借贷合计为零元整口径由前端展示。
    /// </summary>
    public class VoucherTemplate
    {
        /// <summary>模板名称最大长度。</summary>
        public const int MaxNameLength = 100;

        /// <summary>模板行摘要最大长度。</summary>
        public const int MaxSummaryLength = 200;

        private readonly List<VoucherTemplateLine> _lines = new();

        private VoucherTemplate()
        {
        }

        private VoucherTemplate(Guid id, Guid accountSetId, string name, VoucherTemplateType type)
        {
            Id = id;
            AccountSetId = accountSetId;
            Name = name;
            Type = type;
        }

        /// <summary>模板标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>模板名称，同一账套内唯一。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>模板类型（日常支出 / 采购 / 销售 / 其他）。</summary>
        public VoucherTemplateType Type { get; private set; }

        /// <summary>模板行集合（按行号顺序）。</summary>
        public IReadOnlyList<VoucherTemplateLine> Lines => _lines;

        /// <summary>新建凭证模板，须归属于某一账套。</summary>
        public static VoucherTemplate Create(Guid accountSetId, string name, VoucherTemplateType type)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。", nameof(accountSetId));
            }

            GuardName(name);
            if (!Enum.IsDefined(type))
            {
                throw new ArgumentException("模板类型无效。", nameof(type));
            }

            return new VoucherTemplate(Guid.NewGuid(), accountSetId, name.Trim(), type);
        }

        /// <summary>重命名并调整模板类型。</summary>
        public void Update(string name, VoucherTemplateType type)
        {
            GuardName(name);
            if (!Enum.IsDefined(type))
            {
                throw new ArgumentException("模板类型无效。", nameof(type));
            }

            Name = name.Trim();
            Type = type;
        }

        /// <summary>整体替换模板行（行号按顺序重排）。行摘要必填、科目必选、借贷方向二选一。</summary>
        public void ReplaceLines(IReadOnlyList<(string summary, Guid accountId, bool isDebit)> lines)
        {
            var result = new List<VoucherTemplateLine>();
            for (var i = 0; i < lines.Count; i++)
            {
                var (summary, accountId, isDebit) = lines[i];
                if (string.IsNullOrWhiteSpace(summary))
                {
                    throw new ArgumentException($"第 {i + 1} 行模板摘要不能为空。");
                }

                if (accountId == Guid.Empty)
                {
                    throw new ArgumentException($"第 {i + 1} 行模板必须选择会计科目。");
                }

                result.Add(new VoucherTemplateLine(
                    Guid.NewGuid(),
                    Id,
                    i + 1,
                    summary.Trim(),
                    accountId,
                    isDebit));
            }

            if (result.Count == 0)
            {
                throw new ArgumentException("模板至少要有一行分录。");
            }

            _lines.Clear();
            _lines.AddRange(result);
        }

        private static void GuardName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("模板名称不能为空。", nameof(name));
            }

            if (name.Trim().Length > MaxNameLength)
            {
                throw new ArgumentException($"模板名称不能超过 {MaxNameLength} 个字符。", nameof(name));
            }
        }
    }

    /// <summary>凭证模板类型。</summary>
    public enum VoucherTemplateType
    {
        /// <summary>日常支出。</summary>
        DailyExpense = 1,

        /// <summary>采购。</summary>
        Purchase = 2,

        /// <summary>销售。</summary>
        Sales = 3,

        /// <summary>其他。</summary>
        Other = 4,
    }

    /// <summary>凭证模板行。</summary>
    public class VoucherTemplateLine
    {
        private VoucherTemplateLine()
        {
        }

        internal VoucherTemplateLine(Guid id, Guid templateId, int lineNumber, string summary, Guid accountId, bool isDebit)
        {
            Id = id;
            TemplateId = templateId;
            LineNumber = lineNumber;
            Summary = summary;
            AccountId = accountId;
            IsDebit = isDebit;
        }

        /// <summary>模板行标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属模板标识。</summary>
        public Guid TemplateId { get; private set; }

        /// <summary>行号（从 1 开始）。</summary>
        public int LineNumber { get; private set; }

        /// <summary>摘要。</summary>
        public string Summary { get; private set; } = null!;

        /// <summary>会计科目标识。</summary>
        public Guid AccountId { get; private set; }

        /// <summary>是否借方（否则贷方）。模板行金额留空按次填写。</summary>
        public bool IsDebit { get; private set; }
    }
}
