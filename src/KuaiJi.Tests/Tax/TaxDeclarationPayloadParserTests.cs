using KuaiJi.Modules.Tax.Infrastructure;
using Xunit;

namespace KuaiJi.Tests.Tax;

/// <summary>
/// 税局「申报信息查询」（DescribeSbmxcx2）响应的解析测试。
/// 样例取自浙江电子税务局真实返回：接口按「所属期起」过滤，季报整段跨月，
/// 所以解析时要按所选月份做交集筛选，并保持与税局一致的字段口径。
/// 该接口挂在数字账户子系统下，冷启动时税局会另回一种 code/msg 信封，也要认得下来。
/// </summary>
public sealed class TaxDeclarationPayloadParserTests
{
    private const string Payload = """
        {
          "Response": {
            "RequestId": "61a0943bf9d714d8",
            "Error": null,
            "Data": {
              "PageNumber": 1,
              "PageSize": 200,
              "Total": 5,
              "List": [
                {
                  "yzpzzlMc": "《A200000中华人民共和国企业所得税月（季）度预缴纳税申报表（A类，2021年版）》",
                  "skssqq": "2026-07-01 00:00:00",
                  "skssqz": "2026-09-30 00:00:00",
                  "nssbrq": "2026-10-13 00:00:00",
                  "ybtse": 0.33,
                  "zfbz": "N",
                  "gzlx": "新产生申报表",
                  "sbuuid": "row-cit-q3"
                },
                {
                  "yzpzzlMc": "《印花税纳税申报（报告）表》",
                  "skssqq": "2026-07-01 00:00:00",
                  "skssqz": "2026-09-30 00:00:00",
                  "nssbrq": "2026-10-11 00:00:00",
                  "ybtse": 0,
                  "zfbz": "N",
                  "gzlx": "新产生申报表",
                  "sbuuid": "row-stamp-q3"
                },
                {
                  "yzpzzlMc": "《增值税及附加税费申报表（小规模纳税人适用）》",
                  "skssqq": "2026-07-01 00:00:00",
                  "skssqz": "2026-09-30 00:00:00",
                  "nssbrq": "2026-10-11 00:00:00",
                  "ybtse": 0,
                  "zfbz": "N",
                  "gzlx": "新产生申报表"
                },
                {
                  "yzpzzlMc": "《增值税及附加税费申报表（小规模纳税人适用）》",
                  "skssqq": "2026-01-01 00:00:00",
                  "skssqz": "2026-03-31 00:00:00",
                  "nssbrq": "2026-04-20 00:00:00",
                  "ybtse": 0,
                  "zfbz": "N",
                  "gzlx": "新产生申报表"
                },
                {
                  "yzpzzlMc": "《印花税纳税申报（报告）表》",
                  "skssqq": "2026-04-01 00:00:00",
                  "skssqz": "2026-06-30 00:00:00",
                  "nssbrq": "2026-07-15 00:00:00",
                  "ybtse": 0,
                  "zfbz": "Y",
                  "zfrq": "2026-07-18 00:00:00",
                  "gzlx": "新产生申报表"
                }
              ]
            }
          }
        }
        """;

    /// <summary>数字账户子系统没有会话时，税局网关回的信封（HTTP 200，但不是 Response 那一种）。</summary>
    private const string GatewayPayload = """
        {"msg":"会话失效，请重新登录！","code":"9999"}
        """;

    [Fact]
    public void Parse_KeepsOnlyDeclarationsCoveringSelectedMonth()
    {
        var result = TaxDeclarationPayloadParser.Parse(Payload, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal(3, result.Rows.Count);
        Assert.Null(result.Message);
        // 按申报日期倒序：企业所得税 10-13 在前，其余两张 10-11。
        var first = result.Rows[0];
        Assert.Equal("A200000中华人民共和国企业所得税月（季）度预缴纳税申报表（A类，2021年版）", first.DeclareKind);
        Assert.Equal("2026-07-01", first.PeriodStart);
        Assert.Equal("2026-09-30", first.PeriodEnd);
        Assert.Equal("2026-10-13", first.DeclareDate);
        Assert.Equal(0.33m, first.Payable);
        Assert.Equal("已申报", first.Status);
        Assert.Equal("", first.Feedback);
        Assert.All(result.Rows, row => Assert.DoesNotContain("《", row.DeclareKind));
    }

    [Fact]
    public void Parse_ReturnsEmptyMessageWhenSelectedMonthHasNoDeclaration()
    {
        var result = TaxDeclarationPayloadParser.Parse(Payload, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        Assert.Empty(result.Rows);
        Assert.Equal("所选税款所属期没有申报记录。", result.Message);
        // 取不到数据时必须带回税局原文摘要，否则前端只剩一句没有信息量的提示。
        Assert.NotEmpty(result.Diagnostics ?? []);
    }

    [Fact]
    public void Parse_MonthBeforeQuarterEndStillSeesQuarterlyDeclaration()
    {
        // 季报所属期 2026-07-01..09-30，按 7 月查看时也应出现在表里。
        var result = TaxDeclarationPayloadParser.Parse(Payload, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.Equal(3, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Equal("2026-07-01", row.PeriodStart));
    }

    [Fact]
    public void Parse_MarksVoidedDeclarationWithVoidDate()
    {
        var result = TaxDeclarationPayloadParser.Parse(Payload, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));

        var row = Assert.Single(result.Rows);
        Assert.Equal("已作废", row.Status);
        Assert.Equal("作废日期 2026-07-18", row.Feedback);
    }

    [Fact]
    public void Parse_MarksCorrectionDeclaration()
    {
        const string correction = """
            {"Response":{"Error":null,"Data":{"List":[
              {"yzpzzlMc":"《印花税纳税申报（报告）表》","skssqq":"2026-07-01 00:00:00",
               "skssqz":"2026-09-30 00:00:00","nssbrq":"2026-10-20 00:00:00","ybtse":12.5,
               "zfbz":"N","gzlx":"更正申报表"}
            ]}}}
            """;

        var result = TaxDeclarationPayloadParser.Parse(correction, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var row = Assert.Single(result.Rows);
        Assert.Equal("已更正", row.Status);
        Assert.Equal("更正申报表", row.Feedback);
        Assert.Equal(12.5m, row.Payable);
    }

    [Fact]
    public void Parse_ReadsSingleDeclarationListedAsObject()
    {
        // 只有一张申报表时税局把 List 序列化成对象而不是数组，不能当成「没有明细」。
        const string single = """
            {"Response":{"Error":null,"Data":{"Total":1,"List":
              {"yzpzzlMc":"《增值税及附加税费申报表（小规模纳税人适用）》","skssqq":"2025-10-01 00:00:00",
               "skssqz":"2025-12-31 00:00:00","nssbrq":"2026-01-12 00:00:00","ybtse":0,"zfbz":"N",
               "gzlx":"新产生申报表","sbuuid":"row-vat-q4"}
            }}}
            """;

        var result = TaxDeclarationPayloadParser.Parse(single, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31));

        var row = Assert.Single(result.Rows);
        Assert.Equal("增值税及附加税费申报表（小规模纳税人适用）", row.DeclareKind);
        Assert.Equal("2025-10-01", row.PeriodStart);
    }

    [Fact]
    public void Parse_WarnsWhenTaxBureauSentFewerRowsThanTotal()
    {
        // 接口只认小写 pageSize，参数写错时会被截成 10 条；这里要把缺口暴露出来而不是静默少给。
        const string truncated = """
            {"Response":{"Error":null,"Data":{"Total":26,"List":[
              {"yzpzzlMc":"《印花税纳税申报（报告）表》","skssqq":"2026-07-01 00:00:00",
               "skssqz":"2026-09-30 00:00:00","nssbrq":"2026-10-11 00:00:00","ybtse":0,"zfbz":"N"}
            ]}}}
            """;

        var result = TaxDeclarationPayloadParser.Parse(truncated, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Single(result.Rows);
        Assert.Contains(result.Diagnostics ?? [], note => note.Contains("26"));
    }

    [Fact]
    public void ParseReports_KeepsRowsFiledInsideRange()
    {
        // 报表查询页按申报日期区间取数：10 月申报的三张留下，4 月和 7 月申报的排除。
        var result = TaxDeclarationPayloadParser.ParseReports(
            [Payload], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        Assert.Equal(3, result.Rows.Count);
        Assert.Null(result.Message);
        Assert.All(result.Rows, row => Assert.StartsWith("2026-10", row.DeclareDate));
        Assert.Equal("2026-10-13", result.Rows[0].DeclareDate);
    }

    [Fact]
    public void ParseReports_IncludesPreviousYearPeriodFiledThisYear()
    {
        // 所属期是上年度、今年才申报的表（残保金、年报）按申报日期命中即可，所属期落在区间外不影响。
        const string previousYear = """
            {"Response":{"Error":null,"Data":{"List":[
              {"yzpzzlMc":"《残疾人就业保障金缴费申报表》","skssqq":"2025-01-01 00:00:00",
               "skssqz":"2025-12-31 00:00:00","nssbrq":"2026-09-14 00:00:00","ybtse":0,"zfbz":"N"}
            ]}}}
            """;

        var result = TaxDeclarationPayloadParser.ParseReports(
            [previousYear], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        var row = Assert.Single(result.Rows);
        Assert.Equal("残疾人就业保障金缴费申报表", row.DeclareKind);
        Assert.Equal("2025-01-01", row.PeriodStart);
        Assert.Equal("2026-09-14", row.DeclareDate);
    }

    [Fact]
    public void ParseReports_MergesYearWindowsWithoutDuplicates()
    {
        // 按自然年分段取数时，跨年度的所属期（如 2025-10-01..2025-12-31）可能在两段里都出现，去重后只留一张。
        const string sameRow = """
            {"Response":{"Error":null,"Data":{"List":[
              {"yzpzzlMc":"《增值税及附加税费申报表（小规模纳税人适用）》","skssqq":"2025-10-01 00:00:00",
               "skssqz":"2025-12-31 00:00:00","nssbrq":"2026-01-12 00:00:00","ybtse":0,"zfbz":"N","sbuuid":"row-vat-q4"}
            ]}}}
            """;
        const string secondYear = """
            {"Response":{"Error":null,"Data":{"List":[
              {"yzpzzlMc":"《印花税纳税申报（报告）表》","skssqq":"2026-01-01 00:00:00",
               "skssqz":"2026-03-31 00:00:00","nssbrq":"2026-04-15 00:00:00","ybtse":8.8,"zfbz":"N","sbuuid":"row-stamp-q1"}
            ]}}}
            """;

        var result = TaxDeclarationPayloadParser.ParseReports(
            [sameRow, sameRow, secondYear], new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 3));

        Assert.Equal(2, result.Rows.Count);
        // 申报日期倒序：4 月的印花税在前，1 月的增值税在后。
        Assert.Equal("2026-04-15", result.Rows[0].DeclareDate);
        Assert.Equal(8.8m, result.Rows[0].Payable);
        Assert.Equal("2026-01-12", result.Rows[1].DeclareDate);
    }

    [Fact]
    public void ParseReports_ReportsWhenRangeHasNoDeclaration()
    {
        var result = TaxDeclarationPayloadParser.ParseReports(
            [Payload], new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30));

        Assert.Empty(result.Rows);
        Assert.Equal("所选申报日期区间没有申报表。", result.Message);
        Assert.NotEmpty(result.Diagnostics ?? []);
    }

    [Fact]
    public void ParseReports_FailsWholeQueryWhenOneWindowIsRejected()
    {
        // 半份申报表名单比明确报错更危险：任一段被税局拒绝就整次查询失败。
        var result = TaxDeclarationPayloadParser.ParseReports(
            [Payload, GatewayPayload], new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 3));

        Assert.Empty(result.Rows);
        Assert.Contains("会话失效", result.Message);
    }

    [Fact]
    public void Parse_PassesThroughTaxBureauErrorMessage()
    {
        const string error = """
            {"Response":{"Error":{"Code":"4001","Message":"税款所属期与申报日期必须选择一个填写"},"Data":null}}
            """;

        var result = TaxDeclarationPayloadParser.Parse(error, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Empty(result.Rows);
        Assert.Equal("税款所属期与申报日期必须选择一个填写", result.Message);
    }

    [Fact]
    public void Parse_PassesThroughGatewaySessionMessageInsteadOfMissingDetail()
    {
        var result = TaxDeclarationPayloadParser.Parse(GatewayPayload, new DateOnly(2025, 10, 1), new DateOnly(2025, 10, 31));

        Assert.Empty(result.Rows);
        Assert.Contains("会话失效", result.Message);
        Assert.DoesNotContain("没有返回申报明细", result.Message);
        Assert.NotEmpty(result.Diagnostics ?? []);
    }

    [Fact]
    public void NeedsAppSession_RecognizesGatewayEnvelopeOnly()
    {
        Assert.True(TaxDeclarationPayloadParser.NeedsAppSession(GatewayPayload));
        Assert.False(TaxDeclarationPayloadParser.NeedsAppSession(Payload));
        Assert.False(TaxDeclarationPayloadParser.NeedsAppSession("""{"msg":"其他故障","code":"5000"}"""));
        Assert.False(TaxDeclarationPayloadParser.NeedsAppSession("<html>登录已失效</html>"));
        Assert.False(TaxDeclarationPayloadParser.NeedsAppSession(""));
    }

    [Fact]
    public void Parse_ReturnsDiagnosticsWhenBodyIsNotJson()
    {
        var result = TaxDeclarationPayloadParser.Parse("<html>登录已失效</html>", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Empty(result.Rows);
        Assert.NotNull(result.Message);
        Assert.Single(result.Diagnostics ?? []);
    }
}
