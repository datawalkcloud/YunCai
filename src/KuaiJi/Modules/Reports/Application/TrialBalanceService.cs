using KuaiJi.Modules.Ledgers.Application;
using KuaiJi.Modules.Reports.Domain;

namespace KuaiJi.Modules.Reports.Application
{

    /// <summary>
    /// 科目余额表应用服务：按选定期间从账簿数据集合投影各科目的期初、本期发生额与期末余额。
    /// <para>
    /// 口径（设计文档 §0）：只统计**已过账**凭证；查询区间归一到自然月；内部一律以「借 - 贷」净额累计，
    /// 仅在输出层按正负拆回借/贷两栏。期初 = 科目录入的启用期初余额 + 期间起始日之前已过账分录的净额，
    /// 期末 = 期初 + 本期净额。因每张凭证借贷平衡且期初经试算平衡校验，合计恒满足试算平衡。
    /// </para>
    /// </summary>
    public sealed class TrialBalanceService
    {
        private readonly LedgerDataSetFactory _ledgerDataSetFactory;

        public TrialBalanceService(LedgerDataSetFactory ledgerDataSetFactory)
        {
            _ledgerDataSetFactory = ledgerDataSetFactory;
        }

        /// <summary>
        /// 生成指定账套、指定日期范围的科目余额表。startDate/endDate 省略时默认取全部已过账凭证所在月。
        /// </summary>
        public async Task<TrialBalanceDto> GetAsync(Guid accountSetId, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
        {
            var dataSet = await _ledgerDataSetFactory.LoadAsync(
                accountSetId,
                new LedgerQueryOptions { StartDate = startDate, EndDate = endDate },
                cancellationToken);

            var rows = new List<TrialBalanceRowDto>();
            foreach (var pair in dataSet.Balances.ByAccount)
            {
                // 无任何余额与发生额的科目不列示。
                if (pair.Value.IsEmpty)
                {
                    continue;
                }

                var account = dataSet.AccountsById.GetValueOrDefault(pair.Key);
                var (openingDebit, openingCredit) = BalanceAmounts.SplitBySign(pair.Value.OpeningNet);
                var (endingDebit, endingCredit) = BalanceAmounts.SplitBySign(pair.Value.EndingNet);

                rows.Add(new TrialBalanceRowDto
                {
                    Code = account?.Code ?? string.Empty,
                    Name = account?.Name ?? "（未知科目）",
                    OpeningDebit = openingDebit,
                    OpeningCredit = openingCredit,
                    PeriodDebit = pair.Value.PeriodDebit,
                    PeriodCredit = pair.Value.PeriodCredit,
                    EndingDebit = endingDebit,
                    EndingCredit = endingCredit,
                });
            }

            rows = rows.OrderBy(r => r.Code, StringComparer.Ordinal).ToList();

            return new TrialBalanceDto
            {
                StartDate = dataSet.StartDate.ToString("yyyy-MM-dd"),
                EndDate = dataSet.EndDate.ToString("yyyy-MM-dd"),
                PeriodText = dataSet.PeriodText,
                Rows = rows,
            };
        }
    }
}
