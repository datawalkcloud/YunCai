using YunCai.Modules.Vouchers.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Vouchers.Infrastructure
{
    /// <summary>基于 EF Core 的记账凭证仓储实现。以聚合根 <see cref="Voucher"/> 为单位读写（含分录）。</summary>
    public class VoucherRepository : IVoucherRepository
    {
        private readonly AppDbContext _context;

        public VoucherRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Voucher?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Vouchers
                .Include(v => v.Entries)
                .ThenInclude(e => e.Assists)
                .SingleOrDefaultAsync(v => v.AccountSetId == accountSetId && v.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Voucher>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.Vouchers
                .AsNoTracking()
                .Include(v => v.Entries)
                .ThenInclude(e => e.Assists)
                .Where(v => v.AccountSetId == accountSetId)
                .OrderBy(v => v.Date)
                .ThenBy(v => v.Word)
                .ThenBy(v => v.Number)
                .ToListAsync(cancellationToken);
        }

        public Task<bool> ExistsByWordAndNumberAsync(Guid accountSetId, string word, int number, CancellationToken cancellationToken = default)
        {
            return _context.Vouchers.AnyAsync(v => v.AccountSetId == accountSetId && v.Word == word && v.Number == number && v.DeletedAt == null, cancellationToken);
        }

        public Task<bool> ExistsByWordNumberDateAsync(Guid accountSetId, string word, int number, DateOnly date, CancellationToken cancellationToken = default)
        {
            return _context.Vouchers.AnyAsync(
                v => v.AccountSetId == accountSetId && v.Word == word && v.Number == number && v.Date == date && v.DeletedAt == null,
                cancellationToken);
        }

        public Task<Voucher?> GetByWordNumberDateAsync(Guid accountSetId, string word, int number, DateOnly date, CancellationToken cancellationToken = default)
        {
            return _context.Vouchers
                .Include(v => v.Entries)
                .FirstOrDefaultAsync(
                    v => v.AccountSetId == accountSetId && v.Word == word && v.Number == number && v.Date == date && v.DeletedAt == null,
                    cancellationToken);
        }

        public async Task<int> GetMaxNumberAsync(Guid accountSetId, string word, CancellationToken cancellationToken = default)
        {
            var query = _context.Vouchers.Where(v => v.AccountSetId == accountSetId && v.Word == word && v.DeletedAt == null);
            return await query.AnyAsync(cancellationToken)
                ? await query.MaxAsync(v => v.Number, cancellationToken)
                : 0;
        }

        public async Task AddAsync(Voucher voucher, CancellationToken cancellationToken = default)
        {
            await _context.Vouchers.AddAsync(voucher, cancellationToken);
        }

        public void Remove(Voucher voucher)
        {
            _context.Vouchers.Remove(voucher);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
