using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 财务概要信息应用服务：从科目余额与损益发生额归集核心指标——
    /// 资产总额、负债总额、所有者权益（存量，取时点余额）、营业收入、营业成本、
    /// 期间费用、利润总额、净利润（流量，取发生额）与资产负债率。
    /// 应收款/应付款/应交税费/资金收入/资金支付取区间发生额，与柠檬云财务概要同口径。
    /// 流量指标剔除结转损益凭证，与利润表口径一致。
    /// </summary>
    public sealed class FinancialSummaryService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public FinancialSummaryService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>财务概要。year/month 省略时取最近有凭证的月份。</summary>
        public async Task<FinancialSummaryDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);

            // —— 存量类：时点余额（资产借方为正、负债权益贷方为正），含未结转损益并入权益 ——
            BalanceSnapshot Balance(int year, int month)
            {
                var monthEnd = new DateOnly(year, month, 1).AddMonths(1).AddDays(-1);
                var net = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, monthEnd);
                var assets = AccountNetCalculator.SumByCategory(net, accounts, AccountCategory.Asset);
                var liabilities = AccountNetCalculator.SumByCategory(net, accounts, AccountCategory.Liability);
                var equity = AccountNetCalculator.SumByCategory(net, accounts, AccountCategory.Equity)
                             + AccountNetCalculator.SumByCategory(net, accounts, AccountCategory.ProfitAndLoss);
                var debtRatio = assets == 0m ? 0m : Math.Round(liabilities / assets * 100m, 2, MidpointRounding.ToEven);
                return new BalanceSnapshot(assets, liabilities, equity, debtRatio);
            }

            var currentEnd = Balance(resolvedYear, resolvedMonth);
            var lastMonthEnd = resolvedMonth == 1
                ? Balance(resolvedYear - 1, 12)
                : Balance(resolvedYear, resolvedMonth - 1);
            var lastYearEnd = Balance(resolvedYear - 1, 12);

            // —— 流量类：损益发生额（剔除结转凭证）——
            var carryAccountIds = accounts
                .Where(a => IncomeLineMapper.Prefix(a.Code) is "4103" or "3103")
                .Select(a => a.Id)
                .ToHashSet();
            var postedNoCarry = vouchers.Where(v => !v.Entries.Any(e => carryAccountIds.Contains(e.AccountId))).ToList();

            decimal LineAmount(DateOnly start, DateOnly end, params int[] lines)
            {
                var net = AccountNetCalculator.PeriodNetByAccount(postedNoCarry, start, end);
                decimal total = 0m;
                foreach (var account in accounts)
                {
                    if (account.Category != AccountCategory.ProfitAndLoss
                        || !net.TryGetValue(account.Id, out var delta))
                    {
                        continue;
                    }

                    var line = IncomeLineMapper.MapLine(account.Code, account.Direction);
                    if (!lines.Contains(line))
                    {
                        continue;
                    }

                    total += (IncomeLineMapper.IsRevenueLine(line) ? -delta : delta);
                }

                return total;
            }

            static DateOnly MonthStart(int y, int m) => new(y, m, 1);

            var currentMonth = (start: MonthStart(resolvedYear, resolvedMonth), end: MonthStart(resolvedYear, resolvedMonth).AddMonths(1).AddDays(-1));
            var priorMonth = resolvedMonth == 1
                ? (start: MonthStart(resolvedYear - 1, 12), end: MonthStart(resolvedYear - 1, 12).AddMonths(1).AddDays(-1))
                : (start: MonthStart(resolvedYear, resolvedMonth - 1), end: MonthStart(resolvedYear, resolvedMonth - 1).AddMonths(1).AddDays(-1));
            var ytd = (start: MonthStart(resolvedYear, 1), end: currentMonth.end);
            var ytdPrior = (start: MonthStart(resolvedYear - 1, 1), end: ytd.end.AddYears(-1));
            var monthPriorYear = (start: currentMonth.start.AddYears(-1), end: currentMonth.end.AddYears(-1));

            decimal Revenue(DateOnly s, DateOnly e) => LineAmount(s, e, 1);
            decimal Cost(DateOnly s, DateOnly e) => LineAmount(s, e, 2, 3);
            decimal PeriodExpense(DateOnly s, DateOnly e) => LineAmount(s, e, 4, 5, 6, 7);
            decimal NonOperatingIncome(DateOnly s, DateOnly e) => LineAmount(s, e, 8, 9, 12);
            decimal NonOperatingExpense(DateOnly s, DateOnly e) => LineAmount(s, e, 14);
            decimal IncomeTax(DateOnly s, DateOnly e) => LineAmount(s, e, 17);
            decimal TotalProfit(DateOnly s, DateOnly e) => Revenue(s, e) - Cost(s, e) - PeriodExpense(s, e)
                                                           + NonOperatingIncome(s, e) - NonOperatingExpense(s, e);
            decimal NetProfit(DateOnly s, DateOnly e) => TotalProfit(s, e) - IncomeTax(s, e);

            static decimal? Pct(decimal current, decimal baseline)
            {
                return baseline == 0m ? null : Math.Round((current - baseline) / Math.Abs(baseline) * 100m, 2, MidpointRounding.ToEven);
            }

            var rows = new List<FinancialSummaryRowDto>();

            FinancialSummaryRowDto Row(string name, decimal ytdAmount, decimal ytdYoyBase, decimal currentAmount, decimal currentYoyBase, decimal currentMomBase)
            {
                return new FinancialSummaryRowDto
                {
                    Item = name,
                    Line = rows.Count + 1,
                    YearToDateAmount = Math.Round(ytdAmount, 2, MidpointRounding.ToEven),
                    YearToDateYoyPercent = Pct(ytdAmount, ytdYoyBase),
                    CurrentAmount = Math.Round(currentAmount, 2, MidpointRounding.ToEven),
                    CurrentYoyPercent = Pct(currentAmount, currentYoyBase),
                    CurrentMomPercent = Pct(currentAmount, currentMomBase),
                };
            }

            rows.Add(Row("资产总额", currentEnd.Assets, lastYearEnd.Assets, currentEnd.Assets, lastYearEnd.Assets, lastMonthEnd.Assets));
            rows.Add(Row("负债总额", currentEnd.Liabilities, lastYearEnd.Liabilities, currentEnd.Liabilities, lastYearEnd.Liabilities, lastMonthEnd.Liabilities));
            rows.Add(Row("所有者权益", currentEnd.Equity, lastYearEnd.Equity, currentEnd.Equity, lastYearEnd.Equity, lastMonthEnd.Equity));
            rows.Add(Row("营业收入", Revenue(ytd.start, ytd.end), Revenue(ytdPrior.start, ytdPrior.end),
                Revenue(currentMonth.start, currentMonth.end), Revenue(monthPriorYear.start, monthPriorYear.end), Revenue(priorMonth.start, priorMonth.end)));
            rows.Add(Row("营业成本", Cost(ytd.start, ytd.end), Cost(ytdPrior.start, ytdPrior.end),
                Cost(currentMonth.start, currentMonth.end), Cost(monthPriorYear.start, monthPriorYear.end), Cost(priorMonth.start, priorMonth.end)));
            rows.Add(Row("期间费用", PeriodExpense(ytd.start, ytd.end), PeriodExpense(ytdPrior.start, ytdPrior.end),
                PeriodExpense(currentMonth.start, currentMonth.end), PeriodExpense(monthPriorYear.start, monthPriorYear.end), PeriodExpense(priorMonth.start, priorMonth.end)));
            rows.Add(Row("利润总额", TotalProfit(ytd.start, ytd.end), TotalProfit(ytdPrior.start, ytdPrior.end),
                TotalProfit(currentMonth.start, currentMonth.end), TotalProfit(monthPriorYear.start, monthPriorYear.end), TotalProfit(priorMonth.start, priorMonth.end)));
            rows.Add(Row("净利润", NetProfit(ytd.start, ytd.end), NetProfit(ytdPrior.start, ytdPrior.end),
                NetProfit(currentMonth.start, currentMonth.end), NetProfit(monthPriorYear.start, monthPriorYear.end), NetProfit(priorMonth.start, priorMonth.end)));
            rows.Add(Row("管理费用", LineAmount(ytd.start, ytd.end, 5), LineAmount(ytdPrior.start, ytdPrior.end, 5),
                LineAmount(currentMonth.start, currentMonth.end, 5), LineAmount(monthPriorYear.start, monthPriorYear.end, 5), LineAmount(priorMonth.start, priorMonth.end, 5)));
            rows.Add(Row("销售费用", LineAmount(ytd.start, ytd.end, 4), LineAmount(ytdPrior.start, ytdPrior.end, 4),
                LineAmount(currentMonth.start, currentMonth.end, 4), LineAmount(monthPriorYear.start, monthPriorYear.end, 4), LineAmount(priorMonth.start, priorMonth.end, 4)));
            rows.Add(Row("财务费用", LineAmount(ytd.start, ytd.end, 6), LineAmount(ytdPrior.start, ytdPrior.end, 6),
                LineAmount(currentMonth.start, currentMonth.end, 6), LineAmount(monthPriorYear.start, monthPriorYear.end, 6), LineAmount(priorMonth.start, priorMonth.end, 6)));

            // —— 应收/应付/应交税费：区间发生额（资产借方为正、负债贷方为正），与对端「发生额」取数一致 ——
            static bool IsReceivable(Account a) =>
                a.Category == AccountCategory.Asset && IncomeLineMapper.Prefix(a.Code) is "1122" or "1123" or "1131" or "1132" or "1221";
            static bool IsPayable(Account a) =>
                a.Category == AccountCategory.Liability && IncomeLineMapper.Prefix(a.Code) is "2201" or "2202" or "2203" or "2231" or "2241";
            static bool IsTaxPayable(Account a) =>
                a.Category == AccountCategory.Liability
                && (IncomeLineMapper.Prefix(a.Code) is "2171" or "2176" or "2221" || a.Name.Contains("税"));

            decimal GroupMovement(DateOnly start, DateOnly end, Func<Account, bool> filter, bool creditPositive)
            {
                var net = AccountNetCalculator.PeriodNetByAccount(vouchers, start, end);
                decimal total = 0m;
                foreach (var account in accounts.Where(filter))
                {
                    if (net.TryGetValue(account.Id, out var delta))
                    {
                        total += creditPositive ? -delta : delta;
                    }
                }

                return total;
            }

            decimal Receivable(DateOnly s, DateOnly e) => GroupMovement(s, e, IsReceivable, creditPositive: false);
            rows.Add(Row("应收款", Receivable(ytd.start, ytd.end), Receivable(ytdPrior.start, ytdPrior.end),
                Receivable(currentMonth.start, currentMonth.end), Receivable(monthPriorYear.start, monthPriorYear.end), Receivable(priorMonth.start, priorMonth.end)));

            decimal Payable(DateOnly s, DateOnly e) => GroupMovement(s, e, IsPayable, creditPositive: true);
            rows.Add(Row("应付款", Payable(ytd.start, ytd.end), Payable(ytdPrior.start, ytdPrior.end),
                Payable(currentMonth.start, currentMonth.end), Payable(monthPriorYear.start, monthPriorYear.end), Payable(priorMonth.start, priorMonth.end)));

            // —— 资金状况：现金及现金等价物科目的借/贷方毛发生额（红字按原始符号计入）——
            var cashAccountIds = accounts
                .Where(a => a.Category == AccountCategory.Asset && IncomeLineMapper.Prefix(a.Code) is "1001" or "1002" or "1012")
                .Select(a => a.Id)
                .ToHashSet();

            (decimal In, decimal Out) CashFlow(DateOnly start, DateOnly end)
            {
                decimal inflow = 0m;
                decimal outflow = 0m;
                foreach (var voucher in vouchers)
                {
                    if (voucher.Date < start || voucher.Date > end)
                    {
                        continue;
                    }

                    foreach (var entry in voucher.Entries)
                    {
                        if (cashAccountIds.Contains(entry.AccountId))
                        {
                            inflow += entry.Debit;
                            outflow += entry.Credit;
                        }
                    }
                }

                return (inflow, outflow);
            }

            var cashMonth = CashFlow(currentMonth.start, currentMonth.end);
            var cashPriorMonth = CashFlow(priorMonth.start, priorMonth.end);
            var cashYtd = CashFlow(ytd.start, ytd.end);
            var cashYtdPrior = CashFlow(ytdPrior.start, ytdPrior.end);
            var cashMonthPriorYear = CashFlow(monthPriorYear.start, monthPriorYear.end);
            rows.Add(Row("资金收入", cashYtd.In, cashYtdPrior.In, cashMonth.In, cashMonthPriorYear.In, cashPriorMonth.In));
            rows.Add(Row("资金支付", cashYtd.Out, cashYtdPrior.Out, cashMonth.Out, cashMonthPriorYear.Out, cashPriorMonth.Out));
            rows.Add(Row("资金收支", cashYtd.In - cashYtd.Out, cashYtdPrior.In - cashYtdPrior.Out,
                cashMonth.In - cashMonth.Out, cashMonthPriorYear.In - cashMonthPriorYear.Out, cashPriorMonth.In - cashPriorMonth.Out));

            decimal Tax(DateOnly s, DateOnly e) => GroupMovement(s, e, IsTaxPayable, creditPositive: true);
            rows.Add(Row("应交税费", Tax(ytd.start, ytd.end), Tax(ytdPrior.start, ytdPrior.end),
                Tax(currentMonth.start, currentMonth.end), Tax(monthPriorYear.start, monthPriorYear.end), Tax(priorMonth.start, priorMonth.end)));

            // 资产负债率（%）= 负债总额 / 资产总额 * 100，本年累计列为期末值，本期列为同值。
            var yoyDebtRatio = Pct(currentEnd.DebtRatio, lastYearEnd.DebtRatio);
            rows.Add(new FinancialSummaryRowDto
            {
                Item = "资产负债率(%)",
                Line = rows.Count + 1,
                YearToDateAmount = currentEnd.DebtRatio,
                YearToDateYoyPercent = yoyDebtRatio,
                CurrentAmount = currentEnd.DebtRatio,
                CurrentYoyPercent = yoyDebtRatio,
                CurrentMomPercent = Pct(currentEnd.DebtRatio, lastMonthEnd.DebtRatio),
            });

            return new FinancialSummaryDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Rows = rows,
            };
        }

        /// <summary>某时点的存量指标快照。</summary>
        private sealed record BalanceSnapshot(decimal Assets, decimal Liabilities, decimal Equity, decimal DebtRatio);
    }
}
