using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>
    /// 账簿取数端口。以投影查询直出扁平事实与期初聚合，不物化 <see cref="Domain.Vouchers.Voucher"/> 聚合根。
    /// <para>
    /// 口径约定（设计文档 §0.1）：三个取数方法一律只统计**已过账**凭证。
    /// 账簿是法定账册，反过账退回的草稿不得入账；因此各账页与既有报表的取数范围以此为准。
    /// </para>
    /// </summary>
    public interface ILedgerSource
    {
        /// <summary>取指定日期区间（含端点）内的已过账分录事实，凭证头信息已展平。</summary>
        Task<IReadOnlyList<LedgerFact>> GetFactsAsync(Guid accountSetId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

        /// <summary>起始日之前的已过账分录按科目累计净额（借 - 贷），由数据库端聚合。</summary>
        Task<IReadOnlyDictionary<Guid, decimal>> GetOpeningNetByAccountAsync(Guid accountSetId, DateOnly beforeDate, CancellationToken cancellationToken = default);

        /// <summary>起始日之前的已过账分录按「科目 × 辅助核算档案」累计净额（借 - 贷），由数据库端聚合。</summary>
        Task<IReadOnlyDictionary<BalanceKey, decimal>> GetOpeningNetByAssistAsync(Guid accountSetId, DateOnly beforeDate, CancellationToken cancellationToken = default);

        /// <summary>指定区间内未过账（草稿）的凭证张数，供账页页头提示。</summary>
        Task<int> CountUnpostedAsync(Guid accountSetId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

        /// <summary>账套内凭证的最早/最晚日期（无凭证时两项均为 null），用于缺省查询区间。</summary>
        Task<(DateOnly? First, DateOnly? Last)> GetVoucherDateBoundsAsync(Guid accountSetId, CancellationToken cancellationToken = default);
    }
}
