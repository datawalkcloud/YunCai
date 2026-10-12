using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>
    /// 账簿数据装载工厂：把查询条件归一成自然月区间，一次性读入科目、档案、事实与期初，产出 <see cref="LedgerDataSet"/>。
    /// 注册为 Scoped，同一次请求内按「账套 + 区间 + 排序方式」复用同一份装载结果，
    /// 因此一个账页里多次取数不会重复查库。
    /// </summary>
    public sealed class LedgerDataSetFactory
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IAssistItemRepository _assistItemRepository;
        private readonly ILedgerSource _source;
        private readonly Dictionary<(Guid AccountSetId, DateOnly Start, DateOnly End, LedgerSortBy SortBy), LedgerDataSet> _cache = [];

        public LedgerDataSetFactory(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IAssistItemRepository assistItemRepository,
            ILedgerSource source)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _assistItemRepository = assistItemRepository;
            _source = source;
        }

        /// <summary>按条件装载账簿数据。条件非法或账套不存在时抛 <see cref="LedgerException"/>。</summary>
        public async Task<LedgerDataSet> LoadAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var validationError = options.Validate();
            if (validationError is not null)
            {
                throw new LedgerException(validationError);
            }

            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new LedgerException("指定的账套不存在。");

            var (start, end) = await ResolveRangeAsync(accountSet, options, cancellationToken);
            var key = (accountSetId, start, end, options.SortBy);
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var dataSet = await BuildDataSet(accountSet, options, start, end, cancellationToken);
            _cache[key] = dataSet;
            return dataSet;
        }

        private async Task<LedgerDataSet> BuildDataSet(
            AccountSet accountSet,
            LedgerQueryOptions options,
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken)
        {
            var accountSetId = accountSet.Id;
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var assistItems = await _assistItemRepository.GetAllAsync(accountSetId, cancellationToken);
            var facts = await _source.GetFactsAsync(accountSetId, start, end, cancellationToken);
            var openingFromVouchers = await _source.GetOpeningNetByAccountAsync(accountSetId, start, cancellationToken);
            var openingFromAssist = await _source.GetOpeningNetByAssistAsync(accountSetId, start, cancellationToken);
            var unposted = await _source.CountUnpostedAsync(accountSetId, start, end, cancellationToken);

            var opening = MergeAccountOpening(accounts, openingFromVouchers);

            return new LedgerDataSet
            {
                AccountSet = accountSet,
                Options = options,
                StartDate = start,
                EndDate = end,
                Accounts = accounts,
                AccountsById = accounts.ToDictionary(a => a.Id),
                AssistItemsById = assistItems.ToDictionary(i => i.Id),
                Facts = SortFacts(facts, options.SortBy),
                OpeningByAccount = opening,
                OpeningByAccountAssist = openingFromAssist,
                UnpostedVoucherCount = unposted,
            };
        }

        /// <summary>
        /// 科目期初 = 科目录入的启用期初余额（按余额方向换算为借 - 贷视角）+ 起始日之前已过账分录的累计净额。
        /// 启用期初只登记在明细科目上，故仅取 <see cref="Account.IsLeaf"/> 为真的科目。
        /// </summary>
        private static Dictionary<Guid, decimal> MergeAccountOpening(
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> openingFromVouchers)
        {
            var opening = new Dictionary<Guid, decimal>(openingFromVouchers.Count);
            foreach (var pair in openingFromVouchers)
            {
                opening[pair.Key] = pair.Value;
            }

            foreach (var account in accounts)
            {
                if (!account.IsLeaf || account.OpeningBalance == 0m)
                {
                    continue;
                }

                var netDebit = account.Direction == BalanceDirection.Debit ? account.OpeningBalance : -account.OpeningBalance;
                opening[account.Id] = opening.GetValueOrDefault(account.Id) + netDebit;
            }

            return opening;
        }

        private static IReadOnlyList<LedgerFact> SortFacts(IReadOnlyList<LedgerFact> facts, LedgerSortBy sortBy)
        {
            // 装载查询恒按日期序返回，按凭证字号排序时在此重排；行号保证同一凭证内稳定。
            IEnumerable<LedgerFact> ordered = sortBy switch
            {
                LedgerSortBy.VoucherNumber => facts
                    .OrderBy(f => f.Word, StringComparer.Ordinal)
                    .ThenBy(f => f.Number)
                    .ThenBy(f => f.Date)
                    .ThenBy(f => f.LineNumber),
                _ => facts.OrderBy(f => f.Date).ThenBy(f => f.Word, StringComparer.Ordinal).ThenBy(f => f.Number).ThenBy(f => f.LineNumber),
            };

            return ordered.ToList();
        }

        /// <summary>
        /// 归一并补全查询区间（不装载数据）。账页需要知道归一后的月份边界时调用，
        /// 例如明细账要把本年 1 月起的事实一并装入以计算「本年累计」。
        /// </summary>
        public async Task<(DateOnly Start, DateOnly End)> ResolveRangeAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var validationError = options.Validate();
            if (validationError is not null)
            {
                throw new LedgerException(validationError);
            }

            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new LedgerException("指定的账套不存在。");

            return await ResolveRangeAsync(accountSet, options, cancellationToken);
        }

        /// <summary>按账套启用期间与凭证边界补全缺省区间，再把起止日各归一到所在月首日/末日。</summary>
        private async Task<(DateOnly Start, DateOnly End)> ResolveRangeAsync(
            AccountSet accountSet,
            LedgerQueryOptions options,
            CancellationToken cancellationToken)
        {
            if (options.StartDate is null && options.EndDate is null)
            {
                var (first, last) = await _source.GetVoucherDateBoundsAsync(accountSet.Id, cancellationToken);
                if (first is { } min && last is { } max)
                {
                    return (MonthStart(min), MonthEnd(max));
                }
            }

            var enabledStart = new DateOnly(accountSet.StartYear, accountSet.StartMonth, 1);
            var start = MonthStart(options.StartDate ?? (options.EndDate is { } onlyEnd ? onlyEnd : enabledStart));
            var end = MonthEnd(options.EndDate ?? options.StartDate ?? enabledStart);
            return (start, end);
        }

        private static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

        private static DateOnly MonthEnd(DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
    }
}
