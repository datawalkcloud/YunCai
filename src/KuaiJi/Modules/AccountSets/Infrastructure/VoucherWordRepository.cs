using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.AccountSets.Infrastructure
{
    /// <summary>凭证字仓储实现。</summary>
    public class VoucherWordRepository : IVoucherWordRepository
    {
        private readonly AppDbContext _context;

        public VoucherWordRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<VoucherWord?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.VoucherWords
                .FirstOrDefaultAsync(w => w.AccountSetId == accountSetId && w.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<VoucherWord>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.VoucherWords.AsNoTracking()
                .Where(w => w.AccountSetId == accountSetId)
                .OrderByDescending(w => w.IsDefault)
                .ThenBy(w => w.Word)
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<bool> ExistsByWordAsync(Guid accountSetId, string word, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.VoucherWords
                .AnyAsync(w => w.AccountSetId == accountSetId && w.Word == word && (excludeId == null || w.Id != excludeId), cancellationToken);
        }

        public async Task AddAsync(VoucherWord word, CancellationToken cancellationToken = default)
        {
            await _context.VoucherWords.AddAsync(word, cancellationToken);
        }

        public async Task ClearDefaultAsync(Guid accountSetId, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            // 必须加载为跟踪实体：默认标记的取消依赖下一次 SaveChanges 落库。
            var defaults = await _context.VoucherWords
                .Where(w => w.AccountSetId == accountSetId && w.IsDefault && (excludeId == null || w.Id != excludeId))
                .ToListAsync(cancellationToken);
            foreach (var item in defaults)
            {
                item.MarkNotDefault();
            }
        }

        public void Remove(VoucherWord word)
        {
            _context.VoucherWords.Remove(word);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
