using KuaiJi.Domain.AccountSets;
using KuaiJi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Infrastructure.AccountSets
{
    /// <summary>基于 EF Core 的账套仓储实现。以聚合根 <see cref="AccountSet"/> 为单位读写。</summary>
    public class AccountSetRepository : IAccountSetRepository
    {
        private readonly AppDbContext _context;

        public AccountSetRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<AccountSet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.AccountSets.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        }

        public Task<AccountSet?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return _context.AccountSets.SingleOrDefaultAsync(a => a.Name == name, cancellationToken);
        }

        public async Task<IReadOnlyList<AccountSet>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.AccountSets
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<AccountSet?> GetCurrentAsync(CancellationToken cancellationToken = default)
        {
            return _context.AccountSets.SingleOrDefaultAsync(a => a.IsCurrent, cancellationToken);
        }

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return _context.AccountSets.AnyAsync(a => a.Name == name, cancellationToken);
        }

        public async Task AddAsync(AccountSet accountSet, CancellationToken cancellationToken = default)
        {
            await _context.AccountSets.AddAsync(accountSet, cancellationToken);
        }

        public void Remove(AccountSet accountSet)
        {
            _context.AccountSets.Remove(accountSet);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
