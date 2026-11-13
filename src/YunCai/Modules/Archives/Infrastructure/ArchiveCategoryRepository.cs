using YunCai.Modules.Archives.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Archives.Infrastructure
{
    /// <summary>电子档案分类树仓储实现。</summary>
    public class ArchiveCategoryRepository : IArchiveCategoryRepository
    {
        private readonly AppDbContext _context;

        public ArchiveCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<ArchiveCategory>> GetListAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.ArchiveCategories.AsNoTracking()
                .Where(c => c.AccountSetId == accountSetId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public Task<ArchiveCategory?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.ArchiveCategories
                .FirstOrDefaultAsync(c => c.AccountSetId == accountSetId && c.Id == id, cancellationToken);
        }

        public Task<bool> HasChildrenAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.ArchiveCategories.AsNoTracking()
                .AnyAsync(c => c.AccountSetId == accountSetId && c.ParentId == id, cancellationToken);
        }

        public async Task AddRangeAsync(IEnumerable<ArchiveCategory> categories, CancellationToken cancellationToken = default)
        {
            await _context.ArchiveCategories.AddRangeAsync(categories, cancellationToken);
        }

        public async Task AddAsync(ArchiveCategory category, CancellationToken cancellationToken = default)
        {
            await _context.ArchiveCategories.AddAsync(category, cancellationToken);
        }

        public void Remove(ArchiveCategory category)
        {
            _context.ArchiveCategories.Remove(category);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
