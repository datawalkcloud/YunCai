using KuaiJi.Domain.Accounting;
using KuaiJi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Infrastructure.Accounting
{
    /// <summary>基于 EF Core 的会计科目仓储实现。</summary>
    public class AccountRepository : IAccountRepository
    {
        private readonly AppDbContext _context;

        public AccountRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Account?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Accounts.SingleOrDefaultAsync(a => a.AccountSetId == accountSetId && a.Id == id, cancellationToken);
        }

        public Task<Account?> GetByCodeAsync(Guid accountSetId, string code, CancellationToken cancellationToken = default)
        {
            return _context.Accounts.SingleOrDefaultAsync(a => a.AccountSetId == accountSetId && a.Code == code, cancellationToken);
        }

        public async Task<IReadOnlyList<Account>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.AccountSetId == accountSetId)
                .OrderBy(a => a.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Account>> GetChildrenAsync(Guid accountSetId, Guid? parentId, CancellationToken cancellationToken = default)
        {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.AccountSetId == accountSetId && a.ParentId == parentId)
                .OrderBy(a => a.Code)
                .ToListAsync(cancellationToken);
        }

        public Task<bool> ExistsByCodeAsync(Guid accountSetId, string code, CancellationToken cancellationToken = default)
        {
            return _context.Accounts.AnyAsync(a => a.AccountSetId == accountSetId && a.Code == code, cancellationToken);
        }

        public Task<bool> HasChildrenAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Accounts.AnyAsync(a => a.AccountSetId == accountSetId && a.ParentId == id, cancellationToken);
        }

        public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
        {
            await _context.Accounts.AddAsync(account, cancellationToken);
        }

        public void Remove(Account account)
        {
            _context.Accounts.Remove(account);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
