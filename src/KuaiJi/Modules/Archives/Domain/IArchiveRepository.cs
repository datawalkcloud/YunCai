namespace KuaiJi.Modules.Archives.Domain
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

        /// <summary>新增档案。</summary>
        Task AddAsync(ArchiveItem item, CancellationToken cancellationToken = default);

        /// <summary>删除档案。</summary>
        void Remove(ArchiveItem item);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
