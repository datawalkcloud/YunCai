namespace KuaiJi.Modules.Funds.Domain
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

        private Journal(Guid id, Guid accountSetId, JournalType type, string name, Guid accountId, decimal openingBalance, string code, string currency)
        {
            Id = id;
            AccountSetId = accountSetId;
            Type = type;
            Name = name;
            AccountId = accountId;
            OpeningBalance = openingBalance;
            Code = code;
            Currency = currency;
        }

        /// <summary>账户编码最大长度。</summary>
        public const int MaxCodeLength = 16;

        /// <summary>默认币别。</summary>
        public const string DefaultCurrency = "人民币";

        /// <summary>日记账标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。日记账不能脱离账套独立存在。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>日记账类型（现金 / 银行存款）。</summary>
        public JournalType Type { get; private set; }

        /// <summary>账簿名称，如“现金日记账”“工行存款日记账”。</summary>
        public string Name { get; private set; } = null!;

        /// <summary>账户编码（同一账套同一类型内唯一，如 001）。</summary>
        public string Code { get; private set; } = string.Empty;

        /// <summary>币别，默认人民币。</summary>
        public string Currency { get; private set; } = DefaultCurrency;

        /// <summary>对应的会计科目标识（库存现金 / 银行存款科目）。</summary>
        public Guid AccountId { get; private set; }

        /// <summary>期初余额。</summary>
        public decimal OpeningBalance { get; private set; }

        /// <summary>是否启用。停用后不再出现在日常记账的选择列表，但保留全部流水与余额。</summary>
        public bool IsEnabled { get; private set; } = true;

        /// <summary>开户银行（仅银行存款账户使用），可为空。</summary>
        public string? BankName { get; private set; }

        /// <summary>银行账号（仅银行存款账户使用），可为空。</summary>
        public string? BankAccountNo { get; private set; }

        /// <summary>账簿分录集合（按日期顺序）。</summary>
        public IReadOnlyList<JournalEntry> Entries => _entries;

        /// <summary>本期收入合计（借方）。</summary>
        public decimal TotalIncome => _entries.Sum(e => e.Income);

        /// <summary>本期支出合计（贷方）。</summary>
        public decimal TotalExpense => _entries.Sum(e => e.Expense);

        /// <summary>期末余额 = 期初余额 + 本期收入 − 本期支出。</summary>
        public decimal ClosingBalance => OpeningBalance + TotalIncome - TotalExpense;

        /// <summary>创建一本日记账，须归属于某一账套。<paramref name="code"/> 为空时由服务层生成下一个可用编码。</summary>
        public static Journal Create(
            Guid accountSetId,
            JournalType type,
            string name,
            Guid accountId,
            decimal openingBalance = 0m,
            string? code = null,
            string? currency = null)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("账簿名称不能为空。");
            }

            if (accountId == Guid.Empty)
            {
                throw new ArgumentException("必须指定对应的会计科目。");
            }

            var journal = new Journal(
                Guid.NewGuid(),
                accountSetId,
                type,
                name.Trim(),
                accountId,
                openingBalance,
                code ?? string.Empty,
                currency ?? DefaultCurrency);

            if (!string.IsNullOrWhiteSpace(code))
            {
                journal.ChangeCode(code);
            }

            return journal;
        }

        /// <summary>登记一笔流水，登记后自动重排序号与余额。</summary>
        public JournalEntry AddEntry(
            DateOnly date,
            string summary,
            decimal income,
            decimal expense,
            string? category = null,
            string? counterparty = null,
            string? project = null,
            string? department = null,
            Guid? voucherId = null,
            string? settlementMethod = null,
            string? billNo = null,
            string? remark = null,
            string? source = null)
        {
            GuardEntry(summary, income, expense);

            var entry = new JournalEntry(
                Guid.NewGuid(),
                Id,
                date,
                summary.Trim(),
                Normalize(category),
                Normalize(counterparty),
                Normalize(project),
                Normalize(department),
                income,
                expense,
                voucherId,
                Normalize(settlementMethod),
                Normalize(billNo),
                Normalize(remark),
                Normalize(source));

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
            string? project = null,
            string? department = null,
            Guid? voucherId = null)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId)
                        ?? throw new InvalidOperationException("日记账分录不存在。");

            GuardEntry(summary, income, expense);
            entry.Update(date, summary.Trim(), Normalize(category), Normalize(counterparty), Normalize(project), Normalize(department), income, expense, voucherId);
            Reindex();
        }

        /// <summary>单独改写一笔流水的附加信息：结算方式 / 票据号 / 备注 / 流水来源。</summary>
        public void SetEntryExtras(
            Guid entryId,
            string? settlementMethod = null,
            string? billNo = null,
            string? remark = null,
            string? source = null)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == entryId)
                        ?? throw new InvalidOperationException("日记账分录不存在。");
            entry.SetExtras(Normalize(settlementMethod), Normalize(billNo), Normalize(remark), Normalize(source));
        }

        /// <summary>删除一笔流水，删除后自动重排序号与余额。</summary>
        public void RemoveEntry(Guid entryId)
        {
            if (_entries.RemoveAll(e => e.Id == entryId) > 0)
            {
                Reindex();
            }
        }

        /// <summary>
        /// 批量改写分录的摘要 / 收支类别 / 往来单位 / 项目 / 部门，或清除关联凭证（null 表示该项保持不变）。
        /// 返回实际改写的条数。金额与日期不变，因此只改写字段、不触发重排。
        /// </summary>
        public int PatchEntries(
            IReadOnlyCollection<Guid> entryIds,
            string? summary = null,
            string? category = null,
            string? counterparty = null,
            string? project = null,
            string? department = null,
            bool clearVoucher = false,
            Guid? voucherId = null)
        {
            if (entryIds.Count == 0)
            {
                return 0;
            }

            var targets = _entries.Where(e => entryIds.Contains(e.Id)).ToList();
            foreach (var entry in targets)
            {
                entry.Patch(summary, category, counterparty, project, department, clearVoucher, voucherId);
            }

            return targets.Count;
        }

        /// <summary>
        /// 清除失效的关联凭证引用：凭证被删除后流水会残留 VoucherId，
        /// 表现为账页「关联凭证」列空白且该流水无法再次生成凭证。
        /// 传入仍然存在的凭证标识集合，返回被清除的条数（幂等）。
        /// </summary>
        public int ClearStaleVouchers(IReadOnlySet<Guid> existingVoucherIds)
        {
            var changed = 0;
            foreach (var entry in _entries)
            {
                if (entry.ClearStaleVoucher(existingVoucherIds))
                {
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>批量删除分录，删除后自动重排序号与余额。返回实际删除的条数。</summary>
        public int RemoveEntries(IReadOnlyCollection<Guid> entryIds)
        {
            if (entryIds.Count == 0)
            {
                return 0;
            }

            var removed = _entries.RemoveAll(e => entryIds.Contains(e.Id));
            if (removed > 0)
            {
                Reindex();
            }

            return removed;
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
                throw new ArgumentException("账簿名称不能为空。");
            }

            Name = name.Trim();
        }

        /// <summary>启用/停用账户。</summary>
        public void SetEnabled(bool enabled) => IsEnabled = enabled;

        /// <summary>设置银行开户信息（仅银行存款账户使用）；传入空白则清除。</summary>
        public void SetBankInfo(string? bankName, string? bankAccountNo)
        {
            if (Type != JournalType.BankDeposit
                && (!string.IsNullOrWhiteSpace(bankName) || !string.IsNullOrWhiteSpace(bankAccountNo)))
            {
                throw new InvalidOperationException("现金账户不能设置银行开户信息。");
            }

            BankName = string.IsNullOrWhiteSpace(bankName) ? null : bankName.Trim();
            BankAccountNo = string.IsNullOrWhiteSpace(bankAccountNo) ? null : bankAccountNo.Trim();
        }

        /// <summary>修改账户编码（同账套同类型内唯一，由服务层保证）。</summary>
        public void ChangeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("账户编码不能为空。");
            }

            var trimmed = code.Trim();
            if (trimmed.Length > MaxCodeLength)
            {
                throw new ArgumentException($"账户编码不能超过 {MaxCodeLength} 个字符。", nameof(code));
            }

            Code = trimmed;
        }

        /// <summary>修改币别；传空保持人民币。</summary>
        public void ChangeCurrency(string? currency)
        {
            Currency = string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim();
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
                throw new ArgumentException("摘要不能为空。");
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
