namespace YunCai.Modules.AccountSets.Domain
{
    /// <summary>
    /// 账套仓储。以聚合根 <see cref="AccountSet"/> 为单位持久化访问。
    /// </summary>
    public interface IAccountSetRepository
    {
        /// <summary>按标识获取账套。</summary>
        Task<AccountSet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>按名称获取账套。</summary>
        Task<AccountSet?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>获取全部账套。</summary>
        Task<IReadOnlyList<AccountSet>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>获取当前账套（无则返回 null）。</summary>
        Task<AccountSet?> GetCurrentAsync(CancellationToken cancellationToken = default);

        /// <summary>判断指定名称的账套是否已存在。</summary>
        Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>新增账套。</summary>
        Task AddAsync(AccountSet accountSet, CancellationToken cancellationToken = default);

        /// <summary>删除账套。</summary>
        void Remove(AccountSet accountSet);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
