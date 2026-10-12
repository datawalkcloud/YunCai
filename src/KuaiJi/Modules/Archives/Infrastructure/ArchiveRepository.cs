using KuaiJi.Modules.Archives.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Archives.Infrastructure
{
    /// <summary>会计电子档案仓储实现（列表不含内容字节，下载单独加载）。</summary>
    public class ArchiveRepository : IArchiveRepository
    {
        private readonly AppDbContext _context;

        public ArchiveRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<ArchiveItem?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.ArchiveItems
                .FirstOrDefaultAsync(a => a.AccountSetId == accountSetId && a.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<ArchiveItem>> GetListAsync(Guid accountSetId, bool archived, CancellationToken cancellationToken = default)
        {
            var list = await _context.ArchiveItems.AsNoTracking()
                .Where(a => a.AccountSetId == accountSetId && a.IsArchived == archived)
                .OrderByDescending(a => a.UploadedAt)
                .ToListAsync(cancellationToken);
            return list;
        }

        public async Task<byte[]?> GetContentAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            var item = await _context.ArchiveItems.AsNoTracking()
                .Where(a => a.AccountSetId == accountSetId && a.Id == id)
                .Select(a => a.Content)
                .FirstOrDefaultAsync(cancellationToken);
            return item;
        }

        public Task<long> GetTotalSizeAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return _context.ArchiveItems.AsNoTracking()
                .Where(a => a.AccountSetId == accountSetId)
                .SumAsync(a => (long?)a.FileSize, cancellationToken)
                .ContinueWith(t => t.Result ?? 0L, cancellationToken);
        }

        public async Task AddAsync(ArchiveItem item, CancellationToken cancellationToken = default)
        {
            await _context.ArchiveItems.AddAsync(item, cancellationToken);
        }

        public void Remove(ArchiveItem item)
        {
            _context.ArchiveItems.Remove(item);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
