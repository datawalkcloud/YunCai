using YunCai.Modules.Accounting.Application;
using Xunit;

namespace YunCai.Tests.Accounting;

/// <summary>
/// 助记码生成规则：科目名称转拼音首字母小写，标点与空格丢弃；
/// 明细科目以「上级助记码_」为前缀；显式填写始终优先；长度不超过列宽 50。
/// </summary>
public sealed class AccountMnemonicTests
{
    [Theory]
    [InlineData("库存现金", "kcxj")]
    [InlineData("短期投资", "dqtz")]
    [InlineData("银行存款", "yhck")]
    [InlineData("长期借款", "cqjk")]
    [InlineData("其中：原材料", "qzycl")]
    [InlineData("营业外收入-政府补助", "yywsrzfbz")]
    [InlineData("A股", "ag")]
    [InlineData("Q345材料", "q345cl")]
    public void FromName_GeneratesLowercaseInitials(string name, string expected)
    {
        Assert.Equal(expected, AccountMnemonic.FromName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("——")]
    public void FromName_NoUsableCharacter_ReturnsNull(string name)
    {
        Assert.Null(AccountMnemonic.FromName(name));
    }

    [Fact]
    public void Resolve_Child_PrefixesParentMnemonic()
    {
        Assert.Equal("dqtz_gp", AccountMnemonic.Resolve(null, "股票", "dqtz"));
    }

    [Fact]
    public void Resolve_TopLevel_HasNoPrefix()
    {
        Assert.Equal("kcxj", AccountMnemonic.Resolve("", "库存现金", null));
    }

    [Fact]
    public void Resolve_ExplicitMnemonic_Wins()
    {
        Assert.Equal("KJXJ", AccountMnemonic.Resolve(" KJXJ ", "库存现金", "abcd"));
    }

    [Fact]
    public void Resolve_Overlong_TruncatesToColumnWidth()
    {
        var mnemonic = AccountMnemonic.Resolve(new string('a', 60), "库存现金", null);

        Assert.NotNull(mnemonic);
        Assert.Equal(AccountMnemonic.MaxLength, mnemonic!.Length);
    }
}
