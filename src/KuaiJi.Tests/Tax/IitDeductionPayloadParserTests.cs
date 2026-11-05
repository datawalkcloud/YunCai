using KuaiJi.Modules.Tax.Application;
using KuaiJi.Modules.Tax.Infrastructure;
using Xunit;

namespace KuaiJi.Tests.Tax;

/// <summary>
/// 个税「专项附加扣除信息采集」响应的解析测试（<see cref="IitDeductionPayloadParser"/>）。
///
/// 样例不是抓包原文，而是按解析器认得的两种字段名风格（拼音缩写键、中文键）拼出来的信封，
/// 目的是锁死几条与真实接口约定相关的规则，而不是复现某个省的返回：
/// 编码要翻成名称、金额与日期的脏写法要归一、认不出「扣除项目」的行必须丢弃并回传字段名、
/// 同一人多段有效期不能重复出行。
/// </summary>
public sealed class IitDeductionPayloadParserTests
{
    private const string Url = "https://etax.chinatax.gov.cn/iit/deduction/queryList";

    [Fact]
    public void Parse_MapsPinyinKeysAndNormalisesAmountsAndDates()
    {
        var body = """
            {
              "code": "0",
              "data": {
                "list": [
                  {
                    "xm": "张三",
                    "zjlx": "1",
                    "zjhm": "330102199001011234",
                    "kclx": "5",
                    "gx": "本人",
                    "kcnd": "2026",
                    "yxqq": "2026-01-01 00:00:00",
                    "yxqz": "1900-01-01 00:00:00",
                    "jehz": "1,500.00",
                    "sbfs": "1",
                    "yzqk": "已验证"
                  }
                ]
              }
            }
            """;

        var rows = IitDeductionPayloadParser.Parse(Payload(body), out var diagnostics);

        var row = Assert.Single(rows);
        Assert.Equal("张三", row.Name);
        Assert.Equal("居民身份证", row.IdTypeText);
        Assert.Equal("住房租金", row.ProjectText);
        Assert.Equal("2026", row.YearText);
        Assert.Equal("2026-01-01", row.PeriodStart);
        // 1900-01-01 是个税侧「无该日期」的占位值，不能当成有效期止展示。
        Assert.Equal("", row.PeriodEnd);
        Assert.Equal(1500.00m, row.MonthlyAmount);
        Assert.Equal("通过扣缴义务人申报", row.DeclareWayText);
        Assert.Equal("已验证", row.VerifyStateText);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_RepeatOfTheSamePersonAndPeriod_YieldsOneRow()
    {
        var row = """
            { "姓名": "李四", "证件号码": "330102199001011234", "扣除项目": "赡养老人", "有效期起": "2026-01-01", "扣除金额": "3000" }
            """;
        var body = $$"""{ "result": { "rows": [ {{row}}, {{row}} ] } }""";

        var rows = IitDeductionPayloadParser.Parse(Payload(body), out _);

        Assert.Single(rows);
        Assert.Equal("赡养老人", rows[0].ProjectText);
    }

    [Fact]
    public void Parse_DropsRowsWithoutAnIdentifiableProjectAndReportsFieldNames()
    {
        // 只有姓名与证件号＝人员列表，不是扣除记录；码表外的纯数字项目编码同样认不出来。
        var body = """
            {
              "data": {
                "list": [
                  { "e_name": "王五", "e_id": "330102199001011234", "mobile_phone": "13800000000" },
                  { "xm": "赵六", "zjhm": "330102199001015678", "kclx": "99" }
                ]
              }
            }
            """;

        var rows = IitDeductionPayloadParser.Parse(Payload(body), out var diagnostics);

        Assert.Empty(rows);
        var note = Assert.Single(diagnostics);
        Assert.Contains("/iit/deduction/queryList", note);
        Assert.Contains("e_name|e_id|mobile_phone", note);
    }

    private static IReadOnlyList<CapturedJsonPayload> Payload(string body)
        => [new CapturedJsonPayload(Url, body)];
}
