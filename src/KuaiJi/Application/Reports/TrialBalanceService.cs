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
    /// 期初 = 科目录入的启用期初余额 + 期间起始日之前全部分录的净额；本期发生额 = 期间内借/贷合计；期末 = 期初 + 本期净额。
    /// 余额按净额正负归入借/贷方，因每张凭证借贷平衡且期初录入经试算平衡校验，故合计恒满足试算平衡。
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
        /// 生成指定账套、指定日期范围的科目余额表。startDate/endDate 省略时默认取全部凭证。
        /// </summary>
        public async Task<TrialBalanceDto> GetAsync(Guid accountSetId, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            var (resolvedStart, resolvedEnd) = ResolveDateRange(accountSet, vouchers, startDate, endDate);

            // 逐科目累计：期初净额（借-贷），本期借、贷发生额。
            var openingNet = new Dictionary<Guid, decimal>();
            var periodDebit = new Dictionary<Guid, decimal>();
            var periodCredit = new Dictionary<Guid, decimal>();

            // 科目启用期初余额先于一切凭证，直接计入期初净额（按余额方向换算为借-贷视角）。
            foreach (var account in accounts.Where(a => a.IsLeaf && a.OpeningBalance != 0m))
            {
                var netDebit = account.Direction == BalanceDirection.Debit
                    ? account.OpeningBalance
                    : -account.OpeningBalance;
                openingNet[account.Id] = openingNet.GetValueOrDefault(account.Id) + netDebit;
            }

            foreach (var voucher in vouchers)
            {
                foreach (var entry in voucher.Entries)
                {
                    if (voucher.Date < resolvedStart)
                    {
                        openingNet[entry.AccountId] = openingNet.GetValueOrDefault(entry.AccountId) + entry.Debit - entry.Credit;
                    }
                    else if (voucher.Date <= resolvedEnd)
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
                StartDate = resolvedStart.ToString("yyyy-MM-dd"),
                EndDate = resolvedEnd.ToString("yyyy-MM-dd"),
                PeriodText = $"{resolvedStart:yyyy-MM-dd} 至 {resolvedEnd:yyyy-MM-dd}",
                Rows = rows,
            };
        }

        /// <summary>将净额按正负拆入借/贷方：非负记借方，负数取绝对值记贷方。</summary>
        private static (decimal Debit, decimal Credit) SplitBySign(decimal net)
        {
            return net >= 0m ? (net, 0m) : (0m, -net);
        }

        /// <summary>
        /// 解析日期范围：显式给定时校验后采用；否则取全部凭证的最小/最大日期，
        /// 无凭证时回退到账套启用期间的月初至月末。
        /// </summary>
        private static (DateOnly Start, DateOnly End) ResolveDateRange(
            AccountSet accountSet,
            IReadOnlyList<Voucher> vouchers,
            DateOnly? startDate,
            DateOnly? endDate)
        {
            if (startDate is { } s && endDate is { } e)
            {
                if (s > e)
                {
                    throw new ReportException("起始日期不能晚于截止日期。");
                }
                return (s, e);
            }

            if (vouchers.Count > 0)
            {
                var minDate = vouchers.Min(v => v.Date);
                var maxDate = vouchers.Max(v => v.Date);
                return (startDate ?? minDate, endDate ?? maxDate);
            }

            var fallbackStart = new DateOnly(accountSet.StartYear, accountSet.StartMonth, 1);
            var fallbackEnd = fallbackStart.AddMonths(1).AddDays(-1);
            return (startDate ?? fallbackStart, endDate ?? fallbackEnd);
        }
    }
}
