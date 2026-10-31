using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 利润表（会企02表）应用服务：按选定会计期间，从损益类科目的凭证发生额归集各标准行次的
    /// 本期金额与本年累计金额。收入类行次以贷方为正方向，费用类行次以借方为正方向。
    /// 涉及「本年利润」科目的凭证视为期末结转损益凭证，汇总时予以剔除，避免结转后各行清零。
    /// 行次归集与期间解析的共享口径见 <see cref="IncomeLineMapper"/> 与 <see cref="ReportPeriodResolver"/>。
    /// </summary>
    public sealed class IncomeStatementService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public IncomeStatementService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>
        /// 生成指定账套、指定会计期间的利润表。year/month 省略时优先取最近有凭证的月份，
        /// 无凭证时回退账套启用期间。本期金额取选定月份，本年累计取本年 1 月 1 日至选定月末。
        /// </summary>
        public async Task<IncomeStatementDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var (accountSet, accountsById, vouchers) = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);
            var periodStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var lines = BuildLines(vouchers, accountsById, resolvedYear, periodStart, periodStart.AddMonths(1));

            return new IncomeStatementDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Lines = lines,
            };
        }

        /// <summary>
        /// 利润表季报：本季金额 + 本年累计金额。year/quarter 省略时取最近有凭证的季度
        /// （无凭证回退账套启用期间所在季度）。
        /// </summary>
        public async Task<IncomeStatementQuarterDto> GetQuarterAsync(Guid accountSetId, int? year, int? quarter, CancellationToken cancellationToken = default)
        {
            var (accountSet, accountsById, vouchers) = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedQuarter) = ResolveQuarter(accountSet, vouchers, year, quarter);
            var quarterStart = new DateOnly(resolvedYear, (resolvedQuarter - 1) * 3 + 1, 1);
            var lines = BuildLines(vouchers, accountsById, resolvedYear, quarterStart, quarterStart.AddMonths(3));

            return new IncomeStatementQuarterDto
            {
                Year = resolvedYear,
                Quarter = resolvedQuarter,
                PeriodText = $"{resolvedYear:D4}年第{resolvedQuarter}季度",
                Lines = lines,
            };
        }

        /// <summary>
        /// 组装报表行：本期金额、本年累计金额，以及上年同期累计金额（上年 1 月 1 日至上年同期末）。
        /// </summary>
        private static List<IncomeStatementLineDto> BuildLines(
            IReadOnlyList<Voucher> vouchers,
            Dictionary<Guid, Account> accountsById,
            int year,
            DateOnly periodStart,
            DateOnly periodEndExclusive)
        {
            var current = AccumulateLineNets(
                vouchers, accountsById, new DateOnly(year, 1, 1), periodStart, periodEndExclusive);

            var months = (periodEndExclusive.Year - periodStart.Year) * 12 + periodEndExclusive.Month - periodStart.Month;
            var lastYearStart = new DateOnly(year - 1, 1, 1);
            var lastYear = AccumulateLineNets(
                vouchers,
                accountsById,
                lastYearStart,
                periodStart.AddYears(-1),
                periodStart.AddYears(-1).AddMonths(months));

            return current.Keys
                .Union(lastYear.Keys)
                .OrderBy(line => line)
                .Select(line => new IncomeStatementLineDto
                {
                    Line = line,
                    Current = current.TryGetValue(line, out var cur) ? cur.current : 0m,
                    YearToDate = current.TryGetValue(line, out var ytd) ? ytd.ytd : 0m,
                    LastYear = lastYear.TryGetValue(line, out var ly) ? ly.ytd : 0m,
                })
                .ToList();
        }

        /// <summary>加载账套、科目索引与已过账凭证。</summary>
        private async Task<(AccountSet AccountSet, Dictionary<Guid, Account> AccountsById, List<Voucher> PostedVouchers)> LoadAsync(
            Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var accountsById = (await _accountRepository.GetAllAsync(accountSetId, cancellationToken))
                .ToDictionary(a => a.Id, a => a);
            // 报表只统计已过账凭证（口径 §0.1）：草稿与已审核未过账的凭证不影响损益。
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();
            return (accountSet, accountsById, vouchers);
        }

        /// <summary>
        /// 净利润（本期 + 本年累计）：与利润表同源，取剔除结转凭证后的损益类发生额，
        /// 收入方向行次相加、费用方向行次相减。供现金流量表补充资料等间接法报表复用。
        /// </summary>
        internal static (decimal Current, decimal YearToDate) NetProfit(
            IReadOnlyList<Voucher> vouchers,
            Dictionary<Guid, Account> accountsById,
            DateOnly yearStart,
            DateOnly periodStart,
            DateOnly periodEndExclusive)
        {
            var lines = AccumulateLineNets(vouchers, accountsById, yearStart, periodStart, periodEndExclusive);
            decimal Sum(bool yearToDate) => lines.Sum(kv =>
                (yearToDate ? kv.Value.ytd : kv.Value.current) * (IncomeLineMapper.IsRevenueLine(kv.Key) ? 1m : -1m));

            return (Sum(false), Sum(true));
        }

        /// <summary>
        /// 逐科目累计借-贷净发生额并按报表行次归集（值已换算为该行的正常方向）。
        /// 本年窗口为 [yearStart, currentEnd)；本期窗口为 [currentStart, currentEnd)；
        /// 含「本年利润」科目的结转凭证剔除，保留结转前损益发生额。
        /// </summary>
        private static Dictionary<int, (decimal current, decimal ytd)> AccumulateLineNets(
            IReadOnlyList<Voucher> vouchers,
            Dictionary<Guid, Account> accountsById,
            DateOnly yearStart,
            DateOnly currentStart,
            DateOnly currentEndExclusive)
        {
            // 结转损益凭证识别：凭证内出现「本年利润」科目（4103，兼容旧准则 3103）即视为结转凭证。
            var profitCarryAccountIds = accountsById.Values
                .Where(a => IncomeLineMapper.Prefix(a.Code) is "4103" or "3103")
                .Select(a => a.Id)
                .ToHashSet();

            var currentNet = new Dictionary<Guid, decimal>();
            var ytdNet = new Dictionary<Guid, decimal>();

            foreach (var voucher in vouchers)
            {
                if (voucher.Date < yearStart || voucher.Date >= currentEndExclusive)
                {
                    continue;
                }

                if (voucher.Entries.Any(e => profitCarryAccountIds.Contains(e.AccountId)))
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    var delta = entry.Debit - entry.Credit;
                    ytdNet[entry.AccountId] = ytdNet.GetValueOrDefault(entry.AccountId) + delta;
                    if (voucher.Date >= currentStart)
                    {
                        currentNet[entry.AccountId] = currentNet.GetValueOrDefault(entry.AccountId) + delta;
                    }
                }
            }

            var currentByLine = new Dictionary<int, decimal>();
            var ytdByLine = new Dictionary<int, decimal>();
            var accountIds = new HashSet<Guid>(currentNet.Keys);
            accountIds.UnionWith(ytdNet.Keys);

            foreach (var accountId in accountIds)
            {
                if (!accountsById.TryGetValue(accountId, out var account)
                    || account.Category != AccountCategory.ProfitAndLoss)
                {
                    continue;
                }

                var line = IncomeLineMapper.MapLine(account.Code, account.Direction);
                var signed = IncomeLineMapper.IsRevenueLine(line) ? -1m : 1m; // 收入类行取贷-借，费用类行取借-贷。
                currentByLine[line] = currentByLine.GetValueOrDefault(line) + currentNet.GetValueOrDefault(accountId) * signed;
                ytdByLine[line] = ytdByLine.GetValueOrDefault(line) + ytdNet.GetValueOrDefault(accountId) * signed;
            }

            var result = new Dictionary<int, (decimal current, decimal ytd)>();
            foreach (var line in currentByLine.Keys.Union(ytdByLine.Keys))
            {
                result[line] = (currentByLine.GetValueOrDefault(line), ytdByLine.GetValueOrDefault(line));
            }

            return result;
        }

        /// <summary>解析季度：显式给定 year/quarter 时校验后采用；否则取最近凭证或账套启用期间所在季度。</summary>
        private static (int Year, int Quarter) ResolveQuarter(
            AccountSet accountSet,
            IReadOnlyList<Voucher> vouchers,
            int? year,
            int? quarter)
        {
            if (year is { } y && quarter is { } q)
            {
                if (q < 1 || q > 4)
                {
                    throw new ReportException("季度必须在 1 到 4 之间。");
                }

                if (y < 1900 || y > 9999)
                {
                    throw new ReportException("年份不合法。");
                }

                return (y, q);
            }

            if (vouchers.Count > 0)
            {
                var latest = vouchers.Max(v => v.Date);
                return (latest.Year, (latest.Month - 1) / 3 + 1);
            }

            return (accountSet.StartYear, (accountSet.StartMonth - 1) / 3 + 1);
        }
    }
}
