using YunCai.Modules.Closing.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.Closing.Infrastructure
{
    /// <summary>基于 EF Core 的结转模板仓储实现。</summary>
    public class ClosingTemplateRepository : IClosingTemplateRepository
    {
        private readonly AppDbContext _context;

        public ClosingTemplateRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<ClosingTemplate?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.ClosingTemplates
                .Include(t => t.Lines)
                .SingleOrDefaultAsync(t => t.AccountSetId == accountSetId && t.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<ClosingTemplate>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.ClosingTemplates
                .AsNoTracking()
                .Include(t => t.Lines)
                .Where(t => t.AccountSetId == accountSetId)
                .OrderBy(t => t.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<bool> ExistsByNameAsync(Guid accountSetId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.ClosingTemplates.AnyAsync(
                t => t.AccountSetId == accountSetId
                     && t.Name == name
                     && (excludeId == null || t.Id != excludeId),
                cancellationToken);
        }

        public async Task AddAsync(ClosingTemplate template, CancellationToken cancellationToken = default)
        {
            var entry = _context.Entry(template);
            if (entry.State != EntityState.Detached)
            {
                return;
            }

            await _context.ClosingTemplates.AddAsync(template, cancellationToken);
        }

        public void Remove(ClosingTemplate template)
        {
            _context.ClosingTemplates.Remove(template);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
