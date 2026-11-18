using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using Xunit;

namespace YunCai.Tests.Accounting;

/// <summary>
/// 4xxx 编码在两套科目表里含义相反：2007 企业准则为权益（4001 实收资本、4103 本年利润），
/// 旧准则/小企业准则为成本（4001 生产成本、4101 制造费用）。仅凭首位推断会把权益计成存货，
/// 因此按名称中的权益关键字收口，未命中时仍按成本处理。
/// </summary>
public sealed class AccountCategoryInferenceTests
{
    [Theory]
    [InlineData("4001", "实收资本", AccountCategory.Equity)]
    [InlineData("4002", "资本公积", AccountCategory.Equity)]
    [InlineData("4101", "盈余公积", AccountCategory.Equity)]
    [InlineData("4103", "本年利润", AccountCategory.Equity)]
    [InlineData("4104", "利润分配", AccountCategory.Equity)]
    [InlineData("4201", "库存股", AccountCategory.Equity)]
    [InlineData("4001", "股本", AccountCategory.Equity)]
    [InlineData("410101", "实收资本-股东A", AccountCategory.Equity)]
    public void InferCategory_4xxxEquityNames_AreEquity(string code, string name, AccountCategory expected)
    {
        Assert.Equal(expected, AccountAutoCreateService.InferCategoryByCodePrefix(code, name));
    }

    [Theory]
    [InlineData("4001", "生产成本")]
    [InlineData("4101", "制造费用")]
    [InlineData("4301", "研发支出")]
    [InlineData("4401", "工程施工")]
    public void InferCategory_4xxxCostNames_StayCost(string code, string name)
    {
        Assert.Equal(AccountCategory.Cost, AccountAutoCreateService.InferCategoryByCodePrefix(code, name));
    }

    [Fact]
    public void InferCategory_4xxxWithoutName_FallsBackToCost()
    {
        Assert.Equal(AccountCategory.Cost, AccountAutoCreateService.InferCategoryByCodePrefix("4001"));
    }

    [Theory]
    [InlineData("1001", "库存现金", AccountCategory.Asset)]
    [InlineData("2202", "应付账款", AccountCategory.Liability)]
    [InlineData("3001", "实收资本", AccountCategory.Equity)]
    [InlineData("6301", "营业外收入", AccountCategory.ProfitAndLoss)]
    public void InferCategory_OtherPrefixes_Unchanged(string code, string name, AccountCategory expected)
    {
        Assert.Equal(expected, AccountAutoCreateService.InferCategoryByCodePrefix(code, name));
    }
}
