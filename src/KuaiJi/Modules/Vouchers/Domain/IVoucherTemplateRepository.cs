namespace KuaiJi.Modules.Vouchers.Domain
{
    /// <summary>凭证模板仓储。按账套隔离。</summary>
    public interface IVoucherTemplateRepository
    {
        /// <summary>按标识获取模板（含模板行，限定账套）。</summary>
        Task<VoucherTemplate?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部模板（含模板行，按名称排列）。</summary>
        Task<IReadOnlyList<VoucherTemplate>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>判断同一账套内是否已存在同名模板（可排除指定标识）。</summary>
        Task<bool> ExistsByNameAsync(Guid accountSetId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>新增模板。</summary>
        Task AddAsync(VoucherTemplate template, CancellationToken cancellationToken = default);

        /// <summary>删除模板（模板行级联清除）。</summary>
        void Remove(VoucherTemplate template);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
