namespace YunCai.Modules.Budgets.Domain
{
    /// <summary>
    /// 预算单：按账套 + 年度维护的科目预算，一张预算单下每个科目一行、含 12 个月金额。
    /// 执行数不落库，由已过账凭证按月实时汇总（见 <see cref="IBudgetRepository.GetActualsAsync"/>），
    /// 保证预算对比始终跟账簿一致。
    /// </summary>
    public class Budget
    {
        /// <summary>预算名称最大长度。</summary>
        public const int MaxNameLength = 100;

        /// <summary>备注最大长度。</summary>
        public const int MaxNoteLength = 200;

        /// <summary>允许的年度范围。</summary>
        public const int MinYear = 2000;
        public const int MaxYear = 2100;

        private readonly List<BudgetLine> _lines = new();

        private Budget()
        {
        }

        private Budget(Guid id, Guid accountSetId, int year, string name, string? note, string createdBy)
        {
            Id = id;
            AccountSetId = accountSetId;
            Year = year;
            Name = name;
            Note = note;
            CreatedBy = createdBy;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        public Guid Id { get; private set; }

        public Guid AccountSetId { get; private set; }

        /// <summary>预算年度（自然年，yyyy）。</summary>
        public int Year { get; private set; }

        public string Name { get; private set; } = null!;

        public string? Note { get; private set; }

        public string CreatedBy { get; private set; } = null!;

        public DateTime CreatedAt { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        public IReadOnlyList<BudgetLine> Lines => _lines;

        public static Budget Create(Guid accountSetId, int year, string name, string? note, string createdBy)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new BudgetException("必须指定所属账套。");
            }

            var budget = new Budget(Guid.NewGuid(), accountSetId, ValidateYear(year), ValidateName(name),
                TrimNote(note), createdBy?.Trim() ?? string.Empty);
            return budget;
        }

        public void Update(string name, string? note)
        {
            Name = ValidateName(name);
            Note = TrimNote(note);
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>整体覆盖预算行：同一科目只保留一行，金额必须非负。</summary>
        public void ReplaceLines(IEnumerable<(Guid AccountId, IReadOnlyList<decimal> Months)> lines)
        {
            var normalized = NormalizeLines(lines);
            _lines.Clear();
            foreach (var (accountId, months) in normalized)
            {
                _lines.Add(BudgetLine.Create(Id, AccountSetId, accountId, months));
            }

            UpdatedAt = DateTime.UtcNow;
        }

        private static int ValidateYear(int year)
        {
            if (year < MinYear || year > MaxYear)
            {
                throw new BudgetException($"预算年度需在 {MinYear} ~ {MaxYear} 之间。");
            }

            return year;
        }

        private static string ValidateName(string? name)
        {
            var text = (name ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                throw new BudgetException("请填写预算名称。");
            }

            return text.Length > MaxNameLength ? text[..MaxNameLength] : text;
        }

        private static string? TrimNote(string? note)
        {
            var text = (note ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return null;
            }

            return text.Length > MaxNoteLength ? text[..MaxNoteLength] : text;
        }

        private static List<(Guid AccountId, decimal[] Months)> NormalizeLines(
            IEnumerable<(Guid AccountId, IReadOnlyList<decimal> Months)> lines)
        {
            var result = new List<(Guid, decimal[])>();
            var seen = new HashSet<Guid>();
            foreach (var (accountId, months) in lines)
            {
                if (accountId == Guid.Empty)
                {
                    throw new BudgetException("预算行必须选择科目。");
                }

                if (!seen.Add(accountId))
                {
                    throw new BudgetException("同一科目只能维护一行预算。");
                }

                if (months.Count != BudgetLine.MonthCount)
                {
                    throw new BudgetException("预算金额必须按月提供 12 个数。");
                }

                var copy = new decimal[BudgetLine.MonthCount];
                for (var i = 0; i < copy.Length; i++)
                {
                    var amount = months[i];
                    if (amount < 0)
                    {
                        throw new BudgetException("预算金额不能为负数。");
                    }

                    if (amount > BudgetLine.MaxAmount)
                    {
                        throw new BudgetException($"单个预算金额不能超过 {BudgetLine.MaxAmount:N2} 元。");
                    }

                    copy[i] = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
                }

                result.Add((accountId, copy));
            }

            return result;
        }
    }

    /// <summary>预算业务校验失败（控制器转 400 中文提示）。</summary>
    public sealed class BudgetException : Exception
    {
        public BudgetException(string message) : base(message)
        {
        }
    }
}
