using KuaiJi.Domain.Journals;
using KuaiJi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Infrastructure.Journals
{
    /// <summary>基于 EF Core 的日记账仓储实现。以聚合根 <see cref="Journal"/> 为单位读写（含分录），按账套隔离。</summary>
    public class JournalRepository : IJournalRepository
    {
        private readonly AppDbContext _context;

        public JournalRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Journal?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Journals
                .Include(j => j.Entries)
                .SingleOrDefaultAsync(j => j.AccountSetId == accountSetId && j.Id == id, cancellationToken);
        }

        public Task<Journal?> GetByAccountIdAsync(Guid accountSetId, Guid accountId, CancellationToken cancellationToken = default)
        {
            return _context.Journals
                .Include(j => j.Entries)
                .SingleOrDefaultAsync(j => j.AccountSetId == accountSetId && j.AccountId == accountId, cancellationToken);
        }

        public async Task<IReadOnlyList<Journal>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.Journals
                .AsNoTracking()
                .Include(j => j.Entries)
                .Where(j => j.AccountSetId == accountSetId)
                .OrderBy(j => j.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Journal journal, CancellationToken cancellationToken = default)
        {
            await _context.Journals.AddAsync(journal, cancellationToken);
        }

        public void Remove(Journal journal)
        {
            _context.Journals.Remove(journal);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
