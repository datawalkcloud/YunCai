using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.AccountSets.Infrastructure
{
    /// <summary>账套成员仓储实现（EF Core）。</summary>
    public class AccountSetMemberRepository : IAccountSetMemberRepository
    {
        private readonly AppDbContext _context;

        public AccountSetMemberRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<AccountSetMember>> GetMembersAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.AccountSetMembers
                .AsNoTracking()
                .Where(m => m.AccountSetId == accountSetId)
                .OrderByDescending(m => m.IsOwner)
                .ThenBy(m => m.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public Task<AccountSetMember?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.AccountSetMembers
                .FirstOrDefaultAsync(m => m.AccountSetId == accountSetId && m.Id == id, cancellationToken);
        }

        public Task<bool> AnyWithRoleAsync(Guid accountSetId, Guid roleId, CancellationToken cancellationToken = default)
        {
            return _context.AccountSetMembers
                .AsNoTracking()
                .AnyAsync(m => m.AccountSetId == accountSetId && m.RoleId == roleId, cancellationToken);
        }

        public Task<bool> IsEnabledMemberAsync(Guid accountSetId, string userId, string? phone, CancellationToken cancellationToken = default)
        {
            return VisibleMembers(accountSetId, userId, phone).AnyAsync(cancellationToken);
        }

        public async Task<IReadOnlySet<Guid>> GetAccountSetIdsAsync(string userId, string? phone, CancellationToken cancellationToken = default)
        {
            var ids = await _context.AccountSetMembers
                .AsNoTracking()
                .Where(m => m.Enabled
                            && (m.UserId == userId || (phone != null && phone != "" && m.Phone == phone)))
                .Select(m => m.AccountSetId)
                .Distinct()
                .ToListAsync(cancellationToken);
            return ids.ToHashSet();
        }

        public Task<bool> IsAdminAsync(Guid accountSetId, string userId, string? phone, CancellationToken cancellationToken = default)
        {
            return VisibleMembers(accountSetId, userId, phone)
                .Join(_context.AccountSetRoles.Where(r => r.IsAdmin),
                    m => m.RoleId,
                    r => r.Id,
                    (_, _) => true)
                .AnyAsync(cancellationToken);
        }

        public async Task<AccountSetAccess?> GetAccessAsync(Guid accountSetId, string userId, string? phone, CancellationToken cancellationToken = default)
        {
            var access = await VisibleMembers(accountSetId, userId, phone)
                .Join(_context.AccountSetRoles.AsNoTracking(),
                    m => m.RoleId,
                    r => r.Id,
                    (m, r) => new { r.Id, r.Name, r.IsAdmin, r.Permissions })
                .FirstOrDefaultAsync(cancellationToken);
            if (access is null)
            {
                return null;
            }

            var permissions = access.IsAdmin
                ? RolePermissionCatalog.AllKeys
                : AccountSetRole.Parse(access.Permissions);
            return new AccountSetAccess(access.Id, access.Name, access.IsAdmin, permissions);
        }

        private IQueryable<AccountSetMember> VisibleMembers(Guid accountSetId, string userId, string? phone)
        {
            return _context.AccountSetMembers
                .AsNoTracking()
                .Where(m => m.AccountSetId == accountSetId
                            && m.Enabled
                            && (m.UserId == userId || (phone != null && phone != "" && m.Phone == phone)));
        }

        public async Task AddAsync(AccountSetMember member, CancellationToken cancellationToken = default)
        {
            await _context.AccountSetMembers.AddAsync(member, cancellationToken);
        }

        public void Remove(AccountSetMember member) => _context.AccountSetMembers.Remove(member);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
    }
}
