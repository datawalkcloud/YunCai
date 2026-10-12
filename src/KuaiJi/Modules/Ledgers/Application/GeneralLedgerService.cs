using KuaiJi.Modules.Accounting.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>总账的一个期间行（一个月一行）。</summary>
    public sealed class GeneralLedgerRowDto
    {
        /// <summary>期间显示文本，形如 2026-07。</summary>
        public string PeriodText { get; init; } = null!;

        /// <summary>期初余额（借方）。</summary>
        public decimal OpeningDebit { get; init; }

        /// <summary>期初余额（贷方）。</summary>
        public decimal OpeningCredit { get; init; }

        /// <summary>本月借方发生额。</summary>
        public decimal Debit { get; init; }

        /// <summary>本月贷方发生额。</summary>
        public decimal Credit { get; init; }

        /// <summary>期末余额（借方）。</summary>
        public decimal EndingDebit { get; init; }

        /// <summary>期末余额（贷方）。</summary>
        public decimal EndingCredit { get; init; }

        /// <summary>余额方向：借 / 贷 / 平。</summary>
        public string Direction { get; init; } = null!;
    }

    /// <summary>总账的一个科目段。</summary>
    public sealed class GeneralLedgerAccountDto
    {
        /// <summary>科目编码。</summary>
        public string Code { get; init; } = null!;

        /// <summary>科目名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>区间内借方合计。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>区间内贷方合计。</summary>
        public decimal TotalCredit { get; init; }

        /// <summary>期间行（每月一行，含无发生额的月份，期初期末延续滚动）。</summary>
        public IReadOnlyList<GeneralLedgerRowDto> Rows { get; init; } = [];
    }

    /// <summary>总账。</summary>
    public sealed class GeneralLedgerDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>各科目的总账段。</summary>
        public IReadOnlyList<GeneralLedgerAccountDto> Accounts { get; init; } = [];
    }

    /// <summary>
    /// 总账应用服务：按科目 × 会计期间（月）汇总发生额并滚动余额。
    /// 上级科目的发生额与余额恒为其全部下级科目之和，故与科目余额表的上级行完全一致。
    /// </summary>
    public sealed class GeneralLedgerService
    {
        private readonly LedgerDataSetFactory _factory;

        public GeneralLedgerService(LedgerDataSetFactory factory)
        {
            _factory = factory;
        }

        /// <summary>生成总账。</summary>
        public async Task<GeneralLedgerDto> GetAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var dataSet = await _factory.LoadAsync(accountSetId, options, cancellationToken);
            var factsByAccount = dataSet.FactsByAccount;

            // 区间内的月份序列（含无发生额的月份），按自然月逐月滚动。
            var periods = EnumeratePeriods(dataSet.StartPeriod, dataSet.EndPeriod);

            var accounts = new List<GeneralLedgerAccountDto>();
            foreach (var account in dataSet.SelectAccounts())
            {
                var subtreeIds = dataSet.SubtreeAccountIds(account);
                var openingNet = subtreeIds.Sum(id => dataSet.OpeningByAccount.GetValueOrDefault(id));
                var periodActivity = subtreeIds
                    .SelectMany(id => factsByAccount[id])
                    .GroupBy(f => f.Period)
                    .ToDictionary(g => g.Key, g => (Debit: g.Sum(f => f.Debit), Credit: g.Sum(f => f.Credit)));

                var rows = new List<GeneralLedgerRowDto>();
                var running = openingNet;
                decimal totalDebit = 0m, totalCredit = 0m;

                foreach (var period in periods)
                {
                    var (debit, credit) = periodActivity.GetValueOrDefault(period);
                    running += debit - credit;
                    totalDebit += debit;
                    totalCredit += credit;

                    var opening = BalanceAmounts.SplitBySign(running - debit + credit);
                    var ending = BalanceAmounts.SplitBySign(running);
                    rows.Add(new GeneralLedgerRowDto
                    {
                        PeriodText = PeriodText(period),
                        OpeningDebit = opening.Debit,
                        OpeningCredit = opening.Credit,
                        Debit = debit,
                        Credit = credit,
                        EndingDebit = ending.Debit,
                        EndingCredit = ending.Credit,
                        Direction = LedgerDirectionText.Of(running),
                    });
                }

                if (options.HideZeroBalance && running == 0m)
                {
                    continue;
                }

                if (options.HideNoActivity && totalDebit == 0m && totalCredit == 0m && openingNet == 0m && running == 0m)
                {
                    continue;
                }

                if (totalDebit == 0m && totalCredit == 0m && openingNet == 0m && !options.IncludeIdleAccounts)
                {
                    continue;
                }

                accounts.Add(new GeneralLedgerAccountDto
                {
                    Code = account.Code,
                    Name = account.Name,
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    Rows = rows,
                });
            }

            return new GeneralLedgerDto
            {
                Meta = LedgerMetaDto.From(dataSet),
                Accounts = accounts,
            };
        }

        private static string PeriodText(int period) => $"{period / 100:D4}-{period % 100:D2}";

        private static IReadOnlyList<int> EnumeratePeriods(int startPeriod, int endPeriod)
        {
            var periods = new List<int>();
            var year = startPeriod / 100;
            var month = startPeriod % 100;
            while (year * 100 + month <= endPeriod)
            {
                periods.Add(year * 100 + month);
                month++;
                if (month == 13)
                {
                    month = 1;
                    year++;
                }
            }

            return periods;
        }
    }
}
