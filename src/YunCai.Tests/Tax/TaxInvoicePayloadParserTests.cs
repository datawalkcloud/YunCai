using YunCai.Modules.Invoices.Domain;
using YunCai.Modules.Tax.Application;
using YunCai.Modules.Tax.Infrastructure;
using Xunit;

namespace YunCai.Tests.Tax;

/// <summary>
/// 税局「全量发票查询」响应的解析测试。各省字段名不统一：浙江用拼音缩写
/// （fphm/kprq/hjje），也有的页面直接把中文键写进 JSON。方向判定不看页签，
/// 而是拿购销方纳税人识别号与本企业税号比对；红冲发票的负数金额要原样带出来，
/// 翻页时同一张发票出现在多段响应里只保留一行。
/// </summary>
public sealed class TaxInvoicePayloadParserTests
{
    private const string OwnTaxNo = "91330100MA2XXXXX1A";

    private static CapturedJsonPayload Payload(string body, string url = "https://etax.example/invoice-query/list")
        => new(url, body);

    [Fact]
    public void Parse_SalesInvoice_UsesSellerSideAndMapsTypeCode()
    {
        // 销方税号等于本企业：销项，对方名称取购买方。
        const string body = """
            {"Response":{"Data":{"List":[
              {"fphm":"26317000000123456789","kprq":"2026-09-12 10:11:12","fplx":"31",
               "xfmc":"杭州某某有限公司","xfnsrsbh":"91330100MA2XXXXX1A",
               "gfmc":"客户甲","gfnsrsbh":"91330100MA2YYYYY2B",
               "hjje":"1000.00","hjse":"130.00","jshj":"1130.00","hwmc":"技术服务费"}
            ]}}}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out var diagnostics);

        var row = Assert.Single(rows);
        Assert.Equal(InvoiceDirection.Sales, row.Direction);
        Assert.Equal("26317000000123456789", row.InvoiceNo);
        Assert.Equal("数电发票（增值税专用发票）", row.InvoiceType);
        Assert.Equal(new DateOnly(2026, 9, 12), row.IssueDate);
        Assert.Equal("客户甲", row.PartnerName);
        Assert.Equal(1000m, row.Amount);
        Assert.Equal(130m, row.Tax);
        Assert.Equal("技术服务费", row.GoodsName);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_PurchaseInvoice_ReadsChineseKeysAndUsesSellerName()
    {
        // 购方税号等于本企业：进项，对方名称取销售方。
        const string body = """
            {"data":{"rows":[
              {"发票号码":"263170000009999","开票日期":"2026-09-03",
               "发票种类":"增值税电子普通发票",
               "购买方名称":"杭州某某有限公司","购买方纳税人识别号":"91330100MA2XXXXX1A",
               "销售方名称":"供应商乙","销售方纳税人识别号":"91330100MA2ZZZZZ3C",
               "合计金额":"200.00","合计税额":"26.00","价税合计":"226.00","货物名称":"办公用品"}
            ]}}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out _);

        var row = Assert.Single(rows);
        Assert.Equal(InvoiceDirection.Purchase, row.Direction);
        Assert.Equal("供应商乙", row.PartnerName);
        Assert.Equal("增值税电子普通发票", row.InvoiceType);
        Assert.Equal(200m, row.Amount);
        Assert.Equal(26m, row.Tax);
    }

    [Fact]
    public void Parse_ZhejiangDpptUsesGmfAndXsfKeys()
    {
        // 浙江数电「全量发票查询」用 gmf/xsf 前缀（购买方/销售方），不是 g f 两字母缩写。
        const string body = """
            {"code":"200","msg":"成功","data":{"list":[
              {"fphm":"25332000000012345678","kprq":"2025-10-15","fplx":"32",
               "gmfmc":"杭州某某有限公司","gmfnsrsbh":"91330100MA2XXXXX1A",
               "xsfmc":"上游供应商","xsfnsrsbh":"91330100MA2YYYYY2B",
               "hjje":100.00,"hjse":3.00,"jshj":103.00,"hwmc":"服务费"}
            ]}}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out var diagnostics);

        var row = Assert.Single(rows);
        Assert.Equal(InvoiceDirection.Purchase, row.Direction);
        Assert.Equal("上游供应商", row.PartnerName);
        Assert.Equal("数电发票（普通发票）", row.InvoiceType);
        Assert.Equal(100m, row.Amount);
        Assert.Equal(3m, row.Tax);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Parse_ResolvesDirectionByFieldValueWhenKeyIsUnknown()
    {
        // 字段名不认识时，扫值等于本企业税号的字段，再按键名里的「购/销」判方向。
        const string body = """
            {"data":{"rows":[
              {"fphm":"25332000000099999999","kprq":"2025-10-01","fplx":"31",
               "购买方名称":"杭州某某有限公司","购买方税号":"91330100MA2XXXXX1A",
               "销售方名称":"客户丙","销售方税号":"91330100MA2YYYYY2B",
               "合计金额":50,"合计税额":6.5,"价税合计":56.5}
            ]}}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out _);

        var row = Assert.Single(rows);
        Assert.Equal(InvoiceDirection.Purchase, row.Direction);
        Assert.Equal("客户丙", row.PartnerName);
    }

    [Fact]
    public void Parse_DerivesAmountFromTotalWhenAmountFieldMissing()
    {
        const string body = """
            {"list":[{"fphm":"26317000000111111111","kprq":"20260908",
              "xfnsrsbh":"91330100MA2XXXXX1A","gfmc":"客户丙",
              "hjse":"13.00","jshj":"113.00"}]}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out _);

        var row = Assert.Single(rows);
        Assert.Equal(100m, row.Amount);
        Assert.Equal(13m, row.Tax);
    }

    [Fact]
    public void Parse_RedInvoice_KeepsNegativeAmounts()
    {
        const string body = """
            {"list":[{"fphm":"26317000000222222222","kprq":"2026-09-20",
              "fplxmc":"数电发票（普通发票）","xfnsrsbh":"91330100MA2XXXXX1A","gfmc":"客户丁",
              "hjje":"-100.00","hjse":"-13.00","jshj":"-113.00"}]}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out _);

        var row = Assert.Single(rows);
        Assert.Equal(-100m, row.Amount);
        Assert.Equal(-13m, row.Tax);
    }

    [Fact]
    public void Parse_DeduplicatesSameInvoiceAcrossPages()
    {
        const string body = """
            {"list":[{"fphm":"26317000000333333333","kprq":"2026-09-01",
              "xfnsrsbh":"91330100MA2XXXXX1A","gfmc":"客户戊","hjje":"10","hjse":"1.3","jshj":"11.3"}]}
            """;
        // 翻页抓包会把第一页的响应再读一遍，同一张发票不能落两次。
        var rows = TaxInvoicePayloadParser.Parse([Payload(body), Payload(body, "https://etax.example/page2")], OwnTaxNo, out _);

        Assert.Single(rows);
    }

    [Fact]
    public void Parse_SkipsRowsWhoseBuyerAndSellerAreNotOwnCompany()
    {
        const string body = """
            {"list":[{"fphm":"26317000000444444444","kprq":"2026-09-01",
              "xfnsrsbh":"91330100MA2AAAAA1A","gfmc":"别家公司","gfnsrsbh":"91330100MA2BBBBB2B",
              "hjje":"10","hjse":"1.3","jshj":"11.3"}]}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out var diagnostics);

        Assert.Empty(rows);
        // 解析不到时要带回候选字段名，便于按真实响应补映射。
        Assert.Contains(diagnostics, note => note.Contains("fphm"));
    }

    [Fact]
    public void Parse_ReportsZeroResultWhenQueryReturnsNoRows()
    {
        const string body = """
            {"code":"0000","message":"查询成功","data":{"total":0,"list":[]}}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out var diagnostics);

        Assert.Empty(rows);
        // 「税局确实回了 0 条」不能报成「字段不认识」，否则会让人以为是解析规则坏了。
        Assert.Contains(diagnostics, note => note.Contains("返回 0 条"));
    }

    [Fact]
    public void Parse_ReportsResponseShapeWhenNothingLooksLikeInvoice()
    {
        const string body = """
            {"code":"0000","message":"查询成功"}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out var diagnostics);

        Assert.Empty(rows);
        Assert.Contains(diagnostics, note => note.Contains("结构=") && note.Contains("code"));
    }

    [Fact]
    public void Parse_ShapeDiagnosticsDescendIntoDataEnvelope()
    {
        // 浙江的真实外层是 Response.Data，结构摘要要能看出 Total 和 List，才能分清空结果与字段不对。
        const string body = """
            {"Response":{"RequestId":"abc","Data":{"PageNumber":1,"PageSize":20,"Total":0,"List":[]}}}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out var diagnostics);

        Assert.Empty(rows);
        var note = Assert.Single(diagnostics);
        Assert.Contains("返回 0 条", note);
        Assert.Contains("Total", note);
        Assert.Contains("List", note);
    }

    [Fact]
    public void Parse_UnwrapsInvoiceListPackedAsJsonString()
    {
        const string body = """
            {"code":"0000","data":"{\"list\":[{\"fphm\":\"25332000000088888888\",\"kprq\":\"2025-10-08\",\"fplx\":\"32\",\"gmfnsrsbh\":\"91330100MA2XXXXX1A\",\"xsfmc\":\"上游供应商\",\"hjje\":100,\"hjse\":3,\"jshj\":103}]}"}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], OwnTaxNo, out _);

        var row = Assert.Single(rows);
        Assert.Equal(InvoiceDirection.Purchase, row.Direction);
        Assert.Equal("上游供应商", row.PartnerName);
    }

    [Fact]
    public void Parse_WithEmptyOwnTaxNumber_SkipsEverything()
    {
        const string body = """
            {"list":[{"fphm":"26317000000555555555","kprq":"2026-09-01",
              "xfnsrsbh":"91330100MA2XXXXX1A","gfmc":"客户己","hjje":"10","hjse":"1.3","jshj":"11.3"}]}
            """;

        var rows = TaxInvoicePayloadParser.Parse([Payload(body)], "", out _);

        Assert.Empty(rows);
    }
}
