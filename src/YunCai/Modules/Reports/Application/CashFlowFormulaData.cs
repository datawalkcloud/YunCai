using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Assist.Domain;
using YunCai.Modules.Reports.Domain.CashFlow;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Reports.Application
{
    /// <summary>
    /// 一个金额列（时间窗口）的取数数据：科目发生额/余额、现金流项目收付额与系统归集结果，
    /// 供 <see cref="CashFlowFormulaEvaluator"/> 按公式取数。
    /// </summary>
    internal sealed class CashFlowFormulaData : ICashFlowFormulaContext
    {
        private readonly IReadOnlyList<Account> _accounts;
        private readonly IReadOnlyDictionary<Guid, decimal> _debitByAccount;
        private readonly IReadOnlyDictionary<Guid, decimal> _creditByAccount;
        private readonly IReadOnlyDictionary<Guid, decimal> _openingNet;
        private readonly IReadOnlyDictionary<Guid, decimal> _closingNet;
        private readonly IReadOnlyList<CashFlowItemAmount> _cashFlowItems;
        private readonly IReadOnlyDictionary<int, decimal> _autoByLine;

        private CashFlowFormulaData(
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> debitByAccount,
            IReadOnlyDictionary<Guid, decimal> creditByAccount,
            IReadOnlyDictionary<Guid, decimal> openingNet,
            IReadOnlyDictionary<Guid, decimal> closingNet,
            IReadOnlyList<CashFlowItemAmount> cashFlowItems,
            IReadOnlyDictionary<int, decimal> autoByLine)
        {
            _accounts = accounts;
            _debitByAccount = debitByAccount;
            _creditByAccount = creditByAccount;
            _openingNet = openingNet;
            _closingNet = closingNet;
            _cashFlowItems = cashFlowItems;
            _autoByLine = autoByLine;
        }

        /// <summary>按时间窗口汇总取数数据（仅传入已过账凭证）。</summary>
        public static CashFlowFormulaData Build(
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> postedVouchers,
            IReadOnlyList<AssistItem> cashFlowItems,
            DateOnly start,
            DateOnly end)
        {
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var cashAccountIds = CashFlowService.CashAccountIds(accounts);

            var debitByAccount = new Dictionary<Guid, decimal>();
            var creditByAccount = new Dictionary<Guid, decimal>();
            var itemInflow = new Dictionary<Guid, decimal>();
            var itemOutflow = new Dictionary<Guid, decimal>();

            foreach (var voucher in postedVouchers)
            {
                if (voucher.Date < start || voucher.Date > end)
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    debitByAccount[entry.AccountId] = debitByAccount.GetValueOrDefault(entry.AccountId) + entry.Debit;
                    creditByAccount[entry.AccountId] = creditByAccount.GetValueOrDefault(entry.AccountId) + entry.Credit;

                    if (!cashAccountIds.Contains(entry.AccountId))
                    {
                        continue;
                    }

                    // 现金流辅助项目挂在现金类科目分录上：同一分录的净收付按所挂项目均分。
                    var references = entry.Assists
                        .Where(a => a.Type == AssistType.CashFlow)
                        .Select(a => a.AssistItemId)
                        .Distinct()
                        .ToList();
                    if (references.Count == 0)
                    {
                        continue;
                    }

                    var share = (entry.Debit - entry.Credit) / references.Count;
                    foreach (var itemId in references)
                    {
                        if (share > 0m)
                        {
                            itemInflow[itemId] = itemInflow.GetValueOrDefault(itemId) + share;
                        }
                        else if (share < 0m)
                        {
                            itemOutflow[itemId] = itemOutflow.GetValueOrDefault(itemId) - share;
                        }
                    }
                }
            }

            var items = new List<CashFlowItemAmount>();
            foreach (var item in cashFlowItems)
            {
                var inflow = itemInflow.GetValueOrDefault(item.Id);
                var outflow = itemOutflow.GetValueOrDefault(item.Id);
                if (inflow != 0m || outflow != 0m)
                {
                    items.Add(new CashFlowItemAmount(item.Code, inflow, outflow));
                }
            }

            // AUTO()：与直接法主表同一套凭证分摊/归类口径。
            var autoByLine = new Dictionary<int, decimal>();
            foreach (var voucher in postedVouchers)
            {
                if (voucher.Date < start || voucher.Date > end)
                {
                    continue;
                }

                CashFlowService.Allocate(voucher, cashAccountIds, (_, counterpart, amount, inflow) =>
                {
                    if (!accountsById.TryGetValue(counterpart.AccountId, out var account))
                    {
                        return;
                    }

                    var line = CashFlowClassifier.MapLine(account, inflow);
                    autoByLine[line] = autoByLine.GetValueOrDefault(line) + amount;
                });
            }

            return new CashFlowFormulaData(
                accounts,
                debitByAccount,
                creditByAccount,
                AccountNetCalculator.NetByAccountUpTo(accounts, postedVouchers, start.AddDays(-1)),
                AccountNetCalculator.NetByAccountUpTo(accounts, postedVouchers, end),
                items,
                autoByLine);
        }

        public decimal Debit(string accountCode) => SumAccounts(accountCode, _debitByAccount);

        public decimal Credit(string accountCode) => SumAccounts(accountCode, _creditByAccount);

        public decimal Opening(string accountCode) => SumAccounts(accountCode, _openingNet);

        public decimal Closing(string accountCode) => SumAccounts(accountCode, _closingNet);

        public decimal CashInflow(string itemCode) => SumItems(itemCode, item => item.Inflow);

        public decimal CashOutflow(string itemCode) => SumItems(itemCode, item => item.Outflow);

        public decimal CashNet(string itemCode) => SumItems(itemCode, item => item.Inflow - item.Outflow);

        public decimal Auto(int line) => _autoByLine.GetValueOrDefault(line);

        /// <summary>按编码前缀汇总科目金额（父编码包含全部下级）。</summary>
        private decimal SumAccounts(string code, IReadOnlyDictionary<Guid, decimal> amounts)
        {
            var prefix = (code ?? string.Empty).Trim();
            decimal total = 0m;
            foreach (var account in _accounts)
            {
                if (account.Code.StartsWith(prefix, StringComparison.Ordinal))
                {
                    total += amounts.GetValueOrDefault(account.Id);
                }
            }

            return total;
        }

        /// <summary>按编码前缀汇总现金流项目金额（父编码包含全部下级）。</summary>
        private decimal SumItems(string code, Func<CashFlowItemAmount, decimal> selector)
        {
            var prefix = (code ?? string.Empty).Trim();
            decimal total = 0m;
            foreach (var item in _cashFlowItems)
            {
                if (item.Code.StartsWith(prefix, StringComparison.Ordinal))
                {
                    total += selector(item);
                }
            }

            return total;
        }

        /// <summary>现金流项目的流入/流出金额（流出为正数）。</summary>
        private sealed record CashFlowItemAmount(string Code, decimal Inflow, decimal Outflow);
    }
}
