using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Reports
{
    /// <summary>报表业务中的可预期错误（如账套不存在、期间不合法），用于向前端返回友好提示。</summary>
    public sealed class ReportException : Exception
    {
        public ReportException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 科目余额表应用服务：按选定会计期间，从凭证分录汇总各科目的期初、本期发生额与期末余额。
    /// 期初 = 期间起始日之前全部分录的净额；本期发生额 = 期间内借/贷合计；期末 = 期初 + 本期净额。
    /// 余额按净额正负归入借/贷方，因每张凭证借贷平衡，故合计恒满足试算平衡。
    /// </summary>
    public sealed class TrialBalanceService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public TrialBalanceService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>
        /// 生成指定账套、指定会计期间的科目余额表。year/month 省略时：优先取最近有凭证的月份，
        /// 无凭证时回退到账套启用期间。
        /// </summary>
        public async Task<TrialBalanceDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            var (resolvedYear, resolvedMonth) = ResolvePeriod(accountSet, vouchers, year, month);
            var periodStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var periodEnd = periodStart.AddMonths(1);

            // 逐科目累计：期初净额（借-贷），本期借、贷发生额。
            var openingNet = new Dictionary<Guid, decimal>();
            var periodDebit = new Dictionary<Guid, decimal>();
            var periodCredit = new Dictionary<Guid, decimal>();

            foreach (var voucher in vouchers)
            {
                foreach (var entry in voucher.Entries)
                {
                    if (voucher.Date < periodStart)
                    {
                        openingNet[entry.AccountId] = openingNet.GetValueOrDefault(entry.AccountId) + entry.Debit - entry.Credit;
                    }
                    else if (voucher.Date < periodEnd)
                    {
                        periodDebit[entry.AccountId] = periodDebit.GetValueOrDefault(entry.AccountId) + entry.Debit;
                        periodCredit[entry.AccountId] = periodCredit.GetValueOrDefault(entry.AccountId) + entry.Credit;
                    }
                }
            }

            // 汇总所有出现过的科目标识（期初或本期任一有数据）。
            var accountIds = new HashSet<Guid>(openingNet.Keys);
            accountIds.UnionWith(periodDebit.Keys);
            accountIds.UnionWith(periodCredit.Keys);

            var rows = new List<TrialBalanceRowDto>();
            foreach (var accountId in accountIds)
            {
                var opening = openingNet.GetValueOrDefault(accountId);
                var pDebit = periodDebit.GetValueOrDefault(accountId);
                var pCredit = periodCredit.GetValueOrDefault(accountId);
                var ending = opening + pDebit - pCredit;

                // 无任何余额与发生额的科目不列示。
                if (opening == 0m && pDebit == 0m && pCredit == 0m && ending == 0m)
                {
                    continue;
                }

                var account = accountsById.GetValueOrDefault(accountId);
                var (openingDebit, openingCredit) = SplitBySign(opening);
                var (endingDebit, endingCredit) = SplitBySign(ending);

                rows.Add(new TrialBalanceRowDto
                {
                    Code = account?.Code ?? string.Empty,
                    Name = account?.Name ?? "（未知科目）",
                    OpeningDebit = openingDebit,
                    OpeningCredit = openingCredit,
                    PeriodDebit = pDebit,
                    PeriodCredit = pCredit,
                    EndingDebit = endingDebit,
                    EndingCredit = endingCredit,
                });
            }

            rows = rows.OrderBy(r => r.Code, StringComparer.Ordinal).ToList();

            return new TrialBalanceDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Rows = rows,
            };
        }

        /// <summary>将净额按正负拆入借/贷方：非负记借方，负数取绝对值记贷方。</summary>
        private static (decimal Debit, decimal Credit) SplitBySign(decimal net)
        {
            return net >= 0m ? (net, 0m) : (0m, -net);
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
