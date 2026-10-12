using KuaiJi.Modules.Accounting.Application;
using KuaiJi.Modules.Assist.Application;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Vouchers.Application
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
    /// 分录上的辅助核算取值在保存前统一交由 <see cref="AccountAssistGuard"/> 校验（设计文档 A2）。
    /// </summary>
    public sealed class VoucherService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAssistItemRepository _assistItemRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly AccountAssistGuard _assistGuard;

        public VoucherService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IAssistItemRepository assistItemRepository,
            IVoucherRepository voucherRepository,
            AccountAssistGuard assistGuard)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _assistItemRepository = assistItemRepository;
            _voucherRepository = voucherRepository;
            _assistGuard = assistGuard;
        }

        /// <summary>获取指定账套的凭证（可选日期范围过滤，含借贷合计与分录明细，按日期与凭证号排列）。</summary>
        public async Task<IReadOnlyList<VoucherListItemDto>> ListAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            return vouchers
                .Where(v => (startDate == null || v.Date >= startDate) && (endDate == null || v.Date <= endDate))
                .Select(v => VoucherListItemDto.FromDomain(v, accountsById, assistItemsById))
                .ToList();
        }

        /// <summary>获取单张凭证详情（含分录、其科目编码/名称与辅助核算取值）；不存在时返回 null。</summary>
        public async Task<VoucherDetailDto?> GetAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return null;
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);
            return VoucherDetailDto.FromDomain(voucher, accountsById, assistItemsById);
        }

        /// <summary>
        /// 制单：校验分录、按需自动续号后创建一张草稿凭证并持久化。
        /// 每条分录须为借贷单边非零，科目须为该账套下已启用的末级科目，
        /// 且分录挂载的辅助核算取值须通过 <see cref="AccountAssistGuard"/> 的维度校验。
        /// </summary>
        public async Task<VoucherDetailDto> CreateAsync(Guid accountSetId, string preparer, CreateVoucherRequest request, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);

            if (request.Entries is null || request.Entries.Count == 0)
            {
                throw new VoucherException("凭证至少需要一条分录。");
            }

            if (accountSet.IsDateClosed(request.Date))
            {
                throw new VoucherException($"{request.Date.Year:D4}-{request.Date.Month:D2} 期间已结账，不能再录入凭证。");
            }

            var word = string.IsNullOrWhiteSpace(request.Word) ? "记" : request.Word.Trim();

            // 凭证字须取自账套维护的凭证字列表。
            var allowedWords = accountSet.VoucherWordList();
            if (!allowedWords.Contains(word))
            {
                throw new VoucherException($"凭证字「{word}」不在账套凭证字列表（{string.Join("、", allowedWords)}）中。");
            }

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
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);

            Voucher voucher;
            try
            {
                voucher = Voucher.Create(accountSetId, word, number, request.Date, preparer, request.AttachmentCount);
                var lines = new List<AssistLine>(request.Entries.Count);

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

                    var references = ToAssistReferences(entry.Assists);
                    lines.Add(new AssistLine(account, references));
                    voucher.AddEntry(entry.Summary, entry.AccountId, entry.Debit, entry.Credit, references);
                }

                // 末级科目与借贷平衡之外，还要按科目的辅助核算维度配置逐条校验（设计文档 A2）。
                _assistGuard.Validate(accountSet, assistItemsById.Values, lines);
            }
            catch (ArgumentException ex)
            {
                throw new VoucherException(ex.Message);
            }
            catch (AssistanceException ex)
            {
                throw new VoucherException(ex.Message);
            }

            await _voucherRepository.AddAsync(voucher, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            return VoucherDetailDto.FromDomain(voucher, accountsById, assistItemsById);
        }

        /// <summary>
        /// 编辑：仅草稿状态可改。凭证字与号保持不变，覆盖日期、附单据数并整体替换全部分录。
        /// 每条分录须为借贷单边非零，且科目须为该账套下已启用的末级科目；凭证不存在时返回 null。
        /// </summary>
        public async Task<VoucherDetailDto?> UpdateAsync(Guid accountSetId, Guid id, UpdateVoucherRequest request, CancellationToken cancellationToken = default)
        {
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);

            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return null;
            }

            if (accountSet.IsDateClosed(voucher.Date) || accountSet.IsDateClosed(request.Date))
            {
                throw new VoucherException("凭证所属期间已结账，不能修改。");
            }

            if (request.Entries is null || request.Entries.Count == 0)
            {
                throw new VoucherException("凭证至少需要一条分录。");
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);

            try
            {
                // UpdateHeader/RemoveEntry/AddEntry/UpdateEntry 均要求草稿状态，非草稿会抛出 InvalidOperationException。
                voucher.UpdateHeader(request.Date, request.AttachmentCount);

                // 以原地更新方式替换分录，避免 DELETE 操作触发 Npgsql 并发校验。
                var existingEntries = voucher.Entries.ToList();
                var requestEntries = request.Entries;
                var lines = new List<AssistLine>(requestEntries.Count);

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

                    var references = ToAssistReferences(entry.Assists);
                    lines.Add(new AssistLine(account, references));
                    voucher.UpdateEntry(existingEntries[i].Id, entry.Summary, entry.AccountId, entry.Debit, entry.Credit, references);
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

                    var references = ToAssistReferences(entry.Assists);
                    lines.Add(new AssistLine(account, references));
                    voucher.AddEntry(entry.Summary, entry.AccountId, entry.Debit, entry.Credit, references);
                }

                // 删除多余分录
                for (int i = requestEntries.Count; i < existingEntries.Count; i++)
                {
                    voucher.RemoveEntry(existingEntries[i].Id);
                }

                // 编辑草稿同样要整体校验辅助核算取值；传入空集合即清除该分录原有的取值。
                _assistGuard.Validate(accountSet, assistItemsById.Values, lines);
            }
            catch (ArgumentException ex)
            {
                throw new VoucherException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new VoucherException(ex.Message);
            }
            catch (AssistanceException ex)
            {
                throw new VoucherException(ex.Message);
            }

            await _voucherRepository.SaveChangesAsync(cancellationToken);
            return VoucherDetailDto.FromDomain(voucher, accountsById, assistItemsById);
        }

        /// <summary>删除凭证（含其全部分录）；不存在时返回 false。已过账凭证已登记入账、已结期间凭证均不允许删除。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return false;
            }

            if (voucher.Status == VoucherStatus.Posted)
            {
                throw new VoucherException($"凭证「{voucher.Word}-{voucher.Number}」已过账，不能删除。");
            }

            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            if (accountSet.IsDateClosed(voucher.Date))
            {
                throw new VoucherException($"凭证「{voucher.Word}-{voucher.Number}」所属期间已结账，不能删除。");
            }

            _voucherRepository.Remove(voucher);
            await _voucherRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>审核凭证：草稿 → 已审核。要求借贷平衡，审核人取当前登录用户显示名；不存在时返回 null。</summary>
        public async Task<VoucherDetailDto?> ReviewAsync(Guid accountSetId, Guid id, string reviewer, CancellationToken cancellationToken = default)
        {
            return await TransitionAsync(accountSetId, id, v => v.Review(reviewer), cancellationToken);
        }

        /// <summary>反审核凭证：已审核 → 草稿，清空审核人；已结期间内的凭证不允许反审核；不存在时返回 null。</summary>
        public async Task<VoucherDetailDto?> UnreviewAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return await TransitionAsync(accountSetId, id, v => v.Unreview(), cancellationToken, forbidInClosedPeriod: true);
        }

        /// <summary>过账凭证：已审核 → 已过账（登记入账锁定）；不存在时返回 null。</summary>
        public async Task<VoucherDetailDto?> PostAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return await TransitionAsync(accountSetId, id, v => v.Post(), cancellationToken);
        }

        /// <summary>执行凭证状态流转并持久化；域层校验失败时统一转换为友好提示。</summary>
        private async Task<VoucherDetailDto?> TransitionAsync(Guid accountSetId, Guid id, Action<Voucher> transition, CancellationToken cancellationToken, bool forbidInClosedPeriod = false)
        {
            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return null;
            }

            if (forbidInClosedPeriod)
            {
                var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
                if (accountSet.IsDateClosed(voucher.Date))
                {
                    throw new VoucherException($"凭证「{voucher.Word}-{voucher.Number}」所属期间已结账，不能反审核。");
                }
            }

            try
            {
                transition(voucher);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                throw new VoucherException(ex.Message);
            }

            await _voucherRepository.SaveChangesAsync(cancellationToken);
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);
            return VoucherDetailDto.FromDomain(voucher, accountsById, assistItemsById);
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
            await LoadAccountSet(accountSetId, cancellationToken);
        }

        /// <summary>加载账套（用于结账期间、凭证字等校验）；不存在时抛出友好错误。</summary>
        private async Task<AccountSet> LoadAccountSet(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = accountSetId == Guid.Empty
                ? null
                : await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            return accountSet ?? throw new VoucherException("指定的账套不存在。");
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsById(Guid accountSetId, CancellationToken cancellationToken)
        {
            // 制单校验需要科目上已设置的辅助核算维度，故一并载入 AssistTypes（未 Include 时它是空集合）。
            var accounts = await _accountRepository.GetAllWithAssistTypesAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }

        /// <summary>装载该账套的辅助核算档案：既要供校验取档案的类别/末级/启用状态，也要在详情里回显编码与名称。</summary>
        private async Task<IReadOnlyDictionary<Guid, AssistItem>> LoadAssistItemsById(Guid accountSetId, CancellationToken cancellationToken)
        {
            var items = await _assistItemRepository.GetAllAsync(accountSetId, cancellationToken);
            return items.ToDictionary(i => i.Id, i => i);
        }

        /// <summary>把分录请求里的辅助核算取值转成领域引用；空集合表示该分录不挂维度。</summary>
        private static IReadOnlyCollection<AssistRef> ToAssistReferences(IReadOnlyList<VoucherEntryAssistRequest>? assists)
        {
            if (assists is null || assists.Count == 0)
            {
                return [];
            }

            return assists.Select(item => item.ToReference()).ToList();
        }
    }
}
