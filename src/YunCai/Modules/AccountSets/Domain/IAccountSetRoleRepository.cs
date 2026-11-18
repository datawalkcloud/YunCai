namespace YunCai.Modules.AccountSets.Domain
{
    /// <summary>账套角色仓储（按账套隔离）。</summary>
    public interface IAccountSetRoleRepository
    {
        /// <summary>获取账套下全部角色。</summary>
        Task<IReadOnlyList<AccountSetRole>> GetListAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>按标识获取角色（限定账套）。</summary>
        Task<AccountSetRole?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>账套下是否已有角色（用于预置角色播种判断）。</summary>
        Task<bool> AnyAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>新增单个角色。</summary>
        Task AddAsync(AccountSetRole role, CancellationToken cancellationToken = default);

        /// <summary>批量新增角色（预置播种）。</summary>
        Task AddRangeAsync(IEnumerable<AccountSetRole> roles, CancellationToken cancellationToken = default);

        /// <summary>删除角色。</summary>
        void Remove(AccountSetRole role);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
