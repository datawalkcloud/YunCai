using KuaiJi.Modules.AccountSets.Domain;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>账套成员仓储：成员列表、增删改与保存。</summary>
    public interface IAccountSetMemberRepository
    {
        Task<IReadOnlyList<AccountSetMember>> GetMembersAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        Task<AccountSetMember?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>账套下是否有成员使用该角色。</summary>
        Task<bool> AnyWithRoleAsync(Guid accountSetId, Guid roleId, CancellationToken cancellationToken = default);

        Task AddAsync(AccountSetMember member, CancellationToken cancellationToken = default);

        void Remove(AccountSetMember member);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
