using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>核算项目余额表的一个档案行。</summary>
    public sealed class AssistBalanceRowDto
    {
        /// <summary>档案编码。</summary>
        public required string Code { get; init; }

        /// <summary>档案名称。</summary>
        public required string Name { get; init; }

        /// <summary>期初余额（借方）。</summary>
        public decimal OpeningDebit { get; init; }

        /// <summary>期初余额（贷方）。</summary>
        public decimal OpeningCredit { get; init; }

        /// <summary>本期借方发生额。</summary>
        public decimal PeriodDebit { get; init; }

        /// <summary>本期贷方发生额。</summary>
        public decimal PeriodCredit { get; init; }

        /// <summary>期末余额（借方）。</summary>
        public decimal EndingDebit { get; init; }

        /// <summary>期末余额（贷方）。</summary>
        public decimal EndingCredit { get; init; }

        /// <summary>余额方向：借 / 贷 / 平。</summary>
        public required string Direction { get; init; }
    }

    /// <summary>核算项目余额表的一个科目段。</summary>
    public sealed class AssistBalanceAccountDto
    {
        /// <summary>科目编码。</summary>
        public required string Code { get; init; }

        /// <summary>科目名称。</summary>
        public required string Name { get; init; }

        /// <summary>该科目下各档案的余额行，按档案编码排序。</summary>
        public IReadOnlyList<AssistBalanceRowDto> Rows { get; init; } = [];
    }

    /// <summary>核算项目余额表。</summary>
    public sealed class AssistBalanceDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>主维度中文名，如「客户」。</summary>
        public required string AssistTypeText { get; init; }

        /// <summary>按科目分段的档案余额。</summary>
        public IReadOnlyList<AssistBalanceAccountDto> Accounts { get; init; } = [];
    }

    /// <summary>核算项目明细账的一个档案段（结构与明细账的科目段一致）。</summary>
    public sealed class AssistSubsidiaryItemDto
    {
        /// <summary>档案编码。</summary>
        public required string Code { get; init; }

        /// <summary>档案名称。</summary>
        public required string Name { get; init; }

        /// <summary>区间内借方合计。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>区间内贷方合计。</summary>
        public decimal TotalCredit { get; init; }

        /// <summary>期初 → 逐笔（每月末「本月合计」）→ 本年累计。</summary>
        public IReadOnlyList<SubsidiaryLedgerRowDto> Rows { get; init; } = [];
    }

    /// <summary>核算项目明细账的一个科目段。</summary>
    public sealed class AssistSubsidiaryAccountDto
    {
        /// <summary>科目编码。</summary>
        public required string Code { get; init; }

        /// <summary>科目名称。</summary>
        public required string Name { get; init; }

        /// <summary>该科目下各档案的明细账段。</summary>
        public IReadOnlyList<AssistSubsidiaryItemDto> Items { get; init; } = [];
    }

    /// <summary>核算项目明细账。</summary>
    public sealed class AssistSubsidiaryDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>主维度中文名。</summary>
        public required string AssistTypeText { get; init; }

        /// <summary>按科目分段的档案明细账。</summary>
        public IReadOnlyList<AssistSubsidiaryAccountDto> Accounts { get; init; } = [];
    }

    /// <summary>核算项目组合表的一个次维度单元格（列头只需编码名称，金额为零时不填充）。</summary>
    public sealed class AssistCombineCellDto
    {
        /// <summary>档案编码。</summary>
        public required string Code { get; init; }

        /// <summary>档案名称。</summary>
        public required string Name { get; init; }

        /// <summary>借方合计。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方合计。</summary>
        public decimal Credit { get; init; }
    }

    /// <summary>核算项目组合表的一个主维度分组。</summary>
    public sealed class AssistCombineGroupDto
    {
        /// <summary>主档案编码。</summary>
        public required string Code { get; init; }

        /// <summary>主档案名称。</summary>
        public required string Name { get; init; }

        /// <summary>次维度各档案的发生额，与表头列顺序一致。</summary>
        public IReadOnlyList<AssistCombineCellDto> Cells { get; init; } = [];

        /// <summary>该主档案的借方小计。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>该主档案的贷方小计。</summary>
        public decimal TotalCredit { get; init; }
    }

    /// <summary>核算项目组合表：主维度 × 次维度交叉的发生额。</summary>
    public sealed class AssistCombineDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>主维度中文名。</summary>
        public required string AssistTypeText { get; init; }

        /// <summary>次维度中文名。</summary>
        public required string SecondaryAssistTypeText { get; init; }

        /// <summary>次维度列头（区间内出现过的档案，按编码排序）。</summary>
        public IReadOnlyList<AssistCombineCellDto> Columns { get; init; } = [];

        /// <summary>主维度分组行。</summary>
        public IReadOnlyList<AssistCombineGroupDto> Groups { get; init; } = [];

        /// <summary>借方总计。</summary>
        public decimal TotalDebit { get; init; }

        /// <summary>贷方总计。</summary>
        public decimal TotalCredit { get; init; }
    }

    /// <summary>
    /// 核算项目（辅助核算）三张账页：余额表、明细账、组合表。
    /// <para>
    /// 档案行按凭证分录上登记的档案归集：一条分录在同一类别下挂多个档案时，各档案分别计一次全额，
    /// 因此「档案合计 = 科目金额」只在单档案分录上成立，多选档案的分录会同时出现在多行——
    /// 这与柠檬云「核算项目余额表」的口径一致。
    /// </para>
    /// 明细账的逐笔行、期初与本月/本年合计复用明细账的构造器，保证两张账页的余额口径完全相同。
    /// </summary>
    public sealed class AssistLedgerService
    {
        private readonly LedgerDataSetFactory _factory;

        public AssistLedgerService(LedgerDataSetFactory factory)
        {
            _factory = factory;
        }

        /// <summary>生成核算项目余额表。</summary>
        public async Task<AssistBalanceDto> GetBalanceAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var type = RequireType(options);
            var dataSet = await _factory.LoadAsync(accountSetId, options, cancellationToken);

            var accounts = new List<AssistBalanceAccountDto>();
            foreach (var account in dataSet.SelectAccounts())
            {
                var subtreeIds = dataSet.SubtreeAccountIds(account);
                var rows = new List<AssistBalanceRowDto>();

                foreach (var (itemId, code, name) in OrderItems(dataSet, dataSet.AssistItemIds(type, subtreeIds)))
                {
                    var balance = SumBalance(dataSet, type, itemId, subtreeIds);
                    if (balance.IsEmpty && !options.IncludeIdleAccounts)
                    {
                        continue;
                    }

                    if (options.HideZeroBalance && balance.EndingNet == 0m)
                    {
                        continue;
                    }

                    if (options.HideNoActivity && balance.PeriodDebit == 0m && balance.PeriodCredit == 0m && balance.EndingNet == 0m)
                    {
                        continue;
                    }

                    var openingSplit = BalanceAmounts.SplitBySign(balance.OpeningNet);
                    var endingSplit = BalanceAmounts.SplitBySign(balance.EndingNet);
                    rows.Add(new AssistBalanceRowDto
                    {
                        Code = code,
                        Name = name,
                        OpeningDebit = openingSplit.Debit,
                        OpeningCredit = openingSplit.Credit,
                        PeriodDebit = balance.PeriodDebit,
                        PeriodCredit = balance.PeriodCredit,
                        EndingDebit = endingSplit.Debit,
                        EndingCredit = endingSplit.Credit,
                        Direction = LedgerDirectionText.Of(balance.EndingNet),
                    });
                }

                if (rows.Count > 0)
                {
                    accounts.Add(new AssistBalanceAccountDto
                    {
                        Code = account.Code,
                        Name = account.Name,
                        Rows = rows,
                    });
                }
            }

            return new AssistBalanceDto
            {
                Meta = LedgerMetaDto.From(dataSet),
                AssistTypeText = AssistTypeText.Of(type),
                Accounts = accounts,
            };
        }

        /// <summary>生成核算项目明细账。</summary>
        public async Task<AssistSubsidiaryDto> GetSubsidiaryAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var type = RequireType(options);
            var (rangeStart, rangeEnd) = await _factory.ResolveRangeAsync(accountSetId, options, cancellationToken);

            // 与明细账同一做法：向前装到本年 1 月 1 日，逐笔余额与「本年累计」才有数据。
            var dataSet = await _factory.LoadAsync(
                accountSetId,
                options with { StartDate = new DateOnly(rangeStart.Year, 1, 1), EndDate = rangeEnd },
                cancellationToken);

            var keyword = options.SummaryKeyword?.Trim();
            var onlyItemId = options.AssistItemId;
            var factsByAccount = dataSet.FactsByAccount;
            var accounts = new List<AssistSubsidiaryAccountDto>();

            foreach (var account in dataSet.SelectAccounts())
            {
                var subtreeIds = dataSet.SubtreeAccountIds(account);

                // 一条分录可挂多个同类别档案，同一事实会进入多个档案段，各段分别计全额。
                var factsByItem = new Dictionary<Guid, List<LedgerFact>>();
                foreach (var fact in subtreeIds.SelectMany(id => factsByAccount[id]))
                {
                    foreach (var assist in fact.Assists)
                    {
                        if (assist.Type != type || (onlyItemId is { } only && assist.ItemId != only))
                        {
                            continue;
                        }

                        if (!factsByItem.TryGetValue(assist.ItemId, out var list))
                        {
                            list = [];
                            factsByItem[assist.ItemId] = list;
                        }

                        list.Add(fact);
                    }
                }

                var items = new List<AssistSubsidiaryItemDto>();
                foreach (var (itemId, code, name) in OrderItems(dataSet, factsByItem.Keys))
                {
                    var facts = SubsidiaryLedgerService.OrderFacts(factsByItem[itemId]);
                    var priorNet = facts.Where(f => f.Date < rangeStart).Sum(f => f.NetDebit);
                    var inRange = facts.Where(f => f.Date >= rangeStart).ToList();
                    var openingNet = subtreeIds.Sum(id => dataSet.OpeningByAccountAssist.GetValueOrDefault(new BalanceKey(id, type, itemId))) + priorNet;
                    var endingNet = openingNet + inRange.Sum(f => f.NetDebit);

                    if (options.HideZeroBalance && endingNet == 0m)
                    {
                        continue;
                    }

                    if (options.HideNoActivity && inRange.Count == 0 && endingNet == 0m)
                    {
                        continue;
                    }

                    items.Add(new AssistSubsidiaryItemDto
                    {
                        Code = code,
                        Name = name,
                        TotalDebit = inRange.Sum(f => f.Debit),
                        TotalCredit = inRange.Sum(f => f.Credit),
                        Rows = SubsidiaryLedgerService.BuildRows(dataSet, facts, rangeStart, openingNet, keyword, options.ShowCounterAccount),
                    });
                }

                if (items.Count > 0)
                {
                    accounts.Add(new AssistSubsidiaryAccountDto
                    {
                        Code = account.Code,
                        Name = account.Name,
                        Items = items,
                    });
                }
            }

            return new AssistSubsidiaryDto
            {
                Meta = LedgerMetaDto.From(dataSet, rangeStart, rangeEnd),
                AssistTypeText = AssistTypeText.Of(type),
                Accounts = accounts,
            };
        }

        /// <summary>生成核算项目组合表（主维度 × 次维度交叉发生额）。</summary>
        public async Task<AssistCombineDto> GetCombineAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var primary = RequireType(options);
            var secondary = options.SecondaryAssistType
                            ?? throw new LedgerException("组合表需要同时指定主、次两个辅助核算类别。");

            var dataSet = await _factory.LoadAsync(accountSetId, options, cancellationToken);
            var listedIds = dataSet.SelectAccounts().SelectMany(a => dataSet.SubtreeAccountIds(a)).ToHashSet();

            // (主档案, 次档案) → 借贷合计；同一分录两侧的档案做笛卡尔组合。
            var pairs = new Dictionary<(Guid Primary, Guid Secondary), (decimal Debit, decimal Credit)>();
            foreach (var fact in dataSet.Facts)
            {
                if (!listedIds.Contains(fact.AccountId))
                {
                    continue;
                }

                var primaries = fact.Assists.Where(a => a.Type == primary).Select(a => a.ItemId).Distinct().ToList();
                var secondaries = fact.Assists.Where(a => a.Type == secondary).Select(a => a.ItemId).Distinct().ToList();
                if (primaries.Count == 0 || secondaries.Count == 0)
                {
                    continue;
                }

                foreach (var p in primaries)
                {
                    foreach (var s in secondaries)
                    {
                        var current = pairs.GetValueOrDefault((p, s));
                        pairs[(p, s)] = (current.Debit + fact.Debit, current.Credit + fact.Credit);
                    }
                }
            }

            var orderedSecondaries = OrderItems(dataSet, pairs.Keys.Select(k => k.Secondary).Distinct()).ToList();
            var groups = new List<AssistCombineGroupDto>();
            foreach (var primaryItem in OrderItems(dataSet, pairs.Keys.Select(k => k.Primary).Distinct()))
            {
                var cells = orderedSecondaries
                    .Select(s => (s, Value: pairs.GetValueOrDefault((primaryItem.Id, s.Id))))
                    .Where(x => x.Value.Debit != 0m || x.Value.Credit != 0m)
                    .Select(x => new AssistCombineCellDto
                    {
                        Code = x.s.Code,
                        Name = x.s.Name,
                        Debit = x.Value.Debit,
                        Credit = x.Value.Credit,
                    })
                    .ToList();

                if (cells.Count == 0)
                {
                    continue;
                }

                groups.Add(new AssistCombineGroupDto
                {
                    Code = primaryItem.Code,
                    Name = primaryItem.Name,
                    Cells = cells,
                    TotalDebit = cells.Sum(c => c.Debit),
                    TotalCredit = cells.Sum(c => c.Credit),
                });
            }

            return new AssistCombineDto
            {
                Meta = LedgerMetaDto.From(dataSet),
                AssistTypeText = AssistTypeText.Of(primary),
                SecondaryAssistTypeText = AssistTypeText.Of(secondary),
                Columns = orderedSecondaries.Select(i => new AssistCombineCellDto { Code = i.Code, Name = i.Name }).ToList(),
                Groups = groups,
                TotalDebit = groups.Sum(g => g.TotalDebit),
                TotalCredit = groups.Sum(g => g.TotalCredit),
            };
        }

        private static AssistType RequireType(LedgerQueryOptions options)
            => options.AssistType ?? throw new LedgerException("请先选择辅助核算类别。");

        private static BalanceAmounts SumBalance(LedgerDataSet dataSet, AssistType type, Guid itemId, IReadOnlyList<Guid> accountIds)
        {
            decimal opening = 0m, debit = 0m, credit = 0m;
            foreach (var accountId in accountIds)
            {
                var key = new BalanceKey(accountId, type, itemId);
                opening += dataSet.OpeningByAccountAssist.GetValueOrDefault(key);
                var balance = dataSet.Balances.ByAccountAssist.GetValueOrDefault(key);
                debit += balance.PeriodDebit;
                credit += balance.PeriodCredit;
            }

            return new BalanceAmounts(opening, debit, credit);
        }

        /// <summary>把档案标识换成编码与名称并按编码排序；档案不存在（已删除）时排在最后。</summary>
        private static List<(Guid Id, string Code, string Name)> OrderItems(LedgerDataSet dataSet, IEnumerable<Guid> itemIds)
        {
            return itemIds
                .Select(id => (Id: id, Item: dataSet.AssistItemsById.GetValueOrDefault(id)))
                .OrderBy(x => x.Item?.Code ?? "\uffff", StringComparer.Ordinal)
                .Select(x => (x.Id, Code: x.Item?.Code ?? string.Empty, Name: x.Item?.Name ?? string.Empty))
                .ToList();
        }
    }
}
