using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>
    /// 一次账簿查询的装载结果：同一请求内所有账页共用的事实、期初与两级聚合。
    /// <para>
    /// 装载只按「日期区间 + 已过账」裁剪，科目条件、级次、摘要关键字等显示条件一律在投影阶段应用
    /// （见 <see cref="SelectAccounts"/> 与各账页 Service）。这样合计口径不受显示条件影响，
    /// 也允许同一份数据在多个账页间复用。
    /// </para>
    /// </summary>
    public sealed class LedgerDataSet
    {
        private Dictionary<Guid, List<Guid>>? _subtreeIndex;
        private Dictionary<Guid, List<LedgerFact>>? _factsByVoucher;
        private ILookup<Guid, LedgerFact>? _factsByAccount;
        private LedgerBalances? _balances;

        /// <summary>所属账套。</summary>
        public required AccountSet AccountSet { get; init; }

        /// <summary>本次查询条件（已校验）。</summary>
        public required LedgerQueryOptions Options { get; init; }

        /// <summary>起始日期，已归一到所在月首日。</summary>
        public required DateOnly StartDate { get; init; }

        /// <summary>截止日期，已归一到所在月末日。</summary>
        public required DateOnly EndDate { get; init; }

        /// <summary>账套内全部科目，按编码排序。</summary>
        public required IReadOnlyList<Account> Accounts { get; init; }

        /// <summary>科目标识索引。</summary>
        public required IReadOnlyDictionary<Guid, Account> AccountsById { get; init; }

        /// <summary>账套内全部辅助核算档案，按标识索引。</summary>
        public required IReadOnlyDictionary<Guid, AssistItem> AssistItemsById { get; init; }

        /// <summary>区间内的已过账分录事实，已按查询条件的排序方式排好序。</summary>
        public required IReadOnlyList<LedgerFact> Facts { get; init; }

        /// <summary>期初净额（借 - 贷）：科目启用期初 + 起始日之前已过账分录累计。</summary>
        public required IReadOnlyDictionary<Guid, decimal> OpeningByAccount { get; init; }

        /// <summary>期初净额（借 - 贷）：起始日之前已过账分录按「科目 × 档案」累计。</summary>
        public required IReadOnlyDictionary<BalanceKey, decimal> OpeningByAccountAssist { get; init; }

        /// <summary>区间内未过账凭证张数，供页头提示「有 N 张凭证未过账，未纳入账簿」。</summary>
        public required int UnpostedVoucherCount { get; init; }

        /// <summary>起始会计期间（yyyyMM）。</summary>
        public int StartPeriod => PeriodOf(StartDate);

        /// <summary>截止会计期间（yyyyMM）。</summary>
        public int EndPeriod => PeriodOf(EndDate);

        /// <summary>期间显示文本，形如 2026-07-01 至 2026-07-31。</summary>
        public string PeriodText => $"{StartDate:yyyy-MM-dd} 至 {EndDate:yyyy-MM-dd}";

        /// <summary>账套已结账到的期间文本（未结账为 null），供页头标注进度。</summary>
        public string? ClosedPeriodText => AccountSet.ClosedPeriodText();

        /// <summary>截止期间是否已结账（未结账期间允许查询，仅在页头标注状态）。</summary>
        public bool IsEndPeriodClosed => AccountSet.ClosedPeriod >= EndPeriod;

        /// <summary>把日期换算成会计期间标识 yyyyMM。</summary>
        public static int PeriodOf(DateOnly date) => date.Year * 100 + date.Month;

        /// <summary>按查询条件挑选要列示的科目：编码条件、级次上限、是否只列最明细。</summary>
        public IReadOnlyList<Account> SelectAccounts()
        {
            var selector = AccountCodeSelector.Parse(Options.AccountExpression);
            var maxLevel = Options.MaxLevel;
            var minLevel = Options.MinLevel;
            var result = new List<Account>();
            foreach (var account in Accounts)
            {
                if (Options.ShowOnlyLeaf && !account.IsLeaf)
                {
                    continue;
                }

                if (maxLevel is { } level && account.Level > level)
                {
                    continue;
                }

                if (minLevel is { } lo && account.Level < lo)
                {
                    continue;
                }

                if (selector.Matches(account))
                {
                    result.Add(account);
                }
            }

            return result;
        }

        /// <summary>科目自身及其全部下级科目的标识（含自身），按编码排序。用上级链路构建，不依赖编码前缀约定。</summary>
        public IReadOnlyList<Guid> SubtreeAccountIds(Account account)
        {
            _subtreeIndex ??= BuildSubtreeIndex();
            return _subtreeIndex.TryGetValue(account.Id, out var ids) ? ids : [account.Id];
        }

        /// <summary>
        /// 取科目余额：明细科目直接取聚合值；非明细科目为其全部下级科目（含自身直接挂账部分）的合计，
        /// 使总账与余额表的上级行恒等于下级之和。
        /// </summary>
        public BalanceAmounts GetAccountBalance(Account account)
        {
            var ids = SubtreeAccountIds(account);
            if (ids.Count == 1)
            {
                return Balances.ByAccount.GetValueOrDefault(account.Id);
            }

            decimal opening = 0m, debit = 0m, credit = 0m;
            foreach (var id in ids)
            {
                var balance = Balances.ByAccount.GetValueOrDefault(id);
                opening += balance.OpeningNet;
                debit += balance.PeriodDebit;
                credit += balance.PeriodCredit;
            }

            return new BalanceAmounts(opening, debit, credit);
        }

        /// <summary>两级聚合结果（惰性计算并缓存）。</summary>
        public LedgerBalances Balances => _balances ??= BuildBalances();

        /// <summary>同一凭证内的全部事实（保持装载顺序），供「显示对方科目」等需要按凭证取邻行数据的投影复用。</summary>
        public IReadOnlyList<LedgerFact> FactsOfVoucher(Guid voucherId)
        {
            _factsByVoucher ??= Facts.GroupBy(f => f.VoucherId).ToDictionary(g => g.Key, g => g.ToList());
            return _factsByVoucher.TryGetValue(voucherId, out var facts) ? facts : [];
        }

        /// <summary>同一科目下的事实分组（惰性构建），供各账页按科目取流水时避免全表扫描。</summary>
        public ILookup<Guid, LedgerFact> FactsByAccount => _factsByAccount ??= Facts.ToLookup(f => f.AccountId);

        /// <summary>
        /// 指定科目集合（通常是某科目的全部下级）在该辅助核算维度下登记过的档案标识。
        /// 除区间内分录外，还包含起始日之前分录（期初键）登记的档案，否则本期无发生额的档案会丢失余额。
        /// </summary>
        public IEnumerable<Guid> AssistItemIds(AssistType type, IReadOnlyList<Guid> accountIds)
        {
            var set = new HashSet<Guid>();
            var idSet = accountIds.ToHashSet();
            foreach (var accountId in idSet)
            {
                foreach (var fact in FactsByAccount[accountId])
                {
                    foreach (var assist in fact.Assists)
                    {
                        if (assist.Type == type)
                        {
                            set.Add(assist.ItemId);
                        }
                    }
                }
            }

            foreach (var key in OpeningByAccountAssist.Keys)
            {
                if (key.AssistType == type && key.AssistItemId is { } itemId && idSet.Contains(key.AccountId))
                {
                    set.Add(itemId);
                }
            }

            return set;
        }

        /// <summary>把科目名称按编码前缀展开成「上级科目 → 本级」的完整路径文本，用于多栏账与明细账表头。</summary>
        public string AccountPath(Account account)
        {
            var segments = new List<string>();
            var current = account;
            while (current is not null)
            {
                segments.Add(current.Name);
                current = current.ParentId is { } parentId ? AccountsById.GetValueOrDefault(parentId) : null;
            }

            segments.Reverse();
            return string.Join(" / ", segments);
        }

        private LedgerBalances BuildBalances()
        {
            var periodDebit = new Dictionary<Guid, decimal>();
            var periodCredit = new Dictionary<Guid, decimal>();
            var assistDebit = new Dictionary<BalanceKey, decimal>();
            var assistCredit = new Dictionary<BalanceKey, decimal>();

            foreach (var fact in Facts)
            {
                periodDebit[fact.AccountId] = periodDebit.GetValueOrDefault(fact.AccountId) + fact.Debit;
                periodCredit[fact.AccountId] = periodCredit.GetValueOrDefault(fact.AccountId) + fact.Credit;

                // 同一分录挂同一类别的多个档案时，各档案分别计一次全额（多选辅助核算的通行口径）。
                foreach (var assist in fact.Assists)
                {
                    var key = new BalanceKey(fact.AccountId, assist.Type, assist.ItemId);
                    assistDebit[key] = assistDebit.GetValueOrDefault(key) + fact.Debit;
                    assistCredit[key] = assistCredit.GetValueOrDefault(key) + fact.Credit;
                }
            }

            var byAccount = new Dictionary<Guid, BalanceAmounts>();
            foreach (var accountId in UnionKeys(periodDebit.Keys, periodCredit.Keys, OpeningByAccount.Keys))
            {
                var opening = OpeningByAccount.GetValueOrDefault(accountId);
                byAccount[accountId] = new BalanceAmounts(opening, periodDebit.GetValueOrDefault(accountId), periodCredit.GetValueOrDefault(accountId));
            }

            var byAccountAssist = new Dictionary<BalanceKey, BalanceAmounts>();
            foreach (var key in UnionKeys(assistDebit.Keys, assistCredit.Keys, OpeningByAccountAssist.Keys))
            {
                byAccountAssist[key] = new BalanceAmounts(
                    OpeningByAccountAssist.GetValueOrDefault(key),
                    assistDebit.GetValueOrDefault(key),
                    assistCredit.GetValueOrDefault(key));
            }

            return new LedgerBalances
            {
                ByAccount = byAccount,
                ByAccountAssist = byAccountAssist,
            };
        }

        private static IEnumerable<T> UnionKeys<T>(IEnumerable<T> a, IEnumerable<T> b, IEnumerable<T> c)
        {
            var set = new HashSet<T>(a);
            set.UnionWith(b);
            set.UnionWith(c);
            return set;
        }

        private Dictionary<Guid, List<Guid>> BuildSubtreeIndex()
        {
            var childrenByParent = new Dictionary<Guid, List<Account>>();
            foreach (var account in Accounts)
            {
                if (account.ParentId is { } parentId)
                {
                    if (!childrenByParent.TryGetValue(parentId, out var list))
                    {
                        list = [];
                        childrenByParent[parentId] = list;
                    }

                    list.Add(account);
                }
            }

            var index = new Dictionary<Guid, List<Guid>>(Accounts.Count);
            foreach (var account in Accounts)
            {
                CollectSubtree(account, childrenByParent, index);
            }

            return index;
        }

        private static void CollectSubtree(
            Account account,
            Dictionary<Guid, List<Account>> childrenByParent,
            Dictionary<Guid, List<Guid>> index)
        {
            if (index.TryGetValue(account.Id, out var collected))
            {
                return;
            }

            // 先占位再递归，保证父级列表在子级补齐后可直接追加，整棵树只遍历一遍。
            var ids = new List<Guid> { account.Id };
            index[account.Id] = ids;
            if (childrenByParent.TryGetValue(account.Id, out var children))
            {
                children.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));
                foreach (var child in children)
                {
                    CollectSubtree(child, childrenByParent, index);
                    ids.AddRange(index[child.Id]);
                }
            }
        }
    }
}
