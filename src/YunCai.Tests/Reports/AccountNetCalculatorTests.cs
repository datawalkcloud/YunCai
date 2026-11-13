using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Vouchers.Domain;
using YunCai.Modules.Reports.Application;
using Xunit;

namespace YunCai.Tests.Reports;

/// <summary>
/// 报表取数共享口径测试（<see cref="AccountNetCalculator"/>，供所有者权益变动表、纳税统计表、
/// 费用明细表、财务概要、现金流量表期初期末行共用）。
/// 领域依据（AGENTS.md「领域须知」）：
/// - 余额内部一律按「借 − 贷」净额累计，只在输出层拆回借/贷两栏；
/// - 期初余额按科目余额方向换算成同一净额视角后先于一切凭证计入；
/// - 报表只统计已过账凭证（调用方过滤，本口径负责日期窗口与含期初/不含期初两种净额）。
/// </summary>
public sealed class AccountNetCalculatorTests
{
    private static readonly Guid TestAccountSetId = Guid.NewGuid();

    /// <summary>构造一个不落库的末级科目（净额计算只读 Id/类别/方向/期初）。</summary>
    private static Account Acc(
        string code,
        string name,
        AccountCategory category,
        BalanceDirection direction,
        decimal opening = 0m)
    {
        var account = Account.CreateRoot(TestAccountSetId, code, name, category, direction);
        if (opening != 0m)
        {
            account.SetOpeningBalance(opening);
        }

        return account;
    }

    /// <summary>构造一张已过账凭证（净额口径不校验状态，但按生产入参约定仍走完过账）。</summary>
    private static Voucher Posted(int number, DateOnly date, params (Account Account, decimal Debit, decimal Credit)[] entries)
    {
        var voucher = Voucher.Create(TestAccountSetId, "记", number, date, "测试制单人");
        foreach (var (account, debit, credit) in entries)
        {
            voucher.AddEntry("测试分录", account.Id, debit, credit);
        }

        voucher.Post();
        return voucher;
    }

    // —— 借−贷净额 + 期初 ——

    [Fact]
    public void NetByAccountUpTo_OpeningBalances_ConvertedToDebitMinusCreditByDirection()
    {
        // AGENTS.md：余额内部一律按「借 − 贷」净额累计；期初按科目余额方向换算——
        // 资产/成本类借方为正，负债/权益/损益类贷方为正故期初在净额里是负数。
        var cash = Acc("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, 3000m);
        var payable = Acc("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit, 5000m);
        var capital = Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, 8000m);
        var production = Acc("4101", "生产成本", AccountCategory.Cost, BalanceDirection.Debit, 1200m);

        var net = AccountNetCalculator.NetByAccountUpTo([cash, payable, capital, production], [], null);

        Assert.Equal(3000m, net[cash.Id]);
        Assert.Equal(-5000m, net[payable.Id]);
        Assert.Equal(-8000m, net[capital.Id]);
        Assert.Equal(1200m, net[production.Id]);
    }

    [Fact]
    public void NetByAccountUpTo_SameAccountOnBothSides_NetsWithinAccount()
    {
        // 发生额先在科目内部按借−贷轧差，不分别累计两栏（避免新增账页各自按方向累加的旧写法）。
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var tax = Acc("2221", "应交税费", AccountCategory.Liability, BalanceDirection.Credit);

        var vouchers = new[]
        {
            Posted(1, new DateOnly(2026, 3, 5), (bank, 700m, 0m), (revenue, 0m, 500m), (tax, 0m, 200m)),
            Posted(2, new DateOnly(2026, 3, 20), (revenue, 300m, 0m), (bank, 0m, 300m)),
        };

        var net = AccountNetCalculator.NetByAccountUpTo([bank, revenue, tax], vouchers, null);

        Assert.Equal(400m, net[bank.Id]);     // 700 − 300
        Assert.Equal(-200m, net[revenue.Id]); // 贷 500 − 借 300 → 借−贷 = −200
        Assert.Equal(-200m, net[tax.Id]);
    }

    [Fact]
    public void NetByAccountUpTo_CutoffDate_VouchersAfterCutoffExcluded()
    {
        // until 含当日：3 月 31 日的凭证计入、4 月 1 日的不计入；until 为 null 时全量计入。
        // 现金流量表「期初现金余额」正是靠这个截止点取窗口起点前的余额，边界错一天期初就错一笔。
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var vouchers = new[]
        {
            Posted(1, new DateOnly(2026, 3, 31), (bank, 1000m, 0m), (revenue, 0m, 1000m)),
            Posted(2, new DateOnly(2026, 4, 1), (bank, 2000m, 0m), (revenue, 0m, 2000m)),
        };

        var upToMarch = AccountNetCalculator.NetByAccountUpTo([bank, revenue], vouchers, new DateOnly(2026, 3, 31));
        Assert.Equal(1000m, upToMarch[bank.Id]);
        Assert.Equal(-1000m, upToMarch[revenue.Id]);

        var upToAll = AccountNetCalculator.NetByAccountUpTo([bank, revenue], vouchers, null);
        Assert.Equal(3000m, upToAll[bank.Id]);

        // 期初 + 截止日前发生额合并计入同一净额（所有者权益变动表/财务概要口径）。
        var openingBank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, 500m);
        var openingRevenue = Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var openingVouchers = new[]
        {
            Posted(3, new DateOnly(2026, 3, 31), (openingBank, 1000m, 0m), (openingRevenue, 0m, 1000m)),
            Posted(4, new DateOnly(2026, 4, 1), (openingBank, 400m, 0m), (openingRevenue, 0m, 400m)),
        };

        var withOpening = AccountNetCalculator.NetByAccountUpTo([openingBank, openingRevenue], openingVouchers, new DateOnly(2026, 3, 31));
        Assert.Equal(1500m, withOpening[openingBank.Id]);    // 期初 500 + 3 月净收 1000（4 月不计）
        Assert.Equal(-1000m, withOpening[openingRevenue.Id]);
    }

    [Fact]
    public void NetByAccountUpTo_ZeroOpeningAndNoEntries_AccountAbsent()
    {
        // 没有期初也没有发生额的科目不进结果集，汇总方按「缺省为 0」取值；
        // 若把零值科目也塞进去，权益变动表会凭空多出一堆 0 行。
        var idle = Acc("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit);
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var vouchers = new[] { Posted(1, new DateOnly(2026, 5, 6), (bank, 800m, 0m), (revenue, 0m, 800m)) };

        var net = AccountNetCalculator.NetByAccountUpTo([idle, bank, revenue], vouchers, null);

        Assert.False(net.ContainsKey(idle.Id));
        Assert.Equal(2, net.Count);
    }

    [Fact]
    public void NetByAccountUpTo_ParentAccountWithChildren_OnlyLeafCarriesOpening()
    {
        // 期初只录在末级科目（上级由下级汇总），净额计算也只认末级科目的期初；
        // 明细科目 100201 的期初 + 自身分录一起计入它自己的 Id。
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var child = bank.AddChild("100201", "工商银行", BalanceDirection.Debit);
        child.SetOpeningBalance(2000m);
        var revenue = Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var vouchers = new[] { Posted(1, new DateOnly(2026, 6, 1), (child, 500m, 0m), (revenue, 0m, 500m)) };

        var net = AccountNetCalculator.NetByAccountUpTo([bank, child, revenue], vouchers, null);

        Assert.False(net.ContainsKey(bank.Id));
        Assert.Equal(2500m, net[child.Id]);
        Assert.Equal(-500m, net[revenue.Id]);
    }

    // —— 区间净发生额（不含期初）——

    [Fact]
    public void PeriodNetByAccount_RangeBoundariesInclusive_OpeningNotCounted()
    {
        // 区间 [start, end] 含起止日；本口径刻意不含科目期初（利润表各行发生额、费用明细表用）。
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit, 9000m);
        var admin = Acc("6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var vouchers = new[]
        {
            Posted(1, new DateOnly(2026, 6, 30), (admin, 100m, 0m), (bank, 0m, 100m)),  // 起始日：计入
            Posted(2, new DateOnly(2026, 7, 31), (admin, 200m, 0m), (bank, 0m, 200m)),  // 截止日：计入
            Posted(3, new DateOnly(2026, 8, 1), (admin, 500m, 0m), (bank, 0m, 500m)),   // 越界：不计
            Posted(4, new DateOnly(2026, 6, 29), (admin, 700m, 0m), (bank, 0m, 700m)),  // 越界：不计
        };

        var net = AccountNetCalculator.PeriodNetByAccount(vouchers, new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 31));

        Assert.Equal(300m, net[admin.Id]);   // 100 + 200
        Assert.Equal(-300m, net[bank.Id]);   // 不含期初 9000
    }

    [Fact]
    public void PeriodNetByAccount_NegativeNet_KeepsDebitMinusCreditSign()
    {
        // 净额可为负（贷方大于借方）：本口径不做取整/取绝对值，符号留给输出层与 SumByCategory 处理。
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var payable = Acc("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        var vouchers = new[]
        {
            Posted(1, new DateOnly(2026, 9, 1), (payable, 0m, 1200m), (bank, 1200m, 0m)),
            Posted(2, new DateOnly(2026, 9, 2), (payable, 500m, 0m), (bank, 0m, 500m)),
        };

        var net = AccountNetCalculator.PeriodNetByAccount(vouchers, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal(-700m, net[payable.Id]);
        Assert.Equal(700m, net[bank.Id]);
    }

    // —— 按类别汇总（输出层方向换算）——

    [Fact]
    public void SumByCategory_AssetsAndCost_DebitPositive()
    {
        // 资产/成本类以借方为正，直接取净额。
        var cash = Acc("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, 3000m);
        var product = Acc("4101", "生产成本", AccountCategory.Cost, BalanceDirection.Debit, 800m);

        var net = AccountNetCalculator.NetByAccountUpTo([cash, product], [], null);

        Assert.Equal(3000m, AccountNetCalculator.SumByCategory(net, [cash, product], AccountCategory.Asset));
        Assert.Equal(800m, AccountNetCalculator.SumByCategory(net, [cash, product], AccountCategory.Cost));
    }

    [Fact]
    public void SumByCategory_LiabilityEquityAndProfitLoss_CreditPositive()
    {
        // 负债/权益/损益类以贷方为正（与资产负债表口径一致）：净额取负后才是要展示的余额。
        var payable = Acc("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit, 2000m);
        var capital = Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, 8000m);
        var revenue = Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var vouchers = new[] { Posted(1, new DateOnly(2026, 4, 1), (bank, 600m, 0m), (revenue, 0m, 600m)) };

        var net = AccountNetCalculator.NetByAccountUpTo([payable, capital, revenue, bank], vouchers, null);
        var all = new[] { payable, capital, revenue, bank };

        Assert.Equal(2000m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.Liability));
        Assert.Equal(8000m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.Equity));
        Assert.Equal(600m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.ProfitAndLoss));
        Assert.Equal(600m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.Asset));
    }

    [Fact]
    public void SumByCategory_OldStandardEquityAccount_CountedAsEquityNotCost()
    {
        // AGENTS.md 陷阱：旧准则 3xxx 权益、4xxx 成本；新准则 4xxx 权益、5xxx 成本。
        // 汇总按科目 Category 取符号，绝不能用编码首位猜类别——否则旧准则的实收资本会被当成成本（借方为正），
        // 权益变动表与财务概要上的所有者权益会变成负数。
        var capital = Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit, 5000m);
        var production = Acc("4101", "生产成本", AccountCategory.Cost, BalanceDirection.Debit, 900m);

        var net = AccountNetCalculator.NetByAccountUpTo([capital, production], [], null);
        var all = new[] { capital, production };

        Assert.Equal(5000m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.Equity));
        Assert.Equal(900m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.Cost));
        Assert.Equal(0m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.Liability));
    }

    [Fact]
    public void SumByCategory_AccountsOutsideCategoryOrWithoutBalance_Ignored()
    {
        // 不在给定类别中的科目忽略；类别命中但净额字典里没有（零发生额）的科目按 0 处理，不得抛异常。
        var cash = Acc("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, 1000m);
        var payable = Acc("2202", "应付账款", AccountCategory.Liability, BalanceDirection.Credit, 400m);
        var untouched = Acc("1122", "应收账款", AccountCategory.Asset, BalanceDirection.Debit);

        var net = AccountNetCalculator.NetByAccountUpTo([cash, payable, untouched], [], null);
        var all = new[] { cash, payable, untouched };

        Assert.Equal(1000m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.Asset));
        Assert.Equal(400m, AccountNetCalculator.SumByCategory(net, all, AccountCategory.Liability));
        Assert.Equal(0m, AccountNetCalculator.SumByCategory(net, [payable], AccountCategory.Asset));
    }

    [Fact]
    public void SumByCategory_InvestmentAndLoanVouchers_AssetsEqualLiabilityPlusEquity()
    {
        // 会计恒等式（AGENTS.md 记账基线）：资产 = 负债 + 所有者权益。
        // 净额口径与类别符号换算若有一侧写反，这个等式必然破裂——用它守住整条取数链路。
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var equipment = Acc("1601", "固定资产", AccountCategory.Asset, BalanceDirection.Debit);
        var capital = Acc("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);
        var loan = Acc("2501", "长期借款", AccountCategory.Liability, BalanceDirection.Credit);

        var vouchers = new[]
        {
            Posted(1, new DateOnly(2026, 1, 4), (bank, 8000m, 0m), (capital, 0m, 8000m)),   // 收到股东投资
            Posted(2, new DateOnly(2026, 1, 6), (bank, 2000m, 0m), (loan, 0m, 2000m)),      // 取得长期借款
            Posted(3, new DateOnly(2026, 1, 9), (equipment, 3000m, 0m), (bank, 0m, 3000m)), // 购置设备
        };

        var accounts = new[] { bank, equipment, capital, loan };
        var net = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, new DateOnly(2026, 1, 31));

        Assert.Equal(7000m, net[bank.Id]);      // 8000 + 2000 − 3000
        Assert.Equal(3000m, net[equipment.Id]);
        Assert.Equal(-8000m, net[capital.Id]);
        Assert.Equal(-2000m, net[loan.Id]);

        var assets = AccountNetCalculator.SumByCategory(net, accounts, AccountCategory.Asset);
        var liabilities = AccountNetCalculator.SumByCategory(net, accounts, AccountCategory.Liability);
        var equity = AccountNetCalculator.SumByCategory(net, accounts, AccountCategory.Equity);
        Assert.Equal(10000m, assets);
        Assert.Equal(liabilities + equity, assets);
    }

    [Fact]
    public void SumByCategory_LossMakingPeriod_ProfitAndLossSumIsNegative()
    {
        // 损益类以贷方为正：费用大于收入（借方发生额更大）时汇总为负数，即本期亏损，符号不能取绝对值。
        var bank = Acc("1002", "银行存款", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = Acc("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var admin = Acc("6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);

        var vouchers = new[]
        {
            Posted(1, new DateOnly(2026, 7, 1), (bank, 1000m, 0m), (revenue, 0m, 1000m)),
            Posted(2, new DateOnly(2026, 7, 5), (admin, 1600m, 0m), (bank, 0m, 1600m)),
        };

        var accounts = new[] { bank, revenue, admin };
        var net = AccountNetCalculator.NetByAccountUpTo(accounts, vouchers, null);

        Assert.Equal(-600m, AccountNetCalculator.SumByCategory(net, accounts, AccountCategory.ProfitAndLoss));
    }
}
