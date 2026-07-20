namespace KuaiJi.Domain.Journals
{
    /// <summary>
    /// 日记账（序时账簿）聚合根。对应一个货币资金科目（库存现金 / 银行存款），
    /// 含期初余额与一组按日期顺序登记的分录。分录的序号与逐行余额由账簿统一重算，
    /// 保证账面始终连续、平衡。
    /// </summary>
    public class Journal
    {
        private readonly List<JournalEntry> _entries = new();

        // EF Core 物化实体所需的无参构造函数。
        private Journal()
        {
        }

        private Journal(Guid id, Guid accountSetId, JournalType type, string name, Guid accountId, decimal openingBalance)
        {
            Id = id;
            AccountSetId = accountSetId;
            Type = type;
            Name = name;
            AccountId = accountId;
            OpeningBalance = openingBalance;
        }

        /// <summary>日记账标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。日记账不能脱离账套独立存在。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>日记账类型（现金 / 银行存款）。</summary>
        public JournalType Type { get; private set; }

        /// <summary>账簿名称，如“现金日记账”“工行存款日记账”。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>对应的会计科目标识（库存现金 / 银行存款科目）。</summary>
        public Guid AccountId { get; private set; }

        /// <summary>期初余额。</summary>
        public decimal OpeningBalance { get; private set; }

        /// <summary>账簿分录集合（按日期顺序）。</summary>
        public IReadOnlyList<JournalEntry> Entries => _entries;

        /// <summary>本期收入合计（借方）。</summary>
        public decimal TotalIncome => _entries.Sum(e => e.Income);

        /// <summary>本期支出合计（贷方）。</summary>
        public decimal TotalExpense => _entries.Sum(e => e.Expense);

        /// <summary>期末余额 = 期初余额 + 本期收入 − 本期支出。</summary>
        public decimal ClosingBalance => OpeningBalance + TotalIncome - TotalExpense;

        /// <summary>创建一本日记账，须归属于某一账套。</summary>
        public static Journal Create(Guid accountSetId, JournalType type, string name, Guid accountId, decimal openingBalance = 0m)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。", nameof(accountSetId));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("账簿名称不能为空。", nameof(name));
            }

            if (accountId == Guid.Empty)
            {
                throw new ArgumentException("必须指定对应的会计科目。", nameof(accountId));
            }

            return new Journal(Guid.NewGuid(), accountSetId, type, name.Trim(), accountId, openingBalance);
        }

        /// <summary>登记一笔流水，登记后自动重排序号与余额。</summary>
        public JournalEntry AddEntry(
            DateOnly date,
            string summary,
            decimal income,
            decimal expense,
            string? category = null,
            string? counterparty = null,
            Guid? voucherId = null)
        {
            GuardEntry(summary, income, expense);

            var entry = new JournalEntry(
                Guid.NewGuid(),
                Id,
                date,
                summary.Trim(),
                Normalize(category),
                Normalize(counterparty),
                income,
                expense,
                voucherId);

            _entries.Add(entry);
            Reindex();
            return entry;
        }

        /// <summary>修改一笔流水，修改后自动重排序号与余额。</summary>
        public void UpdateEntry(
            Guid entryId,
            DateOnly date,
            string summary,
            decimal income,
            decimal expense,
            string? category = null,
            string? counterparty = null,
            Guid? voucherId = null)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId)
                        ?? throw new InvalidOperationException("日记账分录不存在。");

            GuardEntry(summary, income, expense);
            entry.Update(date, summary.Trim(), Normalize(category), Normalize(counterparty), income, expense, voucherId);
            Reindex();
        }

        /// <summary>删除一笔流水，删除后自动重排序号与余额。</summary>
        public void RemoveEntry(Guid entryId)
        {
            if (_entries.RemoveAll(e => e.Id == entryId) > 0)
            {
                Reindex();
            }
        }

        /// <summary>调整期初余额，随后重算逐行余额。</summary>
        public void ChangeOpeningBalance(decimal openingBalance)
        {
            OpeningBalance = openingBalance;
            Reindex();
        }

        /// <summary>重命名账簿。</summary>
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("账簿名称不能为空。", nameof(name));
            }

            Name = name.Trim();
        }

        // 按日期稳定排序（同日期保持录入先后），重算序号与逐行余额。
        private void Reindex()
        {
            var ordered = _entries.OrderBy(e => e.Date).ToList();
            _entries.Clear();
            _entries.AddRange(ordered);

            var balance = OpeningBalance;
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                balance += entry.Income - entry.Expense;
                entry.SetComputed(i + 1, balance);
            }
        }

        private static void GuardEntry(string summary, decimal income, decimal expense)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                throw new ArgumentException("摘要不能为空。", nameof(summary));
            }

            if (income < 0m || expense < 0m)
            {
                throw new ArgumentException("收入与支出金额不能为负数。");
            }
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
