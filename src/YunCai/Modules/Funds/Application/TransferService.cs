using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>
    /// 内部转账应用服务：在两个资金账户之间登记转账，创建时自动在两端日记账各登记一笔
    /// 转出 / 转入流水（本服务持有两端分录标识，删除转账时一并清理），
    /// 并可生成一张转账凭证（借 转入科目 / 贷 转出科目）。
    /// 已生成凭证的转账不允许修改或删除（凭证已登记入账）。
    /// </summary>
    public sealed class TransferService
    {
        /// <summary>两端自动流水的收支类别文本（仅作展示归类，不参与凭证组装）。</summary>
        public const string TransferCategory = "内部转账";

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IJournalRepository _journalRepository;
        private readonly ITransferRepository _transferRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public TransferService(
            IAccountSetRepository accountSetRepository,
            IJournalRepository journalRepository,
            ITransferRepository transferRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _journalRepository = journalRepository;
            _transferRepository = transferRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>获取账套内全部内部转账（按日期排列）。</summary>
        public async Task<IReadOnlyList<TransferDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);
            var transfers = await _transferRepository.GetAllAsync(accountSetId, cancellationToken);
            var journalsById = (await _journalRepository.GetAllAsync(accountSetId, cancellationToken))
                .ToDictionary(j => j.Id);
            // 只认仍在册的凭证：回收站中的凭证不算有效关联，否则列表会显示已删凭证号。
            var vouchersById = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.DeletedAt is null)
                .ToDictionary(v => v.Id);

            return transfers
                .Select(t => ToDto(t, journalsById, vouchersById))
                .ToList();
        }

        /// <summary>新建内部转账：自动登记两端流水并保存。</summary>
        public async Task<TransferDto> CreateAsync(Guid accountSetId, SaveTransferRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);
            var (from, to) = await LoadJournalsAsync(accountSetId, request.FromJournalId, request.ToJournalId, cancellationToken);
            EnsureEnabled(from);
            EnsureEnabled(to);

            Transfer transfer;
            try
            {
                transfer = Transfer.Create(accountSetId, request.Date, request.Summary, request.FromJournalId, request.ToJournalId, request.Amount, request.Remark);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            // 往来单位留空：内部转账不是对外往来（与柠檬云日记账口径一致）。
            var fromEntry = from.AddEntry(transfer.Date, transfer.Summary, 0m, transfer.Amount, TransferCategory, counterparty: null, remark: transfer.Remark, source: "内部转账");
            var toEntry = to.AddEntry(transfer.Date, transfer.Summary, transfer.Amount, 0m, TransferCategory, counterparty: null, remark: transfer.Remark, source: "内部转账");
            transfer.AttachEntries(fromEntry.Id, toEntry.Id);

            await _transferRepository.AddAsync(transfer, cancellationToken);
            await _transferRepository.SaveChangesAsync(cancellationToken);

            var journalsById = new Dictionary<Guid, Journal> { [from.Id] = from, [to.Id] = to };
            return ToDto(transfer, journalsById, new Dictionary<Guid, Voucher>());
        }

        /// <summary>修改内部转账（未生成凭证时允许）：重建两端流水并更新信息。</summary>
        public async Task<TransferDto?> UpdateAsync(Guid accountSetId, Guid id, SaveTransferRequest request, CancellationToken cancellationToken = default)
        {
            var transfer = await _transferRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (transfer is null)
            {
                return null;
            }

            await HealStaleVoucherAsync(transfer, cancellationToken);

            if (transfer.VoucherId is not null)
            {
                throw new JournalException("该转账已生成凭证，不能修改；如需调整请删除凭证后重建转账。");
            }

            var (oldFrom, oldTo) = await LoadJournalsAsync(accountSetId, transfer.FromJournalId, transfer.ToJournalId, cancellationToken);
            var (from, to) = await LoadJournalsAsync(accountSetId, request.FromJournalId, request.ToJournalId, cancellationToken);
            EnsureEnabled(from);
            EnsureEnabled(to);

            try
            {
                transfer.Update(request.Date, request.Summary, request.Amount, request.Remark);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            // 清理旧流水（可能位于不同账户），再按新参数重建。
            oldFrom.RemoveEntry(transfer.FromEntryId);
            oldTo.RemoveEntry(transfer.ToEntryId);
            var fromEntry = from.AddEntry(transfer.Date, transfer.Summary, 0m, transfer.Amount, TransferCategory, counterparty: null, remark: transfer.Remark, source: "内部转账");
            var toEntry = to.AddEntry(transfer.Date, transfer.Summary, transfer.Amount, 0m, TransferCategory, counterparty: null, remark: transfer.Remark, source: "内部转账");
            transfer.AttachEntries(fromEntry.Id, toEntry.Id);

            await _transferRepository.SaveChangesAsync(cancellationToken);

            var journalsById = new Dictionary<Guid, Journal> { [from.Id] = from, [to.Id] = to };
            var vouchersById = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.DeletedAt is null)
                .ToDictionary(v => v.Id);
            return ToDto(transfer, journalsById, vouchersById);
        }

        /// <summary>删除内部转账（含两端流水）；未生成凭证时才允许。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var transfer = await _transferRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (transfer is null)
            {
                return false;
            }

            await HealStaleVoucherAsync(transfer, cancellationToken);

            if (transfer.VoucherId is not null)
            {
                throw new JournalException("该转账已生成凭证，不能删除；如需删除请先在凭证管理中处理对应凭证。");
            }

            var (from, to) = await LoadJournalsAsync(accountSetId, transfer.FromJournalId, transfer.ToJournalId, cancellationToken);
            from.RemoveEntry(transfer.FromEntryId);
            to.RemoveEntry(transfer.ToEntryId);
            _transferRepository.Remove(transfer);
            await _transferRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>由转账生成一张凭证（借 转入科目 / 贷 转出科目），并回写两端流水的关联凭证。</summary>
        public async Task<Guid> GenerateVoucherAsync(Guid accountSetId, Guid id, string preparer, string? word, CancellationToken cancellationToken = default)
        {
            var transfer = await _transferRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                           ?? throw new JournalException("指定的内部转账不存在。");

            await HealStaleVoucherAsync(transfer, cancellationToken);

            if (transfer.VoucherId is not null)
            {
                throw new JournalException("该转账已生成凭证，请勿重复生成。");
            }

            var (from, to) = await LoadJournalsAsync(accountSetId, transfer.FromJournalId, transfer.ToJournalId, cancellationToken);
            var accountsById = (await _accountRepository.GetAllAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Id);

            var fromAccount = accountsById.GetValueOrDefault(from.AccountId)
                              ?? throw new JournalException("转出账户对应的科目不存在。");
            var toAccount = accountsById.GetValueOrDefault(to.AccountId)
                            ?? throw new JournalException("转入账户对应的科目不存在。");

            var finalWord = string.IsNullOrWhiteSpace(word) ? "记" : word.Trim();
            var number = await _voucherRepository.GetMaxNumberAsync(accountSetId, finalWord, cancellationToken) + 1;

            Voucher voucher;
            try
            {
                voucher = Voucher.Create(accountSetId, finalWord, number, transfer.Date, preparer);
                // 借 转入科目 / 贷 转出科目。
                voucher.AddEntry(transfer.Summary, toAccount.Id, transfer.Amount, 0m);
                voucher.AddEntry(transfer.Summary, fromAccount.Id, 0m, transfer.Amount);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _voucherRepository.AddAsync(voucher, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            // 回写转账与两端流水的关联凭证（分录其余字段保持不变）。
            transfer.AttachVoucher(voucher.Id);
            var fromEntry = from.Entries.FirstOrDefault(e => e.Id == transfer.FromEntryId);
            var toEntry = to.Entries.FirstOrDefault(e => e.Id == transfer.ToEntryId);
            if (fromEntry is not null)
            {
                from.UpdateEntry(fromEntry.Id, fromEntry.Date, fromEntry.Summary, fromEntry.Income, fromEntry.Expense, fromEntry.Category, fromEntry.Counterparty, fromEntry.Project, fromEntry.Department, voucher.Id);
            }

            if (toEntry is not null)
            {
                to.UpdateEntry(toEntry.Id, toEntry.Date, toEntry.Summary, toEntry.Income, toEntry.Expense, toEntry.Category, toEntry.Counterparty, toEntry.Project, toEntry.Department, voucher.Id);
            }

            await _transferRepository.SaveChangesAsync(cancellationToken);
            return voucher.Id;
        }

        /// <summary>
        /// 批量生成转账凭证：逐条出票，已生成凭证的跳过（不报错），
        /// 单条失败不影响其余。返回成功生成的张数。
        /// </summary>
        public async Task<int> GenerateVouchersAsync(Guid accountSetId, IReadOnlyCollection<Guid> ids, string preparer, string? word, CancellationToken cancellationToken = default)
        {
            var created = 0;
            foreach (var id in ids.Distinct())
            {
                var transfer = await _transferRepository.GetByIdAsync(accountSetId, id, cancellationToken);
                if (transfer is null || transfer.VoucherId is not null)
                {
                    continue;
                }

                try
                {
                    await GenerateVoucherAsync(accountSetId, id, preparer, word, cancellationToken);
                    created++;
                }
                catch (JournalException)
                {
                    // 单条失败（如科目缺失）不阻断整批。
                }
            }

            return created;
        }

        /// <summary>批量删除内部转账；已生成凭证的跳过。返回实际删除条数。</summary>
        public async Task<int> DeleteManyAsync(Guid accountSetId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        {
            var removed = 0;
            foreach (var id in ids.Distinct())
            {
                try
                {
                    if (await DeleteAsync(accountSetId, id, cancellationToken))
                    {
                        removed++;
                    }
                }
                catch (JournalException)
                {
                    // 已生成凭证的转账不允许删除，跳过并继续。
                }
            }

            return removed;
        }

        /// <summary>
        /// 自愈：清除转账中指向已删除凭证的残留引用。
        /// 凭证被删除后转账仍保留 VoucherId，会导致该转账既不能修改也不能删除。
        /// 有变更时落库，使修复一次生效。
        /// </summary>
        private async Task HealStaleVoucherAsync(Transfer transfer, CancellationToken cancellationToken)
        {
            var existing = (await _voucherRepository.GetAllAsync(transfer.AccountSetId, cancellationToken))
                .Where(v => v.DeletedAt is null)
                .Select(v => v.Id)
                .ToHashSet();

            if (transfer.ClearStaleVoucher(existing))
            {
                await _transferRepository.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task EnsureAccountSetExists(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new JournalException("指定的账套不存在。");
            }
        }

        private async Task<(Journal From, Journal To)> LoadJournalsAsync(
            Guid accountSetId, Guid fromId, Guid toId, CancellationToken cancellationToken)
        {
            var from = await _journalRepository.GetByIdAsync(accountSetId, fromId, cancellationToken)
                       ?? throw new JournalException("转出账户不存在。");
            var to = await _journalRepository.GetByIdAsync(accountSetId, toId, cancellationToken)
                     ?? throw new JournalException("转入账户不存在。");
            return (from, to);
        }

        private static void EnsureEnabled(Journal journal)
        {
            if (!journal.IsEnabled)
            {
                throw new JournalException($"账户「{journal.Name}」已停用，不能用于转账。");
            }
        }

        private static TransferDto ToDto(
            Transfer transfer,
            IReadOnlyDictionary<Guid, Journal> journalsById,
            IReadOnlyDictionary<Guid, Voucher> vouchersById)
        {
            return new TransferDto
            {
                Id = transfer.Id,
                Date = transfer.Date,
                Summary = transfer.Summary,
                FromJournalId = transfer.FromJournalId,
                FromJournalName = journalsById.GetValueOrDefault(transfer.FromJournalId)?.Name ?? string.Empty,
                ToJournalId = transfer.ToJournalId,
                ToJournalName = journalsById.GetValueOrDefault(transfer.ToJournalId)?.Name ?? string.Empty,
                Amount = transfer.Amount,
                Remark = transfer.Remark,
                VoucherId = transfer.VoucherId,
                VoucherNo = transfer.VoucherId is { } vid && vouchersById.TryGetValue(vid, out var v)
                    ? $"{v.Word}-{v.Number}"
                    : null,
            };
        }
    }
}
