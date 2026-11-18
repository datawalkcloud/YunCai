using YunCai.Modules.Expenses.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Expenses.Infrastructure
{
    /// <summary>费用单据仓储实现。</summary>
    public class ExpenseRepository : IExpenseRepository
    {
        private readonly AppDbContext _context;

        public ExpenseRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<IReadOnlyList<ExpenseBill>> GetBillsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
            => GetBillsInternalAsync(accountSetId, tracked: false, cancellationToken);

        public Task<IReadOnlyList<ExpenseBill>> GetBillsForUpdateAsync(Guid accountSetId, CancellationToken cancellationToken = default)
            => GetBillsInternalAsync(accountSetId, tracked: true, cancellationToken);

        private async Task<IReadOnlyList<ExpenseBill>> GetBillsInternalAsync(Guid accountSetId, bool tracked, CancellationToken cancellationToken)
        {
            var query = _context.ExpenseBills.Where(b => b.AccountSetId == accountSetId);
            if (!tracked)
            {
                query = query.AsNoTracking();
            }

            var list = await query
                .OrderByDescending(b => b.BillDate)
                .ThenBy(b => b.BillNo)
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<ExpenseBill?> GetBillByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.ExpenseBills.AsNoTracking()
                .FirstOrDefaultAsync(b => b.AccountSetId == accountSetId && b.Id == id, cancellationToken);
        }

        public Task<ExpenseBill?> GetBillForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.ExpenseBills
                .FirstOrDefaultAsync(b => b.AccountSetId == accountSetId && b.Id == id, cancellationToken);
        }

        public Task<bool> BillNoExistsAsync(Guid accountSetId, string billNo, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.ExpenseBills
                .AnyAsync(b => b.AccountSetId == accountSetId && b.BillNo == billNo && (excludeId == null || b.Id != excludeId), cancellationToken);
        }

        public async Task AddBillAsync(ExpenseBill bill, CancellationToken cancellationToken = default)
        {
            await _context.ExpenseBills.AddAsync(bill, cancellationToken);
        }

        public void RemoveBill(ExpenseBill bill)
        {
            _context.ExpenseBills.Remove(bill);
        }

        public async Task<IReadOnlyList<VoucherRef>> GetVoucherRefsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.Vouchers.AsNoTracking()
                .Where(v => v.AccountSetId == accountSetId && v.DeletedAt == null)
                .Select(v => new VoucherRef(v.Id, v.Word, v.Number))
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
