namespace KuaiJi.Modules.Assist.Domain
{
    /// <summary>
    /// 辅助核算档案仓储。以聚合根 <see cref="AssistItem"/> 为单位进行持久化访问。
    /// </summary>
    public interface IAssistItemRepository
    {
        /// <summary>按标识获取档案（限定账套）。</summary>
        Task<AssistItem?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>按类别与编码获取档案（限定账套）。</summary>
        Task<AssistItem?> GetByCodeAsync(Guid accountSetId, AssistType type, string code, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部档案（用于在应用层组装档案树与账簿取数）。</summary>
        Task<IReadOnlyList<AssistItem>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套、指定类别的全部档案（按编码排序）。</summary>
        Task<IReadOnlyList<AssistItem>> GetByTypeAsync(Guid accountSetId, AssistType type, CancellationToken cancellationToken = default);

        /// <summary>统计各类别的档案数量（不含下级占位，仅档案总数）。</summary>
        Task<IReadOnlyDictionary<AssistType, int>> CountByTypeAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>判断同一账套同一类别下该编码是否已存在。</summary>
        Task<bool> ExistsByCodeAsync(Guid accountSetId, AssistType type, string code, CancellationToken cancellationToken = default);

        /// <summary>判断档案是否存在下级（限定账套）。</summary>
        Task<bool> HasChildrenAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>统计档案被多少条凭证分录引用，用于删除保护。</summary>
        Task<int> CountVoucherReferencesAsync(Guid accountSetId, Guid assistItemId, CancellationToken cancellationToken = default);

        /// <summary>新增档案。</summary>
        Task AddAsync(AssistItem item, CancellationToken cancellationToken = default);

        /// <summary>删除档案（应用层需先校验无下级、无引用）。</summary>
        void Remove(AssistItem item);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
