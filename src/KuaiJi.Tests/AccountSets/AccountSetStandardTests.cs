using KuaiJi.Modules.AccountSets.Domain;
using Xunit;

namespace KuaiJi.Tests.AccountSets;

/// <summary>
/// 会计准则只支持企业准则与小企业准则；已建账套只能从小企业准则单向升级为企业准则。
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

    [Fact]
    public static void Create_rejects_unsupported_standard()
    {
        var ex = Assert.Throws<ArgumentException>(() => Create(AccountingStandard.NonProfit));

        Assert.Contains("企业会计准则与小企业会计准则", ex.Message);
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
}
