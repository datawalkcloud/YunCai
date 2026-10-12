using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 纳税统计表应用服务：以应交税费类科目（负债类且编码为 2171/2176/2221 或名称含「税」）为对象，
    /// 统计计提净额（贷方发生 - 借方缴纳）的本期、本年累计及其同比变化（相对上年同期）。
    /// 已结账与未结账期间同口径，均只统计已过账凭证。
    /// </summary>
    public sealed class TaxStatisticsService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public TaxStatisticsService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>纳税统计表。year/month 省略时取最近有凭证的月份。</summary>
        public async Task<TaxStatisticsDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);
            var monthStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var yearStart = new DateOnly(resolvedYear, 1, 1);
            var yearEnd = new DateOnly(resolvedYear, 12, 31);
            var priorMonthStart = monthStart.AddYears(-1);
            var priorMonthEnd = monthEnd.AddYears(-1);
            var priorYearStart = yearStart.AddYears(-1);
            var priorYearEnd = yearEnd.AddYears(-1);

            // 贷方（计提）与借方（缴纳）发生额分开累计，分窗口计算。
            Dictionary<Guid, (decimal Credit, decimal Debit)> Movement(DateOnly start, DateOnly end)
            {
                var result = new Dictionary<Guid, (decimal Credit, decimal Debit)>();
                foreach (var voucher in vouchers)
                {
                    if (voucher.Date < start || voucher.Date > end)
                    {
                        continue;
                    }

                    foreach (var entry in voucher.Entries)
                    {
                        var cur = result.GetValueOrDefault(entry.AccountId);
                        result[entry.AccountId] = (cur.Credit + entry.Credit, cur.Debit + entry.Debit);
                    }
                }

                return result;
            }

            var currentMonth = Movement(monthStart, monthEnd);
            var currentYtd = Movement(yearStart, yearEnd);
            var priorMonth = Movement(priorMonthStart, priorMonthEnd);
            var priorYtd = Movement(priorYearStart, priorYearEnd);

            var taxAccounts = accounts
                .Where(a => a.IsLeaf && a.Category == AccountCategory.Liability
                            && (IsTaxPrefix(IncomeLineMapper.Prefix(a.Code)) || a.Name.Contains("税")))
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .ToList();

            static decimal? Yoy(decimal current, decimal prior)
            {
                return prior == 0m ? null : Math.Round((current - prior) / Math.Abs(prior) * 100m, 2, MidpointRounding.ToEven);
            }

            var rows = taxAccounts
                .Select((account, index) =>
                {
                    var (curCredit, curDebit) = currentMonth.GetValueOrDefault(account.Id);
                    var (ytdCredit, ytdDebit) = currentYtd.GetValueOrDefault(account.Id);
                    var (pCredit, _) = priorMonth.GetValueOrDefault(account.Id);
                    var (pyCredit, _) = priorYtd.GetValueOrDefault(account.Id);
                    var current = curCredit - curDebit;
                    var ytd = ytdCredit - ytdDebit;
                    return new TaxStatisticsRowDto
                    {
                        Code = account.Code,
                        Name = account.Name,
                        Line = index + 1,
                        CurrentAmount = Math.Round(current, 2, MidpointRounding.ToEven),
                        CurrentPaidAmount = Math.Round(curDebit, 2, MidpointRounding.ToEven),
                        CurrentYoyPercent = Yoy(current, pCredit),
                        YearToDateAmount = Math.Round(ytd, 2, MidpointRounding.ToEven),
                        YearToDatePaidAmount = Math.Round(ytdDebit, 2, MidpointRounding.ToEven),
                        YearToDateYoyPercent = Yoy(ytd, pyCredit),
                    };
                })
                .Where(r => r.CurrentAmount != 0m || r.YearToDateAmount != 0m || r.CurrentPaidAmount != 0m || r.YearToDatePaidAmount != 0m)
                .ToList();

            return new TaxStatisticsDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Rows = rows,
            };
        }

        /// <summary>应交税费类一级编码（旧准则应交税金/其他应交款，新准则应交税费）。</summary>
        private static bool IsTaxPrefix(string prefix)
        {
            return prefix is "2171" or "2176" or "2221";
        }
    }
}
