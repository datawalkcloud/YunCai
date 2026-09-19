namespace KuaiJi.Domain.Journals
{
    /// <summary>
    /// 收支类别仓储。以 <see cref="EntryCategory"/> 为单位持久化访问，按账套隔离。
    /// </summary>
    public interface IEntryCategoryRepository
    {
        /// <summary>按标识获取类别（限定账套）。</summary>
        Task<EntryCategory?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部类别（按类型、名称排列）。</summary>
        Task<IReadOnlyList<EntryCategory>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>判断同一账套同一类型下是否已存在同名类别（可排除指定标识，供改名校验）。</summary>
        Task<bool> ExistsByNameAsync(Guid accountSetId, EntryCategoryType type, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>新增类别。</summary>
        Task AddAsync(EntryCategory category, CancellationToken cancellationToken = default);

        /// <summary>删除类别。</summary>
        void Remove(EntryCategory category);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
