namespace KuaiJi.Domain.Journals
{
    /// <summary>
    /// 日记账仓储。以聚合根 <see cref="Journal"/> 为单位持久化访问（含其分录）。
    /// </summary>
    public interface IJournalRepository
    {
        /// <summary>按标识获取日记账（含分录）。</summary>
        Task<Journal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>按对应科目获取日记账（含分录）。</summary>
        Task<Journal?> GetByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);

        /// <summary>获取全部日记账（不含分录，用于列表）。</summary>
        Task<IReadOnlyList<Journal>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>新增日记账。</summary>
        Task AddAsync(Journal journal, CancellationToken cancellationToken = default);

        /// <summary>删除日记账（含其全部分录）。</summary>
        void Remove(Journal journal);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
