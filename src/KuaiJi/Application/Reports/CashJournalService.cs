using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Reports
{
    /// <summary>
    /// 现金日记账应用服务：从涉及「库存现金」科目（编码 1001 及其明细）的凭证分录派生序时流水。
    /// 借方计入收入、贷方计入支出；期初余额取期间起始日之前的现金净额；逐行余额由前端累计。
    /// </summary>
    public sealed class CashJournalService
    {
        /// <summary>库存现金一级科目编码。</summary>
        private const string CashCodePrefix = "1001";

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public CashJournalService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>
        /// 生成指定账套、指定会计期间的现金日记账。year/month 省略时优先取最近有凭证的月份，
        /// 无凭证时回退账套启用期间。
        /// </summary>
        public async Task<CashJournalDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var cashAccountIds = accounts
                .Where(a => a.Code.StartsWith(CashCodePrefix, StringComparison.Ordinal))
                .Select(a => a.Id)
                .ToHashSet();

            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            var (resolvedYear, resolvedMonth) = ResolvePeriod(accountSet, vouchers, year, month);
            var periodStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var periodEnd = periodStart.AddMonths(1);

            var openingBalance = 0m;
            var rows = new List<(DateOnly Date, int Number, CashJournalRowDto Row)>();

            foreach (var voucher in vouchers)
            {
                foreach (var entry in voucher.Entries)
                {
                    if (!cashAccountIds.Contains(entry.AccountId))
                    {
                        continue;
                    }

                    // 期间之前：仅累计期初余额（现金净额 = 借 - 贷）。
                    if (voucher.Date < periodStart)
                    {
                        openingBalance += entry.Debit - entry.Credit;
                        continue;
                    }

                    if (voucher.Date >= periodEnd)
                    {
                        continue;
                    }

                    // 对方科目：同一凭证中除库存现金外的其他科目名称。
                    var contra = string.Join("、", voucher.Entries
                        .Where(e => !cashAccountIds.Contains(e.AccountId))
                        .Select(e => accountsById.GetValueOrDefault(e.AccountId)?.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct());

                    rows.Add((voucher.Date, voucher.Number, new CashJournalRowDto
                    {
                        Date = voucher.Date.ToString("yyyy-MM-dd"),
                        VoucherNo = $"{voucher.Word}-{voucher.Number:000}",
                        Summary = entry.Summary,
                        ContraAccount = contra,
                        Income = entry.Debit,
                        Expense = entry.Credit,
                    }));
                }
            }

            var orderedRows = rows
                .OrderBy(r => r.Date)
                .ThenBy(r => r.Number)
                .Select(r => r.Row)
                .ToList();

            return new CashJournalDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                OpeningBalance = openingBalance,
                Rows = orderedRows,
            };
        }

        /// <summary>
        /// 解析会计期间：显式给定 year/month 时校验后采用；否则取最近有凭证的月份，
        /// 无凭证时回退到账套启用期间。
        /// </summary>
        private static (int Year, int Month) ResolvePeriod(
            AccountSet accountSet,
            IReadOnlyList<Voucher> vouchers,
            int? year,
            int? month)
        {
            if (year is { } y && month is { } m)
            {
                if (m < 1 || m > 12)
                {
                    throw new ReportException("会计期间的月份必须在 1 到 12 之间。");
                }

                if (y < 1900 || y > 9999)
                {
                    throw new ReportException("会计期间的年份不合法。");
                }

                return (y, m);
            }

            if (vouchers.Count > 0)
            {
                var latest = vouchers.Max(v => v.Date);
                return (latest.Year, latest.Month);
            }

            return (accountSet.StartYear, accountSet.StartMonth);
        }
    }
}
