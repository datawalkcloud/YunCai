namespace YunCai.Modules.Archives.Domain
{
    /// <summary>会计电子档案仓储。按账套隔离。</summary>
    public interface IArchiveRepository
    {
        /// <summary>按标识获取档案（限定账套）。</summary>
        Task<ArchiveItem?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>按归档状态获取档案列表（按上传时间倒序；content 不加载，仅元数据投影）。</summary>
        Task<IReadOnlyList<ArchiveItem>> GetListAsync(Guid accountSetId, bool archived, CancellationToken cancellationToken = default);

        /// <summary>获取档案文件内容。</summary>
        Task<byte[]?> GetContentAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>统计已用空间（字节）。</summary>
        Task<long> GetTotalSizeAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>是否存在挂在指定分类路径（含其子树）下的档案；leafName 用于兼容早期只存单层名称的历史数据。</summary>
        Task<bool> AnyInCategoryAsync(Guid accountSetId, string path, string leafName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 把档案分类路径中的 oldPath 前缀替换为 newPath（分类重命名时同步）。
        /// 只更新受跟踪实体、不提交，由调用方与分类改动一起 SaveChanges。
        /// </summary>
        Task<int> ReplaceCategoryPathAsync(Guid accountSetId, string oldPath, string newPath, CancellationToken cancellationToken = default);

        /// <summary>新增档案。</summary>
        Task AddAsync(ArchiveItem item, CancellationToken cancellationToken = default);

        /// <summary>删除档案。</summary>
        void Remove(ArchiveItem item);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
