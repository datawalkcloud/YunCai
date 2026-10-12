using KuaiJi.Modules.Accounting.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>多栏账的栏头：一个明细科目占一栏。</summary>
    public sealed class MultiColumnHeaderDto
    {
        /// <summary>科目编码。</summary>
        public required string Code { get; init; }

        /// <summary>科目名称。</summary>
        public required string Name { get; init; }
    }

    /// <summary>多栏账的一行。各栏金额与 <see cref="MultiColumnDto.Columns"/> 按顺序一一对应。</summary>
    public sealed class MultiColumnRowDto
    {
        /// <summary>行种类（期初 / 逐笔 / 本月合计 / 本年累计）。</summary>
        public SubsidiaryRowKind Kind { get; init; }

        /// <summary>凭证日期（期初行为空）。</summary>
        public string? Date { get; init; }

        /// <summary>凭证字号（期初行为空）。</summary>
        public string? VoucherNo { get; init; }

        /// <summary>摘要。合计行显示「本月合计」「本年累计」。</summary>
        public required string Summary { get; init; }

        /// <summary>各栏借方金额。</summary>
        public IReadOnlyList<decimal> Debits { get; init; } = [];

        /// <summary>各栏贷方金额。</summary>
        public IReadOnlyList<decimal> Credits { get; init; } = [];

        /// <summary>余额方向：借 / 贷 / 平（合计行为空）。</summary>
        public string? Direction { get; init; }

        /// <summary>余额金额（合计行为空）。</summary>
        public decimal? Balance { get; init; }
    }

    /// <summary>多栏账：单一科目的逐笔发生额按明细科目分栏列示。</summary>
    public sealed class MultiColumnDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>主科目编码。</summary>
        public required string Code { get; init; }

        /// <summary>主科目名称。</summary>
        public required string Name { get; init; }

        /// <summary>主科目全名路径，如「管理费用 / 办公费」。</summary>
        public required string Path { get; init; }

        /// <summary>栏头：主科目的直接下级科目；主科目为明细科目时只有其自身一栏。</summary>
        public IReadOnlyList<MultiColumnHeaderDto> Columns { get; init; } = [];

        /// <summary>行序列：期初 → 逐笔（每月末「本月合计」）→「本年累计」。</summary>
        public IReadOnlyList<MultiColumnRowDto> Rows { get; init; } = [];

        /// <summary>区间内借方总计。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>区间内贷方总计。</summary>
        public decimal TotalCredit { get; init; }
    }

    /// <summary>
    /// 多栏账应用服务：选定一个科目（如 5602 管理费用），把其下级科目展开成栏，逐笔登记到对应栏。
    /// 与其他明细类账页一样，装载区间向前扩展到本年 1 月 1 日，以支撑逐笔余额与「本年累计」。
    /// </summary>
    public sealed class MultiColumnLedgerService
    {
        private readonly LedgerDataSetFactory _factory;

        public MultiColumnLedgerService(LedgerDataSetFactory factory)
        {
            _factory = factory;
        }

        /// <summary>生成多栏账。</summary>
        public async Task<MultiColumnDto> GetAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            if (options.SingleAccountId is not { } singleAccountId)
            {
                throw new LedgerException("多栏账需要先选择科目。");
            }

            var (rangeStart, rangeEnd) = await _factory.ResolveRangeAsync(accountSetId, options, cancellationToken);
            var dataSet = await _factory.LoadAsync(
                accountSetId,
                options with { StartDate = new DateOnly(rangeStart.Year, 1, 1), EndDate = rangeEnd },
                cancellationToken);

            var account = dataSet.AccountsById.GetValueOrDefault(singleAccountId)
                          ?? throw new LedgerException("选定的科目不属于当前账套。");

            var subtreeIds = dataSet.SubtreeAccountIds(account);
            var factsByAccount = dataSet.Facts.ToLookup(f => f.AccountId);
            var facts = SubsidiaryLedgerService.OrderFacts(subtreeIds.SelectMany(id => factsByAccount[id]));

            // 栏次取直接下级；主科目为明细科目时退化为单栏；本级直接挂账时追加「本级」栏，
            // 使逐笔金额不会因找不到所属栏而丢失。
            var children = dataSet.Accounts
                .Where(a => a.ParentId == account.Id)
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .ToList();
            var columns = children.Count > 0 ? new List<Account>(children) : new List<Account> { account };
            var columnIndex = new Dictionary<Guid, int>();
            for (var i = 0; i < columns.Count; i++)
            {
                columnIndex[columns[i].Id] = i;
            }

            if (children.Count > 0 && facts.Any(f => f.AccountId == account.Id))
            {
                columnIndex[account.Id] = columns.Count;
                columns.Add(account);
            }

            var cellOf = (Guid accountId) => ResolveColumnIndex(dataSet, columnIndex, accountId);
            var priorNet = facts.Where(f => f.Date < rangeStart).Sum(f => f.NetDebit);
            var openingNet = subtreeIds.Sum(id => dataSet.OpeningByAccount.GetValueOrDefault(id)) + priorNet;
            var keyword = options.SummaryKeyword?.Trim();

            var rows = new List<MultiColumnRowDto>
            {
                new MultiColumnRowDto
                {
                    Kind = SubsidiaryRowKind.Opening,
                    Summary = "期初余额",
                    Debits = new decimal[columns.Count],
                    Credits = new decimal[columns.Count],
                    Direction = LedgerDirectionText.Of(openingNet),
                    Balance = Math.Abs(openingNet),
                },
            };

            var yearDebits = new decimal[columns.Count];
            var yearCredits = new decimal[columns.Count];
            var running = openingNet;
            var currentPeriod = 0;
            var monthDebits = new decimal[columns.Count];
            var monthCredits = new decimal[columns.Count];
            decimal rangeDebit = 0m, rangeCredit = 0m;

            foreach (var fact in facts)
            {
                // 逐笔余额按全量事实滚动，摘要关键字只影响是否列示该行。
                running += fact.NetDebit;
                var cell = cellOf(fact.AccountId);

                if (fact.Date < rangeStart)
                {
                    // 区间前的月份只累计入本年，不列示也不占月度行。
                    yearDebits[cell] += fact.Debit;
                    yearCredits[cell] += fact.Credit;
                    continue;
                }

                if (currentPeriod != 0 && fact.Period != currentPeriod)
                {
                    rows.Add(TotalRow("本月合计", monthDebits, monthCredits));
                    Array.Fill(monthDebits, 0m);
                    Array.Fill(monthCredits, 0m);
                }

                currentPeriod = fact.Period;
                monthDebits[cell] += fact.Debit;
                monthCredits[cell] += fact.Credit;
                yearDebits[cell] += fact.Debit;
                yearCredits[cell] += fact.Credit;
                rangeDebit += fact.Debit;
                rangeCredit += fact.Credit;

                if (keyword is { Length: > 0 } && !fact.Summary.Contains(keyword, StringComparison.Ordinal))
                {
                    continue;
                }

                var debits = new decimal[columns.Count];
                var credits = new decimal[columns.Count];
                debits[cell] = fact.Debit;
                credits[cell] = fact.Credit;
                rows.Add(new MultiColumnRowDto
                {
                    Kind = SubsidiaryRowKind.Entry,
                    Date = fact.Date.ToString("yyyy-MM-dd"),
                    VoucherNo = fact.VoucherNo,
                    Summary = fact.Summary,
                    Debits = debits,
                    Credits = credits,
                    Direction = LedgerDirectionText.Of(running),
                    Balance = Math.Abs(running),
                });
            }

            if (currentPeriod != 0)
            {
                rows.Add(TotalRow("本月合计", monthDebits, monthCredits));
            }

            // 本年累计的栏次合计须与逐月合计一致，故直接取逐笔累加结果。
            rows.Add(TotalRow("本年累计", yearDebits, yearCredits));

            return new MultiColumnDto
            {
                Meta = LedgerMetaDto.From(dataSet, rangeStart, rangeEnd),
                Code = account.Code,
                Name = account.Name,
                Path = dataSet.AccountPath(account),
                Columns = columns.Select(c => new MultiColumnHeaderDto { Code = c.Code, Name = c.Name }).ToList(),
                Rows = rows,
                TotalDebit = rangeDebit,
                TotalCredit = rangeCredit,
            };
        }

        private static MultiColumnRowDto TotalRow(string label, decimal[] debits, decimal[] credits) => new()
        {
            Kind = label == "本年累计" ? SubsidiaryRowKind.YearTotal : SubsidiaryRowKind.MonthTotal,
            Summary = label,
            Debits = (decimal[])debits.Clone(),
            Credits = (decimal[])credits.Clone(),
        };

        /// <summary>把分录科目归到最近的祖先栏：分录可登记在比栏次更明细的科目上。</summary>
        private static int ResolveColumnIndex(LedgerDataSet dataSet, Dictionary<Guid, int> columnIndex, Guid accountId)
        {
            var current = dataSet.AccountsById.GetValueOrDefault(accountId);
            while (current is not null)
            {
                if (columnIndex.TryGetValue(current.Id, out var index))
                {
                    return index;
                }

                current = current.ParentId is { } parentId ? dataSet.AccountsById.GetValueOrDefault(parentId) : null;
            }

            throw new LedgerException("科目层级数据不完整，无法分栏。");
        }
    }
}
