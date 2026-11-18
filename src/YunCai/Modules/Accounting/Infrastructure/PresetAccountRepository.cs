using Microsoft.EntityFrameworkCore;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.SharedKernel.Infrastructure.Data;

namespace YunCai.Modules.Accounting.Infrastructure
{
    /// <summary>预置科目清单的 EF 读取实现。</summary>
    public class PresetAccountRepository : IPresetAccountRepository
    {
        private readonly AppDbContext _context;

        public PresetAccountRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<PresetAccount>> GetByStandardAsync(AccountingStandard standard, CancellationToken cancellationToken = default)
        {
            return await _context.PresetAccounts
                .AsNoTracking()
                .Where(p => p.Standard == standard)
                .OrderBy(p => p.OrderNo)
                .ToListAsync(cancellationToken);
        }
    }
}
