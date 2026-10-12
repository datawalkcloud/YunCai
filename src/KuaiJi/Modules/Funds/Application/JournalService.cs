using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Funds.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>日记账业务操作中的可预期错误（如账套/科目无效、重复建账），用于向前端返回友好提示。</summary>
    public sealed class JournalException : Exception
    {
        public JournalException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 日记账应用服务：面向「日记账管理」页的列表、详情、建账、改账、删除、分录增删改，
    /// 以及由分录生成凭证。所有日记账均归属于指定账套，无法脱离账套独立存在。
    /// </summary>
    public sealed class JournalService
    {
        /// <summary>货币资金末级科目编码前缀（库存现金 1001 / 银行存款 1002）。</summary>
        private static readonly string[] CashCodePrefixes = ["1001", "1002"];

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IJournalRepository _journalRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IEntryCategoryRepository _categoryRepository;

        public JournalService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IJournalRepository journalRepository,
            IVoucherRepository voucherRepository,
            IEntryCategoryRepository categoryRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _journalRepository = journalRepository;
            _voucherRepository = voucherRepository;
            _categoryRepository = categoryRepository;
        }

        /// <summary>获取指定账套的全部日记账（含收支合计与期初/期末余额，按名称排列）。</summary>
        public async Task<IReadOnlyList<JournalListItemDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var journals = await _journalRepository.GetAllAsync(accountSetId, cancellationToken);
            return journals
                .Select(j => JournalListItemDto.FromDomain(j, accountsById.GetValueOrDefault(j.AccountId)))
                .ToList();
        }

        /// <summary>获取单本日记账详情（含分录及关联凭证号）；不存在时返回 null。</summary>
        public async Task<JournalDetailDto?> GetAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (journal is null)
            {
                return null;
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var vouchersById = await LoadVouchersById(accountSetId, cancellationToken);
            return JournalDetailDto.FromDomain(journal, accountsById.GetValueOrDefault(journal.AccountId), vouchersById);
        }

        /// <summary>
        /// 建账：指定科目时校验其为货币资金末级科目且尚未建账；
        /// 省略科目时按类型自动在 1001 / 1002 下创建与账簿同名的明细科目并绑定。
        /// </summary>
        public async Task<JournalDetailDto> CreateAsync(Guid accountSetId, CreateJournalRequest request, CancellationToken cancellationToken = default)
        {
            var accountSet = accountSetId == Guid.Empty
                ? null
                : await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                throw new JournalException("指定的账套不存在。");
            }

            if (!Enum.IsDefined(typeof(JournalType), request.Type))
            {
                throw new JournalException("日记账类型无效。");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new JournalException("账簿名称不能为空。");
            }

            var type = (JournalType)request.Type;

            Account account;
            if (request.AccountId is { } accountId)
            {
                account = await _accountRepository.GetByIdAsync(accountSetId, accountId, cancellationToken)
                          ?? throw new JournalException("指定的会计科目不存在。");

                if (!account.IsLeaf)
                {
                    throw new JournalException($"科目「{account.Code} {account.Name}」不是末级科目，不能建立日记账。");
                }

                if (!CashCodePrefixes.Any(p => account.Code.StartsWith(p, StringComparison.Ordinal)))
                {
                    throw new JournalException("日记账只能对应货币资金科目（库存现金 1001 / 银行存款 1002）。");
                }

                if (await _journalRepository.GetByAccountIdAsync(accountSetId, accountId, cancellationToken) is not null)
                {
                    throw new JournalException($"科目「{account.Code} {account.Name}」已建立日记账，不能重复建账。");
                }
            }
            else
            {
                account = await EnsureMoneyAccountAsync(accountSet, type, request.Name.Trim(), cancellationToken);
            }

            Journal journal;
            try
            {
                journal = Journal.Create(accountSetId, type, request.Name, account.Id, request.OpeningBalance);
                journal.SetBankInfo(request.BankName, request.BankAccountNo);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _journalRepository.AddAsync(journal, cancellationToken);
            // 与自动创建的明细科目共享同一工作单元，一次提交保证原子性。
            await _journalRepository.SaveChangesAsync(cancellationToken);

            return await BuildDetail(accountSetId, journal, cancellationToken);
        }

        /// <summary>
        /// 确保账簿对应的货币资金明细科目存在：同名且未建账的明细科目直接复用；
        /// 否则按编码方案在上级科目（现金 1001 / 银行存款 1002）下顺序取号新建明细科目。
        /// </summary>
        private async Task<Account> EnsureMoneyAccountAsync(AccountSet accountSet, JournalType type, string name, CancellationToken cancellationToken)
        {
            var parentCode = type == JournalType.Cash ? "1001" : "1002";
            var parent = await _accountRepository.GetByCodeAsync(accountSet.Id, parentCode, cancellationToken)
                         ?? throw new JournalException($"账套中不存在科目「{parentCode}」，无法自动创建明细科目，请先在科目管理中补建。");

            var siblings = await _accountRepository.GetChildrenAsync(accountSet.Id, parent.Id, cancellationToken);

            // 同名明细科目：未建账则复用，已建账则视为重复账户。
            var sameName = siblings.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));
            if (sameName is not null)
            {
                if (!sameName.IsLeaf)
                {
                    throw new JournalException($"科目「{sameName.Code} {sameName.Name}」不是末级科目，不能建立日记账。");
                }

                if (await _journalRepository.GetByAccountIdAsync(accountSet.Id, sameName.Id, cancellationToken) is not null)
                {
                    throw new JournalException($"账户「{name}」已存在（科目 {sameName.Code}），请换一个名称。");
                }

                return sameName;
            }

            // 按编码方案确定明细科目编码长度，并在兄弟科目中顺序取下一个可用号。
            var scheme = AccountSet.ParseCodingScheme(accountSet.AccountCodingScheme);
            var childLevel = parent.Level + 1;
            if (childLevel > scheme.Length)
            {
                throw new JournalException($"科目编码方案「{accountSet.AccountCodingScheme}」不支持在「{parent.Code} {parent.Name}」下创建明细科目。");
            }

            var suffixLength = scheme[childLevel - 1];
            var maxSuffix = siblings
                .Select(a => a.Code.Length > parent.Code.Length && int.TryParse(a.Code[parent.Code.Length..], out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();
            var next = maxSuffix + 1;
            if (next >= Math.Pow(10, suffixLength))
            {
                throw new JournalException($"科目「{parent.Code} {parent.Name}」下的明细科目编号已用尽，无法再创建新账户。");
            }

            var code = parent.Code + next.ToString($"D{suffixLength}");

            Account child;
            try
            {
                child = parent.AddChild(code, name);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _accountRepository.AddAsync(child, cancellationToken);
            return child;
        }

        /// <summary>改账：修改账簿名称与期初余额；不存在时返回 null。</summary>
        public async Task<JournalDetailDto?> UpdateAsync(Guid accountSetId, Guid id, UpdateJournalRequest request, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (journal is null)
            {
                return null;
            }

            try
            {
                journal.Rename(request.Name);
                journal.ChangeOpeningBalance(request.OpeningBalance);
                if (request.IsEnabled is { } enabled)
                {
                    journal.SetEnabled(enabled);
                }

                // null = 保持原值；空串 = 清除（SetBankInfo 会把空白规整为 null）。
                if (request.BankName is not null || request.BankAccountNo is not null)
                {
                    journal.SetBankInfo(
                        request.BankName ?? journal.BankName,
                        request.BankAccountNo ?? journal.BankAccountNo);
                }
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _journalRepository.SaveChangesAsync(cancellationToken);
            return await BuildDetail(accountSetId, journal, cancellationToken);
        }

        /// <summary>启用/停用账户；不存在时返回 null。</summary>
        public async Task<JournalDetailDto?> SetEnabledAsync(Guid accountSetId, Guid id, bool enabled, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (journal is null)
            {
                return null;
            }

            journal.SetEnabled(enabled);
            await _journalRepository.SaveChangesAsync(cancellationToken);
            return await BuildDetail(accountSetId, journal, cancellationToken);
        }

        /// <summary>删除日记账（含其全部分录）；不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (journal is null)
            {
                return false;
            }

            _journalRepository.Remove(journal);
            await _journalRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>新增一笔分录；日记账不存在时返回 null。</summary>
        public async Task<JournalDetailDto?> AddEntryAsync(Guid accountSetId, Guid id, JournalEntryRequest request, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (journal is null)
            {
                return null;
            }

            EnsureJournalEnabled(journal);

            try
            {
                journal.AddEntry(request.Date, request.Summary, request.Income, request.Expense, request.Category, request.Counterparty);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _journalRepository.SaveChangesAsync(cancellationToken);
            return await BuildDetail(accountSetId, journal, cancellationToken);
        }

        /// <summary>修改一笔分录；日记账不存在时返回 null。保留分录原有的关联凭证。</summary>
        public async Task<JournalDetailDto?> UpdateEntryAsync(Guid accountSetId, Guid id, Guid entryId, JournalEntryRequest request, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (journal is null)
            {
                return null;
            }

            var existing = journal.Entries.FirstOrDefault(e => e.Id == entryId)
                           ?? throw new JournalException("日记账分录不存在。");

            try
            {
                journal.UpdateEntry(entryId, request.Date, request.Summary, request.Income, request.Expense, request.Category, request.Counterparty, existing.VoucherId);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _journalRepository.SaveChangesAsync(cancellationToken);
            return await BuildDetail(accountSetId, journal, cancellationToken);
        }

        /// <summary>删除一笔分录；日记账不存在时返回 null。</summary>
        public async Task<JournalDetailDto?> RemoveEntryAsync(Guid accountSetId, Guid id, Guid entryId, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (journal is null)
            {
                return null;
            }

            journal.RemoveEntry(entryId);
            await _journalRepository.SaveChangesAsync(cancellationToken);
            return await BuildDetail(accountSetId, journal, cancellationToken);
        }

        /// <summary>
        /// 由指定分录生成一张凭证：收入按「借 货币资金 / 贷 对方科目」、支出按「借 对方科目 / 贷 货币资金」
        /// 组装一借一贷分录，保存后回写分录的关联凭证。返回生成的凭证标识。
        /// </summary>
        public async Task<Guid> GenerateVoucherAsync(Guid accountSetId, Guid id, Guid entryId, string preparer, GenerateVoucherRequest request, CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                          ?? throw new JournalException("指定的日记账不存在。");

            var entry = journal.Entries.FirstOrDefault(e => e.Id == entryId)
                        ?? throw new JournalException("日记账分录不存在。");

            if (entry.VoucherId is not null)
            {
                throw new JournalException("该分录已生成凭证，请勿重复生成。");
            }

            var amount = entry.Income - entry.Expense;
            if (amount == 0m)
            {
                throw new JournalException("该分录收支金额为零，无法生成凭证。");
            }

            var moneyAccount = await _accountRepository.GetByIdAsync(accountSetId, journal.AccountId, cancellationToken)
                               ?? throw new JournalException("日记账对应的货币资金科目不存在。");

            var counter = await _accountRepository.GetByIdAsync(accountSetId, request.CounterAccountId, cancellationToken)
                          ?? throw new JournalException("指定的对方科目不存在。");

            if (!counter.IsLeaf)
            {
                throw new JournalException($"对方科目「{counter.Code} {counter.Name}」不是末级科目，不能用于记账。");
            }

            if (!counter.IsEnabled)
            {
                throw new JournalException($"对方科目「{counter.Code} {counter.Name}」已停用，不能用于记账。");
            }

            var word = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim();
            var number = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;

            Voucher voucher;
            try
            {
                voucher = Voucher.Create(accountSetId, word, number, entry.Date, preparer);

                if (entry.Income > 0m)
                {
                    // 收入：借 货币资金 / 贷 对方科目。
                    voucher.AddEntry(entry.Summary, moneyAccount.Id, entry.Income, 0m);
                    voucher.AddEntry(entry.Summary, counter.Id, 0m, entry.Income);
                }
                else
                {
                    // 支出：借 对方科目 / 贷 货币资金。
                    voucher.AddEntry(entry.Summary, counter.Id, entry.Expense, 0m);
                    voucher.AddEntry(entry.Summary, moneyAccount.Id, 0m, entry.Expense);
                }
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _voucherRepository.AddAsync(voucher, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            // 回写分录的关联凭证（保留分录其余字段不变）。
            journal.UpdateEntry(entry.Id, entry.Date, entry.Summary, entry.Income, entry.Expense, entry.Category, entry.Counterparty, voucher.Id);
            await _journalRepository.SaveChangesAsync(cancellationToken);

            return voucher.Id;
        }

        /// <summary>
        /// 由多笔流水批量生成凭证：按合并方式分组（全部 / 日期 / 收支类别 / 收支方向），
        /// 同一张凭证内相同科目与方向的行自动汇总；对方科目优先取统一科目，其次按流水的
        /// 收支类别取「默认对方科目」。任何一笔无法确定对方科目都整单拒绝（不做部分生成）。
        /// </summary>
        public async Task<GenerateVouchersBatchResultDto> GenerateVouchersBatchAsync(
            Guid accountSetId,
            Guid id,
            string preparer,
            GenerateVouchersBatchRequest request,
            CancellationToken cancellationToken = default)
        {
            var journal = await _journalRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                          ?? throw new JournalException("指定的日记账不存在。");

            if (request.EntryIds is null || request.EntryIds.Count == 0)
            {
                throw new JournalException("请先勾选要生成凭证的流水。");
            }

            if (request.MergeBy is < 1 or > 4)
            {
                throw new JournalException("合并方式不合法。");
            }

            var selected = new List<JournalEntry>();
            foreach (var entryId in request.EntryIds.Distinct())
            {
                var entry = journal.Entries.FirstOrDefault(e => e.Id == entryId)
                            ?? throw new JournalException("存在无效的流水记录，请刷新后重试。");
                if (entry.VoucherId is not null)
                {
                    throw new JournalException($"第 {entry.Sequence} 笔流水已生成凭证，请勿重复生成。");
                }

                if (entry.Income == 0m && entry.Expense == 0m)
                {
                    throw new JournalException($"第 {entry.Sequence} 笔流水收支金额为零，无法生成凭证。");
                }

                if (entry.Income > 0m && entry.Expense > 0m)
                {
                    throw new JournalException($"第 {entry.Sequence} 笔流水同时有收入与支出，无法生成凭证。");
                }

                selected.Add(entry);
            }

            var moneyAccount = await _accountRepository.GetByIdAsync(accountSetId, journal.AccountId, cancellationToken)
                               ?? throw new JournalException("日记账对应的货币资金科目不存在。");

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var uniformCounter = request.CounterAccountId is { } uid && uid != Guid.Empty ? uid : (Guid?)null;
            if (uniformCounter is { } uniformId)
            {
                ValidateCounterAccount(accountsById, uniformId);
            }

            var categories = await _categoryRepository.GetAllAsync(accountSetId, cancellationToken);
            var counterByEntry = new Dictionary<Guid, Guid>();
            var missing = new List<string>();
            foreach (var entry in selected)
            {
                if (uniformCounter is { } resolved)
                {
                    counterByEntry[entry.Id] = resolved;
                    continue;
                }

                var subjectId = ResolveCategorySubject(categories, entry);
                if (subjectId is { } matched && accountsById.ContainsKey(matched))
                {
                    counterByEntry[entry.Id] = matched;
                }
                else
                {
                    missing.Add($"第 {entry.Sequence} 笔（{entry.Summary}）");
                }
            }

            if (missing.Count > 0)
            {
                var detail = string.Join("、", missing.Take(5)) + (missing.Count > 5 ? $" 等 {missing.Count} 笔" : string.Empty);
                throw new JournalException($"以下流水未能确定对方科目（可选择统一对方科目，或在「收支类别」中配置默认对方科目）：{detail}");
            }

            var groups = request.MergeBy switch
            {
                2 => selected.GroupBy(e => e.Date).OrderBy(g => g.Key).Select(g => g.ToList()).ToList(),
                3 => selected.GroupBy(e => e.Category ?? "未分类", StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.ToList()).ToList(),
                4 => selected.GroupBy(e => e.Income > 0m ? 0 : 1).OrderBy(g => g.Key).Select(g => g.ToList()).ToList(),
                _ => new List<List<JournalEntry>> { selected },
            };

            var finalWord = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim();
            var nextNumber = await _voucherRepository.GetMaxNumberAsync(accountSetId, finalWord, cancellationToken) + 1;

            var created = new List<(Voucher Voucher, List<JournalEntry> Entries)>();
            foreach (var group in groups)
            {
                var voucher = BuildBatchVoucher(accountSetId, finalWord, nextNumber++, preparer, moneyAccount, group, counterByEntry);
                created.Add((voucher, group));
            }

            foreach (var (voucher, _) in created)
            {
                await _voucherRepository.AddAsync(voucher, cancellationToken);
            }

            await _voucherRepository.SaveChangesAsync(cancellationToken);

            // 回写各流水的关联凭证。
            foreach (var (voucher, entries) in created)
            {
                foreach (var entry in entries)
                {
                    journal.UpdateEntry(entry.Id, entry.Date, entry.Summary, entry.Income, entry.Expense, entry.Category, entry.Counterparty, voucher.Id);
                }
            }

            await _journalRepository.SaveChangesAsync(cancellationToken);

            return new GenerateVouchersBatchResultDto
            {
                EntryCount = selected.Count,
                Vouchers = created
                    .Select(x => new BatchVoucherRefDto
                    {
                        Id = x.Voucher.Id,
                        Label = $"{x.Voucher.Word}-{x.Voucher.Number}",
                        EntryCount = x.Entries.Count,
                    })
                    .ToList(),
            };
        }

        /// <summary>校验统一对方科目：末级且启用。</summary>
        private static void ValidateCounterAccount(IReadOnlyDictionary<Guid, Account> accountsById, Guid accountId)
        {
            if (!accountsById.TryGetValue(accountId, out var account))
            {
                throw new JournalException("统一对方科目不存在。");
            }

            if (!account.IsLeaf)
            {
                throw new JournalException($"对方科目「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
            }

            if (!account.IsEnabled)
            {
                throw new JournalException($"对方科目「{account.Code} {account.Name}」已停用，不能用于记账。");
            }
        }

        /// <summary>按流水的收支类别（名称 + 方向）查找「默认对方科目」。</summary>
        private static Guid? ResolveCategorySubject(IReadOnlyList<EntryCategory> categories, JournalEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Category))
            {
                return null;
            }

            var expectedType = entry.Income > 0m ? EntryCategoryType.Income : EntryCategoryType.Expense;
            var matched = categories.FirstOrDefault(c =>
                c.Type == expectedType && string.Equals(c.Name, entry.Category, StringComparison.Ordinal));
            return matched?.VoucherSubjectId;
        }

        /// <summary>
        /// 把一组流水组装为一张凭证：收入 借货币/贷对方、支出 借对方/贷货币；
        /// 相同（科目, 方向）的行汇总为一行，摘要取首笔、多笔时追加「等 N 笔」。
        /// </summary>
        private static Voucher BuildBatchVoucher(
            Guid accountSetId,
            string word,
            int number,
            string preparer,
            Account moneyAccount,
            IReadOnlyList<JournalEntry> entries,
            IReadOnlyDictionary<Guid, Guid> counterByEntry)
        {
            var date = entries.Min(e => e.Date);
            Voucher voucher;
            try
            {
                voucher = Voucher.Create(accountSetId, word, number, date, preparer);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            var lines = new Dictionary<(Guid AccountId, bool IsDebit), (decimal Amount, List<string> Summaries)>();
            void Accumulate(Guid accountId, bool isDebit, decimal amount, string summary)
            {
                var key = (accountId, isDebit);
                var current = lines.GetValueOrDefault(key);
                var summaries = current.Summaries ?? [];
                summaries.Add(summary);
                lines[key] = (current.Amount + amount, summaries);
            }

            foreach (var entry in entries)
            {
                var counterId = counterByEntry[entry.Id];
                if (entry.Income > 0m)
                {
                    Accumulate(moneyAccount.Id, true, entry.Income, entry.Summary);
                    Accumulate(counterId, false, entry.Income, entry.Summary);
                }
                else
                {
                    Accumulate(counterId, true, entry.Expense, entry.Summary);
                    Accumulate(moneyAccount.Id, false, entry.Expense, entry.Summary);
                }
            }

            try
            {
                foreach (var pair in lines)
                {
                    var summary = Summarize(pair.Value.Summaries);
                    if (pair.Key.IsDebit)
                    {
                        voucher.AddEntry(summary, pair.Key.AccountId, pair.Value.Amount, 0m);
                    }
                    else
                    {
                        voucher.AddEntry(summary, pair.Key.AccountId, 0m, pair.Value.Amount);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            return voucher;
        }

        private static string Summarize(List<string> summaries)
        {
            var first = summaries.FirstOrDefault() ?? string.Empty;
            return summaries.Count > 1 ? $"{first} 等 {summaries.Count} 笔" : first;
        }

        private async Task<JournalDetailDto> BuildDetail(Guid accountSetId, Journal journal, CancellationToken cancellationToken)
        {
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var vouchersById = await LoadVouchersById(accountSetId, cancellationToken);
            return JournalDetailDto.FromDomain(journal, accountsById.GetValueOrDefault(journal.AccountId), vouchersById);
        }

        private async Task EnsureAccountSetExists(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new JournalException("指定的账套不存在。");
            }
        }

        /// <summary>停用的账户不允许再登记新流水；历史数据保留、报表照常参与。</summary>
        internal static void EnsureJournalEnabled(Journal journal)
        {
            if (!journal.IsEnabled)
            {
                throw new JournalException($"账户「{journal.Name}」已停用，请先启用后再登记流水。");
            }
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsById(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }

        private async Task<IReadOnlyDictionary<Guid, Voucher>> LoadVouchersById(Guid accountSetId, CancellationToken cancellationToken)
        {
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            return vouchers.ToDictionary(v => v.Id, v => v);
        }
    }
}
