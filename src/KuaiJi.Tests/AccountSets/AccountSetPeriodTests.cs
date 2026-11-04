using KuaiJi.Modules.AccountSets.Domain;
using Xunit;

namespace KuaiJi.Tests.AccountSets;

/// <summary>
/// 反结账的期间推进规则：可以从任意已结期间起整体回退，但不能退未结账的期间。
/// </summary>
public static class AccountSetPeriodTests
{
    private static AccountSet Closed(DateTime start, params (int Year, int Month)[] periods)
    {
        var set = AccountSet.Create(
            "测试账套",
            "测试账套有限公司",
            AccountingStandard.Enterprise,
            TaxpayerType.General,
            "人民币",
            start.Year,
            start.Month);
        foreach (var (year, month) in periods)
        {
            set.MarkCarried(year, month);
            set.ClosePeriod(year, month);
        }

        return set;
    }

    [Fact]
    public static void ReopenFrom_earlier_closed_period_rolls_back_the_whole_tail()
    {
        var set = Closed(new DateTime(2026, 1, 1), (2026, 1), (2026, 2), (2026, 3));

        set.ReopenFrom(2026, 2);

        Assert.Equal(202601, set.ClosedPeriod);
        Assert.Equal(202603, set.CarriedPeriod);
    }

    [Fact]
    public static void ReopenFrom_start_period_clears_closed_period()
    {
        var set = Closed(new DateTime(2026, 1, 1), (2026, 1), (2026, 2));

        set.ReopenFrom(2026, 1);

        Assert.Equal(0, set.ClosedPeriod);
        Assert.NotNull(set.CarriedPeriodText());
    }

    [Fact]
    public static void ReopenFrom_period_not_yet_closed_throws()
    {
        var set = Closed(new DateTime(2026, 1, 1), (2026, 1), (2026, 2));

        var ex = Assert.Throws<InvalidOperationException>(() => set.ReopenFrom(2026, 3));

        Assert.Contains("尚未结账", ex.Message);
        Assert.Equal(202602, set.ClosedPeriod);
    }

    [Fact]
    public static void ReopenPeriod_still_rolls_back_one_month()
    {
        var set = Closed(new DateTime(2026, 1, 1), (2026, 1), (2026, 2));

        set.ReopenPeriod();

        Assert.Equal(202601, set.ClosedPeriod);
    }
}
