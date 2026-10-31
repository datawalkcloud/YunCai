using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.Modules.Vouchers.Infrastructure;

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

        // —— 导入导出 ——

        /// <summary>导出全部凭证模板（xlsx，导回时按模板名称分组）。</summary>
        public async Task<byte[]> ExportAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var templates = await ListAsync(accountSetId, cancellationToken);
            return VoucherTemplateExportBuilder.BuildTemplates(templates);
        }

        /// <summary>凭证模板导入模板。</summary>
        public byte[] BuildImportTemplate() => VoucherTemplateExportBuilder.BuildTemplate();

        /// <summary>
        /// 导入凭证模板：按模板名称分组；同名模板跳过，科目按编码解析（末级启用）。
        /// </summary>
        public async Task<VoucherTemplateImportResultDto> ImportAsync(Guid accountSetId, IReadOnlyList<VoucherTemplateImportRow> rows, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var existing = (await _templateRepository.GetAllAsync(accountSetId, cancellationToken))
                .Select(t => t.Name)
                .ToHashSet(StringComparer.Ordinal);
            var accountsByCode = (await _accountRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(a => a.IsLeaf && a.IsEnabled)
                .GroupBy(a => a.Code)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var failures = new List<VoucherTemplateImportFailureDto>();
            var groups = new List<(string Name, string TypeText, List<VoucherTemplateImportRow> Lines)>();
            foreach (var row in rows)
            {
                var name = row.Name?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    failures.Add(new VoucherTemplateImportFailureDto { Row = row.Row, Reason = "模板名称不能为空。" });
                    continue;
                }

                if (groups.Count > 0 && groups[^1].Name == name)
                {
                    groups[^1].Lines.Add(row);
                }
                else
                {
                    groups.Add((name, row.TypeText?.Trim() ?? string.Empty, [row]));
                }
            }

            var imported = 0;
            var skipped = 0;
            foreach (var group in groups)
            {
                if (existing.Contains(group.Name))
                {
                    skipped++;
                    continue;
                }

                var type = group.TypeText switch
                {
                    "采购" => VoucherTemplateType.Purchase,
                    "销售" => VoucherTemplateType.Sales,
                    "其他" => VoucherTemplateType.Other,
                    _ => VoucherTemplateType.DailyExpense,
                };

                var lines = new List<(string Summary, Guid AccountId, bool IsDebit)>();
                foreach (var line in group.Lines)
                {
                    var code = line.AccountCode?.Trim() ?? string.Empty;
                    if (!accountsByCode.TryGetValue(code, out var account))
                    {
                        failures.Add(new VoucherTemplateImportFailureDto { Row = line.Row, Reason = $"模板「{group.Name}」的科目编码「{code}」不存在、非末级或已停用。" });
                        lines.Clear();
                        break;
                    }

                    lines.Add((line.Summary?.Trim() ?? "模板分录", account.Id, !string.Equals(line.Direction?.Trim(), "贷", StringComparison.Ordinal)));
                }

                if (lines.Count == 0)
                {
                    continue;
                }

                try
                {
                    var template = VoucherTemplate.Create(accountSetId, group.Name, type);
                    template.ReplaceLines(lines);
                    await _templateRepository.AddAsync(template, cancellationToken);
                    existing.Add(group.Name);
                    imported++;
                }
                catch (ArgumentException ex)
                {
                    failures.Add(new VoucherTemplateImportFailureDto { Row = group.Lines[0].Row, Reason = ex.Message });
                }
            }

            if (imported > 0)
            {
                await _templateRepository.SaveChangesAsync(cancellationToken);
            }

            return new VoucherTemplateImportResultDto { Imported = imported, Skipped = skipped, Failures = failures };
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
            // 必须按行号排序：EF Include 出来的行顺序不保证，否则模板引入后分录会前后颠倒。
            Lines = template.Lines
                .OrderBy(l => l.LineNumber)
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
