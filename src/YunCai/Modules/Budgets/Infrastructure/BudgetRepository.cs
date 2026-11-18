using YunCai.Modules.Budgets.Domain;
using YunCai.Modules.Vouchers.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Budgets.Infrastructure
{
    /// <summary>预算单仓储：预算读写在预算模块内，执行数直接聚合已过账凭证分录。</summary>
    public class BudgetRepository : IBudgetRepository
    {
        private readonly AppDbContext _context;

        public BudgetRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Budget>> GetListAsync(Guid accountSetId, int? year,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Budgets.AsNoTracking()
                .Include(b => b.Lines)
                .Where(b => b.AccountSetId == accountSetId);
            if (year is { } target)
            {
                query = query.Where(b => b.Year == target);
            }

            return await query
                .OrderByDescending(b => b.Year)
                .ThenBy(b => b.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<Budget?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
            => _context.Budgets
                .Include(b => b.Lines)
                .FirstOrDefaultAsync(b => b.AccountSetId == accountSetId && b.Id == id, cancellationToken);

        public Task<bool> ExistsNameAsync(Guid accountSetId, int year, string name, Guid? excludeId,
            CancellationToken cancellationToken = default)
            => _context.Budgets.AsNoTracking()
                .AnyAsync(b => b.AccountSetId == accountSetId
                               && b.Year == year
                               && b.Name == name
                               && (excludeId == null || b.Id != excludeId), cancellationToken);

        public async Task<IReadOnlyList<BudgetActualRow>> GetActualsAsync(Guid accountSetId, int year,
            CancellationToken cancellationToken = default)
        {
            var start = new DateOnly(year, 1, 1);
            var end = new DateOnly(year, 12, 31);

            // 过滤条件全部写在查询表达式里：投影成匿名类型之后再按日期过滤 EF 无法翻译。
            var facts = await (
                    from e in _context.VoucherEntries.AsNoTracking()
                    join v in _context.Vouchers.AsNoTracking() on e.VoucherId equals v.Id
                    where v.AccountSetId == accountSetId
                          && v.Status == VoucherStatus.Posted
                          && v.Date >= start
                          && v.Date <= end
                    select new { e.AccountId, v.Date, e.Debit, e.Credit })
                .ToListAsync(cancellationToken);

            return facts
                .GroupBy(f => new { f.AccountId, f.Date.Month })
                .Select(g => new BudgetActualRow(
                    g.Key.AccountId,
                    g.Key.Month,
                    g.Sum(x => x.Debit),
                    g.Sum(x => x.Credit)))
                .ToList();
        }

        public async Task AddAsync(Budget budget, CancellationToken cancellationToken = default)
            => await _context.Budgets.AddAsync(budget, cancellationToken);

        public void Remove(Budget budget) => _context.Budgets.Remove(budget);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);
    }
}
