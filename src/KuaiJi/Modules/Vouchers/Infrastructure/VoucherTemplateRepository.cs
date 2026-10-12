using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Vouchers.Infrastructure
{
    /// <summary>凭证模板仓储实现（含模板行加载）。</summary>
    public class VoucherTemplateRepository : IVoucherTemplateRepository
    {
        private readonly AppDbContext _context;

        public VoucherTemplateRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<VoucherTemplate?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.VoucherTemplates
                .Include(t => t.Lines)
                .FirstOrDefaultAsync(t => t.AccountSetId == accountSetId && t.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<VoucherTemplate>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.VoucherTemplates.AsNoTracking()
                .Include(t => t.Lines)
                .Where(t => t.AccountSetId == accountSetId)
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<bool> ExistsByNameAsync(Guid accountSetId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.VoucherTemplates
                .AnyAsync(t => t.AccountSetId == accountSetId && t.Name == name && (excludeId == null || t.Id != excludeId), cancellationToken);
        }

        public async Task AddAsync(VoucherTemplate template, CancellationToken cancellationToken = default)
        {
            await _context.VoucherTemplates.AddAsync(template, cancellationToken);
        }

        public void Remove(VoucherTemplate template)
        {
            _context.VoucherTemplates.Remove(template);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
