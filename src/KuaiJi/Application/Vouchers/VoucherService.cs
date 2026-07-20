using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Vouchers
{
    /// <summary>凭证业务操作中的可预期错误（如账套不存在、科目无效、借贷不平衡），用于向前端返回友好提示。</summary>
    public sealed class VoucherException : Exception
    {
        public VoucherException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 凭证应用服务：面向「凭证管理」页的列表、详情、新增、删除。
    /// 所有凭证均归属于指定账套，无法脱离账套独立存在。
    /// </summary>
    public sealed class VoucherService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public VoucherService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>获取指定账套的凭证（可选日期范围过滤，含借贷合计，按日期与凭证号排列）。</summary>
        public async Task<IReadOnlyList<VoucherListItemDto>> ListAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            return vouchers
                .Where(v => (startDate == null || v.Date >= startDate) && (endDate == null || v.Date <= endDate))
                .Select(VoucherListItemDto.FromDomain)
                .ToList();
        }

        /// <summary>获取单张凭证详情（含分录及其科目编码/名称）；不存在时返回 null。</summary>
        public async Task<VoucherDetailDto?> GetAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return null;
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            return VoucherDetailDto.FromDomain(voucher, accountsById);
        }

        /// <summary>
        /// 制单：校验分录、按需自动续号后创建一张草稿凭证并持久化。
        /// 每条分录须为借贷单边非零，且科目须为该账套下已启用的末级科目。
        /// </summary>
        public async Task<VoucherDetailDto> CreateAsync(Guid accountSetId, string preparer, CreateVoucherRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            if (request.Entries is null || request.Entries.Count == 0)
            {
                throw new VoucherException("凭证至少需要一条分录。");
            }

            var word = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim();

            // 凭证号：显式指定则查重，否则按凭证字自动续号。
            int number;
            if (request.Number > 0)
            {
                number = request.Number;
                if (await _voucherRepository.ExistsByWordAndNumberAsync(accountSetId, word, number, cancellationToken))
                {
                    throw new VoucherException($"凭证「{word}-{number}」已存在，请更换凭证号。");
                }
            }
            else
            {
                number = await _voucherRepository.GetMaxNumberAsync(accountSetId, word, cancellationToken) + 1;
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);

            Voucher voucher;
            try
            {
                voucher = Voucher.Create(accountSetId, word, number, request.Date, preparer, request.AttachmentCount);

                foreach (var entry in request.Entries)
                {
                    if (!accountsById.TryGetValue(entry.AccountId, out var account))
                    {
                        throw new VoucherException("存在无效的会计科目，请重新选择。");
                    }

                    if (!account.IsLeaf)
                    {
                        throw new VoucherException($"科目「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
                    }

                    if (!account.IsEnabled)
                    {
                        throw new VoucherException($"科目「{account.Code} {account.Name}」已停用，不能用于记账。");
                    }

                    voucher.AddEntry(entry.Summary, entry.AccountId, entry.Debit, entry.Credit);
                }
            }
            catch (ArgumentException ex)
            {
                throw new VoucherException(ex.Message);
            }

            await _voucherRepository.AddAsync(voucher, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            return VoucherDetailDto.FromDomain(voucher, accountsById);
        }

        /// <summary>
        /// 编辑：仅草稿状态可改。凭证字与号保持不变，覆盖日期、附单据数并整体替换全部分录。
        /// 每条分录须为借贷单边非零，且科目须为该账套下已启用的末级科目；凭证不存在时返回 null。
        /// </summary>
        public async Task<VoucherDetailDto?> UpdateAsync(Guid accountSetId, Guid id, UpdateVoucherRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return null;
            }

            if (request.Entries is null || request.Entries.Count == 0)
            {
                throw new VoucherException("凭证至少需要一条分录。");
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);

            try
            {
                // UpdateHeader/RemoveEntry/AddEntry/UpdateEntry 均要求草稿状态，非草稿会抛出 InvalidOperationException。
                voucher.UpdateHeader(request.Date, request.AttachmentCount);

                // 以原地更新方式替换分录，避免 DELETE 操作触发 Npgsql 并发校验。
                var existingEntries = voucher.Entries.ToList();
                var requestEntries = request.Entries;

                // 更新已有分录（按位置一一对应）
                for (int i = 0; i < Math.Min(existingEntries.Count, requestEntries.Count); i++)
                {
                    var entry = requestEntries[i];
                    if (!accountsById.TryGetValue(entry.AccountId, out var account))
                    {
                        throw new VoucherException("存在无效的会计科目，请重新选择。");
                    }

                    if (!account.IsLeaf)
                    {
                        throw new VoucherException($"科目「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
                    }

                    if (!account.IsEnabled)
                    {
                        throw new VoucherException($"科目「{account.Code} {account.Name}」已停用，不能用于记账。");
                    }

                    voucher.UpdateEntry(existingEntries[i].Id, entry.Summary, entry.AccountId, entry.Debit, entry.Credit);
                }

                // 新增多余分录
                for (int i = existingEntries.Count; i < requestEntries.Count; i++)
                {
                    var entry = requestEntries[i];
                    if (!accountsById.TryGetValue(entry.AccountId, out var account))
                    {
                        throw new VoucherException("存在无效的会计科目，请重新选择。");
                    }

                    if (!account.IsLeaf)
                    {
                        throw new VoucherException($"科目「{account.Code} {account.Name}」不是末级科目，不能用于记账。");
                    }

                    if (!account.IsEnabled)
                    {
                        throw new VoucherException($"科目「{account.Code} {account.Name}」已停用，不能用于记账。");
                    }

                    voucher.AddEntry(entry.Summary, entry.AccountId, entry.Debit, entry.Credit);
                }

                // 删除多余分录
                for (int i = requestEntries.Count; i < existingEntries.Count; i++)
                {
                    voucher.RemoveEntry(existingEntries[i].Id);
                }
            }
            catch (ArgumentException ex)
            {
                throw new VoucherException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new VoucherException(ex.Message);
            }

            await _voucherRepository.SaveChangesAsync(cancellationToken);
            return VoucherDetailDto.FromDomain(voucher, accountsById);
        }

        /// <summary>删除凭证（含其全部分录）；不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return false;
            }

            _voucherRepository.Remove(voucher);
            await _voucherRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>
        /// 导出：将账套全部凭证展开为扁平行（每条分录一行），按「日期 → 字 → 号 → 行号」排序。
        /// 列与凭证导入解析列同构，导出文件可回传导入。科目编码/名称由科目表反查。
        /// </summary>
        public async Task<IReadOnlyList<VoucherExportRow>> ExportRowsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            var rows = new List<VoucherExportRow>();
            foreach (var voucher in vouchers
                .OrderBy(v => v.Date)
                .ThenBy(v => v.Word, StringComparer.Ordinal)
                .ThenBy(v => v.Number))
            {
                foreach (var entry in voucher.Entries.OrderBy(e => e.LineNumber))
                {
                    var account = accountsById.GetValueOrDefault(entry.AccountId);
                    rows.Add(new VoucherExportRow
                    {
                        Date = voucher.Date,
                        VoucherNo = $"{voucher.Word}-{voucher.Number}",
                        LineNumber = entry.LineNumber,
                        Summary = entry.Summary,
                        AccountCode = account?.Code ?? string.Empty,
                        AccountName = account?.Name ?? string.Empty,
                        Debit = entry.Debit,
                        Credit = entry.Credit,
                        Preparer = voucher.Preparer,
                        Reviewer = voucher.Reviewer ?? string.Empty,
                        AttachmentCount = voucher.AttachmentCount,
                    });
                }
            }

            return rows;
        }

        private async Task EnsureAccountSetExists(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new VoucherException("指定的账套不存在。");
            }
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsById(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }
}
