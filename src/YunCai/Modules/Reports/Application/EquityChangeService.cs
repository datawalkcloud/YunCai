using YunCai.Modules.Reports.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Reports.Application
{
    /// <summary>
    /// 所有者权益变动表应用服务：以贷方为正口径，统计实收资本、资本公积、盈余公积、
    /// 未分配利润四类权益项目在本年与上年的期初/期末余额。
    /// 损益类科目余额（结转前的本年利润或未结转损益）并入未分配利润，与资产负债表口径一致。
    /// </summary>
    public sealed class EquityChangeService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public EquityChangeService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>所有者权益变动表。year 省略时取最近有凭证的年份。</summary>
        public async Task<EquityChangeDto> GetAsync(Guid accountSetId, int? year, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var resolvedYear = ResolveYear(accountSet, vouchers, year);

            // 四个时点：上年年初、上年年末、本年年初（=上年年末）、本年年末。
            var priorBeginNet = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, new DateOnly(resolvedYear - 1, 1, 1));
            var priorEndNet = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, new DateOnly(resolvedYear - 1, 12, 31));
            var currentEndNet = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, new DateOnly(resolvedYear, 12, 31));

            decimal[] BucketSum(Dictionary<Guid, decimal> net, params int[] lines)
            {
                var sums = new decimal[4];
                foreach (var account in accounts)
                {
                    var bucket = MapBucket(account);
                    if (bucket < 0 || Array.IndexOf(lines, bucket) < 0
                        || !net.TryGetValue(account.Id, out var delta))
                    {
                        continue;
                    }

                    // 权益类与未结转损益均以贷方为正：借-贷净额取负即得贷方视角。
                    sums[Array.IndexOf(lines, bucket)] += -delta;
                }

                return sums;
            }

            // 行序：实收资本(51)、资本公积(52)、盈余公积(53)、未分配利润(54)。
            int[][] bucketSets =
            {
                new[] { 51 },
                new[] { 52 },
                new[] { 53 },
                new[] { 54 },
            };
            var names = new[] { "实收资本（或股本）", "资本公积", "盈余公积", "未分配利润" };

            var rows = new List<EquityChangeRowDto>();
            decimal[]? priorBeginSums = null;
            decimal[]? priorEndSums = null;
            decimal[]? currentBeginSums = null;
            decimal[]? currentEndSums = null;

            foreach (var bucketSet in bucketSets)
            {
                var pb = BucketSum(priorBeginNet, bucketSet[0]);
                var pe = BucketSum(priorEndNet, bucketSet[0]);
                var ce = BucketSum(currentEndNet, bucketSet[0]);
                priorBeginSums = Add(priorBeginSums, pb);
                priorEndSums = Add(priorEndSums, pe);
                currentBeginSums = Add(currentBeginSums, pe);
                currentEndSums = Add(currentEndSums, ce);
                rows.Add(new EquityChangeRowDto
                {
                    Item = names[bucketSet[0] - 51],
                    Line = rows.Count + 1,
                    PriorBegin = Math.Round(pb[0], 2, MidpointRounding.ToEven),
                    PriorEnd = Math.Round(pe[0], 2, MidpointRounding.ToEven),
                    CurrentBegin = Math.Round(pe[0], 2, MidpointRounding.ToEven),
                    CurrentEnd = Math.Round(ce[0], 2, MidpointRounding.ToEven),
                });
            }

            // 权益合计（含未识别权益科目兜底：并入未分配利润后自然覆盖）。
            // 必须返回新数组：上年末合计与本年年初合计初始指向同一个 BucketSum 结果，
            // 原地累加会让后续行次的金额被重复计入本年年初数（表现为年初合计比各分项之和多）。
            decimal[] Add(decimal[]? acc, decimal[] delta)
            {
                var result = acc is null ? new decimal[4] : (decimal[])acc.Clone();
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] += delta[i];
                }

                return result;
            }

            var totalRow = new EquityChangeRowDto
            {
                Item = "所有者权益合计",
                Line = rows.Count + 1,
                PriorBegin = Math.Round(priorBeginSums![0], 2, MidpointRounding.ToEven),
                PriorEnd = Math.Round(priorEndSums![0], 2, MidpointRounding.ToEven),
                CurrentBegin = Math.Round(currentBeginSums![0], 2, MidpointRounding.ToEven),
                CurrentEnd = Math.Round(currentEndSums![0], 2, MidpointRounding.ToEven),
            };
            rows.Add(totalRow);

            return new EquityChangeDto
            {
                Year = resolvedYear,
                PeriodText = $"{resolvedYear:D4}年度",
                Rows = rows,
            };
        }

        /// <summary>权益科目 → 行次（51=实收资本,52=资本公积,53=盈余公积,54=未分配利润），未识别返回 -1。</summary>
        private static int MapBucket(Account account)
        {
            // 未结转的损益类余额并入未分配利润，否则本表期末权益会与资产负债表（已并入损益）不一致。
            if (account.Category == AccountCategory.ProfitAndLoss)
            {
                return 54;
            }

            var code = account.Code;
            var p = code.Length <= 4 ? code : code[..4];
            return p switch
            {
                "4001" or "3001" => 51,
                "4002" or "3002" => 52,
                "4101" or "3101" => 53,
                "4103" or "4104" or "3103" or "3104" => 54,
                _ => -1,
            };
        }

        private static int ResolveYear(AccountSet accountSet, IReadOnlyList<Voucher> vouchers, int? year)
        {
            if (year is { } y)
            {
                if (y < 1900 || y > 9999)
                {
                    throw new ReportException("年份不合法。");
                }

                return y;
            }

            if (vouchers.Count > 0)
            {
                return vouchers.Max(v => v.Date).Year;
            }

            return accountSet.StartYear;
        }
    }
}
