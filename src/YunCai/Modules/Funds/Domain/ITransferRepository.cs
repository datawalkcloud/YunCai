namespace YunCai.Modules.Funds.Domain
{
    /// <summary>
    /// 内部转账仓储。以聚合根 <see cref="Transfer"/> 为单位持久化访问。
    /// </summary>
    public interface ITransferRepository
    {
        /// <summary>按标识获取转账（限定账套）。</summary>
        Task<Transfer?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部转账（按日期与创建顺序排列）。</summary>
        Task<IReadOnlyList<Transfer>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>新增转账。</summary>
        Task AddAsync(Transfer transfer, CancellationToken cancellationToken = default);

        /// <summary>删除转账。</summary>
        void Remove(Transfer transfer);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
