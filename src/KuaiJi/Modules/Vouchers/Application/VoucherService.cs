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

        /// <summary>获取指定账套的凭证（按日期范围过滤，含借贷合计与分录明细）。</summary>
        public Task<IReadOnlyList<VoucherListItemDto>> ListAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
        {
            return ListAsync(accountSetId, new VoucherQuery { StartDate = startDate, EndDate = endDate }, cancellationToken);
        }

        /// <summary>
        /// 按筛选条件获取凭证列表。条件之间为「且」关系；科目条件命中该科目及其全部下级科目；
        /// 金额条件按分录金额（借方或贷方的绝对值）比较。
        /// </summary>
        public async Task<IReadOnlyList<VoucherListItemDto>> ListAsync(Guid accountSetId, VoucherQuery query, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            // 收藏站与正式列表互斥：两边不混在一起显示。
            IEnumerable<Voucher> filtered = vouchers.Where(v => v.IsDeleted == query.Deleted);

            if (query.StartDate is { } from)
            {
                filtered = filtered.Where(v => v.Date >= from);
            }

            if (query.EndDate is { } to)
            {
                filtered = filtered.Where(v => v.Date <= to);
            }

            if (!string.IsNullOrWhiteSpace(query.Word))
            {
                var word = query.Word.Trim();
                filtered = filtered.Where(v => v.Word == word);
            }

            if (!string.IsNullOrWhiteSpace(query.Preparer))
            {
                var preparer = query.Preparer.Trim();
                filtered = filtered.Where(v => v.Preparer == preparer);
            }

            if (query.Reviewed is { } reviewed)
            {
                // 已过账视同已审核：审核状态在过账后不再回退。
                filtered = filtered.Where(v => (v.Status != VoucherStatus.Draft) == reviewed);
            }

            if (query.HasAssists is { } hasAssists)
            {
                filtered = filtered.Where(v => v.Entries.Any(e => e.Assists.Count > 0) == hasAssists);
            }

            if (!string.IsNullOrWhiteSpace(query.SummaryContains))
            {
                var keyword = query.SummaryContains.Trim();
                filtered = filtered.Where(v => v.Entries.Any(e => e.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(query.RemarkContains))
            {
                var keyword = query.RemarkContains.Trim();
                filtered = filtered.Where(v => v.Remark is not null && v.Remark.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            if (query.AccountId is { } accountId)
            {
                // 条件里的科目不存在时返回空集，不能退化成「不限」。
                if (!accountsById.TryGetValue(accountId, out var account))
                {
                    return [];
                }

                var codePrefix = account.Code;
                filtered = filtered.Where(v => v.Entries.Any(e =>
                    accountsById.TryGetValue(e.AccountId, out var entryAccount)
                    && entryAccount.Code.StartsWith(codePrefix, StringComparison.Ordinal)));
            }

            if (query.Direction is { } isDebit)
            {
                filtered = filtered.Where(v => v.Entries.Any(e => isDebit ? e.Debit != 0m : e.Credit != 0m));
            }

            if (query.AmountFrom is { } amountFrom)
            {
                filtered = filtered.Where(v => v.Entries.Any(e => Math.Abs(e.Debit != 0m ? e.Debit : e.Credit) >= amountFrom));
            }

            if (query.AmountTo is { } amountTo)
            {
                filtered = filtered.Where(v => v.Entries.Any(e => Math.Abs(e.Debit != 0m ? e.Debit : e.Credit) <= amountTo));
            }

            if (query.NumberFrom is { } numberFrom)
            {
                filtered = filtered.Where(v => v.Number >= numberFrom);
            }

            if (query.NumberTo is { } numberTo)
            {
                filtered = filtered.Where(v => v.Number <= numberTo);
            }

            var byNumber = string.Equals(query.SortBy, "number", StringComparison.OrdinalIgnoreCase);
            filtered = byNumber
                ? (query.SortDesc
                    ? filtered.OrderByDescending(v => v.Number).ThenByDescending(v => v.Date)
                    : filtered.OrderBy(v => v.Number).ThenBy(v => v.Date))
                : (query.SortDesc
                    ? filtered.OrderByDescending(v => v.Date).ThenByDescending(v => v.Number)
                    : filtered.OrderBy(v => v.Date).ThenBy(v => v.Number));

            return filtered.Select(v => VoucherListItemDto.FromDomain(v, accountsById, assistItemsById)).ToList();
        }

        /// <summary>
        /// 摘要历史：按最近使用顺序返回账套内出现过的分录摘要（去重，最多 <paramref name="limit"/> 条），
        /// 供新增凭证的摘要输入框做候选提示。
        /// </summary>
        public async Task<IReadOnlyList<string>> ListSummaryHistoryAsync(Guid accountSetId, int limit = 80, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            return vouchers
                .OrderByDescending(v => v.Date)
                .ThenByDescending(v => v.Number)
                .SelectMany(v => v.Entries.OrderBy(e => e.LineNumber))
                .Select(e => e.Summary?.Trim())
                .Where(summary => !string.IsNullOrEmpty(summary))
                .Distinct()
                .Take(Math.Max(1, limit))
                .ToList()!;
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
                voucher = Voucher.Create(accountSetId, word, number, request.Date, preparer, request.AttachmentCount, request.Remark);
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
                voucher.UpdateHeader(request.Date, request.AttachmentCount, request.Remark);

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

            // 删除先移入回收站（软删除），回收站里可还原，也可彻底删除。
            voucher.SoftDelete(DateTimeOffset.UtcNow);
            await _voucherRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>从回收站还原一张凭证。</summary>
        public async Task<VoucherDetailDto?> RestoreAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return null;
            }

            voucher.Restore();
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);
            return VoucherDetailDto.FromDomain(voucher, accountsById, assistItemsById);
        }

        /// <summary>从回收站彻底删除一张凭证（不可恢复）。</summary>
        public async Task<bool> PurgeAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var voucher = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (voucher is null)
            {
                return false;
            }

            if (!voucher.IsDeleted)
            {
                throw new VoucherException("只有回收站中的凭证才能彻底删除，请先移入回收站。");
            }

            _voucherRepository.Remove(voucher);
            await _voucherRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>清空回收站，返回彻底删除的张数。</summary>
        public async Task<int> ClearRecycleBinAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);
            var deleted = vouchers.Where(v => v.IsDeleted).ToList();
            foreach (var voucher in deleted)
            {
                _voucherRepository.Remove(voucher);
            }

            if (deleted.Count > 0)
            {
                await _voucherRepository.SaveChangesAsync(cancellationToken);
            }

            return deleted.Count;
        }

        /// <summary>复制凭证：生成同凭证字下的新草稿，凭证号取当前最大号 +1；未指定日期时沿用原凭证日期。</summary>
        public async Task<VoucherDetailDto> CopyAsync(Guid accountSetId, Guid id, string preparer, DateOnly? date = null, CancellationToken cancellationToken = default)
        {
            var source = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                         ?? throw new VoucherException("凭证不存在。");

            if (source.IsDeleted)
            {
                throw new VoucherException("回收站中的凭证不能复制，请先还原。");
            }

            var targetDate = date ?? source.Date;
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            if (accountSet.IsDateClosed(targetDate))
            {
                throw new VoucherException($"期间 {targetDate:yyyy-MM} 已结账，不能新增凭证。");
            }

            var number = await _voucherRepository.GetMaxNumberAsync(accountSetId, source.Word, cancellationToken) + 1;
            var copy = Voucher.Duplicate(source, number, targetDate, preparer);
            await _voucherRepository.AddAsync(copy, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);
            return VoucherDetailDto.FromDomain(copy, accountsById, assistItemsById);
        }

        /// <summary>
        /// 红冲：生成一张金额取反的草稿凭证，摘要统一加「冲销&lt;期间&gt;&lt;凭证字&gt;-&lt;号&gt;_」前缀，
        /// 用于冲销原凭证的账务影响（原凭证保留，符合红字冲销的习惯做法）。
        /// </summary>
        public async Task<VoucherDetailDto> ReverseAsync(Guid accountSetId, Guid id, string preparer, DateOnly? date = null, CancellationToken cancellationToken = default)
        {
            var source = await _voucherRepository.GetByIdAsync(accountSetId, id, cancellationToken)
                         ?? throw new VoucherException("凭证不存在。");

            if (source.IsDeleted)
            {
                throw new VoucherException("回收站中的凭证不能红冲，请先还原。");
            }

            var targetDate = date ?? source.Date;
            var accountSet = await LoadAccountSet(accountSetId, cancellationToken);
            if (accountSet.IsDateClosed(targetDate))
            {
                throw new VoucherException($"期间 {targetDate:yyyy-MM} 已结账，不能新增凭证。");
            }

            var number = await _voucherRepository.GetMaxNumberAsync(accountSetId, source.Word, cancellationToken) + 1;
            var prefix = $"冲销{source.Date:yyyyMM}{source.Word}-{source.Number}_";
            var reversal = Voucher.Duplicate(source, number, targetDate, preparer, prefix, negateAmounts: true);
            await _voucherRepository.AddAsync(reversal, cancellationToken);
            await _voucherRepository.SaveChangesAsync(cancellationToken);

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var assistItemsById = await LoadAssistItemsById(accountSetId, cancellationToken);
            return VoucherDetailDto.FromDomain(reversal, accountsById, assistItemsById);
        }

        /// <summary>
        /// 整理凭证 / 凭证号重排：按凭证号或凭证日期重新顺次编号，并重新拼接断号。
        /// 只处理草稿与已审核的凭证（已过账凭证号不允许变动）。
        /// </summary>
        public async Task<int> RenumberAsync(
            Guid accountSetId,
            string? word,
            DateOnly? periodStart,
            DateOnly? periodEnd,
            bool byDate,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            var targets = vouchers
                .Where(v => !v.IsDeleted)
                .Where(v => string.IsNullOrWhiteSpace(word) || v.Word == word.Trim())
                .Where(v => periodStart is null || v.Date >= periodStart)
                .Where(v => periodEnd is null || v.Date <= periodEnd)
                .ToList();

            var posted = targets.Where(v => v.Status == VoucherStatus.Posted).ToList();
            if (posted.Count > 0)
            {
                throw new VoucherException("所选范围内存在已过账凭证，凭证号不能重排，请先反过账。");
            }

            // 按凭证字分组独立编号：不同凭证字各自一条序列。
            // 列表查询走的是 AsNoTracking，改号必须重新取受跟踪的实体，否则改动不会落库。
            var changed = 0;
            foreach (var group in targets.GroupBy(v => v.Word))
            {
                var ordered = byDate
                    ? group.OrderBy(v => v.Date).ThenBy(v => v.Number)
                    : group.OrderBy(v => v.Number).ThenBy(v => v.Date);

                var next = 1;
                foreach (var item in ordered)
                {
                    var voucher = await _voucherRepository.GetByIdAsync(accountSetId, item.Id, cancellationToken);
                    if (voucher is null)
                    {
                        next++;
                        continue;
                    }

                    if (voucher.Number != next)
                    {
                        voucher.SetNumber(next);
                        changed++;
                    }

                    next++;
                }
            }

            if (changed > 0)
            {
                await _voucherRepository.SaveChangesAsync(cancellationToken);
            }

            return changed;
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

        /// <summary>
        /// 反过账凭证：已过账 → 已审核（撤销登记入账，保留审核人）。
        /// 已结账期间内的凭证不允许反过账；不存在时返回 null。
        /// </summary>
        public async Task<VoucherDetailDto?> UnpostAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return await TransitionAsync(accountSetId, id, v => v.Unpost(), cancellationToken, forbidInClosedPeriod: true);
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
        /// 导出：将凭证展开为扁平行（每条分录一行），按「日期 → 字 → 号 → 行号」排序。
        /// 列与凭证导入解析列同构，导出文件可回传导入。
        /// </summary>
        public Task<IReadOnlyList<VoucherExportRow>> ExportRowsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return ExportRowsAsync(accountSetId, new VoucherQuery(), null, cancellationToken);
        }

        /// <summary>
        /// 按筛选条件导出：<paramref name="ids"/> 非空时只导出这些凭证（导出「选中凭证」），
        /// 否则导出该查询命中的全部凭证（导出「当前查询」或「全部」）。
        /// </summary>
        public async Task<IReadOnlyList<VoucherExportRow>> ExportRowsAsync(
            Guid accountSetId,
            VoucherQuery query,
            IReadOnlyCollection<Guid>? ids,
            CancellationToken cancellationToken = default)
        {
            // 复用列表筛选，避免导出与列表口径不一致。
            var vouchers = await ListAsync(accountSetId, query, cancellationToken);
            var selected = ids is { Count: > 0 }
                ? vouchers.Where(v => ids.Contains(v.Id))
                : vouchers;

            var rows = new List<VoucherExportRow>();
            foreach (var voucher in selected)
            {
                foreach (var entry in voucher.Entries.OrderBy(e => e.LineNumber))
                {
                    rows.Add(new VoucherExportRow
                    {
                        Date = voucher.Date,
                        VoucherNo = voucher.VoucherNo,
                        LineNumber = entry.LineNumber,
                        Summary = entry.Summary,
                        AccountCode = entry.AccountCode,
                        AccountName = entry.AccountName,
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
