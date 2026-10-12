using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Assist.Infrastructure
{
    /// <summary>基于 EF Core 的辅助核算档案仓储实现。</summary>
    public class AssistItemRepository : IAssistItemRepository
    {
        private readonly AppDbContext _context;

        public AssistItemRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<AssistItem?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.AssistItems.SingleOrDefaultAsync(
                i => i.AccountSetId == accountSetId && i.Id == id, cancellationToken);
        }

        public Task<AssistItem?> GetByCodeAsync(Guid accountSetId, AssistType type, string code, CancellationToken cancellationToken = default)
        {
            return _context.AssistItems.SingleOrDefaultAsync(
                i => i.AccountSetId == accountSetId && i.Type == type && i.Code == code, cancellationToken);
        }

        public async Task<IReadOnlyList<AssistItem>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.AssistItems
                .AsNoTracking()
                .Where(i => i.AccountSetId == accountSetId)
                .OrderBy(i => i.Type)
                .ThenBy(i => i.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<AssistItem>> GetByTypeAsync(Guid accountSetId, AssistType type, CancellationToken cancellationToken = default)
        {
            return await _context.AssistItems
                .AsNoTracking()
                .Where(i => i.AccountSetId == accountSetId && i.Type == type)
                .OrderBy(i => i.Code)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyDictionary<AssistType, int>> CountByTypeAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var counts = await _context.AssistItems
                .Where(i => i.AccountSetId == accountSetId)
                .GroupBy(i => i.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            return counts.ToDictionary(c => c.Type, c => c.Count);
        }

        public Task<bool> ExistsByCodeAsync(Guid accountSetId, AssistType type, string code, CancellationToken cancellationToken = default)
        {
            return _context.AssistItems.AnyAsync(
                i => i.AccountSetId == accountSetId && i.Type == type && i.Code == code, cancellationToken);
        }

        public Task<bool> HasChildrenAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.AssistItems.AnyAsync(
                i => i.AccountSetId == accountSetId && i.ParentId == id, cancellationToken);
        }

        public async Task<int> CountVoucherReferencesAsync(Guid accountSetId, Guid assistItemId, CancellationToken cancellationToken = default)
        {
            // 分录不带账套字段，须经凭证限定，避免跨账套误判引用。
            var count = await (
                from assist in _context.VoucherEntryAssists
                join entry in _context.VoucherEntries on assist.VoucherEntryId equals entry.Id
                join voucher in _context.Vouchers on entry.VoucherId equals voucher.Id
                where voucher.AccountSetId == accountSetId && assist.AssistItemId == assistItemId
                select assist.Id).CountAsync(cancellationToken);
            return count;
        }

        public async Task AddAsync(AssistItem item, CancellationToken cancellationToken = default)
        {
            var entry = _context.Entry(item);
            if (entry.State != EntityState.Detached)
            {
                return;
            }

            await _context.AssistItems.AddAsync(item, cancellationToken);
        }

        public void Remove(AssistItem item)
        {
            _context.AssistItems.Remove(item);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
