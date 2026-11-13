using System.Globalization;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Assist.Domain;
using YunCai.Modules.Funds.Domain;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>
    /// 收支类别应用服务：面向「收支类别」页的列表、新建、修改、删除与启停用。
    /// 类别归属于账套，同一账套同一类型内名称唯一、编码全局唯一（默认按序编号 1、2、3…）；
    /// 日记账分录按类别名称关联，改名/删除类别不回写既有流水。类别可附带上级类别、
    /// 关联现金流项目、智能匹配关键字（导入自动归类）与默认对方科目（由流水生成凭证时使用）。
    /// </summary>
    public sealed class EntryCategoryService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IEntryCategoryRepository _categoryRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAssistItemRepository _assistItemRepository;

        public EntryCategoryService(
            IAccountSetRepository accountSetRepository,
            IEntryCategoryRepository categoryRepository,
            IAccountRepository accountRepository,
            IAssistItemRepository assistItemRepository)
        {
            _accountSetRepository = accountSetRepository;
            _categoryRepository = categoryRepository;
            _accountRepository = accountRepository;
            _assistItemRepository = assistItemRepository;
        }

        /// <summary>获取指定账套的全部收支类别（按类型、编码排列，并解析默认对方科目与上级类别）。</summary>
        public async Task<IReadOnlyList<EntryCategoryDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var categories = await _categoryRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var namesById = categories.ToDictionary(c => c.Id, c => c.Name);
            var cashFlowById = await LoadCashFlowNamesAsync(accountSetId, cancellationToken);

            return categories
                .Select(c => EntryCategoryDto.FromDomain(
                    c,
                    c.VoucherSubjectId is { } sid ? accountsById.GetValueOrDefault(sid) : null,
                    c.ParentId is { } pid ? namesById.GetValueOrDefault(pid) : null,
                    c.CashFlowItemId is { } cid ? cashFlowById.GetValueOrDefault(cid) : null))
                .ToList();
        }

        /// <summary>新建收支类别。同一账套同一类型内名称不可重复，编码在「账套 + 类型」内不可重复（空则按类型自动编号）。</summary>
        public async Task<EntryCategoryDto> CreateAsync(Guid accountSetId, CreateEntryCategoryRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            if (!Enum.IsDefined(typeof(EntryCategoryType), request.Type))
            {
                throw new JournalException("类别类型无效。");
            }

            var type = (EntryCategoryType)request.Type;
            var name = request.Name?.Trim() ?? string.Empty;

            if (await _categoryRepository.ExistsByNameAsync(accountSetId, type, name, cancellationToken: cancellationToken))
            {
                throw new JournalException($"{EntryCategoryDto.TypeTextOf(type)}类别「{name}」已存在。");
            }

            var existing = await _categoryRepository.GetAllAsync(accountSetId, cancellationToken);
            var code = string.IsNullOrWhiteSpace(request.Code) ? NextCode(existing, type) : request.Code.Trim();
            if (existing.Any(c => c.Type == type && string.Equals(c.Code, code, StringComparison.Ordinal)))
            {
                throw new JournalException($"类别编码「{code}」已存在。");
            }

            EntryCategory category;
            try
            {
                category = EntryCategory.Create(accountSetId, type, name, code);
                category.SetSmartKeywords(request.SmartKeywords);
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            if (request.VoucherSubjectId is { } subjectId && subjectId != Guid.Empty)
            {
                ValidateSubject(accountsById, subjectId);
                category.SetVoucherSubject(subjectId);
            }

            if (request.ParentId is { } parentId && parentId != Guid.Empty)
            {
                ValidateParent(existing, parentId, type, category.Id);
                category.SetParent(parentId);
            }

            if (request.CashFlowItemId is { } cashFlowId && cashFlowId != Guid.Empty)
            {
                await ValidateCashFlowAsync(accountSetId, cashFlowId, cancellationToken);
                category.SetCashFlow(cashFlowId);
            }

            await _categoryRepository.AddAsync(category, cancellationToken);
            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return await ToDtoAsync(accountSetId, category, accountsById, cancellationToken);
        }

        /// <summary>
        /// 修改类别：名称必填；编码 / 上级类别 / 关联现金流 / 智能匹配关键字 / 默认对方科目按三态处理
        /// （null = 保持不变，空串 / Guid.Empty = 清除）。既有流水中的类别文本不随之变更。
        /// 类别不存在时返回 null。
        /// </summary>
        public async Task<EntryCategoryDto?> UpdateAsync(Guid accountSetId, Guid id, UpdateEntryCategoryRequest request, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (category is null)
            {
                return null;
            }

            var existing = await _categoryRepository.GetAllAsync(accountSetId, cancellationToken);
            var name = request.Name?.Trim() ?? string.Empty;
            if (await _categoryRepository.ExistsByNameAsync(accountSetId, category.Type, name, category.Id, cancellationToken))
            {
                throw new JournalException($"{EntryCategoryDto.TypeTextOf(category.Type)}类别「{name}」已存在。");
            }

            try
            {
                category.Rename(name);
                if (!string.IsNullOrWhiteSpace(request.Code) && !string.Equals(request.Code.Trim(), category.Code, StringComparison.Ordinal))
                {
                    var code = request.Code.Trim();
                    if (existing.Any(c => c.Id != category.Id && c.Type == category.Type && string.Equals(c.Code, code, StringComparison.Ordinal)))
                    {
                        throw new JournalException($"类别编码「{code}」已存在。");
                    }

                    category.ChangeCode(code);
                }

                if (request.SmartKeywords is not null)
                {
                    category.SetSmartKeywords(request.SmartKeywords);
                }
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            if (request.ParentId is { } parentId)
            {
                if (parentId == Guid.Empty)
                {
                    category.SetParent(null);
                }
                else
                {
                    ValidateParent(existing, parentId, category.Type, category.Id);
                    category.SetParent(parentId);
                }
            }

            if (request.CashFlowItemId is { } cashFlowId)
            {
                if (cashFlowId == Guid.Empty)
                {
                    category.SetCashFlow(null);
                }
                else
                {
                    await ValidateCashFlowAsync(accountSetId, cashFlowId, cancellationToken);
                    category.SetCashFlow(cashFlowId);
                }
            }

            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            if (request.VoucherSubjectId is { } subjectId)
            {
                if (subjectId == Guid.Empty)
                {
                    category.SetVoucherSubject(null);
                }
                else
                {
                    ValidateSubject(accountsById, subjectId);
                    category.SetVoucherSubject(subjectId);
                }
            }

            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return await ToDtoAsync(accountSetId, category, accountsById, cancellationToken);
        }

        /// <summary>启用 / 停用类别；不存在时返回 null。</summary>
        public async Task<EntryCategoryDto?> SetEnabledAsync(Guid accountSetId, Guid id, bool enabled, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (category is null)
            {
                return null;
            }

            if (enabled)
            {
                category.Enable();
            }
            else
            {
                category.Disable();
            }

            await _categoryRepository.SaveChangesAsync(cancellationToken);
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            return await ToDtoAsync(accountSetId, category, accountsById, cancellationToken);
        }

        /// <summary>删除类别；不存在时返回 false。既有流水中的类别文本保留不变。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (category is null)
            {
                return false;
            }

            _categoryRepository.Remove(category);
            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>批量导入（按名称去重）：导入行 = 类型 / 名称 / 上级类别 / 关联现金流 / 智能匹配关键字。</summary>
        public async Task<EntryCategoryImportResult> ImportAsync(
            Guid accountSetId,
            IReadOnlyList<EntryCategoryImportRow> rows,
            CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var warnings = new List<string>();
            var imported = 0;
            var skipped = 0;

            var existing = (await _categoryRepository.GetAllAsync(accountSetId, cancellationToken)).ToList();
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            var cashFlow = await LoadCashFlowItemsAsync(accountSetId, cancellationToken);

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var lineNo = i + 2;
                var name = row.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    warnings.Add($"第 {lineNo} 行：名称不能为空。已跳过。");
                    skipped++;
                    continue;
                }

                var type = string.Equals(row.TypeText?.Trim(), "支出", StringComparison.Ordinal)
                    ? EntryCategoryType.Expense
                    : EntryCategoryType.Income;

                if (existing.Any(c => c.Type == type && string.Equals(c.Name, name, StringComparison.Ordinal)))
                {
                    warnings.Add($"第 {lineNo} 行：{EntryCategoryDto.TypeTextOf(type)}类别「{name}」已存在。已跳过。");
                    skipped++;
                    continue;
                }

                EntryCategory category;
                try
                {
                    var code = string.IsNullOrWhiteSpace(row.Code) ? NextCode(existing, type) : row.Code.Trim();
                    if (existing.Any(c => c.Type == type && string.Equals(c.Code, code, StringComparison.Ordinal)))
                    {
                        code = NextCode(existing, type);
                    }

                    category = EntryCategory.Create(accountSetId, type, name, code);
                    category.SetSmartKeywords(row.SmartKeywords);

                    var parentName = row.ParentName?.Trim();
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        var parent = existing.FirstOrDefault(c => c.Type == type && string.Equals(c.Name, parentName, StringComparison.Ordinal));
                        if (parent is null)
                        {
                            warnings.Add($"第 {lineNo} 行：上级类别「{parentName}」不存在，已按顶级类别导入。");
                        }
                        else
                        {
                            category.SetParent(parent.Id);
                        }
                    }

                    var cashFlowName = row.CashFlowName?.Trim();
                    if (!string.IsNullOrEmpty(cashFlowName))
                    {
                        var item = cashFlow.FirstOrDefault(c => string.Equals(c.Name, cashFlowName, StringComparison.Ordinal));
                        if (item is null)
                        {
                            warnings.Add($"第 {lineNo} 行：现金流项目「{cashFlowName}」不存在，已忽略。");
                        }
                        else
                        {
                            category.SetCashFlow(item.Id);
                        }
                    }

                    await _categoryRepository.AddAsync(category, cancellationToken);
                    await _categoryRepository.SaveChangesAsync(cancellationToken);
                    existing.Add(category);
                    imported++;
                }
                catch (Exception ex) when (ex is ArgumentException or JournalException)
                {
                    warnings.Add($"第 {lineNo} 行：{ex.Message}已跳过。");
                    skipped++;
                }
            }

            _ = accountsById;
            return new EntryCategoryImportResult
            {
                TotalRows = rows.Count,
                Imported = imported,
                Skipped = skipped,
                Warnings = warnings,
            };
        }

        /* ---------------- 内部工具 ---------------- */

        /// <summary>下一个编码：取同类型现有纯数字编码的最大值 + 1（无数字编码时从 1 开始）。</summary>
        private static string NextCode(IReadOnlyList<EntryCategory> existing, EntryCategoryType type)
        {
            var max = 0;
            foreach (var category in existing)
            {
                if (category.Type == type
                    && int.TryParse(category.Code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    && value > max)
                {
                    max = value;
                }
            }

            return (max + 1).ToString(CultureInfo.InvariantCulture);
        }

        private async Task<EntryCategoryDto> ToDtoAsync(
            Guid accountSetId,
            EntryCategory category,
            IReadOnlyDictionary<Guid, Account> accountsById,
            CancellationToken cancellationToken)
        {
            string? parentName = null;
            if (category.ParentId is { } parentId)
            {
                parentName = (await _categoryRepository.GetByIdAsync(accountSetId, parentId, cancellationToken))?.Name;
            }

            string? cashFlowName = null;
            if (category.CashFlowItemId is { } cashFlowId)
            {
                cashFlowName = (await _assistItemRepository.GetByIdAsync(accountSetId, cashFlowId, cancellationToken))?.Name;
            }

            return EntryCategoryDto.FromDomain(
                category,
                category.VoucherSubjectId is { } sid ? accountsById.GetValueOrDefault(sid) : null,
                parentName,
                cashFlowName);
        }

        private async Task<IReadOnlyDictionary<Guid, string>> LoadCashFlowNamesAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var items = await _assistItemRepository.GetByTypeAsync(accountSetId, AssistType.CashFlow, cancellationToken);
            return items.ToDictionary(i => i.Id, i => i.Name);
        }

        private async Task<IReadOnlyList<AssistItem>> LoadCashFlowItemsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            return await _assistItemRepository.GetByTypeAsync(accountSetId, AssistType.CashFlow, cancellationToken);
        }

        /// <summary>校验关联现金流项目：须为账套内「现金流」类别的档案。</summary>
        private async Task ValidateCashFlowAsync(Guid accountSetId, Guid itemId, CancellationToken cancellationToken)
        {
            var item = await _assistItemRepository.GetByIdAsync(accountSetId, itemId, cancellationToken);
            if (item is null || item.Type != AssistType.CashFlow)
            {
                throw new JournalException("关联现金流的项目不存在。");
            }
        }

        /// <summary>校验上级类别：须存在、同类型、且不是自己。</summary>
        private static void ValidateParent(IReadOnlyList<EntryCategory> existing, Guid parentId, EntryCategoryType type, Guid selfId)
        {
            if (parentId == selfId)
            {
                throw new JournalException("上级类别不能是自己。");
            }

            var parent = existing.FirstOrDefault(c => c.Id == parentId)
                         ?? throw new JournalException("上级类别不存在。");
            if (parent.Type != type)
            {
                throw new JournalException("上级类别与当前类别类型不一致。");
            }
        }

        private async Task EnsureAccountSetExists(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new JournalException("指定的账套不存在。");
            }
        }

        /// <summary>校验默认对方科目：须为账套内已启用的末级科目。</summary>
        private static void ValidateSubject(IReadOnlyDictionary<Guid, Account> accountsById, Guid subjectId)
        {
            if (!accountsById.TryGetValue(subjectId, out var account))
            {
                throw new JournalException("默认对方科目不存在。");
            }

            if (!account.IsLeaf)
            {
                throw new JournalException($"默认对方科目「{account.Code} {account.Name}」不是末级科目。");
            }

            if (!account.IsEnabled)
            {
                throw new JournalException($"默认对方科目「{account.Code} {account.Name}」已停用。");
            }
        }

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsById(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }

    /// <summary>导入时的一行收支类别（由 Excel 解析得到）。</summary>
    public sealed class EntryCategoryImportRow
    {
        public string? TypeText { get; init; }

        public string? Code { get; init; }

        public string? Name { get; init; }

        public string? ParentName { get; init; }

        public string? CashFlowName { get; init; }

        public string? SmartKeywords { get; init; }
    }
}
