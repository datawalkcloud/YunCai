using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.AccountSets.Infrastructure
{
    /// <summary>账套角色仓储实现（EF Core）。</summary>
    public class AccountSetRoleRepository : IAccountSetRoleRepository
    {
        private readonly AppDbContext _context;

        public AccountSetRoleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<AccountSetRole>> GetListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.AccountSetRoles
                .AsNoTracking()
                .Where(r => r.AccountSetId == accountSetId)
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public Task<AccountSetRole?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.AccountSetRoles
                .FirstOrDefaultAsync(r => r.AccountSetId == accountSetId && r.Id == id, cancellationToken);
        }

        public Task<bool> AnyAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return _context.AccountSetRoles
                .AsNoTracking()
                .AnyAsync(r => r.AccountSetId == accountSetId, cancellationToken);
        }

        public async Task AddAsync(AccountSetRole role, CancellationToken cancellationToken = default)
        {
            await _context.AccountSetRoles.AddAsync(role, cancellationToken);
        }

        public async Task AddRangeAsync(IEnumerable<AccountSetRole> roles, CancellationToken cancellationToken = default)
        {
            await _context.AccountSetRoles.AddRangeAsync(roles, cancellationToken);
        }

        public void Remove(AccountSetRole role) => _context.AccountSetRoles.Remove(role);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _context.SaveChangesAsync(cancellationToken);
    }
}
