namespace KuaiJi.Modules.Funds.Domain
{
    /// <summary>票据仓储。按账套隔离。</summary>
    public interface IBillRepository
    {
        /// <summary>按标识获取票据（限定账套）。</summary>
        Task<Bill?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>按日期区间获取票据（按出票日倒序）。</summary>
        Task<IReadOnlyList<Bill>> GetRangeAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default);

        /// <summary>判断同一账套内是否已存在同票据号（可排除指定标识）。</summary>
        Task<bool> ExistsByBillNoAsync(Guid accountSetId, string billNo, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>新增票据。</summary>
        Task AddAsync(Bill bill, CancellationToken cancellationToken = default);

        /// <summary>删除票据。</summary>
        void Remove(Bill bill);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
