using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Vouchers;

namespace KuaiJi.Application.Reports
{
    /// <summary>
    /// 资产负债表（会企01表）应用服务：从凭证分录汇总各科目余额，并按《企业会计准则》标准科目编码
    /// 归集到报表标准行次。金额按行次的正常方向净额列示（含累计折旧、减值准备等备抵科目）。
    /// 未做期末结转时，损益类科目的当期净利润并入「未分配利润」，以保证「资产总计 = 负债和所有者权益总计」恒成立。
    /// </summary>
    public sealed class BalanceSheetService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;

        public BalanceSheetService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
        }

        /// <summary>
        /// 生成指定账套、指定会计期间的资产负债表。year/month 省略时优先取最近有凭证的月份，
        /// 无凭证时回退账套启用期间。期末余额取期间月末，年初余额取该年 1 月 1 日。
        /// </summary>
        public async Task<BalanceSheetDto> GetAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");

            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var vouchers = await _voucherRepository.GetAllAsync(accountSetId, cancellationToken);

            var (resolvedYear, resolvedMonth) = ResolvePeriod(accountSet, vouchers, year, month);
            var periodStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var periodEnd = periodStart.AddMonths(1);          // 期末：不含次月
            var yearStart = new DateOnly(resolvedYear, 1, 1);   // 年初：本年 1 月 1 日之前

            // 逐科目累计净发生额：期末（截至月末）与年初（截至本年 1 月 1 日前）。
            var endingNet = new Dictionary<Guid, decimal>();
            var openingNet = new Dictionary<Guid, decimal>();

            foreach (var voucher in vouchers)
            {
                foreach (var entry in voucher.Entries)
                {
                    var delta = entry.Debit - entry.Credit;
                    if (voucher.Date < periodEnd)
                    {
                        endingNet[entry.AccountId] = endingNet.GetValueOrDefault(entry.AccountId) + delta;
                    }

                    if (voucher.Date < yearStart)
                    {
                        openingNet[entry.AccountId] = openingNet.GetValueOrDefault(entry.AccountId) + delta;
                    }
                }
            }

            // 按报表行次累计金额（值已换算为该行的正常方向：资产借方为正，负债/权益贷方为正）。
            var endingByLine = new Dictionary<int, decimal>();
            var openingByLine = new Dictionary<int, decimal>();

            var accountIds = new HashSet<Guid>(endingNet.Keys);
            accountIds.UnionWith(openingNet.Keys);

            foreach (var accountId in accountIds)
            {
                if (!accountsById.TryGetValue(accountId, out var account))
                {
                    continue;
                }

                var endingDelta = endingNet.GetValueOrDefault(accountId); // 借-贷
                var openingDelta = openingNet.GetValueOrDefault(accountId);
                var line = MapLine(account.Code, account.Category);

                // 资产/成本类以借方为正方向；负债/权益/损益类以贷方为正方向。
                var signed = account.Category is AccountCategory.Asset or AccountCategory.Cost ? 1m : -1m;
                endingByLine[line] = endingByLine.GetValueOrDefault(line) + endingDelta * signed;
                openingByLine[line] = openingByLine.GetValueOrDefault(line) + openingDelta * signed;
            }

            var lines = new List<BalanceSheetLineDto>();
            var allLines = new HashSet<int>(endingByLine.Keys);
            allLines.UnionWith(openingByLine.Keys);
            foreach (var line in allLines.OrderBy(l => l))
            {
                lines.Add(new BalanceSheetLineDto
                {
                    Line = line,
                    Ending = endingByLine.GetValueOrDefault(line),
                    Opening = openingByLine.GetValueOrDefault(line),
                });
            }

            var endingDay = periodEnd.AddDays(-1);
            return new BalanceSheetDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                EndingDate = $"{endingDay.Year:D4}年{endingDay.Month:D2}月{endingDay.Day:D2}日",
                OpeningDate = $"{resolvedYear:D4}年01月01日",
                Lines = lines,
            };
        }

        /// <summary>
        /// 将科目按《企业会计准则》标准编码归集到会企01表行次。损益类（含本期结转前净利润）
        /// 与未识别的权益科目并入「未分配利润」(54)；未识别的资产/负债并入「其他流动资产/负债」。
        /// 备抵科目（累计折旧、坏账准备等）与其对应科目归入同一行，靠净额自动抵减。
        /// </summary>
        private static int MapLine(string code, AccountCategory category)
        {
            return category switch
            {
                AccountCategory.Asset or AccountCategory.Cost => MapAssetLine(code, category),
                AccountCategory.Liability => MapLiabilityLine(code),
                AccountCategory.Equity => MapEquityLine(code),
                _ => 54, // 损益类 → 未分配利润
            };
        }

        private static int MapAssetLine(string code, AccountCategory category)
        {
            // 成本类（生产成本、制造费用、劳务成本等）余额属在产品，列入存货。
            if (category == AccountCategory.Cost)
            {
                return 7;
            }

            var p = Prefix(code);
            return p switch
            {
                "1001" or "1002" or "1012" => 1,   // 货币资金
                "1101" or "1111" => 2,             // 交易性金融资产
                "1121" => 3,                       // 应收票据
                "1122" or "1231" => 4,             // 应收账款（含坏账准备备抵）
                "1123" => 5,                       // 预付款项
                "1131" or "1132" or "1221" => 6,   // 其他应收款（含应收股利/利息）
                "1401" or "1402" or "1403" or "1404" or "1405" or "1406"
                    or "1407" or "1408" or "1409" or "1411" or "1421" or "1471" => 7, // 存货
                "1501" or "1503" or "1511" or "1512" => 11, // 长期股权投资
                "1601" or "1602" or "1603" => 12,  // 固定资产（含累计折旧、减值准备备抵）
                "1604" or "1605" or "1606" => 13,  // 在建工程
                "1701" or "1702" or "1703" or "1711" => 14, // 无形资产（含累计摊销、减值备抵、商誉）
                "1801" => 15,                      // 长期待摊费用
                "1811" => 16,                      // 递延所得税资产
                _ => 9,                            // 其他流动资产（兜底，保证总计）
            };
        }

        private static int MapLiabilityLine(string code)
        {
            var p = Prefix(code);
            return p switch
            {
                "2001" => 31,             // 短期借款
                "2201" => 32,             // 应付票据
                "2202" => 33,             // 应付账款
                "2203" => 34,             // 预收款项
                "2211" => 35,             // 应付职工薪酬
                "2221" => 36,             // 应交税费
                "2231" or "2232" or "2241" => 37, // 其他应付款（含应付利息/股利）
                "2501" => 41,             // 长期借款
                "2502" => 42,             // 应付债券
                "2701" => 43,             // 长期应付款
                "2901" => 44,             // 递延所得税负债
                _ => 39,                  // 其他流动负债（兜底）
            };
        }

        private static int MapEquityLine(string code)
        {
            var p = Prefix(code);
            return p switch
            {
                // 新准则（4xxx 权益类）
                "4001" => 51,             // 实收资本（或股本）
                "4002" => 52,             // 资本公积
                "4101" => 53,             // 盈余公积
                "4103" or "4104" => 54,   // 未分配利润（本年利润 + 利润分配）
                // 旧准则兼容（3xxx 权益类），避免历史数据遗漏
                "3001" => 51,             // 实收资本（旧准则）
                "3002" => 52,             // 资本公积（旧准则）
                "3101" => 53,             // 盈余公积（旧准则）
                "3103" or "3104" => 54,   // 本年利润 / 利润分配（旧准则）
                _ => 54,                  // 库存股等其他权益并入未分配利润（兜底）
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
