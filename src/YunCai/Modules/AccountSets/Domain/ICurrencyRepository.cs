namespace YunCai.Modules.AccountSets.Domain
{
    /// <summary>币别仓储。按账套隔离。</summary>
    public interface ICurrencyRepository
    {
        /// <summary>按标识获取币别（限定账套）。</summary>
        Task<Currency?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部币别（本位币优先）。</summary>
        Task<IReadOnlyList<Currency>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>判断同一账套内是否已存在同代码币别（可排除指定标识）。</summary>
        Task<bool> ExistsByCodeAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>新增币别。</summary>
        Task AddAsync(Currency currency, CancellationToken cancellationToken = default);

        /// <summary>删除币别。</summary>
        void Remove(Currency currency);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
