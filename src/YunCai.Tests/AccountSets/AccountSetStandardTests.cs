using YunCai.Modules.AccountSets.Domain;
using Xunit;

namespace YunCai.Tests.AccountSets;

/// <summary>
/// 可建账的会计准则：企业、小企业、民非、新会计准则（2006年颁）各有一套预置科目表；
/// 已建账套只能从小企业准则单向升级为企业准则，其余切换要重新建账。
/// </summary>
public static class AccountSetStandardTests
{
    private static AccountSet Create(AccountingStandard standard) =>
        AccountSet.Create(
            "测试账套",
            "测试账套有限公司",
            standard,
            TaxpayerType.General,
            "人民币",
            2026,
            1);

    [Theory]
    [InlineData(AccountingStandard.Enterprise)]
    [InlineData(AccountingStandard.SmallBusiness)]
    [InlineData(AccountingStandard.NonProfit)]
    [InlineData(AccountingStandard.NewEnterprise2006)]
    public static void Create_accepts_standard_with_preset_chart(AccountingStandard standard)
    {
        Assert.Equal(standard, Create(standard).Standard);
    }

    [Fact]
    public static void Create_rejects_unsupported_standard()
    {
        // 没有预置科目表的准则取值（脏数据或将来新增却忘了配表）不得建账。
        var ex = Assert.Throws<ArgumentException>(() => Create((AccountingStandard)99));

        Assert.Contains("会计准则取值无效", ex.Message);
    }

    [Fact]
    public static void Small_business_can_upgrade_to_enterprise()
    {
        var set = Create(AccountingStandard.SmallBusiness);

        set.UpdateProfile("测试账套有限公司", AccountingStandard.Enterprise, TaxpayerType.General, "人民币");

        Assert.Equal(AccountingStandard.Enterprise, set.Standard);
    }

    [Fact]
    public static void Enterprise_cannot_switch_back_to_small_business()
    {
        var set = Create(AccountingStandard.Enterprise);

        var ex = Assert.Throws<ArgumentException>(() =>
            set.UpdateProfile("测试账套有限公司", AccountingStandard.SmallBusiness, TaxpayerType.General, "人民币"));

        Assert.Contains("不能切换", ex.Message);
        Assert.Equal(AccountingStandard.Enterprise, set.Standard);
    }

    [Theory]
    [InlineData(AccountingStandard.NonProfit, AccountingStandard.Enterprise)]
    [InlineData(AccountingStandard.NewEnterprise2006, AccountingStandard.Enterprise)]
    [InlineData(AccountingStandard.Enterprise, AccountingStandard.NewEnterprise2006)]
    public static void Only_small_business_upgrade_is_allowed(AccountingStandard from, AccountingStandard to)
    {
        var set = Create(from);

        var ex = Assert.Throws<ArgumentException>(() =>
            set.UpdateProfile("测试账套有限公司", to, TaxpayerType.General, "人民币"));

        Assert.Contains("重新建账", ex.Message);
        Assert.Equal(from, set.Standard);
    }
}
