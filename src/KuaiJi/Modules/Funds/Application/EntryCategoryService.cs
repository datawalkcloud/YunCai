using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Funds.Domain;

namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>
    /// 收支类别应用服务：面向「收支类别」页的列表、新建、修改、删除。
    /// 类别归属于账套，同一账套同一类型内名称唯一；日记账分录按类别名称关联，
    /// 改名/删除类别不回写既有流水。类别可附带智能匹配关键字（导入自动归类）
    /// 与默认对方科目（由流水生成凭证时使用）。
    /// </summary>
    public sealed class EntryCategoryService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IEntryCategoryRepository _categoryRepository;
        private readonly IAccountRepository _accountRepository;

        public EntryCategoryService(
            IAccountSetRepository accountSetRepository,
            IEntryCategoryRepository categoryRepository,
            IAccountRepository accountRepository)
        {
            _accountSetRepository = accountSetRepository;
            _categoryRepository = categoryRepository;
            _accountRepository = accountRepository;
        }

        /// <summary>获取指定账套的全部收支类别（按类型、名称排列，并解析默认对方科目）。</summary>
        public async Task<IReadOnlyList<EntryCategoryDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var categories = await _categoryRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = await LoadAccountsById(accountSetId, cancellationToken);
            return categories
                .Select(c => EntryCategoryDto.FromDomain(
                    c,
                    c.VoucherSubjectId is { } sid ? accountsById.GetValueOrDefault(sid) : null))
                .ToList();
        }

        /// <summary>新建收支类别。同一账套同一类型内名称不可重复。</summary>
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

            EntryCategory category;
            try
            {
                category = EntryCategory.Create(accountSetId, type, name);
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

            await _categoryRepository.AddAsync(category, cancellationToken);
            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return EntryCategoryDto.FromDomain(category, ResolveSubject(accountsById, category));
        }

        /// <summary>
        /// 修改类别：名称必填；智能匹配关键字与默认对方科目按三态处理
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

            var name = request.Name?.Trim() ?? string.Empty;
            if (await _categoryRepository.ExistsByNameAsync(accountSetId, category.Type, name, category.Id, cancellationToken))
            {
                throw new JournalException($"{EntryCategoryDto.TypeTextOf(category.Type)}类别「{name}」已存在。");
            }

            try
            {
                category.Rename(name);
                if (request.SmartKeywords is not null)
                {
                    category.SetSmartKeywords(request.SmartKeywords);
                }
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
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
            return EntryCategoryDto.FromDomain(category, ResolveSubject(accountsById, category));
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

        private static Account? ResolveSubject(IReadOnlyDictionary<Guid, Account> accountsById, EntryCategory category) =>
            category.VoucherSubjectId is { } sid ? accountsById.GetValueOrDefault(sid) : null;

        private async Task<IReadOnlyDictionary<Guid, Account>> LoadAccountsById(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            return accounts.ToDictionary(a => a.Id, a => a);
        }
    }
}
