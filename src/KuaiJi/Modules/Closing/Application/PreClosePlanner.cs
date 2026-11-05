using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Vouchers.Domain;

namespace KuaiJi.Modules.Closing.Application
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
    }

    /// <summary>预置结转项定义（顺序即界面展示顺序，与柠檬云期末检查一致）。</summary>
    internal sealed record PreCloseDefinition(string Key, string Title, PreCloseSource Source);

    /// <summary>一条计划中的结转分录（尚未落库）。</summary>
    internal sealed record PreCloseLine(string Summary, Guid AccountId, decimal Debit, decimal Credit);

    /// <summary>预置结转项的计算结果：金额、可用性与将生成的分录。</summary>
    internal sealed record PreClosePlan(bool Available, decimal Amount, string Hint, IReadOnlyList<PreCloseLine> Lines)
    {
        public static PreClosePlan None(string hint) => new(true, 0m, hint, []);

        public static PreClosePlan Unavailable(string hint) => new(false, 0m, hint, []);
    }

    /// <summary>
    /// 预置结转项的计算规则：只读账簿数据（已过账凭证 + 期初），不落库。
    /// 取数口径与结转损益一致（借 - 贷净额），科目按编码前缀或名称解析；科目缺失时返回不可用并给出提示。
    /// </summary>
    internal static class PreClosePlanner
    {
        /// <summary>预置结转项清单（与柠檬云「第 1 步 期末检查」的卡片一致）。</summary>
        public static readonly IReadOnlyList<PreCloseDefinition> Definitions =
        [
            new("sales-cost", "结转销售成本", PreCloseSource.Unsupported),
            new("payroll-accrual", "计提职工薪酬", PreCloseSource.Unsupported),
            new("salary-accrue", "计提工资（工资模块）", PreCloseSource.Salary),
            new("salary-pay", "发放工资（工资模块）", PreCloseSource.Salary),
            new("tax-accrual", "计提税金", PreCloseSource.Unsupported),
            new("vat-transfer", "结转未交增值税", PreCloseSource.Ledger),
            new("income-tax", "计提所得税", PreCloseSource.Ledger),
            new("bad-debt", "计提坏账", PreCloseSource.Unsupported),
            new("manufacturing", "结转制造费用", PreCloseSource.Ledger),
            new("finished-goods", "结转完工成本", PreCloseSource.Ledger),
        ];

        /// <summary>不可用结转项的说明（按预置顺序）。</summary>
        public static string UnsupportedHint(string key) => key switch
        {
            "sales-cost" => "未启用库存核算，暂无销售出库成本数据；请使用自定义结转模板或手工凭证结转。",
            "payroll-accrual" => "工资模块的计提凭证已包含公司承担款项；社保等单独计提请使用自定义结转模板。",
            "tax-accrual" => "暂无税金计提数据源，请使用自定义结转模板按税种计提。",
            "bad-debt" => "未配置坏账计提比例，请使用自定义结转模板或手工凭证计提。",
            _ => "该结转项暂未接入数据，请使用自定义结转模板。",
        };

        /// <summary>按结转项计算本期金额与分录。summary 为分录摘要（如「9月 结转未交增值税」）。</summary>
        public static PreClosePlan Build(
            string key,
            string summary,
            IReadOnlyList<Account> accounts,
            IReadOnlyDictionary<Guid, decimal> balanceById,
            decimal incomeTaxRate,
            IReadOnlyList<Voucher> postedVouchers,
            int year,
            DateOnly periodEnd)
        {
            return key switch
            {
                "vat-transfer" => BuildVatTransfer(summary, accounts, balanceById),
                "income-tax" => BuildIncomeTax(summary, accounts, incomeTaxRate, postedVouchers, year, periodEnd),
                "manufacturing" => BuildManufacturing(summary, accounts, balanceById),
                "finished-goods" => BuildFinishedGoods(summary, accounts, balanceById),
                _ => PreClosePlan.Unavailable(UnsupportedHint(key)),
            };
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
            var sources = accounts
                .Where(a => a.IsLeaf && a.IsEnabled
                            && (a.Code.StartsWith("5101", StringComparison.Ordinal) || a.Name.Contains("制造费用")))
                .Select(a => (Account: a, Net: balanceById.GetValueOrDefault(a.Id)))
                .Where(x => x.Net > 0m)
                .ToList();
            if (sources.Count == 0)
            {
                return PreClosePlan.None("本期制造费用无借方余额，无需结转。");
            }

            var target = FirstLeafUnder(accounts, "5001", "基本生产成本") ?? FirstLeafByName(accounts, "生产成本");
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
            var sources = accounts
                .Where(a => a.IsLeaf && a.IsEnabled && a.Code.StartsWith("5001", StringComparison.Ordinal))
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

        /// <summary>取指定编码下的末级科目（父级自身是末级时取父级）；同名科目优先。</summary>
        private static Account? FirstLeafUnder(IReadOnlyList<Account> accounts, string code, string? preferredName)
        {
            var leaves = accounts
                .Where(a => a.IsLeaf && a.IsEnabled
                            && (a.Code == code || a.Code.StartsWith(code, StringComparison.Ordinal)))
                .OrderBy(a => a.Code.Length)
                .ThenBy(a => a.Code, StringComparer.Ordinal)
                .ToList();
            if (preferredName is not null)
            {
                var preferred = leaves.FirstOrDefault(a => a.Name.Contains(preferredName, StringComparison.Ordinal));
                if (preferred is not null)
                {
                    return preferred;
                }
            }

            return leaves.FirstOrDefault();
        }
    }
}
