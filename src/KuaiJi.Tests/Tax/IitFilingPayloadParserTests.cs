using KuaiJi.Modules.Tax.Application;
using KuaiJi.Modules.Tax.Infrastructure;
using Xunit;

namespace KuaiJi.Tests.Tax;

/// <summary>
/// 个税「查询统计 → 单位申报记录查询」的解析测试（<see cref="IitFilingPayloadParser"/>）。
///
/// 这一页的报文被瑞数改写成密文，行是从 el-table 抓下来的「列名: 文本」，且它是树形表：
/// 父行第一列是「2025年10月」这样的所属期，子行第一列是申报表名，取数脚本压平后才交给解析器。
/// 这里锁死三种写法：压平后的标准行、非树形省份直接按表头命名的行、以及没有所属期的噪声行。
/// </summary>
public sealed class IitFilingPayloadParserTests
{
    private const string Url = "https://etax.chinatax.gov.cn/withholding/index.html#/statistics_company#table";

    [Fact]
    public void Parse_FlattenedTreeRowKeepsPeriodAndForm()
    {
        // 2026-11-02 实测杭州税局这一页压平后的一行（父行 2025年10月 只在没有子行时才保留）。
        var body = """
            [
              {
                "税款所属期": "2025年10月",
                "申报表": "综合所得预扣预缴表",
                "申报人次": "1",
                "当期收入": "0.00",
                "应补/退税额": "0.00",
                "申报类型": "正常申报",
                "申报渠道": "客户端",
                "申报时间": "2026-01-21"
              }
            ]
            """;

        var row = Assert.Single(IitFilingPayloadParser.Parse(Payload(body), out var diagnostics));
        Assert.Equal("2025年10月", row.Period);
        Assert.Equal("综合所得预扣预缴表", row.FormName);
        Assert.Equal("1", row.Headcount);
        Assert.Equal("0.00", row.Income);
        Assert.Equal("0.00", row.TaxDue);
        Assert.Equal("正常申报", row.DeclareType);
        Assert.Equal("客户端", row.Channel);
        Assert.Equal("2026-01-21", row.DeclareTime);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_NonTreeTableReadsLiteralColumnNames()
    {
        // 别的省份可能不是树形表，所属期与税额直接落在税局的列名上。
        var body = """
            [
              { "税款所属月份": "2026年01月", "申报人次": "3", "当期收入": "12,000.00", "应补/退税额": "360.00" }
            ]
            """;

        var row = Assert.Single(IitFilingPayloadParser.Parse(Payload(body), out _));
        Assert.Equal("2026年01月", row.Period);
        Assert.Equal("360.00", row.TaxDue);
    }

    [Fact]
    public void Parse_IgnoresRowsWithoutPeriodAndReportsTheirColumns()
    {
        var body = """
            [
              { "姓名": "张三", "应补/退税额": "0.00" },
              { "序号": "1", "合计": "3" }
            ]
            """;

        Assert.Empty(IitFilingPayloadParser.Parse(Payload(body), out var diagnostics));
        var note = Assert.Single(diagnostics);
        Assert.Contains("index.html", note);
        Assert.Contains("姓名|应补/退税额", note);
    }

    private static IReadOnlyList<CapturedJsonPayload> Payload(string body)
        => [new CapturedJsonPayload(Url, body)];
}
