using KuaiJi.Modules.Tax.Application;
using KuaiJi.Modules.Tax.Infrastructure;
using Xunit;

namespace KuaiJi.Tests.Tax;

/// <summary>
/// 个税「查询统计 → 缴税记录查询」的解析测试（<see cref="IitPaymentPayloadParser"/>）。
///
/// 报文同样被瑞数改写成密文，行来自 el-table 抓下来的「列名: 文本」；锚是「缴税日期 + 票号/金额」，
/// 申报记录表没有缴税日期，必须挡在门外。这里锁死：按缴税日期过滤、按税款所属期过滤（含中文年月）、
/// 日期解析不出的行保留，以及解析不出时把候选列名回传诊断。
/// </summary>
public sealed class IitPaymentPayloadParserTests
{
    private const string Url = "https://etax.chinatax.gov.cn/withholding/index.html#/payment#table";

    private const string TablePayload = """
        [
          {
            "税款所属期": "2026年09月",
            "报表类型": "综合所得预扣预缴表",
            "电子税票号": "333026260900001234",
            "缴税金额": "1,234.50",
            "缴税日期": "2026-10-15",
            "缴税种类": "正税",
            "缴税方式": "三方协议"
          },
          {
            "税款所属期": "2025年10月",
            "报表类型": "综合所得预扣预缴表",
            "电子税票号": "333026251000000001",
            "缴税金额": "0.00",
            "缴税日期": "2026-01-12",
            "缴税种类": "正税",
            "缴税方式": "三方协议"
          },
          {
            "税款所属期": "2026年10月",
            "报表类型": "综合所得预扣预缴表",
            "申报人次": "1",
            "当期收入": "0.00",
            "应补/退税额": "0.00",
            "申报时间": "2026-11-02"
          }
        ]
        """;

    [Fact]
    public void Parse_ReadsRenderedTableKeepsAnchoredRowsAndFiltersByPaidDate()
    {
        var rows = IitPaymentPayloadParser.Parse(
            Payload(TablePayload), byPeriod: false, from: "2026-10-01", to: "2026-10-31", out var diagnostics);

        var row = Assert.Single(rows);
        Assert.Equal("2026年09月", row.Period);
        Assert.Equal("综合所得预扣预缴表", row.FormName);
        Assert.Equal("333026260900001234", row.ETicketNo);
        Assert.Equal("1,234.50", row.Amount);
        Assert.Equal("2026-10-15", row.PaidOn);
        Assert.Equal("正税", row.TaxKind);
        Assert.Equal("三方协议", row.Method);
        Assert.Empty(diagnostics);

        // 另一条缴税记录（2026-01-12）不在区间内，申报记录行没有缴税日期，两者都不该出现。
    }

    [Fact]
    public void Parse_FiltersByPeriodIncludingChineseMonthText()
    {
        var rows = IitPaymentPayloadParser.Parse(
            Payload(TablePayload), byPeriod: true, from: "2025-10", to: "2025-12", out _);

        var row = Assert.Single(rows);
        Assert.Equal("2025年10月", row.Period);
        Assert.Equal("333026251000000001", row.ETicketNo);
    }

    [Fact]
    public void Parse_KeepsRowsWhoseDateCannotBeParsed()
    {
        // 税局偶尔只回「2026-10」这种月份或空日期：定位不了区间时宁可多展示，不能当成没缴过。
        var body = """
            [
              { "税款所属期": "2026-10", "电子税票号": "A1", "缴税金额": "10.00", "缴税日期": "2026-10" }
            ]
            """;

        var row = Assert.Single(IitPaymentPayloadParser.Parse(
            Payload(body), byPeriod: false, from: "2026-01-01", to: "2026-01-31", out _));
        Assert.Equal("2026-10", row.PaidOn);
    }

    [Fact]
    public void Parse_DedupsAndSortsByPaidDateDescending()
    {
        var body = """
            [
              { "税款所属期": "2026年08月", "电子税票号": "A2", "缴税金额": "20.00", "缴税日期": "2026-09-10" },
              { "税款所属期": "2026年09月", "电子税票号": "A3", "缴税金额": "30.00", "缴税日期": "2026-10-10" },
              { "税款所属期": "2026年08月", "电子税票号": "A2", "缴税金额": "20.00", "缴税日期": "2026-09-10" }
            ]
            """;

        var rows = IitPaymentPayloadParser.Parse(
            Payload(body), byPeriod: false, from: "2026-01-01", to: "2026-12-31", out _);

        Assert.Equal(2, rows.Count);
        Assert.Equal("A3", rows[0].ETicketNo);
        Assert.Equal("A2", rows[1].ETicketNo);
    }

    [Fact]
    public void Parse_ReportsColumnsWhenNothingIsAnchored()
    {
        var body = """
            [
              { "姓名": "张三", "应补/退税额": "0.00" },
              { "序号": "1", "合计": "3" }
            ]
            """;

        Assert.Empty(IitPaymentPayloadParser.Parse(Payload(body), byPeriod: false, null, null, out var diagnostics));
        var note = Assert.Single(diagnostics);
        Assert.Contains("index.html", note);
        Assert.Contains("姓名|应补/退税额", note);
    }

    private static IReadOnlyList<CapturedJsonPayload> Payload(string body)
        => [new CapturedJsonPayload(Url, body)];
}
