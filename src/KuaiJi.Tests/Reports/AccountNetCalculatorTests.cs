using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Vouchers.Domain;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 报表共享取数口径 <see cref="AccountNetCalculator"/> 的测试：
/// 以「借 - 贷」净额逐科目累计，并把末级科目的启用期初按余额方向换算进同一视角（AGENTS.md「领域须知」）。
/// 所有者权益变动表、纳税统计表、费用明细表、财务概要都走这三个方法，口径一旦漂移会同时错四张表。
/// </summary>
public sealed class AccountNetCalculatorTests
{
    private static readonly AccountSet Set = AccountSet.Create(
        "取数口径账套", "取数口径账套", AccountingStandard.Enterprise, TaxpayerType.General, "人民币", 2026, 1);

    /// <summary>建一张已过账凭证（借贷必须平衡，否则领域层拒绝过账）。</summary>
    private static Voucher Posted(DateOnly date, params (string Summary, Guid AccountId, decimal Debit, decimal Credit)[] legs)
    {
        var voucher = Voucher.Create(Set.Id, "记", 1, date, "测试制单人");
        foreach (var (summary, accountId, debit, credit) in legs)
        {
            voucher.AddEntry(summary, accountId, debit, credit);
        }

        voucher.Review("测试审核人");
        voucher.Post();
        return voucher;
    }

    private static Account Root(string code, string name, AccountCategory category, BalanceDirection direction, decimal opening = 0m)
    {
        var account = Account.CreateRoot(Set.Id, code, name, category, direction);
        if (opening != 0m)
        {
            account.SetOpeningBalance(opening);
        }

        return account;
    }

    [Fact]
    public void NetByAccountUpTo_AddsEnabledOpeningBalanceConvertedByDirection()
    {
        var cash = Root("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        var payable = Root("2101", "应付账款", AccountCategory.Liability, BalanceDirection.Credit, opening: 800m);

        var net = AccountNetCalculator.NetByAccountUpTo([cash, payable], [], null);

        // 借方科目期初为正、贷方科目期初为负：全部换算成「借 - 贷」净额
        Assert.Equal(1_000m, net[cash.Id]);
        Assert.Equal(-800m, net[payable.Id]);
    }

    [Fact]
    public void NetByAccountUpTo_SumsEntryNetsAndKeepsSign()
    {
        var cash = Root("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 1_000m);
        var revenue = Root("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var vouchers = new[]
        {
            Posted(new DateOnly(2026, 1, 10), ("销售收现", cash.Id, 300m, 0m), ("销售收现", revenue.Id, 0m, 300m)),
            Posted(new DateOnly(2026, 1, 20), ("银行提现", cash.Id, 0m, 100m), ("银行提现", revenue.Id, 100m, 0m)),
        };

        var net = AccountNetCalculator.NetByAccountUpTo([cash, revenue], vouchers, null);

        // 期初 1_000 + 借 300 - 贷 100
        Assert.Equal(1_200m, net[cash.Id]);
        Assert.Equal(-200m, net[revenue.Id]);
    }

    [Fact]
    public void NetByAccountUpTo_VoucherOnUntilDate_IsCounted()
    {
        var cash = Root("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = Root("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var vouchers = new[]
        {
            Posted(new DateOnly(2026, 2, 28), ("截止日内", cash.Id, 100m, 0m), ("截止日内", revenue.Id, 0m, 100m)),
            Posted(new DateOnly(2026, 3, 1), ("截止日后", cash.Id, 900m, 0m), ("截止日后", revenue.Id, 0m, 900m)),
        };

        var net = AccountNetCalculator.NetByAccountUpTo([cash, revenue], vouchers, new DateOnly(2026, 2, 28));

        // 含当日（截止日期是月末当天，报表期末必须把它算进来）
        Assert.Equal(100m, net[cash.Id]);
    }

    [Fact]
    public void NetByAccountUpTo_UntilNull_CountsEveryVoucher()
    {
        var cash = Root("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = Root("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var vouchers = new[]
        {
            Posted(new DateOnly(2026, 1, 5), ("一月", cash.Id, 10m, 0m), ("一月", revenue.Id, 0m, 10m)),
            Posted(new DateOnly(2026, 12, 5), ("十二月", cash.Id, 20m, 0m), ("十二月", revenue.Id, 0m, 20m)),
        };

        Assert.Equal(30m, AccountNetCalculator.NetByAccountUpTo([cash, revenue], vouchers, null)[cash.Id]);
    }

    [Fact]
    public void PeriodNetByAccount_UsesInclusiveRangeAndIgnoresOpening()
    {
        var cash = Root("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 5_000m);
        var revenue = Root("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var vouchers = new[]
        {
            Posted(new DateOnly(2026, 2, 28), ("上月末", cash.Id, 10m, 0m), ("上月末", revenue.Id, 0m, 10m)),
            Posted(new DateOnly(2026, 3, 1), ("三月初", cash.Id, 70m, 0m), ("三月初", revenue.Id, 0m, 70m)),
            Posted(new DateOnly(2026, 3, 31), ("三月末", cash.Id, 0m, 30m), ("三月末", revenue.Id, 30m, 0m)),
            Posted(new DateOnly(2026, 4, 1), ("四月初", cash.Id, 999m, 0m), ("四月初", revenue.Id, 0m, 999m)),
        };

        var net = AccountNetCalculator.PeriodNetByAccount(vouchers, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        // 区间含两端；本期净发生额不带期初，也不该把上月/下月算进来
        Assert.Equal(40m, net[cash.Id]);
        Assert.Equal(-40m, net[revenue.Id]);
    }

    [Fact]
    public void PeriodNetByAccount_AccountWithOnlyOpening_HasNoPeriodKey()
    {
        var cash = Root("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit, opening: 5_000m);

        var net = AccountNetCalculator.PeriodNetByAccount([], new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.False(net.ContainsKey(cash.Id));
    }

    [Fact]
    public void SumByCategory_AssetAndCostReadDebitPositive()
    {
        var cash = Root("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        var production = Root("4101", "生产成本", AccountCategory.Cost, BalanceDirection.Debit);
        var net = new Dictionary<Guid, decimal> { [cash.Id] = 1_000m, [production.Id] = 300m };

        Assert.Equal(1_000m, AccountNetCalculator.SumByCategory(net, [cash], AccountCategory.Asset));
        Assert.Equal(300m, AccountNetCalculator.SumByCategory(net, [production], AccountCategory.Cost));
    }

    [Fact]
    public void SumByCategory_LiabilityEquityAndProfitReadCreditPositive()
    {
        var payable = Root("2101", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        var capital = Root("3001", "实收资本", AccountCategory.Equity, BalanceDirection.Credit);
        var revenue = Root("6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        // 净额是「借 - 贷」，这三类报表要看贷方为正，故整体取负
        Assert.Equal(200m, AccountNetCalculator.SumByCategory(
            new Dictionary<Guid, decimal> { [payable.Id] = -200m }, [payable], AccountCategory.Liability));
        Assert.Equal(5_000m, AccountNetCalculator.SumByCategory(
            new Dictionary<Guid, decimal> { [capital.Id] = -5_000m }, [capital], AccountCategory.Equity));
        Assert.Equal(30m, AccountNetCalculator.SumByCategory(
            new Dictionary<Guid, decimal> { [revenue.Id] = -30m }, [revenue], AccountCategory.ProfitAndLoss));
    }

    [Fact]
    public void SumByCategory_IgnoresOtherCategoriesAndMissingKeys()
    {
        var cash = Root("1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        var payable = Root("2101", "应付账款", AccountCategory.Liability, BalanceDirection.Credit);
        var net = new Dictionary<Guid, decimal> { [cash.Id] = 1_000m };

        // 只给现金的净额，按负债汇总必须是 0：不能因为遍历了全部科目就把别的类别混进来
        Assert.Equal(0m, AccountNetCalculator.SumByCategory(net, [cash, payable], AccountCategory.Liability));
        // 有科目但没有发生额：跳过而不是抛异常
        Assert.Equal(0m, AccountNetCalculator.SumByCategory(net, [payable], AccountCategory.Liability));
    }
}
