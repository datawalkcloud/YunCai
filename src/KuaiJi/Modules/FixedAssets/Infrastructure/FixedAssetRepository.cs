using KuaiJi.Modules.FixedAssets.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.FixedAssets.Infrastructure
{
    /// <summary>固定资产仓储实现（类别 / 卡片 / 变动记录）。</summary>
    public class FixedAssetRepository : IFixedAssetRepository
    {
        private readonly AppDbContext _context;

        public FixedAssetRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<FixedAssetCategory?> GetCategoryByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssetCategories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.AccountSetId == accountSetId && c.Id == id, cancellationToken);
        }

        public Task<FixedAssetCategory?> GetCategoryForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssetCategories
                .FirstOrDefaultAsync(c => c.AccountSetId == accountSetId && c.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<FixedAssetCategory>> GetCategoriesAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.FixedAssetCategories.AsNoTracking()
                .Where(c => c.AccountSetId == accountSetId)
                .OrderBy(c => c.Code)
                .ToListAsync(cancellationToken);
            return list;
        }

        public Task<bool> CategoryCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssetCategories
                .AnyAsync(c => c.AccountSetId == accountSetId && c.Code == code && (excludeId == null || c.Id != excludeId), cancellationToken);
        }

        public async Task AddCategoryAsync(FixedAssetCategory category, CancellationToken cancellationToken = default)
        {
            await _context.FixedAssetCategories.AddAsync(category, cancellationToken);
        }

        public void RemoveCategory(FixedAssetCategory category)
        {
            _context.FixedAssetCategories.Remove(category);
        }

        public Task<FixedAsset?> GetAssetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssets.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountSetId == accountSetId && a.Id == id, cancellationToken);
        }

        public Task<FixedAsset?> GetAssetForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssets
                .FirstOrDefaultAsync(a => a.AccountSetId == accountSetId && a.Id == id, cancellationToken);
        }

        public void Update(FixedAsset asset)
        {
            if (_context.Entry(asset).State == EntityState.Detached)
            {
                _context.FixedAssets.Attach(asset);
            }

            _context.Entry(asset).State = EntityState.Modified;
        }

        public async Task<IReadOnlyList<FixedAsset>> GetAssetsAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            var list = await _context.FixedAssets.AsNoTracking()
                .Where(a => a.AccountSetId == accountSetId)
                .OrderBy(a => a.Code)
                .ToListAsync(cancellationToken);
            return list;
        }

        public async Task<IReadOnlyList<FixedAsset>> GetAssetsForUpdateAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return await _context.FixedAssets
                .Where(a => a.AccountSetId == accountSetId)
                .OrderBy(a => a.Code)
                .ToListAsync(cancellationToken);
        }

        public Task<bool> AssetCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssets
                .AnyAsync(a => a.AccountSetId == accountSetId && a.Code == code && (excludeId == null || a.Id != excludeId), cancellationToken);
        }

        public async Task AddAssetAsync(FixedAsset asset, CancellationToken cancellationToken = default)
        {
            await _context.FixedAssets.AddAsync(asset, cancellationToken);
        }

        public void RemoveAsset(FixedAsset asset)
        {
            _context.FixedAssets.Remove(asset);
        }

        public async Task<IReadOnlyList<FixedAssetChange>> GetChangesAsync(Guid accountSetId, Guid? assetId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.FixedAssetChanges.AsNoTracking()
                .Where(c => c.AccountSetId == accountSetId);
            if (assetId is { } aid)
            {
                query = query.Where(c => c.AssetId == aid);
            }

            var list = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);
            return list;
        }

        public async Task AddChangeAsync(FixedAssetChange change, CancellationToken cancellationToken = default)
        {
            await _context.FixedAssetChanges.AddAsync(change, cancellationToken);
        }

        public async Task<IReadOnlyList<FixedAssetChange>> GetChangesForUpdateAsync(
            Guid accountSetId,
            int? period = null,
            Guid? assetId = null,
            CancellationToken cancellationToken = default)
        {
            var query = _context.FixedAssetChanges
                .Where(c => c.AccountSetId == accountSetId);
            if (period is { } p)
            {
                query = query.Where(c => c.Period == p);
            }

            if (assetId is { } aid)
            {
                query = query.Where(c => c.AssetId == aid);
            }

            return await query.OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);
        }

        public Task<FixedAssetChange?> GetChangeByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssetChanges.AsNoTracking()
                .FirstOrDefaultAsync(c => c.AccountSetId == accountSetId && c.Id == id, cancellationToken);
        }

        public void Update(FixedAssetChange change)
        {
            if (_context.Entry(change).State == EntityState.Detached)
            {
                _context.FixedAssetChanges.Attach(change);
            }

            _context.Entry(change).State = EntityState.Modified;
        }

        public void RemoveChanges(IEnumerable<FixedAssetChange> changes)
        {
            _context.FixedAssetChanges.RemoveRange(changes);
        }

        public Task<FixedAssetVoucherSetting?> GetVoucherSettingAsync(Guid accountSetId, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssetVoucherSettings
                .FirstOrDefaultAsync(s => s.AccountSetId == accountSetId, cancellationToken);
        }

        public async Task AddVoucherSettingAsync(FixedAssetVoucherSetting setting, CancellationToken cancellationToken = default)
        {
            await _context.FixedAssetVoucherSettings.AddAsync(setting, cancellationToken);
        }

        public Task<bool> CategoryHasAssetsAsync(Guid accountSetId, Guid categoryId, CancellationToken cancellationToken = default)
        {
            return _context.FixedAssets
                .AnyAsync(a => a.AccountSetId == accountSetId && a.CategoryId == categoryId, cancellationToken);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
