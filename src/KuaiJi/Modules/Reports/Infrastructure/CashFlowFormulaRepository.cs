using KuaiJi.Modules.Reports.Domain.CashFlow;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Reports.Infrastructure
{
    /// <summary>现金流量表行取数公式仓储实现。</summary>
    public sealed class CashFlowFormulaRepository : ICashFlowFormulaRepository
    {
        private readonly AppDbContext _db;

        public CashFlowFormulaRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<CashFlowFormula>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _db.CashFlowFormulas
                .Where(f => f.AccountSetId == accountSetId)
                .OrderBy(f => f.Line)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(CashFlowFormula formula, CancellationToken cancellationToken = default)
        {
            await _db.CashFlowFormulas.AddAsync(formula, cancellationToken);
        }

        public void Remove(CashFlowFormula formula)
        {
            _db.CashFlowFormulas.Remove(formula);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _db.SaveChangesAsync(cancellationToken);
        }
    }
}
