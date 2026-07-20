namespace KuaiJi.Domain.Accounting
{
    /// <summary>
    /// 会计科目仓储。以聚合根 <see cref="Account"/> 为单位进行持久化访问。
    /// </summary>
    public interface IAccountRepository
    {
        /// <summary>按标识获取科目（限定账套）。</summary>
        Task<Account?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>按科目编码获取科目（限定账套）。</summary>
        Task<Account?> GetByCodeAsync(Guid accountSetId, string code, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部科目（用于在应用层组装科目树）。</summary>
        Task<IReadOnlyList<Account>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>获取指定上级下的直接下级科目（限定账套）；parentId 为 null 时返回顶级科目。</summary>
        Task<IReadOnlyList<Account>> GetChildrenAsync(Guid accountSetId, Guid? parentId, CancellationToken cancellationToken = default);

        /// <summary>判断科目编码在指定账套内是否已存在。</summary>
        Task<bool> ExistsByCodeAsync(Guid accountSetId, string code, CancellationToken cancellationToken = default);

        /// <summary>判断科目是否存在下级明细科目（限定账套）。</summary>
        Task<bool> HasChildrenAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>新增科目。</summary>
        Task AddAsync(Account account, CancellationToken cancellationToken = default);

        /// <summary>删除科目（应用层需先校验非预置且无下级）。</summary>
        void Remove(Account account);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
