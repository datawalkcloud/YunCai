using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Vouchers.Application
{
    /// <summary>
    /// 凭证模板应用服务：列表 / 新建 / 修改 / 删除。模板行整体替换，
    /// 科目必须为账套内已启用的末级科目。
    /// </summary>
    public sealed class VoucherTemplateService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IVoucherTemplateRepository _templateRepository;
        private readonly IAccountRepository _accountRepository;

        public VoucherTemplateService(
            IAccountSetRepository accountSetRepository,
            IVoucherTemplateRepository templateRepository,
            IAccountRepository accountRepository)
        {
            _accountSetRepository = accountSetRepository;
            _templateRepository = templateRepository;
            _accountRepository = accountRepository;
        }

        /// <summary>获取全部凭证模板（含模板行与科目解析）。</summary>
        public async Task<IReadOnlyList<VoucherTemplateDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var templates = await _templateRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            return templates
                .OrderBy(t => t.Name)
                .Select(t => ToDto(t, accountsById))
                .ToList();
        }

        /// <summary>新建凭证模板。</summary>
        public async Task<VoucherTemplateDto> CreateAsync(Guid accountSetId, SaveVoucherTemplateRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var name = request.Name?.Trim() ?? string.Empty;
            if (await _templateRepository.ExistsByNameAsync(accountSetId, name, cancellationToken: cancellationToken))
            {
                throw new VoucherException($"凭证模板「{name}」已存在。");
            }

            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            var type = (VoucherTemplateType)request.Type;
            VoucherTemplate template;
            try
            {
                template = VoucherTemplate.Create(accountSetId, name, type);
                template.ReplaceLines(request.Lines.Select(l => (l.Summary, l.AccountId, l.IsDebit)).ToList());
            }
            catch (ArgumentException ex)
            {
                throw new VoucherException(ex.Message);
            }

            ValidateLines(template, accountsById);
            await _templateRepository.AddAsync(template, cancellationToken);
            await _templateRepository.SaveChangesAsync(cancellationToken);
            return ToDto(template, accountsById);
        }

        /// <summary>修改凭证模板（整体替换模板行）；不存在时返回 null。</summary>
        public async Task<VoucherTemplateDto?> UpdateAsync(Guid accountSetId, Guid id, SaveVoucherTemplateRequest request, CancellationToken cancellationToken = default)
        {
            var template = await _templateRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (template is null)
            {
                return null;
            }

            var name = request.Name?.Trim() ?? string.Empty;
            if (await _templateRepository.ExistsByNameAsync(accountSetId, name, template.Id, cancellationToken))
            {
                throw new VoucherException($"凭证模板「{name}」已存在。");
            }

            var accountsById = await LoadAccountsByIdAsync(accountSetId, cancellationToken);
            try
            {
                template.Update(name, (VoucherTemplateType)request.Type);
                template.ReplaceLines(request.Lines.Select(l => (l.Summary, l.AccountId, l.IsDebit)).ToList());
            }
            catch (ArgumentException ex)
            {
                throw new VoucherException(ex.Message);
            }

            ValidateLines(template, accountsById);
            await _templateRepository.SaveChangesAsync(cancellationToken);
            return ToDto(template, accountsById);
        }

        /// <summary>删除凭证模板；不存在时返回 false。</summary>
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

        private static void ValidateLines(VoucherTemplate template, IReadOnlyDictionary<Guid, Account> accountsById)
        {
            foreach (var line in template.Lines)
            {
                if (!accountsById.TryGetValue(line.AccountId, out var account))
                {
                    throw new VoucherException("模板行会计科目不存在。");
                }

                if (!account.IsLeaf)
                {
                    throw new VoucherException($"模板行科目「{account.Code} {account.Name}」不是末级科目。");
                }

                if (!account.IsEnabled)
                {
                    throw new VoucherException($"模板行科目「{account.Code} {account.Name}」已停用。");
                }
            }
        }

        private static VoucherTemplateDto ToDto(VoucherTemplate template, IReadOnlyDictionary<Guid, Account> accountsById) => new()
        {
            Id = template.Id,
            Name = template.Name,
            TypeText = VoucherTemplateDto.TypeTextOf(template.Type),
            Lines = template.Lines
                .Select(l =>
                {
                    var account = accountsById.GetValueOrDefault(l.AccountId);
                    return new VoucherTemplateLineDto
                    {
                        Id = l.Id,
                        LineNumber = l.LineNumber,
                        Summary = l.Summary,
                        AccountId = l.AccountId,
                        AccountCode = account?.Code ?? string.Empty,
                        AccountName = account?.Name ?? string.Empty,
                        DirectionText = l.IsDebit ? "借" : "贷",
                        IsDebit = l.IsDebit,
                    };
                })
                .ToList(),
        };

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new VoucherException("指定的账套不存在。");
            }
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsByIdAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }
}
