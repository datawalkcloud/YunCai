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
    /// <para>
    /// 输出按科目层级铺开：有余额或发生额的科目连同其全部上级一并列出，上级行金额为整棵子树合计
    /// （与总账 <see cref="LedgerDataSet.GetAccountBalance"/> 同口径）；<see cref="TrialBalanceRowDto.Level"/>
    /// 与 <see cref="TrialBalanceRowDto.ParentCode"/> 供前端挂成树。合计只累加一级科目，避免上下级重复。
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

            // 先找出自身有余额或发生额的科目，再沿上级链补齐祖先——上级行要有下级合计才成树，
            // 否则明细科目在表上会凭空出现、看不出挂在哪个一级科目下。
            var visible = new HashSet<Guid>();
            foreach (var pair in dataSet.Balances.ByAccount)
            {
                if (pair.Value.IsEmpty)
                {
                    continue;
                }

                var current = dataSet.AccountsById.GetValueOrDefault(pair.Key);
                while (current is not null && visible.Add(current.Id))
                {
                    current = current.ParentId is { } parentId
                        ? dataSet.AccountsById.GetValueOrDefault(parentId)
                        : null;
                }
            }

            var rows = new List<TrialBalanceRowDto>(visible.Count);
            foreach (var accountId in visible)
            {
                var account = dataSet.AccountsById[accountId];
                // 上级科目取整棵子树合计，与总账 GetAccountBalance 同一口径：上级行恒等于下级之和。
                var balance = dataSet.GetAccountBalance(account);
                var (openingDebit, openingCredit) = BalanceAmounts.SplitBySign(balance.OpeningNet);
                var (endingDebit, endingCredit) = BalanceAmounts.SplitBySign(balance.EndingNet);

                rows.Add(new TrialBalanceRowDto
                {
                    Code = account.Code,
                    Name = account.Name,
                    Level = account.Level,
                    ParentCode = account.ParentId is { } parentId
                        ? dataSet.AccountsById[parentId].Code
                        : null,
                    OpeningDebit = openingDebit,
                    OpeningCredit = openingCredit,
                    PeriodDebit = balance.PeriodDebit,
                    PeriodCredit = balance.PeriodCredit,
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
