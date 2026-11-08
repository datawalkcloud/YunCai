namespace KuaiJi.Modules.AccountSets.Domain
{
    /// <summary>凭证字仓储。按账套隔离。</summary>
    public interface IVoucherWordRepository
    {
        /// <summary>按标识获取凭证字（限定账套）。</summary>
        Task<VoucherWord?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部凭证字（默认在前）。</summary>
        Task<IReadOnlyList<VoucherWord>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>判断同一账套内是否已存在同字凭证字（可排除指定标识）。</summary>
        Task<bool> ExistsByWordAsync(Guid accountSetId, string word, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>新增凭证字。</summary>
        Task AddAsync(VoucherWord word, CancellationToken cancellationToken = default);

        /// <summary>把账套内除指定凭证字外的默认标记全部取消（随下一次 SaveChanges 落库）。</summary>
        Task ClearDefaultAsync(Guid accountSetId, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>删除凭证字。</summary>
        void Remove(VoucherWord word);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
