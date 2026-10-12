using KuaiJi.Modules.Funds.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Funds.Infrastructure
{
    /// <summary>基于 EF Core 的收支类别仓储实现，按账套隔离。</summary>
    public class EntryCategoryRepository : IEntryCategoryRepository
    {
        private readonly AppDbContext _context;

        public EntryCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<EntryCategory?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.EntryCategories
                .SingleOrDefaultAsync(c => c.AccountSetId == accountSetId && c.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<EntryCategory>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.EntryCategories
                .AsNoTracking()
                .Where(c => c.AccountSetId == accountSetId)
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<bool> ExistsByNameAsync(Guid accountSetId, EntryCategoryType type, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.EntryCategories.AnyAsync(
                c => c.AccountSetId == accountSetId
                     && c.Type == type
                     && c.Name == name
                     && (excludeId == null || c.Id != excludeId),
                cancellationToken);
        }

        public async Task AddAsync(EntryCategory category, CancellationToken cancellationToken = default)
        {
            await _context.EntryCategories.AddAsync(category, cancellationToken);
        }

        public void Remove(EntryCategory category)
        {
            _context.EntryCategories.Remove(category);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
