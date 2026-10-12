using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.AccountSets.Infrastructure
{
    /// <summary>币别仓储实现（PostgreSQL / AppDbContext）。</summary>
    public class CurrencyRepository : ICurrencyRepository
    {
        private readonly AppDbContext _context;

        public CurrencyRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Currency?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Currencies
                .FirstOrDefaultAsync(c => c.AccountSetId == accountSetId && c.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Currency>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.Currencies.AsNoTracking()
                .Where(c => c.AccountSetId == accountSetId)
                .OrderByDescending(c => c.IsBase)
                .ThenBy(c => c.Code)
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<bool> ExistsByCodeAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.Currencies
                .AnyAsync(c => c.AccountSetId == accountSetId && c.Code == code && (excludeId == null || c.Id != excludeId), cancellationToken);
        }

        public async Task AddAsync(Currency currency, CancellationToken cancellationToken = default)
        {
            await _context.Currencies.AddAsync(currency, cancellationToken);
        }

        public void Remove(Currency currency)
        {
            _context.Currencies.Remove(currency);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
