using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Reports.Domain;
using KuaiJi.Modules.Vouchers.Domain;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 报表期间解析 <see cref="ReportPeriodResolver"/> 的测试：资产负债表/利润表/现金流量表在用户不填期间时
/// 必须落到同一个「最近有凭证的月份」，否则三张表的表头期间会各说各话；
/// 无凭证时回退账套启用期间；显式期间要按中文文案拒绝非法年月（会计期间标识统一 yyyyMM）。
/// </summary>
public sealed class ReportPeriodResolverTests
{
    private static readonly AccountSet Set = AccountSet.Create(
        "期间解析账套", "期间解析账套", AccountingStandard.Enterprise, TaxpayerType.General, "人民币", 2026, 3);

    private static readonly Account Cash = Account.CreateRoot(
        Set.Id, "1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);

    private static readonly Account Revenue = Account.CreateRoot(
        Set.Id, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

    /// <summary>造一张已过账凭证，只为拿日期：解析器只看 Date 与条数。</summary>
    private static Voucher VoucherOn(int year, int month, int day)
    {
        var voucher = Voucher.Create(Set.Id, "记", 1, new DateOnly(year, month, day), "测试制单人");
        voucher.AddEntry("收款", Cash.Id, 10m, 0m);
        voucher.AddEntry("收款", Revenue.Id, 0m, 10m);
        voucher.Review("测试审核人");
        voucher.Post();
        return voucher;
    }

    [Fact]
    public void Resolve_ExplicitYearAndMonth_IsUsedAsGiven()
    {
        var vouchers = new[] { VoucherOn(2026, 5, 10) };

        // 用户点了 2026-02 就查 2026-02，不能被凭证「纠正」成 5 月
        Assert.Equal((2026, 2), ReportPeriodResolver.Resolve(Set, vouchers, 2026, 2));
    }

    [Fact]
    public void Resolve_MonthOutOfRange_ThrowsChineseHint()
    {
        var tooHigh = Assert.Throws<ReportException>(() => ReportPeriodResolver.Resolve(Set, [], 2026, 13));
        var zero = Assert.Throws<ReportException>(() => ReportPeriodResolver.Resolve(Set, [], 2026, 0));

        Assert.Equal("会计期间的月份必须在 1 到 12 之间。", tooHigh.Message);
        Assert.Equal("会计期间的月份必须在 1 到 12 之间。", zero.Message);
    }

    [Fact]
    public void Resolve_YearOutOfRange_ThrowsChineseHint()
    {
        var error = Assert.Throws<ReportException>(() => ReportPeriodResolver.Resolve(Set, [], 1899, 6));

        Assert.Equal("会计期间的年份不合法。", error.Message);
    }

    [Fact]
    public void Resolve_OnlyYearGiven_FallsBackToLatestVoucherMonth()
    {
        var vouchers = new[] { VoucherOn(2026, 5, 10), VoucherOn(2026, 7, 1) };

        // 只给年不给月时按「最近有凭证的月份」，年参数不参与——三张表因此口径一致
        Assert.Equal((2026, 7), ReportPeriodResolver.Resolve(Set, vouchers, 2027, null));
    }

    [Fact]
    public void Resolve_NoVouchers_FallsBackToEnabledPeriod()
    {
        Assert.Equal((2026, 3), ReportPeriodResolver.Resolve(Set, [], null, null));
    }

    [Fact]
    public void Resolve_LatestMonthIsPickedRegardlessOfVoucherOrder()
    {
        var vouchers = new[] { VoucherOn(2026, 12, 30), VoucherOn(2026, 4, 1), VoucherOn(2026, 1, 31) };

        Assert.Equal((2026, 12), ReportPeriodResolver.Resolve(Set, vouchers, null, null));
    }

    [Fact]
    public void Resolve_DecemberRollover_KeepsYearWithMonth()
    {
        var vouchers = new[] { VoucherOn(2025, 12, 31) };

        // 年月必须成对返回，跨年时不能把 2025-12 说成 2026-12
        Assert.Equal((2025, 12), ReportPeriodResolver.Resolve(Set, vouchers, null, null));
    }
}
