using YunCai.Modules.Tax.Application;
using YunCai.Modules.Tax.Infrastructure;
using Xunit;

namespace YunCai.Tests.Tax;

/// <summary>
/// 个税「人员信息采集」响应的解析测试（<see cref="IitPersonnelPayloadParser"/>）。
///
/// 样例按解析器认得的字段名风格拼出来，锁的是与脱敏号码相关的三条规则：
/// 点过明文开关后同一份名册会按脱敏和明文各回一遍，只留明文；同名两人都只有脱敏号时不能并成一个人；
/// 性别不能拿脱敏号的第 17 位猜。
/// </summary>
public sealed class IitPersonnelPayloadParserTests
{
    private const string Url = "https://etax.chinatax.gov.cn/iit/person/queryRyxx";

    [Fact]
    public void Parse_SameNameMaskedAndPlainId_KeepsOnlyPlainRow()
    {
        var body = """
            {
              "data": {
                "list": [
                  { "xm": "张三", "zjhm": "3301************34" },
                  { "xm": "张三", "zjhm": "330102199001011234" }
                ]
              }
            }
            """;

        var rows = IitPersonnelPayloadParser.Parse(Payload(body), out _);

        // 两条都留下，同步时脱敏那条会先按姓名把档案认走，明文那条再落成新档案 → 一个人变两条。
        var row = Assert.Single(rows);
        Assert.Equal("330102199001011234", row.IdNo);
    }

    [Fact]
    public void Parse_TwoSameNameMaskedIds_KeepsBothRows()
    {
        var body = """
            {
              "data": {
                "list": [
                  { "xm": "李四", "zjhm": "3301************34" },
                  { "xm": "李四", "zjhm": "3301************78" }
                ]
              }
            }
            """;

        var rows = IitPersonnelPayloadParser.Parse(Payload(body), out _);

        // 都没有明文号码时分不出谁是谁，两行都要交给同步按姓名逐个认领。
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("", row.GenderText));
    }

    [Fact]
    public void Parse_GenderInferredOnlyFromRealId()
    {
        var body = """
            {
              "data": {
                "list": [
                  { "xm": "王五", "zjhm": "330102199001011234" },
                  { "xm": "赵六", "zjhm": "330102199001011244" },
                  { "xm": "钱七", "zjhm": "3301************34" }
                ]
              }
            }
            """;

        var rows = IitPersonnelPayloadParser.Parse(Payload(body), out _).ToDictionary(row => row.Name);

        Assert.Equal("男", rows["王五"].GenderText);
        Assert.Equal("女", rows["赵六"].GenderText);
        // 脱敏号也是 18 位，第 17 位照样能取到数字，照推会把整个单位的人按掩码尾巴判成男。
        Assert.Equal("", rows["钱七"].GenderText);
    }

    private static IReadOnlyList<Modules.Tax.Application.CapturedJsonPayload> Payload(string body)
        => [new Modules.Tax.Application.CapturedJsonPayload(Url, body)];
}
