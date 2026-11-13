using YunCai.Modules.Tax.Application;
using YunCai.Modules.Tax.Infrastructure;
using Xunit;

namespace YunCai.Tests.Tax;

/// <summary>
/// 税局「税费种认定」报文的解析测试。除认定行本身，还要取出页头的「征收类型」：
/// 它按户认定、不在表格列里，可能出现在认定数组之外的对象上，也可能只回一个代码。
/// </summary>
public sealed class TaxKindPayloadParserTests
{
    private static TaxKindParseResult Parse(string body)
        => TaxKindPayloadParser.Parse([new CapturedJsonPayload("https://etax.zhejiang.chinatax.gov.cn/sfsz", body)]);

    [Fact]
    public void Parse_TakesLevyTypeFromHeaderObjectAlongWithRows()
    {
        const string body = """
            {
              "Data": {
                "zslxmc": "服务型",
                "List": [
                  {
                    "zsxm": "增值税",
                    "sbzl": "增值税及附加税费申报（小规模纳税人）",
                    "sbqx": "按季",
                    "yxqq": "2026-01-01 00:00:00",
                    "yxqz": "9999-12-31 00:00:00"
                  }
                ]
              }
            }
            """;

        var result = Parse(body);

        Assert.Equal("服务型", result.LevyType);
        var row = Assert.Single(result.Rows);
        Assert.Equal("增值税", row.Project);
        Assert.Equal("季", row.Period);
        Assert.Equal("2026-01-01", row.ValidFrom);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parse_AcceptsAnyTypeNameWhenFieldNamedAsLevyType()
    {
        const string body = """
            {
              "head": { "征收类型": "其他型" },
              "List": [
                { "zsxm": "城市维护建设税", "sbqx": "按月" }
              ]
            }
            """;

        Assert.Equal("其他型", Parse(body).LevyType);
    }

    [Fact]
    public void Parse_IgnoresOtherTypeNameFieldsWithoutLevyTypeField()
    {
        // 申报小类里的「核定型」不是征收类型，不能因为带个「型」字就当成页头标签。
        const string body = """
            {
              "other": { "sbxlx": "核定型" },
              "List": [
                { "zsxm": "印花税", "sbqx": "按次" }
              ]
            }
            """;

        var result = Parse(body);

        Assert.Null(result.LevyType);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void Parse_KeepsLevyTypeEmptyWhenBureauSentCodeOnly()
    {
        const string body = """
            {
              "head": { "zslx": "1" },
              "List": [
                { "zsxm": "增值税", "sbqx": "按季" }
              ]
            }
            """;

        var result = Parse(body);

        Assert.Null(result.LevyType);
        Assert.Single(result.Rows);
    }

    [Fact]
    public void Parse_TreatsDictionaryArrayAsNoRow()
    {
        const string body = """
            {
              "dict": [ { "zsmc": "增值税" }, { "zsmc": "印花税" } ]
            }
            """;

        var result = Parse(body);

        Assert.Empty(result.Rows);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Parse_RealZhejiangRow_PrefersNamesAndNonZeroRate()
    {
        // 浙江税局 initView 的真实一行：代码列排在名称列前面，主税种「税率」(Zsl) 是 0，
        // 真实比例挂在「税率或单位税额」(Slhdwse)，申报期限代码 74 不能被当成纳税期限。
        const string body = """
            {
              "Response": {
                "Data": {
                  "ReturnCode": "0",
                  "SfzrdxxList": [
                    {
                      "ZsxmDm": "30218",
                      "Zsxmmc": "残疾人就业保障金",
                      "ZspmDm": "302180100",
                      "Zspmmc": "残疾人就业保障金",
                      "Rdyxqq": "2025-10-01",
                      "Rdyxqz": "9999-12-31",
                      "SbqxDm": "74",
                      "Sbqxmc": "次年9月30号",
                      "NsqxDm": "10",
                      "Nsqxmc": "年",
                      "Slhdwse": 0.015,
                      "Zsl": 0.0
                    }
                  ]
                }
              }
            }
            """;

        var result = Parse(body);

        var row = Assert.Single(result.Rows);
        Assert.Equal("残疾人就业保障金", row.Project);
        Assert.Equal("年", row.Period);
        Assert.Equal("2025-10-01", row.ValidFrom);
        Assert.NotNull(row.Items);
        var item = Assert.Single(row.Items);
        Assert.Equal("0.015", item.Rate);
    }
}
