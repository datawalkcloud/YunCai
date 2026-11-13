using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Closing.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Closing.Application
{
    /// <summary>
    /// 期末结转相关的金额计算共享工具：科目余额、期间发生额与模板行取数的唯一出处，
    /// 供期末检查、结转损益、结转模板预览/执行共同使用，避免口径散落。
    /// </summary>
    internal static class ClosingMath
    {
        /// <summary>计算各科目截至指定日期（含当日）的累计净发生额（借 - 贷，仅已过账凭证 + 启用期初）。</summary>
        public static Dictionary<Guid, decimal> ComputeBalanceById(
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly endDate)
        {
            var balanceById = new Dictionary<Guid, decimal>();

            // 启用期初只登记在明细科目上，按余额方向换算为借 - 贷视角。
            foreach (var account in accounts)
            {
                if (!account.IsLeaf || account.OpeningBalance == 0m)
                {
                    continue;
                }

                var netDebit = account.Direction == BalanceDirection.Debit ? account.OpeningBalance : -account.OpeningBalance;
                balanceById[account.Id] = balanceById.GetValueOrDefault(account.Id) + netDebit;
            }

            foreach (var voucher in postedVouchers)
            {
                if (voucher.Date > endDate)
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    balanceById[entry.AccountId] = balanceById.GetValueOrDefault(entry.AccountId)
                        + entry.Debit - entry.Credit;
                }
            }

            return balanceById;
        }

        /// <summary>计算各科目在指定区间内（含起止）的借方/贷方发生额（仅已过账凭证）。</summary>
        public static (Dictionary<Guid, decimal> DebitById, Dictionary<Guid, decimal> CreditById) ComputePeriodSums(
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly startDate,
            DateOnly endDate)
        {
            var debitById = new Dictionary<Guid, decimal>();
            var creditById = new Dictionary<Guid, decimal>();

            foreach (var voucher in postedVouchers)
            {
                if (voucher.Date < startDate || voucher.Date > endDate)
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    debitById[entry.AccountId] = debitById.GetValueOrDefault(entry.AccountId) + entry.Debit;
                    creditById[entry.AccountId] = creditById.GetValueOrDefault(entry.AccountId) + entry.Credit;
                }
            }

            return (debitById, creditById);
        }

        /// <summary>按行的取数方式计算金额（固定金额 / 来源科目发生额 / 来源科目期末余额绝对值）。</summary>
        public static decimal ComputeLineAmount(
            ClosingTemplateLine line,
            IReadOnlyDictionary<Guid, decimal> debitById,
            IReadOnlyDictionary<Guid, decimal> creditById,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            return line.SourceType switch
            {
                ClosingTemplateSourceType.Fixed => line.Amount,
                ClosingTemplateSourceType.PeriodDebit => debitById.GetValueOrDefault(line.SourceAccountId!.Value),
                ClosingTemplateSourceType.PeriodCredit => creditById.GetValueOrDefault(line.SourceAccountId!.Value),
                _ => Math.Abs(balanceById.GetValueOrDefault(line.SourceAccountId!.Value)),
            };
        }

        /// <summary>校验会计期间的合法性。</summary>
        public static void ValidatePeriod(int year, int month)
        {
            if (year < 1900 || year > 9999)
            {
                throw new ClosingException("会计期间的年份不合法。");
            }

            if (month < 1 || month > 12)
            {
                throw new ClosingException("会计期间的月份必须在 1 到 12 之间。");
            }
        }
    }
}
