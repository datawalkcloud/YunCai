namespace KuaiJi.Modules.Closing.Domain
{
    /// <summary>
    /// 结转模板仓储。以聚合根 <see cref="ClosingTemplate"/> 为单位持久化访问（含其模板行）。
    /// </summary>
    public interface IClosingTemplateRepository
    {
        /// <summary>按标识获取模板（含行，限定账套）。</summary>
        Task<ClosingTemplate?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部模板（含行，按名称排序）。</summary>
        Task<IReadOnlyList<ClosingTemplate>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>判断同一账套内模板名称是否已存在；编辑时用 excludeId 排除自身。</summary>
        Task<bool> ExistsByNameAsync(Guid accountSetId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>新增模板。</summary>
        Task AddAsync(ClosingTemplate template, CancellationToken cancellationToken = default);

        /// <summary>删除模板（含其模板行，级联清除）。</summary>
        void Remove(ClosingTemplate template);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
