using KuaiJi.Modules.Funds.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Funds.Infrastructure
{
    /// <summary>票据仓储实现。</summary>
    public class BillRepository : IBillRepository
    {
        private readonly AppDbContext _context;

        public BillRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Bill?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Bills
                .FirstOrDefaultAsync(b => b.AccountSetId == accountSetId && b.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<Bill>> GetRangeAsync(Guid accountSetId, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
        {
            var query = _context.Bills.AsNoTracking()
                .Where(b => b.AccountSetId == accountSetId);
            if (startDate is { } st)
            {
                query = query.Where(b => b.IssueDate >= st);
            }

            if (endDate is { } en)
            {
                query = query.Where(b => b.IssueDate <= en);
            }

            var list = await query.OrderByDescending(b => b.IssueDate).ToListAsync(cancellationToken);
            return list;
        }

        public Task<bool> ExistsByBillNoAsync(Guid accountSetId, string billNo, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.Bills
                .AnyAsync(b => b.AccountSetId == accountSetId && b.BillNo == billNo && (excludeId == null || b.Id != excludeId), cancellationToken);
        }

        public async Task AddAsync(Bill bill, CancellationToken cancellationToken = default)
        {
            await _context.Bills.AddAsync(bill, cancellationToken);
        }

        public void Remove(Bill bill)
        {
            _context.Bills.Remove(bill);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
