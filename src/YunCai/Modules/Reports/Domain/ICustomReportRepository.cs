namespace YunCai.Modules.Reports.Domain
{
    /// <summary>自定义报表（数据透视表）仓储。</summary>
    public interface ICustomReportRepository
    {
        /// <summary>获取账套内全部自定义报表定义（按编码排序）。</summary>
        Task<IReadOnlyList<CustomReport>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>按标识获取（限定账套）。</summary>
        Task<CustomReport?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>判断同一账套内该编码是否已被其他报表占用。</summary>
        Task<bool> CodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>新增报表定义。</summary>
        Task AddAsync(CustomReport report, CancellationToken cancellationToken = default);

        /// <summary>删除报表定义。</summary>
        void Remove(CustomReport report);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
