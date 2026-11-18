namespace YunCai.Modules.Expenses.Domain
{
    /// <summary>费用单据仓储。</summary>
    public interface IExpenseRepository
    {
        Task<IReadOnlyList<ExpenseBill>> GetBillsAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>跟踪查询（生成凭证 / 导入更新需要回写）。</summary>
        Task<IReadOnlyList<ExpenseBill>> GetBillsForUpdateAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        Task<ExpenseBill?> GetBillByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>跟踪查询单张单据。</summary>
        Task<ExpenseBill?> GetBillForUpdateAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        Task<bool> BillNoExistsAsync(Guid accountSetId, string billNo, Guid? excludeId = null, CancellationToken cancellationToken = default);

        Task AddBillAsync(ExpenseBill bill, CancellationToken cancellationToken = default);

        void RemoveBill(ExpenseBill bill);

        Task<IReadOnlyList<VoucherRef>> GetVoucherRefsAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>凭证引用（字号回显与自愈判断用），避免直接依赖凭证聚合。</summary>
    public sealed record VoucherRef(Guid Id, string Word, int Number);
}
