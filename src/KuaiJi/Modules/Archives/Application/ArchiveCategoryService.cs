using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Archives.Domain;

namespace KuaiJi.Modules.Archives.Application
{
    /// <summary>
    /// 电子档案分类树应用服务：按账套维护一棵可任意层级的目录树。
    /// 首次读取时播种内置节点（所有文件 → 七类、数据备份），内置节点不可删改但可挂子节点。
    /// 档案的 Category 存节点全路径，重命名节点时同步改写档案路径，避免文件与目录失联。
    /// </summary>
    public sealed class ArchiveCategoryService
    {
        /// <summary>内置根节点：表示全部档案。</summary>
        public const string AllFilesName = "所有文件";

        /// <summary>内置根节点：数据备份（只读占位）。</summary>
        public const string BackupName = "数据备份";

        /// <summary>「所有文件」下的七个内置分类。</summary>
        public static readonly string[] BuiltInFolders =
        [
            "凭证附件", "回单", "发票", "现金流水", "票据", "工资", "资产",
        ];

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IArchiveCategoryRepository _categoryRepository;
        private readonly IArchiveRepository _archiveRepository;

        public ArchiveCategoryService(
            IAccountSetRepository accountSetRepository,
            IArchiveCategoryRepository categoryRepository,
            IArchiveRepository archiveRepository)
        {
            _accountSetRepository = accountSetRepository;
            _categoryRepository = categoryRepository;
            _archiveRepository = archiveRepository;
        }

        /// <summary>获取账套下全部节点；空账套自动播种内置分类。</summary>
        public async Task<IReadOnlyList<ArchiveCategoryDto>> GetListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _categoryRepository.GetListAsync(accountSetId, cancellationToken);
            if (list.Count == 0)
            {
                await SeedBuiltInAsync(accountSetId, cancellationToken);
                list = await _categoryRepository.GetListAsync(accountSetId, cancellationToken);
            }

            return list.Select(ArchiveCategoryDto.FromDomain).ToList();
        }

        /// <summary>在指定节点下新增子分类（同级不允许重名）。</summary>
        public async Task<ArchiveCategoryDto> CreateAsync(Guid accountSetId, SaveArchiveCategoryRequest request, CancellationToken cancellationToken = default)
        {
            await EnsureAccountSetExistsAsync(accountSetId, cancellationToken);
            var list = await _categoryRepository.GetListAsync(accountSetId, cancellationToken);
            if (list.Count == 0)
            {
                // 先补齐内置节点，避免新增的一级分类被误判成根节点集合
                await GetListAsync(accountSetId, cancellationToken);
                list = await _categoryRepository.GetListAsync(accountSetId, cancellationToken);
            }

            if (request.ParentId is { } parentId && list.All(c => c.Id != parentId))
            {
                throw new ArchiveException("上级分类不存在。");
            }

            var name = request.Name?.Trim() ?? string.Empty;
            if (list.Any(c => c.ParentId == request.ParentId && c.Name == name))
            {
                throw new ArchiveException("同级下已存在同名分类。");
            }

            var sortOrder = list.Where(c => c.ParentId == request.ParentId)
                .Select(c => c.SortOrder)
                .DefaultIfEmpty(0)
                .Max() + 1;

            ArchiveCategory node;
            try
            {
                node = ArchiveCategory.Create(accountSetId, request.ParentId, name, sortOrder);
            }
            catch (ArgumentException ex)
            {
                throw new ArchiveException(ex.Message);
            }

            await _categoryRepository.AddAsync(node, cancellationToken);
            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return ArchiveCategoryDto.FromDomain(node);
        }

        /// <summary>重命名自建分类，并同步该节点及其后代路径下的档案分类。</summary>
        public async Task<ArchiveCategoryDto?> RenameAsync(Guid accountSetId, Guid id, SaveArchiveCategoryRequest request, CancellationToken cancellationToken = default)
        {
            var node = await _categoryRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (node is null)
            {
                return null;
            }

            if (node.IsSystem)
            {
                throw new ArchiveException("内置分类不能重命名。");
            }

            var list = await _categoryRepository.GetListAsync(accountSetId, cancellationToken);
            var name = request.Name?.Trim() ?? string.Empty;
            if (list.Any(c => c.Id != id && c.ParentId == node.ParentId && c.Name == name))
            {
                throw new ArchiveException("同级下已存在同名分类。");
            }

            var oldPath = BuildPath(list, id);
            try
            {
                node.Rename(name);
            }
            catch (ArgumentException ex)
            {
                throw new ArchiveException(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                throw new ArchiveException(ex.Message);
            }

            var newPath = BuildPath(list, id, name);
            if (oldPath != newPath)
            {
                await _archiveRepository.ReplaceCategoryPathAsync(accountSetId, oldPath, newPath, cancellationToken);
            }

            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return ArchiveCategoryDto.FromDomain(node);
        }

        /// <summary>删除自建分类；存在子节点或已挂档案时拒绝。</summary>
        public async Task<bool> DeleteAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var node = await _categoryRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (node is null)
            {
                return false;
            }

            if (node.IsSystem)
            {
                throw new ArchiveException("内置分类不能删除。");
            }

            if (await _categoryRepository.HasChildrenAsync(accountSetId, id, cancellationToken))
            {
                throw new ArchiveException("请先删除该分类下的子分类。");
            }

            var list = await _categoryRepository.GetListAsync(accountSetId, cancellationToken);
            var path = BuildPath(list, id);
            if (await _archiveRepository.AnyInCategoryAsync(accountSetId, path, node.Name, cancellationToken))
            {
                throw new ArchiveException("该分类下已有档案，不能删除。");
            }

            _categoryRepository.Remove(node);
            await _categoryRepository.SaveChangesAsync(cancellationToken);
            return true;
        }

        /// <summary>播种内置目录：所有文件（根）→ 七类；另有独立的「数据备份」根节点。</summary>
        private async Task SeedBuiltInAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var allFiles = ArchiveCategory.Create(accountSetId, null, AllFilesName, 0, isSystem: true);
            var nodes = new List<ArchiveCategory> { allFiles };
            for (var i = 0; i < BuiltInFolders.Length; i++)
            {
                nodes.Add(ArchiveCategory.Create(accountSetId, allFiles.Id, BuiltInFolders[i], i + 1, isSystem: true));
            }

            nodes.Add(ArchiveCategory.Create(accountSetId, null, BackupName, BuiltInFolders.Length + 1, isSystem: true));
            await _categoryRepository.AddRangeAsync(nodes, cancellationToken);
            await _categoryRepository.SaveChangesAsync(cancellationToken);
        }

        /// <summary>由扁平节点列表向上拼出全路径；overrideName 用于重命名时预览新路径。</summary>
        private static string BuildPath(IReadOnlyList<ArchiveCategory> list, Guid id, string? overrideName = null)
        {
            var map = list.ToDictionary(c => c.Id);
            var parts = new List<string>();
            Guid? current = id;
            var guard = 0;
            while (current is { } currentId && map.TryGetValue(currentId, out var node) && guard++ < 64)
            {
                parts.Add(parts.Count == 0 && overrideName is not null ? overrideName : node.Name);
                current = node.ParentId;
            }

            parts.Reverse();
            return string.Join(ArchiveCategory.PathSeparator, parts);
        }

        private async Task EnsureAccountSetExistsAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            if (accountSetId == Guid.Empty || await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken) is null)
            {
                throw new ArchiveException("指定的账套不存在。");
            }
        }
    }
}
