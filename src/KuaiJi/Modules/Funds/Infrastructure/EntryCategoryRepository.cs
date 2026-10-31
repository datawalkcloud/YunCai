using KuaiJi.Modules.Funds.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.Funds.Infrastructure
{
    /// <summary>基于 EF Core 的收支类别仓储实现，按账套隔离。</summary>
    public class EntryCategoryRepository : IEntryCategoryRepository
    {
        private readonly AppDbContext _context;

        public EntryCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<EntryCategory?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.EntryCategories
                .SingleOrDefaultAsync(c => c.AccountSetId == accountSetId && c.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<EntryCategory>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.EntryCategories
                .AsNoTracking()
                .Where(c => c.AccountSetId == accountSetId)
                .ToListAsync(cancellationToken);

            // 编码多为「1、2、…、10」这样的序号，按数值排序才与账面顺序一致；纯数字以外的编码退化为字符串比较。
            return list
                .OrderBy(c => c.Type)
                .ThenBy(c => c.Code, CodeComparer.Instance)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>编码比较器：两边都是纯数字时按数值比较，其余按序号比较。</summary>
        private sealed class CodeComparer : IComparer<string>
        {
            public static readonly CodeComparer Instance = new();

            public int Compare(string? x, string? y)
            {
                var leftIsNumber = long.TryParse(x, out var left);
                var rightIsNumber = long.TryParse(y, out var right);
                if (leftIsNumber && rightIsNumber)
                {
                    return left.CompareTo(right);
                }

                if (leftIsNumber)
                {
                    return -1;
                }

                if (rightIsNumber)
                {
                    return 1;
                }

                return string.CompareOrdinal(x, y);
            }
        }

        public Task<bool> ExistsByNameAsync(Guid accountSetId, EntryCategoryType type, string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.EntryCategories.AnyAsync(
                c => c.AccountSetId == accountSetId
                     && c.Type == type
                     && c.Name == name
                     && (excludeId == null || c.Id != excludeId),
                cancellationToken);
        }

        public Task<bool> ExistsByCodeAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.EntryCategories.AnyAsync(
                c => c.AccountSetId == accountSetId
                     && c.Code == code
                     && (excludeId == null || c.Id != excludeId),
                cancellationToken);
        }

        public async Task AddAsync(EntryCategory category, CancellationToken cancellationToken = default)
        {
            await _context.EntryCategories.AddAsync(category, cancellationToken);
        }

        public void Remove(EntryCategory category)
        {
            _context.EntryCategories.Remove(category);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
