using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Reports.Application;
using Xunit;

namespace KuaiJi.Tests.Reports;

/// <summary>
/// 利润表（会企02表）行次归集 <see cref="IncomeLineMapper"/> 的测试。
/// 利润表、季报、部门/项目利润表、费用明细表共用这套归集（AGENTS.md「报表新口径」），
/// 关键口径是：未识别的损益科目按余额方向兜底并入营业收入/营业成本，保证净利润不漏项。
/// 项目实际使用旧准则编码体系（2001 版《企业会计制度》），新旧两套编码必须都能归集——
/// 这正是 AGENTS.md「领域须知」点名的兼容陷阱。
/// </summary>
public sealed class IncomeLineMapperTests
{
    [Theory]
    [InlineData("6001", 1)]      // 主营业务收入
    [InlineData("6051", 1)]      // 其他业务收入
    [InlineData("6401", 2)]      // 主营业务成本
    [InlineData("6402", 2)]      // 其他业务成本
    [InlineData("6403", 3)]      // 税金及附加
    [InlineData("6601", 4)]      // 销售费用
    [InlineData("6602", 5)]      // 管理费用
    [InlineData("6604", 6)]      // 研发费用
    [InlineData("6603", 7)]      // 财务费用
    [InlineData("6117", 8)]      // 其他收益
    [InlineData("6111", 9)]      // 投资收益
    [InlineData("6101", 11)]     // 公允价值变动收益
    [InlineData("6702", 12)]     // 信用减值损失
    [InlineData("6701", 13)]     // 资产减值损失
    [InlineData("6115", 14)]     // 资产处置收益
    [InlineData("6116", 14)]
    [InlineData("6301", 16)]     // 营业外收入
    [InlineData("6711", 17)]     // 营业外支出
    [InlineData("6801", 19)]     // 所得税费用
    public void MapLine_NewStandardCodes_LandOnTheirRow(string code, int expectedLine)
    {
        // 方向传借方也要按编码归集，编码识别优先于方向兜底
        Assert.Equal(expectedLine, IncomeLineMapper.MapLine(code, BalanceDirection.Debit));
    }

    [Theory]
    [InlineData("600101", 1)]   // 主营业务收入——华东区
    [InlineData("660201", 5)]   // 管理费用——工资
    [InlineData("6401001", 2)]  // 更深层级仍按前四位归集
    public void MapLine_DetailCodes_AggregateByFirstFourDigits(string code, int expectedLine)
    {
        Assert.Equal(expectedLine, IncomeLineMapper.MapLine(code, BalanceDirection.Debit));
    }

    [Theory]
    [InlineData("5101", BalanceDirection.Credit, 1)]  // 旧准则：产品销售收入（贷方）
    [InlineData("5301", BalanceDirection.Debit, 2)]   // 旧准则：营业外支出/费用类（借方）
    [InlineData("9999", BalanceDirection.Credit, 1)]  // 完全陌生的贷方损益科目
    [InlineData("9999", BalanceDirection.Debit, 2)]   // 完全陌生的借方损益科目
    public void MapLine_UnknownCode_FallsBackToBalanceDirection(string code, BalanceDirection direction, int expectedLine)
    {
        // 兜底方向是「净利润完整性」的唯一保证：识别不了的损益科目不能凭空消失
        Assert.Equal(expectedLine, IncomeLineMapper.MapLine(code, direction));
    }

    [Theory]
    [InlineData(1, true)]   // 营业收入
    [InlineData(8, true)]   // 其他收益
    [InlineData(9, true)]   // 投资收益
    [InlineData(11, true)]  // 公允价值变动收益
    [InlineData(14, true)]  // 资产处置收益
    [InlineData(16, true)]  // 营业外收入
    [InlineData(2, false)]  // 营业成本
    [InlineData(3, false)]  // 税金及附加
    [InlineData(17, false)] // 营业外支出
    [InlineData(19, false)] // 所得税费用
    public void IsRevenueLine_OnlyIncomeRowsAreCreditPositive(int line, bool expected)
    {
        Assert.Equal(expected, IncomeLineMapper.IsRevenueLine(line));
    }

    [Theory]
    [InlineData(1, "营业收入")]
    [InlineData(2, "营业成本")]
    [InlineData(5, "管理费用")]
    [InlineData(19, "所得税费用")]
    public void LineName_KnownRows_UseStandardNames(int line, string expected)
    {
        Assert.Equal(expected, IncomeLineMapper.LineName(line));
    }

    [Theory]
    [InlineData(10)]       // 经营活动现金流入小计——不属于利润表行
    [InlineData(15)]       // 空行次
    [InlineData(999)]
    public void LineName_UnknownRow_FallsBackToOtherItem(int line)
    {
        // 部门/项目利润表按项目名输出，兜底名不能是空串
        Assert.Equal("其他项目", IncomeLineMapper.LineName(line));
    }

    [Theory]
    [InlineData("6001", "6001")]
    [InlineData("600101", "6001")]
    [InlineData("6", "6")]           // 短于四位原样返回
    [InlineData("1001000001", "1001")]
    public void Prefix_TakesFirstFourDigits(string code, string expected)
    {
        Assert.Equal(expected, IncomeLineMapper.Prefix(code));
    }
}
