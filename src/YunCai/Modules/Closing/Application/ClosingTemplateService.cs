using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Closing.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Closing.Application
{
    /// <summary>
    /// 结转模板应用服务：模板的增删改查与按月预览（计算将生成的金额与平衡情况）。
    /// 模板行的科目校验（末级、启用）与「全固定金额时保存即校验借贷平衡」在此完成；
    /// 取数型模板的实际金额随月份数据变化，执行/预览时再逐行计算。
    /// </summary>
    public sealed class ClosingTemplateService
    {
        private readonly IClosingTemplateRepository _templateRepository;
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public ClosingTemplateService(
            IClosingTemplateRepository templateRepository,
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _templateRepository = templateRepository;
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>获取账套下全部结转模板（含行明细与科目名称）。</summary>
        public async Task<IReadOnlyList<ClosingTemplateDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await LoadAccountSet(accountSetId, cancellationToken);
            var templates = await _templateRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            return templates.Select(t => ToDto(t, accountsById)).ToList();
        }

        /// <summary>新建结转模板。</summary>
        public async Task<ClosingTemplateDto> CreateAsync(Guid accountSetId, SaveClosingTemplateRequest request, CancellationToken cancellationToken = default)
        {
            await LoadAccountSet(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);

            var name = (request.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                throw new ClosingException("模板名称不能为空。");
            }

            if (await _templateRepository.ExistsByNameAsync(accountSetId, name, null, cancellationToken))
            {
                throw new ClosingException($"模板名称「{name}」已存在。");
            }

            var lines = BuildLines(request.Lines, accountsById);
            ClosingTemplate template;
            try
            {
                template = ClosingTemplate.Create(accountSetId, name, request.IsEnabled);
            }
            catch (ArgumentException ex)
            {
                throw new ClosingException(ex.Message);
            }

            template.ReplaceLines(lines);
            await _templateRepository.AddAsync(template, cancellationToken);
            await _templateRepository.SaveChangesAsync(cancellationToken);
            return ToDto(template, accountsById);
        }

        /// <summary>编辑结转模板（整体替换行）；模板不存在时返回 null。</summary>
        public async Task<ClosingTemplateDto?> UpdateAsync(Guid accountSetId, Guid id, SaveClosingTemplateRequest request, CancellationToken cancellationToken = default)
        {
            await LoadAccountSet(accountSetId, cancellationToken);
            var template = await _templateRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (template is null)
            {
                return null;
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);

            var name = (request.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                throw new ClosingException("模板名称不能为空。");
            }

            if (await _templateRepository.ExistsByNameAsync(accountSetId, name, id, cancellationToken))
            {
                throw new ClosingException($"模板名称「{name}」已存在。");
            }

            var lines = BuildLines(request.Lines, accountsById);
            try
            {
                template.Rename(name);
            }
            catch (ArgumentException ex)
            {
                throw new ClosingException(ex.Message);
            }

            template.SetEnabled(request.IsEnabled);
            template.ReplaceLines(lines);
            await _templateRepository.SaveChangesAsync(cancellationToken);
            return ToDto(template, accountsById);
        }

        /// <summary>删除结转模板；不存在时返回 false。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var template = await _templateRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (template is null)
            {
                return false;
            }

            _templateRepository.Remove(template);
            await _templateRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>按月预览全部模板：计算各模板将生成的金额与借贷平衡情况。</summary>
        public async Task<ClosingTemplatePreviewDto> PreviewAsync(Guid accountSetId, int year, int month, CancellationToken cancellationToken = default)
        {
            await LoadAccountSet(accountSetId, cancellationToken);
            ClosingMath.ValidatePeriod(year, month);

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var posted = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var monthStart = new DateOnly(year, month, 1);
            var monthEnd = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var balanceById = ClosingMath.ComputeBalanceById(accountsById.Values.ToList(), posted, monthEnd);
            var (debitById, creditById) = ClosingMath.ComputePeriodSums(posted, monthStart, monthEnd);

            var templates = await _templateRepository.GetAllAsync(accountSetId, cancellationToken);
            var items = templates
                .Select(t => ToPreviewItem(t, accountsById, debitById, creditById, balanceById))
                .ToList();

            return new ClosingTemplatePreviewDto
            {
                Year = year,
                Month = month,
                PeriodText = $"{year:D4}-{month:D2}",
                Templates = items,
            };
        }

        /// <summary>把保存请求转换为模板行并做科目级校验；全固定金额的模板保存时即校验借贷平衡。</summary>
        private static List<ClosingTemplateLine> BuildLines(
            IReadOnlyList<SaveClosingTemplateLineRequest>? requests,
            IReadOnlyDictionary<Guid, Account> accountsById)
        {
            if (requests is null || requests.Count == 0)
            {
                throw new ClosingException("模板至少需要一行分录。");
            }

            if (requests.Count > 100)
            {
                throw new ClosingException("单个模板最多 100 行分录。");
            }

            var lines = new List<ClosingTemplateLine>(requests.Count);
            foreach (var request in requests)
            {
                if (!accountsById.TryGetValue(request.AccountId, out var account))
                {
                    throw new ClosingException("模板行引用了不存在的科目，请重新选择。");
                }

                EnsureUsableAccount(account, "记账科目");

                if (request.SourceType != ClosingTemplateSourceType.Fixed)
                {
                    if (request.SourceAccountId is not { } sourceId || !accountsById.TryGetValue(sourceId, out var source))
                    {
                        throw new ClosingException("取数方式需要选择一个有效的来源科目。");
                    }

                    EnsureUsableAccount(source, "来源科目");
                }

                try
                {
                    lines.Add(ClosingTemplateLine.Create(
                        request.Summary,
                        request.AccountId,
                        request.Direction,
                        request.SourceType,
                        request.SourceAccountId,
                        request.Amount));
                }
                catch (ArgumentException ex)
                {
                    throw new ClosingException(ex.Message);
                }
            }

            // 全部为固定金额时可在保存阶段直接校验平衡，避免留下明显不可用的模板。
            if (lines.All(l => l.SourceType == ClosingTemplateSourceType.Fixed))
            {
                var totalDebit = lines.Where(l => l.Direction == BalanceDirection.Debit).Sum(l => l.Amount);
                var totalCredit = lines.Where(l => l.Direction == BalanceDirection.Credit).Sum(l => l.Amount);
                if (totalDebit != totalCredit)
                {
                    throw new ClosingException($"模板借贷不平衡（借方 {totalDebit:0.00}，贷方 {totalCredit:0.00}），请调整后再保存。");
                }
            }

            return lines;
        }

        private static void EnsureUsableAccount(Account account, string role)
        {
            if (!account.IsLeaf)
            {
                throw new ClosingException($"{role}「{account.Code} {account.Name}」不是末级科目，不能用于结转模板。");
            }

            if (!account.IsEnabled)
            {
                throw new ClosingException($"{role}「{account.Code} {account.Name}」已停用，不能用于结转模板。");
            }
        }

        private static ClosingTemplateDto ToDto(ClosingTemplate template, IReadOnlyDictionary<Guid, Account> accountsById)
        {
            return new ClosingTemplateDto
            {
                Id = template.Id,
                Name = template.Name,
                IsEnabled = template.IsEnabled,
                Lines = template.Lines
                    .OrderBy(l => l.LineNumber)
                    .Select(l => new ClosingTemplateLineDto
                    {
                        Id = l.Id,
                        LineNumber = l.LineNumber,
                        Summary = l.Summary,
                        AccountId = l.AccountId,
                        AccountCode = accountsById.GetValueOrDefault(l.AccountId)?.Code ?? string.Empty,
                        AccountName = accountsById.GetValueOrDefault(l.AccountId)?.Name ?? string.Empty,
                        Direction = (int)l.Direction,
                        DirectionText = DirectionText(l.Direction),
                        SourceType = (int)l.SourceType,
                        SourceTypeText = SourceTypeText(l.SourceType),
                        SourceAccountId = l.SourceAccountId,
                        SourceAccountCode = l.SourceAccountId is { } sid ? accountsById.GetValueOrDefault(sid)?.Code : null,
                        SourceAccountName = l.SourceAccountId is { } sid2 ? accountsById.GetValueOrDefault(sid2)?.Name : null,
                        Amount = l.Amount,
                    })
                    .ToList(),
            };
        }

        private static ClosingTemplatePreviewItemDto ToPreviewItem(
            ClosingTemplate template,
            IReadOnlyDictionary<Guid, Account> accountsById,
            IReadOnlyDictionary<Guid, decimal> debitById,
            IReadOnlyDictionary<Guid, decimal> creditById,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            var lines = new List<ClosingTemplatePreviewLineDto>();
            string? missingAccount = null;

            foreach (var line in template.Lines.OrderBy(l => l.LineNumber))
            {
                if (!accountsById.TryGetValue(line.AccountId, out var account))
                {
                    missingAccount = "模板行引用了已不存在的科目，请编辑模板后重试。";
                    continue;
                }

                lines.Add(new ClosingTemplatePreviewLineDto
                {
                    Summary = line.Summary,
                    AccountId = line.AccountId,
                    AccountCode = account.Code,
                    AccountName = account.Name,
                    Direction = line.Direction,
                    Amount = ClosingMath.ComputeLineAmount(line, debitById, creditById, balanceById),
                });
            }

            var totalDebit = lines.Where(l => l.Direction == BalanceDirection.Debit).Sum(l => l.Amount);
            var totalCredit = lines.Where(l => l.Direction == BalanceDirection.Credit).Sum(l => l.Amount);
            var balanced = missingAccount is null && totalDebit == totalCredit;

            return new ClosingTemplatePreviewItemDto
            {
                TemplateId = template.Id,
                Name = template.Name,
                IsEnabled = template.IsEnabled,
                Lines = lines,
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                Balanced = balanced,
                Error = missingAccount ?? (balanced ? null : $"借贷不平衡（借方 {totalDebit:0.00}，贷方 {totalCredit:0.00}）"),
            };
        }

        private static string DirectionText(BalanceDirection direction) =>
            direction == BalanceDirection.Debit ? "借" : "贷";

        private static string SourceTypeText(ClosingTemplateSourceType sourceType) => sourceType switch
        {
            ClosingTemplateSourceType.Fixed => "固定金额",
            ClosingTemplateSourceType.PeriodDebit => "科目本期借方发生额",
            ClosingTemplateSourceType.PeriodCredit => "科目本期贷方发生额",
            _ => "科目期末余额",
        };

        private async Task<AccountSet> LoadAccountSet(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = accountSetId == Guid.Empty
                ? null
                : await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            return accountSet ?? throw new ClosingException("指定的账套不存在。");
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsById(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }
}
