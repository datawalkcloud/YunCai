using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
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
        /// 生成指定账套、指定日期范围的现金日记账。startDate/endDate 省略时默认取全部凭证。
        /// </summary>
        public async Task<CashJournalDto> GetAsync(Guid accountSetId, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a.Name);
            var cashAccountIds = accounts
                .Where(a => a.Code.StartsWith(CashCodePrefix, StringComparison.Ordinal))
                .Select(a => a.Id)
                .ToHashSet();

            // 账簿只登记已过账凭证（口径 §0.1）：草稿与已审核未过账的凭证不参与日记账。
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            // 解析日期范围：未指定时取全部凭证的最小/最大日期，无凭证则回退账套启用期间。
            var (resolvedStart, resolvedEnd) = ResolveDateRange(accountSet, vouchers, startDate, endDate);

            var openingBalance = 0m;
            var rows = new List<(DateOnly Date, int Number, CashJournalRowDto Row)>();

            // 现金科目的启用期初余额先于一切凭证，直接计入期初（按余额方向换算为现金净额）。
            foreach (var account in accounts.Where(a => a.IsLeaf && a.OpeningBalance != 0m && cashAccountIds.Contains(a.Id)))
            {
                openingBalance += account.Direction == BalanceDirection.Debit
                    ? account.OpeningBalance
                    : -account.OpeningBalance;
            }

            foreach (var voucher in vouchers)
            {
                foreach (var entry in voucher.Entries)
                {
                    if (!cashAccountIds.Contains(entry.AccountId))
                    {
                        continue;
                    }

                    // 期间之前：仅累计期初余额（现金净额 = 借 - 贷）。
                    if (voucher.Date < resolvedStart)
                    {
                        openingBalance += entry.Debit - entry.Credit;
                        continue;
                    }

                    if (voucher.Date > resolvedEnd)
                    {
                        continue;
                    }

                    // 对方科目：同一凭证中除库存现金外的其他科目名称。
                    var contra = string.Join("、", voucher.Entries
                        .Where(e => !cashAccountIds.Contains(e.AccountId))
                        .Select(e => accountsById.GetValueOrDefault(e.AccountId))
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
                StartDate = resolvedStart.ToString("yyyy-MM-dd"),
                EndDate = resolvedEnd.ToString("yyyy-MM-dd"),
                PeriodText = $"{resolvedStart:yyyy-MM-dd} 至 {resolvedEnd:yyyy-MM-dd}",
                OpeningBalance = openingBalance,
                Rows = orderedRows,
            };
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
