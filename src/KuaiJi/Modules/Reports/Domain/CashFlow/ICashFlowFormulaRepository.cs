namespace KuaiJi.Modules.Reports.Domain.CashFlow
{
    /// <summary>现金流量表行取数公式仓储（按账套隔离）。</summary>
    public interface ICashFlowFormulaRepository
    {
        /// <summary>获取账套内已保存的公式（按行次排序）。</summary>
        Task<IReadOnlyList<CashFlowFormula>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>新增一行公式。</summary>
        Task AddAsync(CashFlowFormula formula, CancellationToken cancellationToken = default);

        /// <summary>删除一行公式（删除后该行回落默认公式）。</summary>
        void Remove(CashFlowFormula formula);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
