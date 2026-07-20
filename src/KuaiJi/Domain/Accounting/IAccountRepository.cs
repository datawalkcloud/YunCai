namespace KuaiJi.Domain.Accounting
{
    /// <summary>
    /// 会计科目仓储。以聚合根 <see cref="Account"/> 为单位进行持久化访问。
    /// </summary>
    public interface IAccountRepository
    {
        /// <summary>按标识获取科目。</summary>
        Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>按科目编码获取科目。</summary>
        Task<Account?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>获取全部科目（用于在应用层组装科目树）。</summary>
        Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>获取指定上级下的直接下级科目；parentId 为 null 时返回顶级科目。</summary>
        Task<IReadOnlyList<Account>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken = default);

        /// <summary>判断科目编码是否已存在。</summary>
        Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>判断科目是否存在下级明细科目。</summary>
        Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>新增科目。</summary>
        Task AddAsync(Account account, CancellationToken cancellationToken = default);

        /// <summary>删除科目（应用层需先校验非预置且无下级）。</summary>
        void Remove(Account account);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
