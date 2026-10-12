using KuaiJi.Modules.Auditing.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Auditing.Infrastructure
{
    /// <summary>操作日志仓储实现。</summary>
    public class OperationLogRepository : IOperationLogRepository
    {
        private readonly AppDbContext _context;

        public OperationLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<OperationLog>> QueryAsync(
            Guid accountSetId,
            DateTime? start = null,
            DateTime? end = null,
            string? userId = null,
            string? keyword = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.OperationLogs.AsNoTracking()
                .Where(l => l.AccountSetId == accountSetId);
            if (start is { } st)
            {
                query = query.Where(l => l.Time >= st);
            }

            if (end is { } en)
            {
                query = query.Where(l => l.Time < en);
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(l => l.UserId == userId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(l => l.Log.Contains(keyword) || l.Module.Contains(keyword));
            }

            var list = await query.OrderByDescending(l => l.Time).ToListAsync(cancellationToken);
            return list;
        }

        public async Task AddAsync(OperationLog log, CancellationToken cancellationToken = default)
        {
            await _context.OperationLogs.AddAsync(log, cancellationToken);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
