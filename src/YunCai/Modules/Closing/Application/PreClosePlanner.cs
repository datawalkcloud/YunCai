using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Vouchers.Domain;

namespace YunCai.Modules.Closing.Application
{
    /// <summary>期末检查中一个预置结转项的来源。</summary>
    internal enum PreCloseSource
    {
        /// <summary>由本模块按账簿数据计算并生成凭证。</summary>
        Ledger = 1,

        /// <summary>由工资模块生成（金额取自工资单）。</summary>
        Salary = 2,

        /// <summary>暂未接入数据（库存、坏账等），仅提示使用自定义结转模板。</summary>
        Unsupported = 3,

        /// <summary>已入账检查项：金额取本期发生额，命中凭证供「查看凭证」，本身不生成凭证。</summary>
        Check = 4,
    }

    /// <summary>预置结转项定义（顺序即界面展示顺序，与柠檬云期末检查一致）。</summary>
    internal sealed record PreCloseDefinition(string Key, string Title, PreCloseSource Source);

    /// <summary>账套设置的附加税费率（百分比数值）。</summary>
    internal sealed record SurtaxRates(decimal CityBuild, decimal Education, decimal LocalEducation)
    {
        public decimal Total => CityBuild + Education + LocalEducation;
    }

    /// <summary>一条计划中的结转分录（尚未落库）。</summary>
    internal sealed record PreCloseLine(string Summary, Guid AccountId, decimal Debit, decimal Credit);

    /// <summary>预置结转项的计算结果：金额、可用性与将生成的分录。</summary>
    internal sealed record PreClosePlan(bool Available, decimal Amount, string Hint, IReadOnlyList<PreCloseLine> Lines)
    {
        public static PreClosePlan None(string hint) => new(true, 0m, hint, []);

        public static PreClosePlan Unavailable(string hint) => new(false, 0m, hint, []);

        /// <summary>已入账检查项：本期登记该金额的凭证（按字号顺序），供界面「查看凭证」跳转。</summary>
        public IReadOnlyList<Voucher> SourceVouchers { get; init; } = [];
    }

    /// <summary>
    /// 预置结转项的计算规则：只读账簿数据（已过账凭证 + 期初），不落库。
    /// 取数口径与结转损益一致（借 - 贷净额），科目按编码前缀或名称解析；科目缺失时返回不可用并给出提示。
    /// </summary>
    internal static class PreClosePlanner
    {
        /// <summary>预置结转项清单（与柠檬云「第 1 步 期末检查」渲染出的卡片一致）。</summary>
        public static readonly IReadOnlyList<PreCloseDefinition> Definitions =
        [
            new("sales-cost", "结转销售成本", PreCloseSource.Check),
            new("payroll-accrual", "计提职工薪酬", PreCloseSource.Check),
            new("salary-accrue", "计提工资（工资模块）", PreCloseSource.Salary),
            new("salary-pay", "发放工资（工资模块）", PreCloseSource.Salary),
            new("tax-accrual", "计提税金", PreCloseSource.Ledger),
            new("vat-transfer", "结转未交增值税", PreCloseSource.Ledger),
            new("income-tax", "计提所得税", PreCloseSource.Ledger),
            new("manufacturing", "结转制造费用", PreCloseSource.Ledger),
            new("finished-goods", "结转完工成本", PreCloseSource.Ledger),
        ];

        /// <summary>暂未接入数据源的结转项说明（当前清单内暂无此类项，保留兜底文案）。</summary>
        public static string UnsupportedHint(string key) => "该结转项暂未接入数据，请使用自定义结转模板。";

        /// <summary>按结转项计算本期金额与分录。summary 为分录摘要（如「9月 结转未交增值税」）。</summary>
        public static PreClosePlan Build(
            string key,
            string summary,
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById,
            decimal incomeTaxRate,
            SurtaxRates surtaxRates,
            IReadOnlyList<Voucher> postedVouchers,
            int year,
            DateOnly periodEnd)
        {
            var periodStart = new DateOnly(periodEnd.Year, periodEnd.Month, 1);
            return key switch
            {
                "sales-cost" => BuildSalesCost(accounts, postedVouchers, periodStart, periodEnd),
                "payroll-accrual" => BuildPayrollAccrual(accounts, postedVouchers, periodStart, periodEnd),
                "tax-accrual" => BuildTaxAccrual(summary, accounts, postedVouchers, periodStart, periodEnd, surtaxRates),
                "vat-transfer" => BuildVatTransfer(summary, accounts, balanceById),
                "income-tax" => BuildIncomeTax(summary, accounts, incomeTaxRate, postedVouchers, year, periodEnd),
                "manufacturing" => BuildManufacturing(summary, accounts, balanceById),
                "finished-goods" => BuildFinishedGoods(summary, accounts, balanceById),
                _ => PreClosePlan.Unavailable(UnsupportedHint(key)),
            };
        }

        /// <summary>
        /// 计提税金：本期应交增值税贷方净额 × 附加税费率（城建税 / 教育费附加 / 地方教育附加）。
        /// 与柠檬云一致以本期增值税实缴额为基数；费率为 0 或账套无对应明细科目的税种跳过，
        /// 各税种金额分别四舍五入后再汇总到「税金及附加」，保证借贷严格相等。
        /// </summary>
        private static PreClosePlan BuildTaxAccrual(
            string summary,
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly periodStart,
            DateOnly periodEnd,
            SurtaxRates surtaxRates)
        {
            var vatIds = accounts
                .Where(a => a.IsLeaf && a.Code.StartsWith("2221001", StringComparison.Ordinal))
                .Select(a => a.Id)
                .ToHashSet();
            if (vatIds.Count == 0)
            {
                return PreClosePlan.Unavailable("未找到「应交税费—应交增值税」明细科目，无法计提附加税。");
            }

            var periodEntries = postedVouchers
                .Where(v => v.Date >= periodStart && v.Date <= periodEnd)
                .SelectMany(v => v.Entries)
                .Where(e => vatIds.Contains(e.AccountId))
                .ToList();
            var vatPayable = periodEntries.Sum(e => e.Credit) - periodEntries.Sum(e => e.Debit);
            if (vatPayable <= 0m)
            {
                return PreClosePlan.None("本期应交增值税无贷方净额，无需计提附加税。");
            }

            var expenseAccount = FirstLeafByName(accounts, "税金及附加")
                                 ?? FirstLeafByName(accounts, "主营业务税金及附加")
                                 ?? FirstLeafByCode(accounts, "6403", "5602");
            if (expenseAccount is null)
            {
                return PreClosePlan.Unavailable("未找到「税金及附加」科目，无法计提附加税。");
            }

            var components = new List<(string Name, Account Account, decimal Rate, decimal Amount)>();
            var skipped = new List<string>();
            foreach (var (name, rate, prefixes) in new (string, decimal, string[])[]
                     {
                         ("城市维护建设税", surtaxRates.CityBuild, ["2221008"]),
                         ("教育费附加", surtaxRates.Education, ["2221013"]),
                         ("地方教育附加", surtaxRates.LocalEducation, ["2221014"]),
                     })
            {
                if (rate <= 0m)
                {
                    continue;
                }

                var account = FirstLeafByName(accounts, name)
                              ?? FirstLeafByCode(accounts, prefixes);
                if (account is null)
                {
                    skipped.Add(name);
                    continue;
                }

                components.Add((name, account, rate, Math.Round(vatPayable * rate / 100m, 2, MidpointRounding.AwayFromZero)));
            }

            if (components.Count == 0)
            {
                return PreClosePlan.Unavailable(skipped.Count > 0
                    ? $"未找到附加税明细科目（{string.Join("、", skipped)}），请先在「设置 - 科目」中建立。"
                    : "账套未设置附加税费率，请先在「设置 - 账套」的税率中填写。");
            }

            var total = components.Sum(c => c.Amount);
            var detail = string.Join("、", components.Select(c => $"{c.Name} {c.Amount:0.00}（{c.Rate:0.##}%）"));
            var hint = $"按本期应交增值税 {vatPayable:0.00} 计提：{detail}。";
            if (skipped.Count > 0)
            {
                hint += $"未建立明细科目的税种已跳过：{string.Join("、", skipped)}。";
            }

            var lines = new List<PreCloseLine> { new(summary, expenseAccount.Id, total, 0m) };
            lines.AddRange(components.Select(c => new PreCloseLine(summary, c.Account.Id, 0m, c.Amount)));
            return new PreClosePlan(true, total, hint, lines);
        }

        /// <summary>
        /// 结转销售成本（已入账回显）：本期主营业务成本借方发生额。
        /// 柠檬云该项检查的是「销售成本是否已入账」，已入账时给金额 + 查看凭证而不是再生成一张；
        /// 本系统未接库存核算，无发生额时只能提示走模板或手工凭证。
        /// </summary>
        private static PreClosePlan BuildSalesCost(
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly periodStart,
            DateOnly periodEnd)
        {
            var sources = accounts
                .Where(a => a.IsLeaf && a.IsEnabled
                            && (a.Name.Contains("主营业务成本", StringComparison.Ordinal)
                                || a.Code.StartsWith("6401", StringComparison.Ordinal)))
                .Select(a => a.Id)
                .ToHashSet();
            var booked = VouchersBooking(postedVouchers, periodStart, periodEnd, sources, debitSide: true);
            var amount = booked.Sum(v => v.Entries
                .Where(e => sources.Contains(e.AccountId))
                .Sum(e => e.Debit));
            if (amount == 0m)
            {
                return PreClosePlan.Unavailable("本期无主营业务成本发生额；如需按出库成本结转，请使用自定义结转模板或手工凭证。");
            }

            return new PreClosePlan(false, amount, $"本期已入账主营业务成本 {amount:0.00}，无需重复结转。", [])
            {
                SourceVouchers = booked,
            };
        }

        /// <summary>
        /// 计提职工薪酬（已入账回显）：本期应付职工薪酬贷方发生额，即已计提的工资社保等；
        /// 发放工资走借方，不计入本项。
        /// </summary>
        private static PreClosePlan BuildPayrollAccrual(
            IReadOnlyList<Account> accounts,
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly periodStart,
            DateOnly periodEnd)
        {
            var sources = accounts
                .Where(a => a.IsLeaf && a.IsEnabled
                            && (a.Name.Contains("应付职工薪酬", StringComparison.Ordinal)
                                || a.Code.StartsWith("2211", StringComparison.Ordinal)))
                .Select(a => a.Id)
                .ToHashSet();
            var booked = VouchersBooking(postedVouchers, periodStart, periodEnd, sources, debitSide: false);
            var amount = booked.Sum(v => v.Entries
                .Where(e => sources.Contains(e.AccountId))
                .Sum(e => e.Credit));
            if (amount == 0m)
            {
                return PreClosePlan.Unavailable("本期无应付职工薪酬贷方发生额；社保、公积金等单独计提请使用自定义结转模板。");
            }

            return new PreClosePlan(false, amount, $"本期已计提职工薪酬 {amount:0.00}，无需重复结转。", [])
            {
                SourceVouchers = booked,
            };
        }

        /// <summary>本期登记了指定科目借方（或贷方）金额的已过账凭证，按字号升序。</summary>
        private static IReadOnlyList<Voucher> VouchersBooking(
            IReadOnlyList<Voucher> postedVouchers,
            DateOnly periodStart,
            DateOnly periodEnd,
            HashSet<Guid> accountIds,
            bool debitSide)
        {
            return postedVouchers
                .Where(v => v.Date >= periodStart && v.Date <= periodEnd)
                .Where(v => v.Entries.Any(e => accountIds.Contains(e.AccountId)
                                               && (debitSide ? e.Debit : e.Credit) > 0m))
                .OrderBy(v => v.Word, StringComparer.Ordinal)
                .ThenBy(v => v.Number)
                .ToList();
        }

        /// <summary>结转未交增值税：应交增值税子树贷方净额 → 转出未交增值税 / 未交增值税。</summary>
        private static PreClosePlan BuildVatTransfer(
            string summary,
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            var vatAccounts = accounts
                .Where(a => a.IsLeaf && a.Code.StartsWith("2221001", StringComparison.Ordinal))
                .ToList();
            if (vatAccounts.Count == 0)
            {
                return PreClosePlan.Unavailable("未找到「应交税费—应交增值税」明细科目，无法结转未交增值税。");
            }

            var net = vatAccounts.Sum(a => balanceById.GetValueOrDefault(a.Id));
            if (net >= 0m)
            {
                return PreClosePlan.None("本期应交增值税无贷方余额，无需结转。");
            }

            var debitAccount = FirstLeafByName(accounts, "转出未交增值税")
                               ?? (vatAccounts.Count == 1 ? vatAccounts[0] : null);
            var creditAccount = FirstLeafByNameExact(accounts, "未交增值税") ?? FirstLeafByName(accounts, "未交增值税");
            if (debitAccount is null || creditAccount is null)
            {
                return PreClosePlan.Unavailable("未找到「转出未交增值税」或「未交增值税」科目，无法结转。");
            }

            var amount = -net;
            return new PreClosePlan(true, amount, $"按应交增值税贷方余额 {amount:0.00} 结转。",
            [
                new PreCloseLine(summary, debitAccount.Id, amount, 0m),
                new PreCloseLine(summary, creditAccount.Id, 0m, amount),
            ]);
        }

        /// <summary>
        /// 计提所得税：本年累计利润总额 × 税率 − 本年累计已计提所得税。
        /// 按累计口径而非当月损益余额：结转损益后损益类科目已结平，只看余额会退化成按月计提，
        /// 与柠檬云（累计应缴 − 累计已计提）差出小数。
        /// </summary>
        private static PreClosePlan BuildIncomeTax(
            string summary,
            IReadOnlyList<Account> accounts,
            decimal incomeTaxRate,
            IReadOnlyList<Voucher> postedVouchers,
            int year,
            DateOnly periodEnd)
        {
            if (incomeTaxRate <= 0m)
            {
                return PreClosePlan.Unavailable("账套未设置企业所得税税率，请先在账套设置中填写。");
            }

            var expenseAccount = FirstLeafByName(accounts, "所得税费用")
                                 ?? FirstLeafByCode(accounts, "6801", "5801");
            var payableAccount = FirstLeafByNameExact(accounts, "应交所得税")
                                 ?? FirstLeafByName(accounts, "应交所得税")
                                 ?? FirstLeafByName(accounts, "企业所得税");
            if (expenseAccount is null || payableAccount is null)
            {
                return PreClosePlan.Unavailable("未找到「所得税费用」或「应交所得税」科目，无法计提所得税。");
            }

            // 含「本年利润」科目的结转凭证与利润表同口径剔除：它会把损益发生额冲平、把已计提所得税冲回。
            var profitAccountIds = accounts
                .Where(a => a.Code.StartsWith("4103", StringComparison.Ordinal)
                            || a.Code.StartsWith("3103", StringComparison.Ordinal))
                .Select(a => a.Id)
                .ToHashSet();
            var yearStart = new DateOnly(year, 1, 1);
            var plainEntries = postedVouchers
                .Where(v => v.Date >= yearStart && v.Date <= periodEnd)
                .Where(v => !v.Entries.Any(e => profitAccountIds.Contains(e.AccountId)))
                .SelectMany(v => v.Entries)
                .ToList();

            var pnlAccountIds = accounts
                .Where(a => a.IsLeaf && a.Category == AccountCategory.ProfitAndLoss)
                .Select(a => a.Id)
                .ToHashSet();
            var profitBeforeTax = plainEntries
                .Where(e => pnlAccountIds.Contains(e.AccountId) && e.AccountId != expenseAccount.Id)
                .Sum(e => e.Credit - e.Debit);
            var accruedTax = plainEntries
                .Where(e => e.AccountId == expenseAccount.Id)
                .Sum(e => e.Debit - e.Credit);

            var target = profitBeforeTax <= 0m
                ? 0m
                : Math.Round(profitBeforeTax * incomeTaxRate / 100m, 2, MidpointRounding.AwayFromZero);
            var amount = Math.Max(0m, target - accruedTax);
            var hint = $"按本年累计利润总额 {profitBeforeTax:0.00} × {incomeTaxRate:0.##}% 计算，本年累计已计提 {accruedTax:0.00}。";

            return amount == 0m
                ? PreClosePlan.None(hint + "本期无需补提。")
                : new PreClosePlan(true, amount, hint,
                [
                    new PreCloseLine(summary, expenseAccount.Id, amount, 0m),
                    new PreCloseLine(summary, payableAccount.Id, 0m, amount),
                ]);
        }

        /// <summary>结转制造费用：制造费用借方余额 → 生产成本。</summary>
        private static PreClosePlan BuildManufacturing(
            string summary,
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            var sources = CostLeavesByName(accounts, "制造费用")
                .Select(a => (Account: a, Net: balanceById.GetValueOrDefault(a.Id)))
                .Where(x => x.Net > 0m)
                .ToList();
            if (sources.Count == 0)
            {
                return PreClosePlan.None("本期制造费用无借方余额，无需结转。");
            }

            var target = FirstLeafByName(accounts, "生产成本");
            if (target is null)
            {
                return PreClosePlan.Unavailable("未找到「生产成本」科目，无法结转制造费用。");
            }

            var total = sources.Sum(x => x.Net);
            var lines = new List<PreCloseLine> { new(summary, target.Id, total, 0m) };
            lines.AddRange(sources.Select(x => new PreCloseLine(summary, x.Account.Id, 0m, x.Net)));
            return new PreClosePlan(true, total, $"按制造费用借方余额 {total:0.00} 转入生产成本。", lines);
        }

        /// <summary>结转完工成本：生产成本借方余额 → 库存商品。</summary>
        private static PreClosePlan BuildFinishedGoods(
            string summary,
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById)
        {
            var sources = CostLeavesByName(accounts, "生产成本")
                .Select(a => (Account: a, Net: balanceById.GetValueOrDefault(a.Id)))
                .Where(x => x.Net > 0m)
                .ToList();
            if (sources.Count == 0)
            {
                return PreClosePlan.None("本期生产成本无借方余额，无需结转。");
            }

            var target = FirstLeafByName(accounts, "库存商品") ?? FirstLeafByCode(accounts, "1405", "1243");
            if (target is null)
            {
                return PreClosePlan.Unavailable("未找到「库存商品」科目，无法结转完工成本。");
            }

            var total = sources.Sum(x => x.Net);
            var lines = new List<PreCloseLine> { new(summary, target.Id, total, 0m) };
            lines.AddRange(sources.Select(x => new PreCloseLine(summary, x.Account.Id, 0m, x.Net)));
            return new PreClosePlan(true, total, $"按生产成本借方余额 {total:0.00} 转入库存商品。", lines);
        }

        private static Account? FirstLeafByName(IReadOnlyList<Account> accounts, string name) =>
            accounts
                .Where(a => a.IsLeaf && a.IsEnabled && a.Name.Contains(name, StringComparison.Ordinal))
                .OrderBy(a => a.Code.Length)
                .ThenBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();

        private static Account? FirstLeafByNameExact(IReadOnlyList<Account> accounts, string name) =>
            accounts
                .Where(a => a.IsLeaf && a.IsEnabled && a.Name == name)
                .OrderBy(a => a.Code.Length)
                .ThenBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();

        private static Account? FirstLeafByCode(IReadOnlyList<Account> accounts, params string[] prefixes) =>
            accounts
                .Where(a => a.IsLeaf && a.IsEnabled
                            && prefixes.Any(p => a.Code.StartsWith(p, StringComparison.Ordinal)))
                .OrderBy(a => a.Code.Length)
                .ThenBy(a => a.Code, StringComparer.Ordinal)
                .FirstOrDefault();

        /// <summary>
        /// 取名称命中关键字的成本类末级科目（按编码排序）。
        /// 成本类编码在新旧准则里含义不同（小企业准则 5001 是主营业务收入、4001 才是生产成本），
        /// 所以这几处一律按科目名取数，不按编码前缀猜。
        /// </summary>
        private static List<Account> CostLeavesByName(IReadOnlyList<Account> accounts, string name) =>
            accounts
                .Where(a => a.IsLeaf && a.IsEnabled && a.Category == AccountCategory.Cost
                            && a.Name.Contains(name, StringComparison.Ordinal))
                .OrderBy(a => a.Code.Length)
                .ThenBy(a => a.Code, StringComparer.Ordinal)
                .ToList();
    }
}
