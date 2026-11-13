namespace YunCai.Modules.Auditing.Domain
{
    /// <summary>操作日志仓储（只增查）。按账套隔离。</summary>
    public interface IOperationLogRepository
    {
        /// <summary>按条件查询日志（时间倒序，支持日期区间 / 用户 / 关键字）。</summary>
        Task<IReadOnlyList<OperationLog>> QueryAsync(
            Guid accountSetId,
            DateTime? start = null,
            DateTime? end = null,
            string? userId = null,
            string? keyword = null,
            CancellationToken cancellationToken = default);

        /// <summary>写入日志。</summary>
        Task AddAsync(OperationLog log, CancellationToken cancellationToken = default);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
