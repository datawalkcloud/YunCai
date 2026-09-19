using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Journals;

namespace KuaiJi.Application.Journals
{
    /// <summary>
    /// 收支类别应用服务：面向「收支类别」页的列表、新建、改名、删除。
    /// 类别归属于账套，同一账套同一类型内名称唯一；日记账分录按类别名称关联，
    /// 改名/删除类别不回写既有流水。
    /// </summary>
    public sealed class EntryCategoryService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IEntryCategoryRepository _categoryRepository;

        public EntryCategoryService(
            IAccountSetRepository accountSetRepository,
            IEntryCategoryRepository categoryRepository)
        {
            _accountSetRepository = accountSetRepository;
            _categoryRepository = categoryRepository;
        }

        /// <summary>获取指定账套的全部收支类别（按类型、名称排列）。</summary>
        public async Task<IReadOnlyList<EntryCategoryDto>> ListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExists(accountSetId, cancellationToken);

            var categories = await _categoryRepository.GetAllAsync(accountSetId, cancellationToken);
            return categories.Select(EntryCategoryDto.FromDomain).ToList();
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
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _categoryRepository.AddAsync(category, cancellationToken);
            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return EntryCategoryDto.FromDomain(category);
        }

        /// <summary>修改类别名称；不存在时返回 null。既有流水中的类别文本不随之变更。</summary>
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
            }
            catch (ArgumentException ex)
            {
                throw new JournalException(ex.Message);
            }

            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return EntryCategoryDto.FromDomain(category);
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
    }
}
