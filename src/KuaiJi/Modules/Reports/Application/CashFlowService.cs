using System.Globalization;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Reports.Domain.CashFlow;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 现金流量表应用服务（直接法工作底稿的简化实现）：
    /// 对每张已过账凭证，把现金及现金等价物科目的净收付按比例分摊到同凭证的对手方科目，
    /// 再由 <see cref="CashFlowClassifier"/> 归类到标准行次；小计/净额/期初期末行由服务端汇总。
    /// <para>
    /// 现金及现金等价物 = 一级编码 1001/1002/1012（库存现金、银行存款、其他货币资金）的资产科目，
    /// 与资产负债表「货币资金」行口径一致。本币核算下汇率变动行为 0。
    /// </para>
    /// </summary>
    public sealed class CashFlowService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IAssistItemRepository _assistItemRepository;

        public CashFlowService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IAssistItemRepository assistItemRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _assistItemRepository = assistItemRepository;
        }

        /// <summary>月度现金流量表。year/month 省略时取最近有凭证的月份（无凭证回退账套启用期间）。</summary>
        public async Task<CashFlowDto> GetPeriodAsync(Guid accountSetId, int? year, int? month, CancellationToken cancellationToken = default)
        {
            var (accountSet, accounts, vouchers) = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);

            var monthStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var yearStart = new DateOnly(resolvedYear, 1, 1);
            var yearEnd = monthEnd;

            var (monthByLine, ytdByLine, monthOpening, yearOpening) = Accumulate(
                accountSet.Standard, accounts, vouchers, monthStart, monthEnd, yearStart, yearEnd);
            var lastYear = AccumulateLastYear(accountSet.Standard, accounts, vouchers, monthStart, 1);
            var lines = BuildLines(monthByLine, ytdByLine, monthOpening, yearOpening, accountSet.Standard,
                lastYear.ByLine, lastYear.Opening);

            return new CashFlowDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Lines = lines,
            };
        }

        /// <summary>
        /// 现金流量表（辅助核算法）：按现金分录上挂的「现金流」辅助项目归集到标准行次。
        /// 预置项目按名称对应主表行次（编码是 01–18 顺序号）、自定义项目退回「编码前两位即行号」，
        /// 未挂项目或无法对应的现金收付不计入；小计/净额/期初期末与其他口径共用同一套汇总。
        /// </summary>
        public async Task<CashFlowDto> GetAssistAsync(
            Guid accountSetId,
            int? year,
            int? month,
            CancellationToken cancellationToken = default)
        {
            var (accountSet, accounts, vouchers) = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);

            var monthStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var yearStart = new DateOnly(resolvedYear, 1, 1);
            var lastYearStart = new DateOnly(resolvedYear - 1, 1, 1);
            var lastYearEnd = new DateOnly(resolvedYear - 1, resolvedMonth, 1).AddMonths(1).AddDays(-1);

            var cashFlowItems = await _assistItemRepository.GetByTypeAsync(accountSetId, AssistType.CashFlow, cancellationToken);
            var itemsById = cashFlowItems.ToDictionary(i => i.Id);
            var cashAccountIds = CashAccountIds(accounts);

            var periodByLine = AccumulateAssist(accountSet.Standard, vouchers, itemsById, cashAccountIds, monthStart, monthEnd);
            var ytdByLine = AccumulateAssist(accountSet.Standard, vouchers, itemsById, cashAccountIds, yearStart, monthEnd);
            var lastYearByLine = AccumulateAssist(accountSet.Standard, vouchers, itemsById, cashAccountIds, lastYearStart, lastYearEnd);

            var lines = BuildLines(
                periodByLine,
                ytdByLine,
                CashOpening(accounts, vouchers, cashAccountIds, monthStart),
                CashOpening(accounts, vouchers, cashAccountIds, yearStart),
                accountSet.Standard,
                lastYearByLine,
                CashOpening(accounts, vouchers, cashAccountIds, lastYearStart));

            return new CashFlowDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Lines = lines,
            };
        }

        /// <summary>季度现金流量表：本季金额 + 本年累计金额。quarter 取 1-4。</summary>
        public async Task<CashFlowQuarterDto> GetQuarterAsync(Guid accountSetId, int? year, int? quarter, CancellationToken cancellationToken = default)
        {
            var (accountSet, accounts, vouchers) = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedQuarter) = ResolveQuarter(accountSet, vouchers, year, quarter);

            var quarterStart = new DateOnly(resolvedYear, (resolvedQuarter - 1) * 3 + 1, 1);
            var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
            var yearStart = new DateOnly(resolvedYear, 1, 1);
            var yearEnd = quarterEnd;

            var (quarterByLine, ytdByLine, quarterOpening, yearOpening) = Accumulate(
                accountSet.Standard, accounts, vouchers, quarterStart, quarterEnd, yearStart, yearEnd);
            var lastYear = AccumulateLastYear(accountSet.Standard, accounts, vouchers, quarterStart, 3);
            var lines = BuildLines(quarterByLine, ytdByLine, quarterOpening, yearOpening, accountSet.Standard,
                lastYear.ByLine, lastYear.Opening);

            return new CashFlowQuarterDto
            {
                Year = resolvedYear,
                Quarter = resolvedQuarter,
                PeriodText = $"{resolvedYear:D4}年第{resolvedQuarter}季度",
                Lines = lines,
            };
        }

        /// <summary>
        /// 现金流量表底稿：按凭证列示现金收付如何分摊到对手方科目、归入哪一标准行次，
        /// 用于解释主表数字的来源（与主表共用同一分摊与归类逻辑）。
        /// </summary>
        public async Task<CashFlowWorkpaperDto> GetWorkpaperAsync(
            Guid accountSetId,
            int? year,
            int? month,
            CancellationToken cancellationToken = default)
        {
            var (accountSet, accounts, vouchers) = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);
            var monthStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var cashAccountIds = CashAccountIds(accounts);
            var rows = new List<CashFlowWorkpaperRowDto>();

            foreach (var voucher in vouchers
                         .Where(v => v.Date >= monthStart && v.Date <= monthEnd)
                         .OrderBy(v => v.Date)
                         .ThenBy(v => v.Number))
            {
                Allocate(voucher, cashAccountIds, (v, counterpart, amount, inflow) =>
                {
                    if (!accountsById.TryGetValue(counterpart.AccountId, out var account))
                    {
                        return;
                    }

                    var line = ClassifyLine(accountSet.Standard, account, inflow);
                    rows.Add(new CashFlowWorkpaperRowDto
                    {
                        Date = v.Date,
                        VoucherText = $"{v.Word}-{v.Number:D4}",
                        Summary = counterpart.Summary,
                        AccountCode = account.Code,
                        AccountName = account.Name,
                        Line = line,
                        LineName = LineName(accountSet.Standard, line),
                        Inflow = inflow,
                        Amount = Round(amount),
                    });
                });
            }

            return new CashFlowWorkpaperDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Rows = rows,
                InflowTotal = Round(rows.Where(r => r.Inflow).Sum(r => r.Amount)),
                OutflowTotal = Round(rows.Where(r => !r.Inflow).Sum(r => r.Amount)),
            };
        }

        private async Task<(AccountSet AccountSet, IReadOnlyList<Account> Accounts, List<Voucher> PostedVouchers)> LoadAsync(
            Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();
            return (accountSet, accounts, vouchers);
        }

        /// <summary>
        /// 汇总明细行金额：periodByLine=期间窗口，ytdByLine=本年窗口；
        /// 期初余额分别取期间窗口起点与年初时点的现金科目余额。
        /// </summary>
        private (Dictionary<int, decimal> periodByLine, Dictionary<int, decimal> ytdByLine, decimal periodOpening, decimal yearOpening) Accumulate(
            AccountingStandard standard,
            IReadOnlyList<Account> accounts,
            List<Voucher> vouchers,
            DateOnly periodStart,
            DateOnly periodEnd,
            DateOnly yearStart,
            DateOnly yearEnd)
        {
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var cashAccountIds = CashAccountIds(accounts);

            var periodByLine = new Dictionary<int, decimal>();
            var ytdByLine = new Dictionary<int, decimal>();

            void Accumulate(Voucher voucher, Dictionary<int, decimal> target)
            {
                Allocate(voucher, cashAccountIds, (v, counterpart, amount, inflow) =>
                {
                    if (!accountsById.TryGetValue(counterpart.AccountId, out var account))
                    {
                        return;
                    }

                    var line = ClassifyLine(standard, account, inflow);
                    target[line] = target.GetValueOrDefault(line) + amount;
                });
            }

            foreach (var voucher in vouchers)
            {
                if (voucher.Date >= periodStart && voucher.Date <= periodEnd)
                {
                    Accumulate(voucher, periodByLine);
                }

                if (voucher.Date >= yearStart && voucher.Date <= yearEnd)
                {
                    Accumulate(voucher, ytdByLine);
                }
            }

            return (periodByLine, ytdByLine,
                CashOpening(accounts, vouchers, cashAccountIds, periodStart),
                CashOpening(accounts, vouchers, cashAccountIds, yearStart));
        }

        /// <summary>期初现金余额：现金科目（借方为正）在窗口起点前的净额（含启用期初）。</summary>
        private static decimal CashOpening(
            IReadOnlyList<Account> accounts,
            List<Voucher> vouchers,
            HashSet<Guid> cashAccountIds,
            DateOnly periodStart)
        {
            var openingNet = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, periodStart.AddDays(-1));
            return cashAccountIds.Sum(id => openingNet.GetValueOrDefault(id));
        }

        /// <summary>
        /// 辅助核算法：按现金分录上挂的现金流项目归入主表行次。同一分录挂多个项目时
        /// 净收付均分；流入行取正数、流出行取相反数（流出仍为正）。
        /// </summary>
        private static Dictionary<int, decimal> AccumulateAssist(
            AccountingStandard standard,
            List<Voucher> vouchers,
            IReadOnlyDictionary<Guid, AssistItem> itemsById,
            HashSet<Guid> cashAccountIds,
            DateOnly start,
            DateOnly end)
        {
            var byLine = new Dictionary<int, decimal>();
            foreach (var voucher in vouchers)
            {
                if (voucher.Date < start || voucher.Date > end)
                {
                    continue;
                }

                foreach (var entry in voucher.Entries)
                {
                    if (!cashAccountIds.Contains(entry.AccountId))
                    {
                        continue;
                    }

                    var itemIds = entry.Assists
                        .Where(a => a.Type == AssistType.CashFlow)
                        .Select(a => a.AssistItemId)
                        .Distinct()
                        .ToList();
                    if (itemIds.Count == 0)
                    {
                        continue;
                    }

                    var share = (entry.Debit - entry.Credit) / itemIds.Count;
                    foreach (var itemId in itemIds)
                    {
                        if (!itemsById.TryGetValue(itemId, out var item) || ResolveAssistLine(standard, item) is not { } line)
                        {
                            continue;
                        }

                        var signed = LineDef(standard, line).Kind == CashFlowRowKind.Inflow ? share : -share;
                        byLine[line] = byLine.GetValueOrDefault(line) + signed;
                    }
                }
            }

            return byLine;
        }

        /// <summary>
        /// 期初/期末现金余额：按现金科目列示期间起点前与截止日的余额（含科目启用期初），
        /// 合计即主表「期初现金余额」「期末现金余额」两行。
        /// </summary>
        public async Task<CashFlowOpeningDto> GetOpeningAsync(
            Guid accountSetId,
            int? year,
            int? month,
            CancellationToken cancellationToken = default)
        {
            var (accountSet, accounts, vouchers) = await LoadAsync(accountSetId, cancellationToken);
            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);
            var monthStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var openingNet = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, monthStart.AddDays(-1));
            var closingNet = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, monthEnd);

            // 只列末级明细科目（父科目自身无分录时不重复列示）；父科目若被直接记账则保留，保证合计与主表一致。
            var codes = accounts.Select(a => a.Code).ToList();
            var rows = CashAccounts(accounts)
                .Where(a => openingNet.GetValueOrDefault(a.Id) != 0m
                            || closingNet.GetValueOrDefault(a.Id) != 0m
                            || !codes.Any(c => c.Length > a.Code.Length && c.StartsWith(a.Code, StringComparison.Ordinal)))
                .Select(a => new CashFlowOpeningRowDto
                {
                    AccountCode = a.Code,
                    AccountName = a.Name,
                    Opening = Round(openingNet.GetValueOrDefault(a.Id)),
                    Closing = Round(closingNet.GetValueOrDefault(a.Id)),
                })
                .ToList();

            return new CashFlowOpeningDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Rows = rows,
                OpeningTotal = Round(rows.Sum(r => r.Opening)),
                ClosingTotal = Round(rows.Sum(r => r.Closing)),
            };
        }

        /// <summary>
        /// 上年同期累计（上年 1 月 1 日至上年同期末）的标准行次金额与上年年初现金余额，
        /// 供报表「显示上年累计金额」列使用。
        /// </summary>
        private (Dictionary<int, decimal> ByLine, decimal Opening) AccumulateLastYear(
            AccountingStandard standard,
            IReadOnlyList<Account> accounts,
            List<Voucher> vouchers,
            DateOnly periodStart,
            int months)
        {
            var lastYearPeriodStart = periodStart.AddYears(-1);
            var lastYearPeriodEnd = lastYearPeriodStart.AddMonths(months).AddDays(-1);
            var lastYearStart = new DateOnly(lastYearPeriodStart.Year, 1, 1);

            var (_, ytdByLine, _, ytdOpening) = Accumulate(
                standard, accounts, vouchers, lastYearPeriodStart, lastYearPeriodEnd, lastYearStart, lastYearPeriodEnd);
            return (ytdByLine, ytdOpening);
        }

        /// <summary>报表、底稿与公式法 AUTO() 共用的现金科目集合（一级编码 1001/1002/1012 的资产科目）。</summary>
        internal static List<Account> CashAccounts(IReadOnlyList<Account> accounts)
        {
            return accounts
                .Where(a => a.Category == AccountCategory.Asset
                            && CashFlowClassifier.CashAccountPrefixes.Contains(IncomeLineMapper.Prefix(a.Code)))
                .OrderBy(a => a.Code)
                .ToList();
        }

        /// <summary>现金科目 Id 集合（主表分摊用）。</summary>
        internal static HashSet<Guid> CashAccountIds(IReadOnlyList<Account> accounts)
        {
            return CashAccounts(accounts).Select(a => a.Id).ToHashSet();
        }

        /// <summary>对手方科目归类：小企业准则归 22 行（会小企03表），其余归 35 行（会企03表）。</summary>
        private static int ClassifyLine(AccountingStandard standard, Account account, bool inflow)
        {
            return standard == AccountingStandard.SmallBusiness
                ? CashFlowClassifier.MapSmallBusinessLine(account, inflow)
                : CashFlowClassifier.MapLine(account, inflow);
        }

        private static CashFlowRowDef LineDef(AccountingStandard standard, int line)
        {
            return standard == AccountingStandard.SmallBusiness
                ? CashFlowSmallBusinessRows.ByLine[line]
                : CashFlowStandardRows.ByLine[line];
        }

        private static string LineName(AccountingStandard standard, int line)
        {
            var definitions = standard == AccountingStandard.SmallBusiness
                ? CashFlowSmallBusinessRows.ByLine
                : CashFlowStandardRows.ByLine;
            return definitions.TryGetValue(line, out var def) ? def.Name : $"行次{line}";
        }

        private static int? ResolveAssistLine(AccountingStandard standard, AssistItem item)
        {
            return standard == AccountingStandard.SmallBusiness
                ? CashFlowSmallBusinessRows.ResolveAssistItemLine(item.Code, item.Name)
                : CashFlowStandardRows.ResolveAssistItemLine(item.Code, item.Name);
        }

        /// <summary>
        /// 把一张凭证的现金净收付按对手方科目分摊（主表汇总与底稿明细共用，保证两边口径一致）。
        /// amount 已换算成报表方向：流入、流出均为正数，方向由 inflow 区分。
        /// </summary>
        internal static void Allocate(
            Voucher voucher,
            HashSet<Guid> cashAccountIds,
            Action<Voucher, VoucherEntry, decimal, bool> onAllocated)
        {
            var cashNet = voucher.Entries
                .Where(e => cashAccountIds.Contains(e.AccountId))
                .Sum(e => e.Debit - e.Credit);
            if (cashNet == 0m)
            {
                return;
            }

            // 对手方科目按各自方向分摊现金净收付；对手方合计与现金净额可能不等（多现金行凭证），按比例归一。
            var counterparts = voucher.Entries.Where(e => !cashAccountIds.Contains(e.AccountId)).ToList();
            var sideTotal = counterparts.Sum(e => cashNet > 0m ? e.Credit - e.Debit : e.Debit - e.Credit);
            if (sideTotal == 0m)
            {
                return;
            }

            var inflow = cashNet > 0m;
            foreach (var counterpart in counterparts)
            {
                var side = inflow ? counterpart.Credit - counterpart.Debit : counterpart.Debit - counterpart.Credit;
                var amount = cashNet * side / sideTotal;
                if (amount != 0m)
                {
                    onAllocated(voucher, counterpart, inflow ? amount : -amount, inflow);
                }
            }
        }

        /// <summary>由明细行汇总小计、净额、期初期末，输出完整标准行集；lastYearByLine 为上年同期累计。</summary>
        private List<CashFlowLineDto> BuildLines(
            Dictionary<int, decimal> periodByLine,
            Dictionary<int, decimal> ytdByLine,
            decimal periodOpeningCash,
            decimal yearOpeningCash,
            AccountingStandard standard,
            Dictionary<int, decimal>? lastYearByLine = null,
            decimal lastYearOpeningCash = 0m)
        {
            var smallBusiness = standard == AccountingStandard.SmallBusiness;

            void Fill(Dictionary<int, decimal> byLine, decimal openingCash)
            {
                if (smallBusiness)
                {
                    // 会小企03表没有流入/流出小计，各活动直接由明细行算出净额；净增加额/期初/期末落在 20-22。
                    foreach (var section in new[] { CashFlowSection.Operating, CashFlowSection.Investing, CashFlowSection.Financing })
                    {
                        var netLine = CashFlowSmallBusinessRows.NetLine(section);
                        byLine[netLine] = CashFlowSmallBusinessRows.InflowLines(section).Sum(l => byLine.GetValueOrDefault(l))
                                          - CashFlowSmallBusinessRows.OutflowLines(section).Sum(l => byLine.GetValueOrDefault(l));
                    }

                    var smallNet = byLine.GetValueOrDefault(7) + byLine.GetValueOrDefault(13) + byLine.GetValueOrDefault(19);
                    byLine[CashFlowSmallBusinessRows.NetIncreaseLine] = smallNet;
                    byLine[CashFlowSmallBusinessRows.OpeningCashLine] = openingCash;
                    byLine[CashFlowSmallBusinessRows.EndingCashLine] = openingCash + smallNet;
                    return;
                }

                foreach (var section in new[] { CashFlowSection.Operating, CashFlowSection.Investing, CashFlowSection.Financing })
                {
                    var (inflowLine, outflowLine, netLine) = CashFlowStandardRows.SubtotalLines(section);
                    byLine[inflowLine] = CashFlowStandardRows.InflowLines(section).Sum(l => byLine.GetValueOrDefault(l));
                    byLine[outflowLine] = CashFlowStandardRows.OutflowLines(section).Sum(l => byLine.GetValueOrDefault(l));
                    byLine[netLine] = byLine.GetValueOrDefault(inflowLine) - byLine.GetValueOrDefault(outflowLine);
                }

                var net = byLine.GetValueOrDefault(10) + byLine.GetValueOrDefault(22) + byLine.GetValueOrDefault(31);
                byLine[33] = net;
                byLine[34] = openingCash;
                byLine[35] = openingCash + net;
            }

            Fill(periodByLine, periodOpeningCash);
            Fill(ytdByLine, yearOpeningCash);
            if (lastYearByLine != null)
            {
                Fill(lastYearByLine, lastYearOpeningCash);
            }

            var definitions = smallBusiness ? CashFlowSmallBusinessRows.All : CashFlowStandardRows.All;
            var lines = new List<CashFlowLineDto>();
            foreach (var def in definitions)
            {
                lines.Add(new CashFlowLineDto
                {
                    Line = def.Line,
                    Name = def.Name,
                    Section = def.Section,
                    Kind = def.Kind,
                    Current = Round(periodByLine.GetValueOrDefault(def.Line)),
                    YearToDate = Round(ytdByLine.GetValueOrDefault(def.Line)),
                    LastYear = Round(lastYearByLine?.GetValueOrDefault(def.Line) ?? 0m),
                });
            }

            return lines;
        }

        private static decimal Round(decimal value)
        {
            return Math.Round(value, 2, MidpointRounding.ToEven);
        }

        private static (int Year, int Quarter) ResolveQuarter(
            AccountSet accountSet,
            IReadOnlyList<Voucher> vouchers,
            int? year,
            int? quarter)
        {
            if (year is { } y && quarter is { } q)
            {
                if (q < 1 || q > 4)
                {
                    throw new ReportException("季度必须在 1 到 4 之间。");
                }

                if (y < 1900 || y > 9999)
                {
                    throw new ReportException("年份不合法。");
                }

                return (y, q);
            }

            if (vouchers.Count > 0)
            {
                var latest = vouchers.Max(v => v.Date);
                var resolvedQuarter = (latest.Month - 1) / 3 + 1;
                return (latest.Year, resolvedQuarter);
            }

            return (accountSet.StartYear, (accountSet.StartMonth - 1) / 3 + 1);
        }
    }
}
