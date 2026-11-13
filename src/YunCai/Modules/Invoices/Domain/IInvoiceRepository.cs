namespace YunCai.Modules.Invoices.Domain
{
    /// <summary>发票仓储。按账套隔离。</summary>
    public interface IInvoiceRepository
    {
        /// <summary>按标识获取发票（含明细行，限定账套）。</summary>
        Task<Invoice?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>按方向与日期区间获取发票（含明细行，按录入日期倒序）。</summary>
        Task<IReadOnlyList<Invoice>> GetRangeAsync(
            Guid accountSetId,
            InvoiceDirection direction,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            InvoiceStatus? status = null,
            CancellationToken cancellationToken = default);

        /// <summary>判断同一账套同一方向内是否已存在同发票号（可排除指定标识）。</summary>
        Task<bool> ExistsByInvoiceNoAsync(Guid accountSetId, InvoiceDirection direction, string invoiceNo, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>新增发票。</summary>
        Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default);

        /// <summary>删除发票（明细行级联清除）。</summary>
        void Remove(Invoice invoice);

        /// <summary>
        /// 标记发票为已修改，使其变更能被提交。
        /// 列表查询走 AsNoTracking，实体不被上下文跟踪，自愈等非跟踪场景下
        /// 必须先 Attach 再置为 Modified，否则 SaveChanges 检测不到变更。
        /// </summary>
        void Update(Invoice invoice);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
