using KuaiJi.Modules.Tax.Application;
using KuaiJi.Modules.Tax.Infrastructure;
using Xunit;

namespace KuaiJi.Tests.Tax;

/// <summary>
/// 数字账户「缴款信息查询」「证明信息查询」接口返回报文的解析测试。
/// 样例取自真实接口报文，并保留中文列名版本，验证按日期区间过滤、金额/日期归一、
/// 无关行被锚字段挡掉，以及多省份字段命名差异的兼容。
/// </summary>
public sealed class TaxPaymentPayloadParserTests
{
    private const string TablePayload = """
        [
          {"征收项目":"增值税","征收品目":"销售货物(13%)","税款所属期起":"2026-10-01",
           "税款所属期止":"2026-10-31","实缴金额":"1,234.50","税款种类":"正税",
           "缴款日期":"2026-11-05","税费缴纳方式":"三方协议","票证种类":"电子缴款书",
           "票证号码":"202611050001","电子税票号码":"123456"},
          {"征收项目":"城市维护建设税","征收品目":"市区(7%)","税款所属期起":"2026-10-01",
           "税款所属期止":"2026-10-31","实缴（退）金额":"(86.42)","税款种类":"正税",
           "缴款日期":"2026-11-05","税费缴纳方式":"三方协议","票证种类":"电子缴款书",
           "票证号码":"202611050002","电子税票号码":"123457"},
          {"序号":"1","操作":"查看"}
        ]
        """;

    private const string PinyinPayload = """
        {"Response":{"Data":{"List":[
          {"zsxm":"增值税","zspm":"提供服务(6%)","skssqq":"2026-10-01 00:00:00",
           "skssqz":"2026-10-31 00:00:00","sjje":500.25,"jkrq":"2026-11-06 00:00:00",
           "jkfs":"银行端查询缴税","pzzl":"电子缴款书","pzh":"A0001","dzsphm":"900001"}
        ]}}}
        """;

    /// <summary>浙江数字账户 DescribeJkxxcx 的真实响应。</summary>
    private const string DigitalAccountPaymentPayload = """
        {"Response":{"RequestId":"410a58fa46cbede5","Error":null,"Data":{"PageNumber":1,"PageSize":500,"Total":1,
         "List":[{"zsxmDm":"10111","zsxmMc":"印花税","zspmDm":"101110502","zspmMc":"营业账簿",
           "skssqq":"2025-01-01 00:00:00","skssqz":"2025-12-31 00:00:00","skzlDm":"10","skzlMc":"正税",
           "sjje":2.51,"jkrq":"2026-01-12 19:49:22","skjnfsDm":"99","skjnfsMc":"其他缴费方式",
           "pzzlDm":"000006010","pzzlMc":"税收电子缴款书","pzhm":null,
           "dzsphm":"333016260100314673","zkwszmbz":null}]}}}
        """;

    /// <summary>浙江数字账户 DescribeWszmxx 的真实响应。</summary>
    private const string DigitalAccountCertificatePayload = """
        {"Response":{"RequestId":"bc721991bbb96388","Error":null,"Data":{"PageNumber":1,"PageSize":500,"Total":1,
         "List":[{"zmmx":"税收完税证明","kjrq":"2026-01-12 00:00:00","kjje":2.51,"pzzg":null,
           "pzhm":"333015260100105600","kjswjgDm":null,
           "kjswjgMc":"国家税务总局杭州市拱墅区税务局纳税服务科","pzzlDm":null,
           "pzzl":"税收完税证明（非印刷）","gjhdqszDm":null,"gjhdqszMc":null,"bh":null,
           "sqrq":null,"nsnd":null,"shrq":null,"wszmhm":null}]}}}
        """;

    [Fact]
    public void ParsePayments_ReadsRenderedTableAndFiltersByPaidDate()
    {
        var result = TaxPaymentPayloadParser.ParsePayments(
            [new CapturedJsonPayload("https://etax.example/szzh#table", TablePayload)],
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30));

        Assert.Equal(2, result.Rows.Count);
        Assert.Null(result.Message);

        var vat = result.Rows.Single(row => row.Project == "增值税");
        Assert.Equal("销售货物(13%)", vat.Item);
        Assert.Equal("2026-10-01", vat.PeriodStart);
        Assert.Equal("2026-10-31", vat.PeriodEnd);
        Assert.Equal(1234.50m, vat.Amount);
        Assert.Equal("正税", vat.TaxKind);
        Assert.Equal("2026-11-05", vat.PaidOn);
        Assert.Equal("三方协议", vat.Method);
        Assert.Equal("电子缴款书", vat.VoucherKind);
        Assert.Equal("202611050001", vat.VoucherNo);
        Assert.Equal("123456", vat.ETicketNo);

        // 会计负数写法 (86.42) 要还原成 -86.42。
        var surtax = result.Rows.Single(row => row.Project == "城市维护建设税");
        Assert.Equal(-86.42m, surtax.Amount);
    }

    [Fact]
    public void ParsePayments_ReadsPinyinEnvelopeAndSortsByPaidDateDescending()
    {
        var result = TaxPaymentPayloadParser.ParsePayments(
            [
                new CapturedJsonPayload("https://etax.example/api/payment", PinyinPayload),
                new CapturedJsonPayload("https://etax.example/szzh#table", TablePayload)
            ],
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30));

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal("2026-11-06", result.Rows[0].PaidOn);
        Assert.Equal(500.25m, result.Rows[0].Amount);
        Assert.Equal("银行端查询缴税", result.Rows[0].Method);
    }

    [Fact]
    public void ParsePayments_DropsRowsOutsidePaidDateRange()
    {
        var result = TaxPaymentPayloadParser.ParsePayments(
            [new CapturedJsonPayload("table", TablePayload)],
            new DateOnly(2026, 12, 1),
            new DateOnly(2026, 12, 31));

        Assert.Empty(result.Rows);
        Assert.Equal("所选缴款日期区间没有缴款记录。", result.Message);
        Assert.NotEmpty(result.Diagnostics ?? []);
    }

    [Fact]
    public void ParsePayments_IgnoringDeclarationTableWithoutPaidDate()
    {
        // 申报表也有征收项目与所属期，但没有缴款日期，不能被当成缴款记录。
        const string declarationTable = """
            [{"征收项目":"增值税","税款所属期起":"2026-10-01","税款所属期止":"2026-10-31","应补（退）税额":"1200.00"}]
            """;

        var result = TaxPaymentPayloadParser.ParsePayments(
            [new CapturedJsonPayload("table", declarationTable)],
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30));

        Assert.Empty(result.Rows);
        Assert.Contains(result.Diagnostics ?? [], note => note.Contains("征收项目"));
    }

    [Fact]
    public void ParsePayments_ReadsDigitalAccountEnvelope()
    {
        var result = TaxPaymentPayloadParser.ParsePayments(
            [new CapturedJsonPayload("https://etax.example/szc/szzh/sjswszzh/zhcx/jkxx/v1/DescribeJkxxcx",
                DigitalAccountPaymentPayload)],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        var row = Assert.Single(result.Rows);
        Assert.Equal("印花税", row.Project);
        Assert.Equal("营业账簿", row.Item);
        Assert.Equal("2025-01-01", row.PeriodStart);
        Assert.Equal("2025-12-31", row.PeriodEnd);
        Assert.Equal(2.51m, row.Amount);
        Assert.Equal("正税", row.TaxKind);
        Assert.Equal("2026-01-12", row.PaidOn);
        Assert.Equal("其他缴费方式", row.Method);
        Assert.Equal("税收电子缴款书", row.VoucherKind);
        Assert.Equal("333016260100314673", row.ETicketNo);
    }

    [Fact]
    public void ParseCertificates_FiltersByIssuedDateAndKeepsFormat()
    {
        const string payload = """
            [
              {"原凭证号":"202611050001","税款所属期起":"2026-10-01","税款所属期止":"2026-10-31",
               "入库日期":"2026-11-05","实缴（退）金额":"1,234.50","开具日期":"2026-11-06","开具格式":"表格式"},
              {"原凭证号":"202611050002","税款所属期起":"2026-10-01","税款所属期止":"2026-10-31",
               "入库日期":"2026-11-05","实缴（退）金额":"86.42","开具日期":"2026-10-06","开具格式":"文书式"}
            ]
            """;

        var result = TaxPaymentPayloadParser.ParseCertificates(
            [new CapturedJsonPayload("table", payload)],
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30));

        var row = Assert.Single(result.Rows);
        Assert.Equal("202611050001", row.VoucherNo);
        Assert.Equal("2026-11-05", row.PaidOn);
        Assert.Equal(1234.50m, row.Amount);
        Assert.Equal("2026-11-06", row.IssuedOn);
        Assert.Equal("表格式", row.Format);
    }

    [Fact]
    public void ParseCertificates_ReadsDigitalAccountEnvelope()
    {
        var result = TaxPaymentPayloadParser.ParseCertificates(
            [new CapturedJsonPayload("https://etax.example/szc/szzh/sjswszzh/zhcx/wszm/v1/DescribeWszmxx",
                DigitalAccountCertificatePayload)],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        var row = Assert.Single(result.Rows);
        Assert.Equal("税收完税证明", row.Name);
        Assert.Equal("333015260100105600", row.VoucherNo);
        Assert.Equal("国家税务总局杭州市拱墅区税务局纳税服务科", row.Authority);
        Assert.Equal(2.51m, row.Amount);
        Assert.Equal("2026-01-12", row.IssuedOn);
        Assert.Equal("税收完税证明（非印刷）", row.Format);
    }

    [Fact]
    public void ParseCertificates_ReportsNoCertificateMessage()
    {
        var result = TaxPaymentPayloadParser.ParseCertificates(
            [new CapturedJsonPayload("table", TablePayload)],
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30));

        Assert.Empty(result.Rows);
        Assert.Equal("所选开具日期区间没有已开具的完税证明。", result.Message);
        Assert.NotEmpty(result.Diagnostics ?? []);
    }

    [Fact]
    public void ParsePayments_ReturnsDiagnosticsWhenBodyIsNotJson()
    {
        var result = TaxPaymentPayloadParser.ParsePayments(
            [new CapturedJsonPayload("https://etax.example/query", "<html>登录已失效</html>")],
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30));

        Assert.Empty(result.Rows);
        Assert.NotNull(result.Message);
        Assert.NotNull(result.Diagnostics);
    }
}
