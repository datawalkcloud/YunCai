using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 费用明细表应用服务：对成本类与损益类（借方向）科目统计本期（选定月份）
    /// 与本年累计的借-贷净发生额，非叶级次由下级科目汇总，供前端按层级展开。
    /// 含「本年利润」科目的结转凭证剔除，与利润表口径一致。
    /// </summary>
    public sealed class ExpenseDetailService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public ExpenseDetailService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>费用明细表。year/month 省略时取最近有凭证的月份。</summary>
        public async Task<ExpenseDetailDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);
            var monthStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var monthEndExclusive = monthStart.AddMonths(1);
            var yearStart = new DateOnly(resolvedYear, 1, 1);

            var carryAccountIds = accounts
                .Where(a => IncomeLineMapper.Prefix(a.Code) is "4103" or "3103")
                .Select(a => a.Id)
                .ToHashSet();

            var currentNet = new Dictionary<Guid, decimal>();
            var ytdNet = new Dictionary<Guid, decimal>();

            foreach (var voucher in vouchers)
            {
                if (carryAccountIds.Count > 0 && voucher.Entries.Any(e => carryAccountIds.Contains(e.AccountId)))
                {
                    continue;
                }

                if (voucher.Date >= yearStart)
                {
                    foreach (var entry in voucher.Entries)
                    {
                        ytdNet[entry.AccountId] = ytdNet.GetValueOrDefault(entry.AccountId) + entry.Debit - entry.Credit;
                        if (voucher.Date >= monthStart && voucher.Date < monthEndExclusive)
                        {
                            currentNet[entry.AccountId] = currentNet.GetValueOrDefault(entry.AccountId) + entry.Debit - entry.Credit;
                        }
                    }
                }
            }

            // 费用类科目：成本类 + 损益类（借方向）。叶级取自身发生额，非叶级由直属下级逐层汇总。
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var childrenByParent = accounts
                .Where(a => a.ParentId is not null)
                .GroupBy(a => a.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            decimal RolledCurrent(Guid accountId, HashSet<Guid> visiting)
            {
                if (!visiting.Add(accountId))
                {
                    return 0m; // 防御脏数据成环
                }

                try
                {
                    if (childrenByParent.TryGetValue(accountId, out var children))
                    {
                        return children.Sum(c => RolledCurrent(c.Id, visiting));
                    }

                    return currentNet.GetValueOrDefault(accountId);
                }
                finally
                {
                    visiting.Remove(accountId);
                }
            }

            decimal RolledYtd(Guid accountId, HashSet<Guid> visiting)
            {
                if (!visiting.Add(accountId))
                {
                    return 0m;
                }

                try
                {
                    if (childrenByParent.TryGetValue(accountId, out var children))
                    {
                        return children.Sum(c => RolledYtd(c.Id, visiting));
                    }

                    return ytdNet.GetValueOrDefault(accountId);
                }
                finally
                {
                    visiting.Remove(accountId);
                }
            }

            var rows = new List<ExpenseDetailRowDto>();
            foreach (var account in accounts
                         .Where(a => a.Category is AccountCategory.Cost or AccountCategory.ProfitAndLoss
                                     && a.Direction == BalanceDirection.Debit)
                         .OrderBy(a => a.Code, StringComparer.Ordinal))
            {
                var current = Math.Round(RolledCurrent(account.Id, new HashSet<Guid>()), 2, MidpointRounding.ToEven);
                var total = Math.Round(RolledYtd(account.Id, new HashSet<Guid>()), 2, MidpointRounding.ToEven);
                if (current == 0m && total == 0m)
                {
                    continue; // 全年无发生的费用科目不展示
                }

                rows.Add(new ExpenseDetailRowDto
                {
                    Id = account.Id,
                    Code = account.Code,
                    Name = account.Name,
                    Level = account.Level,
                    ParentId = account.ParentId,
                    HasChildren = !account.IsLeaf,
                    Current = current,
                    Total = total,
                });
            }

            return new ExpenseDetailDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Rows = rows,
            };
        }
    }
}
