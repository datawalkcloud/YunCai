using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Journals;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Journals
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

        public JournalService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IJournalRepository journalRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _journalRepository = journalRepository;
            _voucherRepository = voucherRepository;
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
        /// 建账：校验对应科目为货币资金末级科目、且账套内该科目尚未建账后，创建一本日记账。
        /// </summary>
        public async Task<JournalDetailDto> CreateAsync(Guid accountSetId, CreateJournalRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            if (!Enum.IsDefined(typeof(JournalType), request.Type))
            {
                throw new JournalException("日记账类型无效。");
            }

            var account = await _accountRepository.GetByIdAsync(accountSetId, request.AccountId, cancellationToken)
                          ?? throw new JournalException("指定的会计科目不存在。");

            if (!account.IsLeaf)
            {
                throw new JournalException($"科目「{account.Code} {account.Name}」不是末级科目，不能建立日记账。");
            }

            if (!CashCodePrefixes.Any(p => account.Code.StartsWith(p, StringComparison.Ordinal)))
            {
                throw new JournalException("日记账只能对应货币资金科目（库存现金 1001 / 银行存款 1002）。");
            }

            if (await _journalRepository.GetByAccountIdAsync(accountSetId, request.AccountId, cancellationToken) is not null)
            {
                throw new JournalException($"科目「{account.Code} {account.Name}」已建立日记账，不能重复建账。");
            }

            Journal journal;
            try
            {
                journal = Journal.Create(accountSetId, (JournalType)request.Type, request.Name, request.AccountId, request.OpeningBalance);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _journalRepository.AddAsync(journal, cancellationToken);
            await _journalRepository.SaveChangesAsync(cancellationToken);

            return await BuildDetail(accountSetId, journal, cancellationToken);
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
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

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
