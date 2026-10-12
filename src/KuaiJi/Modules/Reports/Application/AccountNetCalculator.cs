using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 报表取数的共享口径：以「借 - 贷」净额逐科目累计已过账凭证发生额，
    /// 并把科目启用期初余额（按余额方向）换算成同一视角一并计入。
    /// 供所有者权益变动表、纳税统计表、费用明细表、财务概要等共用。
    /// </summary>
    public static class AccountNetCalculator
    {
        /// <summary>
        /// 计算截止日期（含当日）各科目的借-贷净额（含启用期初余额）。
        /// until 为 null 时计入全部凭证。
        /// </summary>
        public static Dictionary<Guid, decimal> NetByAccountUpTo(
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly? until)
        {
            var net = new Dictionary<Guid, decimal>();

            // 科目启用期初余额先于一切凭证，按余额方向换算为借-贷视角。
            foreach (var account in accounts.Where(a => a.IsLeaf && a.OpeningBalance != 0m))
            {
                var netDebit = account.Direction == BalanceDirection.Debit
                    ? account.OpeningBalance
                    : -account.OpeningBalance;
                net[account.Id] = net.GetValueOrDefault(account.Id) + netDebit;
            }

            foreach (var voucher in postedVouchers)
            {
                if (until is { } bound && voucher.Date > bound)
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    net[entry.AccountId] = net.GetValueOrDefault(entry.AccountId) + entry.Debit - entry.Credit;
                }
            }

            return net;
        }

        /// <summary>计算日期区间 [start, end]（含起止日）内各科目的借-贷净发生额（不含期初）。</summary>
        public static Dictionary<Guid, decimal> PeriodNetByAccount(
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly start,
            DateOnly end)
        {
            var net = new Dictionary<Guid, decimal>();
            foreach (var voucher in postedVouchers)
            {
                if (voucher.Date < start || voucher.Date > end)
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    net[entry.AccountId] = net.GetValueOrDefault(entry.AccountId) + entry.Debit - entry.Credit;
                }
            }

            return net;
        }

        /// <summary>
        /// 按科目类别汇总净额。资产/成本类以借方为正，负债/权益/损益类以贷方为正
        /// （与资产负债表口径一致）；不在给定类别中的科目忽略。
        /// </summary>
        public static decimal SumByCategory(
            IDictionary<Guid, decimal> netByAccount,
            IReadOnlyList<Account> accounts,
            AccountCategory category)
        {
            decimal total = 0m;
            foreach (var account in accounts)
            {
                if (account.Category != category || !netByAccount.TryGetValue(account.Id, out var delta))
                {
                    continue;
                }

                var signed = account.Category is AccountCategory.Asset or AccountCategory.Cost ? 1m : -1m;
                total += delta * signed;
            }

            return total;
        }
    }
}
