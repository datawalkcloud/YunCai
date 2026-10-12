using KuaiJi.Modules.Reports.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Reports.Infrastructure
{
    /// <summary>自定义报表（数据透视表）仓储实现。</summary>
    public sealed class CustomReportRepository : ICustomReportRepository
    {
        private readonly AppDbContext _db;

        public CustomReportRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<CustomReport>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _db.CustomReports
                .AsNoTracking()
                .Where(r => r.AccountSetId == accountSetId)
                .OrderBy(r => r.Code)
                .ToListAsync(cancellationToken);
        }

        public Task<CustomReport?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _db.CustomReports
                .FirstOrDefaultAsync(r => r.AccountSetId == accountSetId && r.Id == id, cancellationToken);
        }

        public Task<bool> CodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _db.CustomReports
                .AnyAsync(r => r.AccountSetId == accountSetId
                               && r.Code == code
                               && (excludeId == null || r.Id != excludeId),
                    cancellationToken);
        }

        public async Task AddAsync(CustomReport report, CancellationToken cancellationToken = default)
        {
            await _db.CustomReports.AddAsync(report, cancellationToken);
        }

        public void Remove(CustomReport report)
        {
            _db.CustomReports.Remove(report);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _db.SaveChangesAsync(cancellationToken);
        }
    }
}
