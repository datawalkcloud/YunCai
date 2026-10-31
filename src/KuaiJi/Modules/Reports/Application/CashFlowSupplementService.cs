using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.Reports.Domain.CashFlow;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Reports.Application
{
    /// <summary>
    /// 现金流量表补充资料（间接法）：从「净利润」出发，加回不影响经营活动现金流的损益项，
    /// 再按经营性资产负债的期初期末变动调节出「经营活动产生的现金流量净额」，
    /// 末段列示现金及现金等价物的期末/期初余额与净增加额。
    /// <para>
    /// 取数一律来自已过账凭证与科目启用期初；科目归集沿用资产负债表口径
    /// （<see cref="BalanceSheetService.MapLine"/>），净利润沿用利润表口径
    /// （<see cref="IncomeStatementService.NetProfit"/>，剔除结转凭证）。
    /// </para>
    /// <para>
    /// 折旧/摊销/减值/清理等按科目名称关键字与一级编码识别，账套科目命名不规范时对应行会偏小，
    /// 差额集中体现在 <see cref="CashFlowSupplementDto.Difference"/>（与主表直接法净额的差异）。
    /// </para>
    /// </summary>
    public sealed class CashFlowSupplementService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly CashFlowService _cashFlowService;

        public CashFlowSupplementService(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            CashFlowService cashFlowService)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _cashFlowService = cashFlowService;
        }

        /// <summary>月度补充资料。year/month 省略时取最近有凭证的月份，与主表同一期间解析规则。</summary>
        public async Task<CashFlowSupplementDto> GetPeriodAsync(
            Guid accountSetId,
            int? year,
            int? month,
            CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReportException("指定的账套不存在。");
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountsById = accounts.ToDictionary(a => a.Id, a => a);
            var vouchers = (await _voucherRepository.GetAllAsync(accountSetId, cancellationToken))
                .Where(v => v.Status == VoucherStatus.Posted)
                .ToList();

            var (resolvedYear, resolvedMonth) = ReportPeriodResolver.Resolve(accountSet, vouchers, year, month);
            var periodStart = new DateOnly(resolvedYear, resolvedMonth, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var yearStart = new DateOnly(resolvedYear, 1, 1);

            // 三个时点的科目借-贷净额（含启用期初）：年初、期间期初、期间期末。
            var atYearStart = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, yearStart.AddDays(-1));
            var atPeriodStart = periodStart == yearStart
                ? atYearStart
                : AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, periodStart.AddDays(-1));
            var atPeriodEnd = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, periodEnd);

            // 区间内发生额（不含期初）：本月、本年累计。
            var periodMoves = AccountNetCalculator.PeriodNetByAccount(vouchers, periodStart, periodEnd);
            var yearMoves = AccountNetCalculator.PeriodNetByAccount(vouchers, yearStart, periodEnd);

            var sets = new AccountSets(accounts);
            var (netProfit, netProfitYtd) = IncomeStatementService.NetProfit(
                vouchers, accountsById, yearStart, periodStart, periodStart.AddMonths(1));

            // 备抵科目（累计折旧、减值准备等）贷方为正，计提额取「贷 - 借」＝净发生额取负。
            decimal Provision(IEnumerable<Account> group, Dictionary<Guid, decimal> moves) =>
                -group.Sum(a => moves.GetValueOrDefault(a.Id));

            // 资产类余额减少（期初 - 期末）；负债类余额增加（期末 - 期初，同样换算成借-贷视角后取负）。
            decimal AssetDecrease(IEnumerable<Account> group, Dictionary<Guid, decimal> opening, Dictionary<Guid, decimal> ending) =>
                group.Sum(a => opening.GetValueOrDefault(a.Id) - ending.GetValueOrDefault(a.Id));

            decimal LiabilityIncrease(IEnumerable<Account> group, Dictionary<Guid, decimal> opening, Dictionary<Guid, decimal> ending) =>
                -group.Sum(a => ending.GetValueOrDefault(a.Id) - opening.GetValueOrDefault(a.Id));

            // 行次 36-52：与企业会计准则（2007）「将净利润调节为经营活动现金流量」一致
            var adjustment = new List<CashFlowSupplementLineDto>
            {
                Line(36, "净利润", netProfit, netProfitYtd),
                Line(37, "加：资产减值准备",
                    Provision(sets.Impairments, periodMoves), Provision(sets.Impairments, yearMoves)),
                Line(38, "固定资产折旧、油气资产折耗、生产性生物资产折旧",
                    Provision(sets.Depreciation, periodMoves), Provision(sets.Depreciation, yearMoves)),
                Line(39, "无形资产摊销",
                    Provision(sets.IntangibleAmortization, periodMoves), Provision(sets.IntangibleAmortization, yearMoves)),
                Line(40, "长期待摊费用摊销",
                    AssetDecrease(sets.LongTermDeferred, atPeriodStart, atPeriodEnd),
                    AssetDecrease(sets.LongTermDeferred, atYearStart, atPeriodEnd)),
                Line(41, "处置固定资产、无形资产和其他长期资产的损失",
                    Sum(sets.AssetDisposal, periodMoves), Sum(sets.AssetDisposal, yearMoves)),
                Line(42, "固定资产报废损失", 0m, 0m),
                Line(43, "公允价值变动损失", 0m, 0m),
                Line(44, "财务费用", Sum(sets.FinanceExpense, periodMoves), Sum(sets.FinanceExpense, yearMoves)),
                Line(45, "投资损失", 0m, 0m),
                Line(46, "递延所得税资产减少", 0m, 0m),
                Line(47, "递延所得税负债增加", 0m, 0m),
                Line(48, "存货的减少",
                    AssetDecrease(sets.Inventories, atPeriodStart, atPeriodEnd),
                    AssetDecrease(sets.Inventories, atYearStart, atPeriodEnd)),
                Line(49, "经营性应收项目的减少",
                    AssetDecrease(sets.OperatingReceivables, atPeriodStart, atPeriodEnd),
                    AssetDecrease(sets.OperatingReceivables, atYearStart, atPeriodEnd)),
                Line(50, "经营性应付项目的增加",
                    LiabilityIncrease(sets.OperatingPayables, atPeriodStart, atPeriodEnd),
                    LiabilityIncrease(sets.OperatingPayables, atYearStart, atPeriodEnd)),
                Line(51, "其他", 0m, 0m),
            };

            var operatingNet = adjustment.Sum(l => l.Current);
            var operatingNetYtd = adjustment.Sum(l => l.YearToDate);
            adjustment.Add(Line(52, "经营活动产生的现金流量净额", operatingNet, operatingNetYtd, isTotal: true));

            var nonCash = new List<CashFlowSupplementLineDto>
            {
                Line(53, "债务转为资本", 0m, 0m, CashFlowSupplementGroup.NonCashActivities),
                Line(54, "一年内到期的可转换公司债券", 0m, 0m, CashFlowSupplementGroup.NonCashActivities),
                Line(55, "融资租入固定资产", 0m, 0m, CashFlowSupplementGroup.NonCashActivities),
            };

            // 现金及现金等价物与主表同口径（一级编码 1001/1002/1012）；现金等价物本系统未单独核算。
            var cashEnding = AssetBalance(sets.Cash, atPeriodEnd);
            var cashOpening = AssetBalance(sets.Cash, atPeriodStart);
            var cashYearOpening = AssetBalance(sets.Cash, atYearStart);
            var cashChanges = new List<CashFlowSupplementLineDto>
            {
                // 期末余额两栏相同（本年累计栏取同一期末数），期初余额按各栏窗口起点区分。
                Line(56, "现金的期末余额", cashEnding, cashEnding, CashFlowSupplementGroup.CashChanges),
                Line(57, "减：现金的期初余额", cashOpening, cashYearOpening, CashFlowSupplementGroup.CashChanges),
                Line(58, "加：现金等价物的期末余额", 0m, 0m, CashFlowSupplementGroup.CashChanges),
                Line(59, "减：现金等价物的期初余额", 0m, 0m, CashFlowSupplementGroup.CashChanges),
                Line(60, "现金及现金等价物净增加额", cashEnding - cashOpening, cashEnding - cashYearOpening,
                    CashFlowSupplementGroup.CashChanges, isTotal: true),
            };

            var mainTable = await _cashFlowService.GetPeriodAsync(accountSetId, resolvedYear, resolvedMonth, cancellationToken);
            var mainOperatingNet = mainTable.Lines.FirstOrDefault(l => l.Line == 10)?.Current ?? 0m;

            return new CashFlowSupplementDto
            {
                Year = resolvedYear,
                Month = resolvedMonth,
                PeriodText = $"{resolvedYear:D4}年{resolvedMonth:D2}月",
                Lines = [.. adjustment, .. nonCash, .. cashChanges],
                MainTableOperatingNet = Round(mainOperatingNet),
                Difference = Round(operatingNet - mainOperatingNet),
            };
        }

        private static decimal Sum(IEnumerable<Account> group, Dictionary<Guid, decimal> moves) =>
            group.Sum(a => moves.GetValueOrDefault(a.Id));

        /// <summary>资产类科目余额（借方为正）。</summary>
        private static decimal AssetBalance(IEnumerable<Account> group, Dictionary<Guid, decimal> net) =>
            group.Sum(a => net.GetValueOrDefault(a.Id));

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.ToEven);

        private static CashFlowSupplementLineDto Line(
            int line,
            string name,
            decimal current,
            decimal yearToDate,
            CashFlowSupplementGroup group = CashFlowSupplementGroup.Adjustment,
            bool isTotal = false)
        {
            return new CashFlowSupplementLineDto
            {
                Line = line,
                Name = name,
                Group = group,
                IsTotal = isTotal,
                Current = Round(current),
                YearToDate = Round(yearToDate),
            };
        }

        /// <summary>
        /// 补充资料各调节项的科目集合。名称关键字优先、一级编码兜底，兼容新旧准则命名差异。
        /// </summary>
        private sealed class AccountSets
        {
            public AccountSets(IReadOnlyList<Account> accounts)
            {
                var assets = accounts.Where(a => a.Category is AccountCategory.Asset or AccountCategory.Cost).ToList();

                Impairments = assets.Where(a => Contains(a, "减值准备", "坏账准备", "跌价准备")).ToList();

                Depreciation = assets
                    .Where(a => !Impairments.Contains(a)
                                && (Contains(a, "累计折旧") || PrefixIn(a, "1602", "1622", "1651")))
                    .ToList();

                IntangibleAmortization = assets
                    .Where(a => !Impairments.Contains(a)
                                && (Contains(a, "累计摊销") || PrefixIn(a, "1702")))
                    .ToList();

                // 账套未设「累计摊销」时（旧制度直接冲减无形资产），以无形资产科目本期贷方发生额代替摊销额。
                if (IntangibleAmortization.Count == 0)
                {
                    IntangibleAmortization = assets
                        .Where(a => !Impairments.Contains(a)
                                    && (Contains(a, "无形资产") || PrefixIn(a, "1701")))
                        .ToList();
                }

                LongTermDeferred = assets.Where(a => Contains(a, "长期待摊") || PrefixIn(a, "1801")).ToList();

                DeferredExpenses = assets
                    .Where(a => Contains(a, "待摊费用") && !Contains(a, "长期待摊"))
                    .ToList();

                AccruedExpenses = accounts
                    .Where(a => a.Category == AccountCategory.Liability && Contains(a, "预提费用"))
                    .ToList();

                AssetDisposal = assets
                    .Where(a => Contains(a, "固定资产清理") || PrefixIn(a, "1606"))
                    .ToList();

                FinanceExpense = accounts
                    .Where(a => a.Category == AccountCategory.ProfitAndLoss
                                && (Contains(a, "财务费用") || PrefixIn(a, "6603", "5301")))
                    .ToList();

                Inventories = assets.Where(a => BalanceSheetService.MapLine(a.Code, a.Category) == 7).ToList();

                // 流动资产中除货币资金、短期投资、存货外的项目，均视为经营性应收。
                OperatingReceivables = assets
                    .Where(a => a.Category == AccountCategory.Asset)
                    .Where(a => new[] { 3, 4, 5, 6, 9 }.Contains(BalanceSheetService.MapLine(a.Code, a.Category)))
                    .ToList();

                // 流动负债中除短期借款（带息，属筹资活动）外的项目，均视为经营性应付。
                OperatingPayables = accounts
                    .Where(a => a.Category == AccountCategory.Liability)
                    .Where(a => new[] { 32, 33, 34, 35, 36, 37, 39 }.Contains(BalanceSheetService.MapLine(a.Code, a.Category)))
                    .ToList();

                Cash = assets
                    .Where(a => a.Category == AccountCategory.Asset
                                && CashFlowClassifier.CashAccountPrefixes.Contains(IncomeLineMapper.Prefix(a.Code)))
                    .ToList();
            }

            /// <summary>计提的资产减值准备（坏账准备、跌价准备等备抵科目）。</summary>
            public IReadOnlyList<Account> Impairments { get; private set; }

            public IReadOnlyList<Account> Depreciation { get; private set; }

            public IReadOnlyList<Account> IntangibleAmortization { get; private set; }

            public IReadOnlyList<Account> LongTermDeferred { get; private set; }

            public IReadOnlyList<Account> DeferredExpenses { get; private set; }

            public IReadOnlyList<Account> AccruedExpenses { get; private set; }

            public IReadOnlyList<Account> AssetDisposal { get; private set; }

            public IReadOnlyList<Account> FinanceExpense { get; private set; }

            public IReadOnlyList<Account> Inventories { get; private set; }

            public IReadOnlyList<Account> OperatingReceivables { get; private set; }

            public IReadOnlyList<Account> OperatingPayables { get; private set; }

            public IReadOnlyList<Account> Cash { get; private set; }

            private static bool Contains(Account account, params string[] keywords) =>
                keywords.Any(k => account.Name.Contains(k, StringComparison.Ordinal));

            private static bool PrefixIn(Account account, params string[] prefixes) =>
                prefixes.Contains(IncomeLineMapper.Prefix(account.Code));
        }
    }
}
