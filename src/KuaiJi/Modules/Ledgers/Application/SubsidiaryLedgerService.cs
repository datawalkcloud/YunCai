using KuaiJi.Modules.Accounting.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>明细账行种类。</summary>
    public enum SubsidiaryRowKind
    {
        /// <summary>逐笔发生额。</summary>
        Entry = 0,

        /// <summary>期初余额行（不参与发生额合计）。</summary>
        Opening = 1,

        /// <summary>本月合计。</summary>
        MonthTotal = 2,

        /// <summary>本年累计。</summary>
        YearTotal = 3,
    }

    /// <summary>明细账的一行。</summary>
    public sealed class SubsidiaryLedgerRowDto
    {
        /// <summary>行种类。</summary>
        public SubsidiaryRowKind Kind { get; init; }

        /// <summary>凭证日期（期初行为空）。</summary>
        public string? Date { get; init; }

        /// <summary>凭证字号，如「记-1」（期初行为空）。</summary>
        public string? VoucherNo { get; init; }

        /// <summary>摘要。合计行显示「本月合计」「本年累计」。</summary>
        public string Summary { get; init; } = null!;

        /// <summary>对方科目（查询条件开启「显示对方科目」时填充）。</summary>
        public string? CounterAccount { get; init; }

        /// <summary>借方金额。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方金额。</summary>
        public decimal Credit { get; init; }

        /// <summary>余额方向：借 / 贷 / 平。</summary>
        public string? Direction { get; init; }

        /// <summary>余额金额（按方向取绝对值）。</summary>
        public decimal? Balance { get; init; }
    }

    /// <summary>明细账的一个科目段。</summary>
    public sealed class SubsidiaryLedgerAccountDto
    {
        /// <summary>科目编码。</summary>
        public string Code { get; init; } = null!;

        /// <summary>科目名称。</summary>
        public string Name { get; init; } = null!;

        /// <summary>该科目在查询区间内的借方合计。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>该科目在查询区间内的贷方合计。</summary>
        public decimal TotalCredit { get; init; }

        /// <summary>行序列：期初 → 逐笔（每月末插「本月合计」「本年累计」）。</summary>
        public IReadOnlyList<SubsidiaryLedgerRowDto> Rows { get; init; } = [];
    }

    /// <summary>明细账。</summary>
    public sealed class SubsidiaryLedgerDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>各科目的明细账段（按科目编码排序，空段已按显示开关剔除）。</summary>
        public IReadOnlyList<SubsidiaryLedgerAccountDto> Accounts { get; init; } = [];
    }

    /// <summary>
    /// 明细账应用服务：按科目分段投影逐笔余额。
    /// <para>
    /// 为算出「本年累计」，装载区间会向前扩展到查询起始月所在年的 1 月 1 日；
    /// 1 月至查询起始月之间的事实只参与累计与余额滚动，不逐笔列示。
    /// 摘要关键字只影响列示的行，余额滚动始终按全量事实计算，保证逐笔余额连续且末行等于科目余额表期末。
    /// </para>
    /// </summary>
    public sealed class SubsidiaryLedgerService
    {
        private readonly LedgerDataSetFactory _factory;

        public SubsidiaryLedgerService(LedgerDataSetFactory factory)
        {
            _factory = factory;
        }

        /// <summary>生成明细账。</summary>
        public async Task<SubsidiaryLedgerDto> GetAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var (rangeStart, rangeEnd) = await _factory.ResolveRangeAsync(accountSetId, options, cancellationToken);

            // 装载本年 1 月起的事实，以便逐笔滚动余额与「本年累计」都用同一份数据。
            // 注意：因此 dataSet.Balances 的「本期」是年累计口径，本账页一律自行按 rangeStart 切分区间。
            var dataSet = await _factory.LoadAsync(
                accountSetId,
                options with { StartDate = new DateOnly(rangeStart.Year, 1, 1), EndDate = rangeEnd },
                cancellationToken);

            var keyword = options.SummaryKeyword?.Trim();
            var factsByAccount = dataSet.FactsByAccount;
            var accounts = new List<SubsidiaryLedgerAccountDto>();

            foreach (var account in dataSet.SelectAccounts())
            {
                // 非明细科目取其全部下级科目的事实合并列示（明细账允许按上级科目查）。
                var facts = OrderFacts(dataSet.SubtreeAccountIds(account).SelectMany(id => factsByAccount[id]));
                var priorNet = facts.Where(f => f.Date < rangeStart).Sum(f => f.NetDebit);
                var inRange = facts.Where(f => f.Date >= rangeStart).ToList();
                var periodDebit = inRange.Sum(f => f.Debit);
                var periodCredit = inRange.Sum(f => f.Credit);

                // 期初 = 本年 1 月 1 日的期初（含启用期初与各下级）+ 查询起始月之前的累计净额。
                var openingNet = dataSet.SubtreeAccountIds(account).Sum(id => dataSet.OpeningByAccount.GetValueOrDefault(id)) + priorNet;
                var endingNet = openingNet + periodDebit - periodCredit;

                if (options.HideZeroBalance && endingNet == 0m)
                {
                    continue;
                }

                if (options.HideNoActivity && periodDebit == 0m && periodCredit == 0m && endingNet == 0m)
                {
                    continue;
                }

                if (inRange.Count == 0 && openingNet == 0m && !options.IncludeIdleAccounts)
                {
                    continue;
                }

                accounts.Add(new SubsidiaryLedgerAccountDto
                {
                    Code = account.Code,
                    Name = account.Name,
                    TotalDebit = periodDebit,
                    TotalCredit = periodCredit,
                    Rows = BuildRows(dataSet, facts, rangeStart, rangeEnd, openingNet, keyword, options.ShowCounterAccount, options.HideTotalsNoActivity),
                });
            }

            return new SubsidiaryLedgerDto
            {
                Meta = LedgerMetaDto.From(dataSet, rangeStart, rangeEnd),
                Accounts = accounts,
            };
        }

        /// <summary>
        /// 把跨科目的事实子集重排成账簿顺序。逐笔余额要求严格按时间推进，
        /// 因此明细类账页恒以日期为主键（同一天内再按凭证字、号、行号），不随查询条件的排序方式变化。
        /// </summary>
        internal static List<LedgerFact> OrderFacts(IEnumerable<LedgerFact> facts)
        {
            return facts
                .OrderBy(f => f.Date)
                .ThenBy(f => f.Word, StringComparer.Ordinal)
                .ThenBy(f => f.Number)
                .ThenBy(f => f.LineNumber)
                .ToList();
        }

        /// <summary>
        /// 明细账行序列（期初 → 逐笔 → 每月「本月合计」「本年累计」）。
        /// 区间内没有发生额的月份同样列出两组合计行，本年累计按自然年重新起算。
        /// 核算项目明细账与本账页同构，故共用此构造器：传入该口径下的全部事实与期初净额即可。
        /// </summary>
        internal static List<SubsidiaryLedgerRowDto> BuildRows(
            LedgerDataSet dataSet,
            IReadOnlyList<LedgerFact> facts,
            DateOnly rangeStart,
            DateOnly rangeEnd,
            decimal openingNet,
            string? keyword,
            bool showCounterAccount,
            bool hideTotalsNoActivity = false)
        {
            var running = openingNet;
            var year = rangeStart.Year;
            decimal yearDebit = 0m, yearCredit = 0m;
            var index = 0;

            // openingNet 已是查询起始日的余额，查询起始月之前的事实只补进「本年累计」，不重复滚入余额。
            while (index < facts.Count && facts[index].Date < rangeStart)
            {
                yearDebit += facts[index].Debit;
                yearCredit += facts[index].Credit;
                index++;
            }

            // 期初行与柠檬云一致：余额列是期初余额，借贷两栏是本年初至期初的累计发生额。
            var rows = new List<SubsidiaryLedgerRowDto>
            {
                new SubsidiaryLedgerRowDto
                {
                    Kind = SubsidiaryRowKind.Opening,
                    Date = rangeStart.ToString("yyyy-MM-dd"),
                    Summary = "期初余额",
                    Debit = yearDebit,
                    Credit = yearCredit,
                    Direction = LedgerDirectionText.Of(openingNet),
                    Balance = Math.Abs(openingNet),
                },
            };

            var lastMonth = new DateOnly(rangeEnd.Year, rangeEnd.Month, 1);
            for (var monthStart = rangeStart; monthStart <= lastMonth; monthStart = monthStart.AddMonths(1))
            {
                if (monthStart.Year != year)
                {
                    year = monthStart.Year;
                    yearDebit = 0m;
                    yearCredit = 0m;
                }

                var period = monthStart.Year * 100 + monthStart.Month;
                decimal monthDebit = 0m, monthCredit = 0m;

                while (index < facts.Count && facts[index].Period == period)
                {
                    var fact = facts[index++];
                    // 逐笔滚动始终按全量事实推进，摘要过滤只影响是否列示该行。
                    running += fact.NetDebit;
                    monthDebit += fact.Debit;
                    monthCredit += fact.Credit;
                    yearDebit += fact.Debit;
                    yearCredit += fact.Credit;

                    if (keyword is { Length: > 0 } && !fact.Summary.Contains(keyword, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    rows.Add(new SubsidiaryLedgerRowDto
                    {
                        Kind = SubsidiaryRowKind.Entry,
                        Date = fact.Date.ToString("yyyy-MM-dd"),
                        VoucherNo = fact.VoucherNo,
                        Summary = fact.Summary,
                        CounterAccount = showCounterAccount ? CounterAccountOf(dataSet, fact) : null,
                        Debit = fact.Debit,
                        Credit = fact.Credit,
                        Direction = LedgerDirectionText.Of(running),
                        Balance = Math.Abs(running),
                    });
                }

                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                // 无发生额时按条件跳过「本月合计」「本年累计」，避免空月份仍堆两行合计。
                if (!hideTotalsNoActivity || monthDebit != 0m || monthCredit != 0m)
                {
                    rows.Add(TotalRow(SubsidiaryRowKind.MonthTotal, "本月合计", monthEnd, monthDebit, monthCredit, running));
                    rows.Add(TotalRow(SubsidiaryRowKind.YearTotal, "本年累计", monthEnd, yearDebit, yearCredit, running));
                }
            }

            return rows;
        }

        private static SubsidiaryLedgerRowDto TotalRow(
            SubsidiaryRowKind kind,
            string label,
            DateOnly date,
            decimal debit,
            decimal credit,
            decimal running)
        {
            return new SubsidiaryLedgerRowDto
            {
                Kind = kind,
                Date = date.ToString("yyyy-MM-dd"),
                Summary = label,
                Debit = debit,
                Credit = credit,
                Direction = LedgerDirectionText.Of(running),
                Balance = Math.Abs(running),
            };
        }

        /// <summary>对方科目：同一凭证内方向相反的分录科目名称串，多科目用顿号连接。</summary>
        internal static string? CounterAccountOf(LedgerDataSet dataSet, LedgerFact fact)
        {
            // 一借多贷 / 一贷多借时只取与本行方向相反的那些分录的科目。
            var takeCredit = fact.Debit != 0m;
            var names = dataSet.FactsOfVoucher(fact.VoucherId)
                .Where(f => f.AccountId != fact.AccountId && (takeCredit ? f.Credit != 0m : f.Debit != 0m))
                .Select(f => dataSet.AccountsById.GetValueOrDefault(f.AccountId)?.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .ToList();

            return names.Count == 0 ? null : string.Join("、", names);
        }
    }
}
