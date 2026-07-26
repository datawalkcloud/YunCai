using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Reports
{
    /// <summary>
    /// 利润表（会企02表）应用服务：按选定会计期间，从损益类科目的凭证发生额归集各标准行次的
    /// 本期金额与本年累计金额。收入类行次以贷方为正方向，费用类行次以借方为正方向。
    /// 涉及「本年利润」科目的凭证视为期末结转损益凭证，汇总时予以剔除，避免结转后各行清零。
    /// </summary>
    public sealed class IncomeStatementService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public IncomeStatementService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>
        /// 生成指定账套、指定会计期间的利润表。year/month 省略时优先取最近有凭证的月份，
        /// 无凭证时回退账套启用期间。本期金额取选定月份，本年累计取本年 1 月 1 日至选定月末。
        /// </summary>
        public async Task<IncomeStatementDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            var (resolvedYear, resolvedMonth) = ResolvePeriod(accountSet, vouchers, year, month);
            var periodStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var periodEnd = periodStart.AddMonths(1);          // 本期：不含次月
            var yearStart = new DateOnly(resolvedYear, 1, 1);   // 本年累计起点

            // 结转损益凭证识别：凭证内出现「本年利润」科目（4103，兼容旧准则 3103）即视为结转凭证。
            var profitCarryAccountIds = accounts
                .Where(a => Prefix(a.Code) is "4103" or "3103")
                .Select(a => a.Id)
                .ToHashSet();

            // 逐科目累计借-贷净发生额：本期（选定月份）与本年累计（年初至月末）。
            var currentNet = new Dictionary<Guid, decimal>();
            var ytdNet = new Dictionary<Guid, decimal>();

            foreach (var voucher in vouchers)
            {
                if (voucher.Date < yearStart || voucher.Date >= periodEnd)
                {
                    continue;
                }

                // 剔除期末结转损益凭证，保留结转前的损益发生额。
                if (voucher.Entries.Any(e => profitCarryAccountIds.Contains(e.AccountId)))
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    var delta = entry.Debit - entry.Credit;
                    ytdNet[entry.AccountId] = ytdNet.GetValueOrDefault(entry.AccountId) + delta;
                    if (voucher.Date >= periodStart)
                    {
                        currentNet[entry.AccountId] = currentNet.GetValueOrDefault(entry.AccountId) + delta;
                    }
                }
            }

            // 按报表行次归集（值已换算为该行的正常方向：收入类贷方为正，费用类借方为正）。
            var currentByLine = new Dictionary<int, decimal>();
            var ytdByLine = new Dictionary<int, decimal>();

            var accountIds = new HashSet<Guid>(currentNet.Keys);
            accountIds.UnionWith(ytdNet.Keys);

            foreach (var accountId in accountIds)
            {
                if (!accountsById.TryGetValue(accountId, out var account)
                    || account.Category != AccountCategory.ProfitAndLoss)
                {
                    continue;
                }

                var line = MapLine(account.Code, account.Direction);
                var signed = IsRevenueLine(line) ? -1m : 1m; // 收入类行取贷-借，费用类行取借-贷。
                currentByLine[line] = currentByLine.GetValueOrDefault(line) + currentNet.GetValueOrDefault(accountId) * signed;
                ytdByLine[line] = ytdByLine.GetValueOrDefault(line) + ytdNet.GetValueOrDefault(accountId) * signed;
            }

            var lines = new List<IncomeStatementLineDto>();
            var allLines = new HashSet<int>(currentByLine.Keys);
            allLines.UnionWith(ytdByLine.Keys);
            foreach (var line in allLines.OrderBy(l => l))
            {
                lines.Add(new IncomeStatementLineDto
                {
                    Line = line,
                    Current = currentByLine.GetValueOrDefault(line),
                    YearToDate = ytdByLine.GetValueOrDefault(line),
                });
            }

            return new IncomeStatementDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Lines = lines,
            };
        }

        /// <summary>收入方向的行次（贷方为正）：营业收入、其他收益、投资收益、公允价值变动收益、资产处置收益、营业外收入。</summary>
        private static bool IsRevenueLine(int line)
        {
            return line is 1 or 8 or 9 or 11 or 14 or 16;
        }

        /// <summary>
        /// 将损益类科目按《企业会计准则》标准编码归集到会企02表行次。
        /// 未识别的损益科目按余额方向兜底：贷方并入营业收入(1)，借方并入营业成本(2)，保证净利润完整。
        /// </summary>
        private static int MapLine(string code, BalanceDirection direction)
        {
            var p = Prefix(code);
            return p switch
            {
                "6001" or "6051" => 1,             // 营业收入（主营业务收入 + 其他业务收入）
                "6401" or "6402" => 2,             // 营业成本（主营业务成本 + 其他业务成本）
                "6403" => 3,                       // 税金及附加
                "6601" => 4,                       // 销售费用
                "6602" => 5,                       // 管理费用
                "6604" => 6,                       // 研发费用（企业单设时）
                "6603" => 7,                       // 财务费用
                "6117" => 8,                       // 其他收益
                "6111" => 9,                       // 投资收益
                "6101" => 11,                      // 公允价值变动收益
                "6702" => 12,                      // 信用减值损失
                "6701" => 13,                      // 资产减值损失
                "6115" or "6116" => 14,            // 资产处置收益
                "6301" => 16,                      // 营业外收入
                "6711" => 17,                      // 营业外支出
                "6801" => 19,                      // 所得税费用
                _ => direction == BalanceDirection.Credit ? 1 : 2, // 兜底：按方向并入营业收入/营业成本
            };
        }

        /// <summary>取科目编码前 4 位作为一级科目归集键（明细科目以一级编码为前缀）。</summary>
        private static string Prefix(string code)
        {
            return code.Length <= 4 ? code : code[..4];
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
