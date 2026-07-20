namespace KuaiJi.Domain.Vouchers
{
    /// <summary>
    /// 记账凭证仓储。以聚合根 <see cref="Voucher"/> 为单位持久化访问（含其分录）。
    /// </summary>
    public interface IVoucherRepository
    {
        /// <summary>按标识获取凭证（含分录）。</summary>
        Task<Voucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>获取全部凭证（不含分录，用于列表）。</summary>
        Task<IReadOnlyList<Voucher>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>判断指定凭证字与号是否已存在。</summary>
        Task<bool> ExistsByWordAndNumberAsync(string word, int number, CancellationToken cancellationToken = default);

        /// <summary>获取指定凭证字下当前的最大凭证号（无则返回 0），用于生成下一号。</summary>
        Task<int> GetMaxNumberAsync(string word, CancellationToken cancellationToken = default);

        /// <summary>新增凭证。</summary>
        Task AddAsync(Voucher voucher, CancellationToken cancellationToken = default);

        /// <summary>删除凭证（含其全部分录）。</summary>
        void Remove(Voucher voucher);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
