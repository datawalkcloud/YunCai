namespace KuaiJi.Domain.Vouchers
{
    /// <summary>
    /// 记账凭证仓储。以聚合根 <see cref="Voucher"/> 为单位持久化访问（含其分录）。
    /// </summary>
    public interface IVoucherRepository
    {
        /// <summary>按标识获取凭证（含分录，限定账套）。</summary>
        Task<Voucher?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部凭证（含分录，用于列表展示借贷合计）。</summary>
        Task<IReadOnlyList<Voucher>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>判断指定凭证字与号在账套内是否已存在。</summary>
        Task<bool> ExistsByWordAndNumberAsync(Guid accountSetId, string word, int number, CancellationToken cancellationToken = default);

        /// <summary>判断指定「凭证字+号+日期」的凭证在账套内是否已存在，用于导入去重。</summary>
        Task<bool> ExistsByWordNumberDateAsync(Guid accountSetId, string word, int number, DateOnly date, CancellationToken cancellationToken = default);

        /// <summary>获取指定凭证字在账套内当前的最大凭证号（无则返回 0），用于生成下一号。</summary>
        Task<int> GetMaxNumberAsync(Guid accountSetId, string word, CancellationToken cancellationToken = default);

        /// <summary>新增凭证。</summary>
        Task AddAsync(Voucher voucher, CancellationToken cancellationToken = default);

        /// <summary>删除凭证（含其全部分录）。</summary>
        void Remove(Voucher voucher);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
