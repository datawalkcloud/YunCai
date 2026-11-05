using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Tax.Domain;
using Xunit;

namespace KuaiJi.Tests.Tax;

/// <summary>
/// 申报种类推导测试。税局认定报文不带这一列，页面要显示的是「这行税费并进哪张申报表」，
/// 所以附加税费必须跟增值税同一张表、企业所得税与财务报表要按期限分季/年两张表。
/// </summary>
public sealed class TaxDeclareKindMapperTests
{
    private static readonly TaxKindProfile SmallScale = new(TaxpayerType.SmallScale, AccountingStandard.SmallBusiness);
    private static readonly TaxKindProfile General = new(TaxpayerType.General, AccountingStandard.Enterprise);

    [Theory]
    [InlineData("增值税", "季", "申报", "")]
    [InlineData("城市维护建设税", "季", "申报", "")]
    [InlineData("教育费附加", "季", "申报", "")]
    [InlineData("地方教育附加", "季", "申报", "")]
    public void VatAndSurchargesShareOneForm(string project, string period, string option, string _)
    {
        const string expected = "增值税及附加税费申报（小规模纳税人）";

        Assert.Equal(expected, TaxDeclareKindMapper.Map(project, period, option, SmallScale));
        Assert.Equal("增值税及附加税费申报（一般纳税人）", TaxDeclareKindMapper.Map(project, period, option, General));
    }

    [Fact]
    public void IncomeTaxSplitsAnnualReturnFromMonthlyOrQuarterly()
    {
        Assert.Equal("居民企业（查账征收）企业所得税月（季）度申报表",
            TaxDeclareKindMapper.Map("企业所得税", "季", "申报", SmallScale));
        Assert.Equal("居民企业（查账征收）企业所得税年度申报表",
            TaxDeclareKindMapper.Map("企业所得税", "年", "申报", SmallScale));
    }

    [Fact]
    public void PersonalIncomeTaxFollowsDeclareOption()
    {
        Assert.Equal("个人所得税（综合所得）", TaxDeclareKindMapper.Map("个人所得税", "月", "申报", SmallScale));
        Assert.Equal("个人所得税（代扣代缴）", TaxDeclareKindMapper.Map("个人所得税", "月", "代扣代缴", SmallScale));
    }

    [Fact]
    public void StampTaxKeepsItsPeriodInTheFormName()
    {
        Assert.Equal("印花税纳税申报表（季）", TaxDeclareKindMapper.Map("印花税", "季", "申报", SmallScale));
        Assert.Equal("印花税纳税申报表（年）", TaxDeclareKindMapper.Map("印花税", "年", "申报", SmallScale));
        Assert.Equal("印花税纳税申报表", TaxDeclareKindMapper.Map("印花税", "", "申报", SmallScale));
    }

    [Theory]
    [InlineData(AccountingStandard.SmallBusiness, "财务报表（小企业会计准则）_季报")]
    [InlineData(AccountingStandard.Enterprise, "财务报表（企业会计准则）_季报")]
    [InlineData(AccountingStandard.NonProfit, "财务报表（民间非营利组织会计制度）_季报")]
    public void FinancialReportUsesTheFiledAccountingStandard(AccountingStandard standard, string expected)
    {
        Assert.Equal(expected, TaxDeclareKindMapper.Map("财务报表", "季", "申报", new TaxKindProfile(TaxpayerType.SmallScale, standard)));
        Assert.Equal(expected.Replace("季报", "年报"),
            TaxDeclareKindMapper.Map("财务报表", "年", "申报", new TaxKindProfile(TaxpayerType.SmallScale, standard)));
    }

    [Fact]
    public void KnownSubsidiesAndUnknownProjectsFallBackToTheirOwnForm()
    {
        Assert.Equal("残疾人就业保障金申报表", TaxDeclareKindMapper.Map("残疾人就业保障金", "年", "申报", SmallScale));
        Assert.Equal("消费税及附加税费申报表", TaxDeclareKindMapper.Map("消费税", "月", "申报", General));
        Assert.Equal("房产税纳税申报表", TaxDeclareKindMapper.Map("房产税", "年", "申报", SmallScale));
        Assert.Equal("水利建设基金申报表", TaxDeclareKindMapper.Map("水利建设基金", "季", "申报", SmallScale));
    }

    [Fact]
    public void BlankProjectYieldsBlankForm()
    {
        Assert.Equal("", TaxDeclareKindMapper.Map("", "季", "申报", SmallScale));
    }
}
