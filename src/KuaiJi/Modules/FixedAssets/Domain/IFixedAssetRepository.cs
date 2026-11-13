namespace KuaiJi.Modules.FixedAssets.Domain
{
    /// <summary>固定资产仓储：类别 / 卡片 / 变动记录 / 凭证科目设置统一出入。</summary>
    public interface IFixedAssetRepository
    {
        // —— 类别 ——
        Task<FixedAssetCategory?> GetCategoryByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>跟踪查询（需要回写时使用；AsNoTracking 实例改完不落库）。</summary>
        Task<FixedAssetCategory?> GetCategoryForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FixedAssetCategory>> GetCategoriesAsync(Guid accountSetId, CancellationToken cancellationToken = default);
        Task<bool> CategoryCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
        Task AddCategoryAsync(FixedAssetCategory category, CancellationToken cancellationToken = default);
        void RemoveCategory(FixedAssetCategory category);

        // —— 卡片 ——
        Task<FixedAsset?> GetAssetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>跟踪查询（需要回写时使用；AsNoTracking 实例改完不落库）。</summary>
        Task<FixedAsset?> GetAssetForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FixedAsset>> GetAssetsAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>跟踪查询全部卡片（计提折旧 / 批量清理等需要回写的场景）。</summary>
        Task<IReadOnlyList<FixedAsset>> GetAssetsForUpdateAsync(Guid accountSetId, CancellationToken cancellationToken = default);
        Task<bool> AssetCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
        Task AddAssetAsync(FixedAsset asset, CancellationToken cancellationToken = default);

        /// <summary>标记卡片为已修改（配合 AsNoTracking 查询的写回）。</summary>
        void Update(FixedAsset asset);

        void RemoveAsset(FixedAsset asset);

        // —— 变动记录 ——
        Task<IReadOnlyList<FixedAssetChange>> GetChangesAsync(Guid accountSetId, Guid? assetId = null, CancellationToken cancellationToken = default);

        /// <summary>跟踪查询变动记录（可按期间 / 资产过滤），用于回填凭证关联。</summary>
        Task<IReadOnlyList<FixedAssetChange>> GetChangesForUpdateAsync(Guid accountSetId, int? period = null, Guid? assetId = null, CancellationToken cancellationToken = default);

        Task<FixedAssetChange?> GetChangeByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);
        Task AddChangeAsync(FixedAssetChange change, CancellationToken cancellationToken = default);
        void Update(FixedAssetChange change);
        void RemoveChanges(IEnumerable<FixedAssetChange> changes);

        /// <summary>判断类别下是否仍有资产卡片（删除类别前校验）。</summary>
        Task<bool> CategoryHasAssetsAsync(Guid accountSetId, Guid categoryId, CancellationToken cancellationToken = default);

        // —— 凭证科目设置 ——
        Task<FixedAssetVoucherSetting?> GetVoucherSettingAsync(Guid accountSetId, CancellationToken cancellationToken = default);
        Task AddVoucherSettingAsync(FixedAssetVoucherSetting setting, CancellationToken cancellationToken = default);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
