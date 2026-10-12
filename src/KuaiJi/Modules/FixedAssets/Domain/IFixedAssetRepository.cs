namespace KuaiJi.Modules.FixedAssets.Domain
{
    /// <summary>固定资产仓储：类别 / 卡片 / 变动记录统一出入。</summary>
    public interface IFixedAssetRepository
    {
        // —— 类别 ——
        Task<FixedAssetCategory?> GetCategoryByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FixedAssetCategory>> GetCategoriesAsync(Guid accountSetId, CancellationToken cancellationToken = default);
        Task<bool> CategoryCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
        Task AddCategoryAsync(FixedAssetCategory category, CancellationToken cancellationToken = default);
        void RemoveCategory(FixedAssetCategory category);

        // —— 卡片 ——
        Task<FixedAsset?> GetAssetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<FixedAsset>> GetAssetsAsync(Guid accountSetId, CancellationToken cancellationToken = default);
        Task<bool> AssetCodeExistsAsync(Guid accountSetId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
        Task AddAssetAsync(FixedAsset asset, CancellationToken cancellationToken = default);
        void RemoveAsset(FixedAsset asset);

        // —— 变动记录 ——
        Task<IReadOnlyList<FixedAssetChange>> GetChangesAsync(Guid accountSetId, Guid? assetId = null, CancellationToken cancellationToken = default);
        Task AddChangeAsync(FixedAssetChange change, CancellationToken cancellationToken = default);

        /// <summary>判断类别下是否仍有资产卡片（删除类别前校验）。</summary>
        Task<bool> CategoryHasAssetsAsync(Guid accountSetId, Guid categoryId, CancellationToken cancellationToken = default);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
