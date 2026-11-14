using YunCai.Modules.AccountSets.Domain;

namespace YunCai.Modules.AccountSets.Application
{
    /// <summary>登录用户在某个账套内的角色与权限键。</summary>
    public sealed record AccountSetAccess(Guid RoleId, string RoleName, bool IsAdmin, IReadOnlyList<string> Permissions);

    /// <summary>账套成员仓储：成员列表、增删改与保存。</summary>
    public interface IAccountSetMemberRepository
    {
        Task<IReadOnlyList<AccountSetMember>> GetMembersAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        Task<AccountSetMember?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>账套下是否有成员使用该角色。</summary>
        Task<bool> AnyWithRoleAsync(Guid accountSetId, Guid roleId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 判断登录用户是否为该账套的启用成员：优先按登录用户标识匹配，
        /// 其次按成员手机号匹配（受邀但尚未绑定账号的成员）。
        /// </summary>
        Task<bool> IsEnabledMemberAsync(Guid accountSetId, string userId, string? phone, CancellationToken cancellationToken = default);

        /// <summary>该登录用户可访问的账套标识集合（仅启用成员）。</summary>
        Task<IReadOnlySet<Guid>> GetAccountSetIdsAsync(string userId, string? phone, CancellationToken cancellationToken = default);

        /// <summary>该登录用户在该账套中的角色是否为账套管理员；不是成员或未启用时返回 false。</summary>
        Task<bool> IsAdminAsync(Guid accountSetId, string userId, string? phone, CancellationToken cancellationToken = default);

        /// <summary>
        /// 当前登录用户在该账套的访问权限；不是启用成员时返回 null。
        /// 账套管理员直接返回全部权限键。
        /// </summary>
        Task<AccountSetAccess?> GetAccessAsync(Guid accountSetId, string userId, string? phone, CancellationToken cancellationToken = default);

        Task AddAsync(AccountSetMember member, CancellationToken cancellationToken = default);

        void Remove(AccountSetMember member);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
