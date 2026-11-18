namespace YunCai.Modules.Budgets.Domain
{
    /// <summary>某科目某月的实际发生额（借 / 贷分开，由应用层按科目方向折成执行额）。</summary>
    public sealed record BudgetActualRow(Guid AccountId, int Month, decimal Debit, decimal Credit);

    /// <summary>预算单仓储（按账套隔离，含执行数聚合）。</summary>
    public interface IBudgetRepository
    {
        /// <summary>按年度列出预算单（year 为空时返回全部年度，按年度倒序）。</summary>
        Task<IReadOnlyList<Budget>> GetListAsync(Guid accountSetId, int? year, CancellationToken cancellationToken = default);

        /// <summary>按标识获取预算单，含预算行。</summary>
        Task<Budget?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>同一年度内名称是否已被占用（excludeId 用于更新时排除自身）。</summary>
        Task<bool> ExistsNameAsync(Guid accountSetId, int year, string name, Guid? excludeId,
            CancellationToken cancellationToken = default);

        /// <summary>全年已过账分录按科目 + 月份聚合的实际发生额。</summary>
        Task<IReadOnlyList<BudgetActualRow>> GetActualsAsync(Guid accountSetId, int year,
            CancellationToken cancellationToken = default);

        Task AddAsync(Budget budget, CancellationToken cancellationToken = default);

        void Remove(Budget budget);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
