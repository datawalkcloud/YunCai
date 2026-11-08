namespace KuaiJi.Modules.Archives.Domain
{
    /// <summary>电子档案分类树仓储（按账套隔离，返回扁平节点列表由上层组树）。</summary>
    public interface IArchiveCategoryRepository
    {
        /// <summary>获取账套下全部节点。</summary>
        Task<IReadOnlyList<ArchiveCategory>> GetListAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>按标识获取节点（限定账套）。</summary>
        Task<ArchiveCategory?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>是否存在子节点。</summary>
        Task<bool> HasChildrenAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>批量新增节点（用于内置节点播种）。</summary>
        Task AddRangeAsync(IEnumerable<ArchiveCategory> categories, CancellationToken cancellationToken = default);

        /// <summary>新增单个节点。</summary>
        Task AddAsync(ArchiveCategory category, CancellationToken cancellationToken = default);

        /// <summary>删除节点。</summary>
        void Remove(ArchiveCategory category);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
