using KuaiJi.Modules.Funds.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Funds.Infrastructure
{
    /// <summary>基于 EF Core 的日记账仓储实现。以聚合根 <see cref="Journal"/> 为单位读写（含分录），按账套隔离。</summary>
    public class JournalRepository : IJournalRepository
    {
    
        /// <summary>获取资金类（现金 + 银行存款）日记账在日期区间内的分录，按日期升序。</summary>
        public async Task<IReadOnlyList<(Journal Journal, JournalEntry Entry)>> GetFundEntriesRangeAsync(
            Guid accountSetId,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default)
        {
            var journals = await _context.Journals.AsNoTracking()
                .Where(j => j.AccountSetId == accountSetId)
                .Include(j => j.Entries)
                .ToListAsync(cancellationToken);
            var result = new List<(Journal Journal, JournalEntry Entry)>();
            foreach (var journal in journals)
            {
                foreach (var entry in journal.Entries)
                {
                    if (startDate is { } st && entry.Date < st) continue;
                    if (endDate is { } en && entry.Date > en) continue;
                    result.Add((journal, entry));
                }
            }

            return result.OrderBy(x => x.Entry.Date).ToList();
        }
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

        public async Task<string> NextCodeAsync(Guid accountSetId, JournalType type, CancellationToken cancellationToken = default)
        {
            var codes = await _context.Journals
                .AsNoTracking()
                .Where(j => j.AccountSetId == accountSetId && j.Type == type && !string.IsNullOrWhiteSpace(j.Code))
                .Select(j => j.Code)
                .ToListAsync(cancellationToken);

            var max = 0;
            foreach (var code in codes)
            {
                if (int.TryParse(code, out var n) && n > max)
                {
                    max = n;
                }
            }

            return (max + 1).ToString().PadLeft(3, '0');
        }

        public Task<bool> ExistsByCodeAsync(Guid accountSetId, JournalType type, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            var normalized = code.Trim();
            return _context.Journals
                .AsNoTracking()
                .AnyAsync(j => j.AccountSetId == accountSetId
                            && j.Type == type
                            && j.Code == normalized
                            && (excludeId == null || j.Id != excludeId), cancellationToken);
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
