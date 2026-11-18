using System.Net;
using System.Net.Http.Json;
using System.Text;
using YunCai.Modules.Accounting.Domain;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 报表打印 PDF 端点测试：走真实组合根取数并渲染，校验响应是可用 PDF、文件名带账套名与期间。
/// 设 YUNCAI_PDF_ARTIFACTS=目录 时额外落盘 pdf，供人工核对版式（CI 不写文件）。
/// </summary>
public sealed class ReportPdfApiTests : IDisposable
{
    private readonly ApiHarness _h = new();

    [Fact]
    public async Task TrialBalancePdf_ReturnsPdfWithAccountSetAndPeriodInFileName()
    {
        var (accountSetId, _, _) = await SeedVouchersAsync("科目余额表打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "Reports")}/pdf?kind=trial-balance"
            + "&startDate=2026-01-01&endDate=2026-03-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var fileName = FileNameOf(response);
        Assert.Contains("科目余额表", fileName);
        Assert.Contains("科目余额表打印", fileName);
        Assert.Contains("202601-202603", fileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1_000, $"PDF 体积异常偏小：{bytes.Length} 字节");

        WriteArtifacts("trial-balance", bytes);
    }

    [Fact]
    public async Task IncomeStatementPdf_ReturnsPdfWithPeriodInFileName()
    {
        var (accountSetId, _, _) = await SeedVouchersAsync("利润表打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "Reports")}/pdf?kind=income-statement&year=2026&month=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("利润表", FileNameOf(response));
        Assert.Contains("202603", FileNameOf(response));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        WriteArtifacts("income-statement", bytes);
    }

    /// <summary>页面上带打印按钮的报表都要能出 PDF；各 kind 的 DTO 强转与版式都在这条路上跑一遍。</summary>
    [Theory]
    [InlineData("income-quarter&year=2026&quarter=1")]
    [InlineData("cash-flow&year=2026&month=3")]
    [InlineData("cash-flow&year=2026&month=3&method=formula")]
    [InlineData("cash-flow-quarter&year=2026&quarter=1")]
    [InlineData("expense-detail&year=2026&month=3")]
    [InlineData("department-income&year=2026&month=3")]
    [InlineData("tax&year=2026&month=3")]
    [InlineData("financial-summary&year=2026&month=3")]
    public async Task Pdf_KnownKinds_ReturnPdf(string query)
    {
        var (accountSetId, _, _) = await SeedVouchersAsync("报表打印版式");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "Reports")}/pdf?kind={query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1_000, $"PDF 体积异常偏小：{bytes.Length} 字节");

        WriteArtifacts(query.Split('&')[0].Replace("=", "-"), bytes);
    }

    /// <summary>只有 xlsx 导出、页面无打印按钮的报表，打印时给中文提示。</summary>
    [Theory]
    [InlineData("equity-change")]
    [InlineData("project-income")]
    public async Task Pdf_UnprintedKind_BadRequestWithChineseMessage(string kind)
    {
        var accountSetId = await _h.CreateAccountSetAsync("未做打印版式");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "Reports")}/pdf?kind={kind}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("暂不支持打印", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Pdf_UnknownKindText_BadRequestWithChineseMessage()
    {
        var accountSetId = await _h.CreateAccountSetAsync("报表打印参数");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "Reports")}/pdf?kind=not-a-report");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("不支持打印的报表类型", await response.Content.ReadAsStringAsync());
    }

    // —— 报表下钻（行 → 科目 → 明细账）——

    [Fact]
    public async Task BalanceSheetDrilldown_ReturnsAccountsSummingToReportLine()
    {
        var (accountSetId, cash, _) = await SeedVouchersAsync("资产负债表下钻");
        var reports = ApiHarness.ResourcePath(accountSetId, "Reports");

        var drill = await _h.Client.GetFromJsonAsync<DrilldownModel>(
            $"{reports}/balance-sheet/drilldown?year=2026&month=3&line=1");
        var sheet = await _h.Client.GetFromJsonAsync<BalanceSheetModel>(
            $"{reports}/balance-sheet?year=2026&month=3");

        // 货币资金 = 300 - 200 + 100 = 200；下钻合计必须等于报表行金额（同源同口径）。
        var row = Assert.Single(drill!.Accounts);
        Assert.Equal(cash, row.AccountId);
        Assert.Equal(200m, row.Current);
        Assert.Equal(200m, drill.TotalCurrent);
        Assert.Equal(sheet!.Lines.Single(l => l.Line == 1).Ending, drill.TotalCurrent);
        Assert.Equal("2026年03月", drill.PeriodText);
    }

    [Fact]
    public async Task IncomeStatementDrilldown_ReturnsAccountsAndRejectsInvalidLine()
    {
        var (accountSetId, _, revenue) = await SeedVouchersAsync("利润表下钻");
        var reports = ApiHarness.ResourcePath(accountSetId, "Reports");

        var drill = await _h.Client.GetFromJsonAsync<DrilldownModel>(
            $"{reports}/income-statement/drilldown?year=2026&month=3&line=1");
        var statement = await _h.Client.GetFromJsonAsync<IncomeStatementModel>(
            $"{reports}/income-statement?year=2026&month=3");

        // 3 月收入 100，本年累计 400（1 月 300 + 3 月 100）。
        var row = Assert.Single(drill!.Accounts);
        Assert.Equal(revenue, row.AccountId);
        Assert.Equal(100m, row.Current);
        Assert.Equal(400m, row.YearToDate);
        Assert.Equal(statement!.Lines.Single(l => l.Line == 1).Current, drill.TotalCurrent);
        Assert.Equal(statement.Lines.Single(l => l.Line == 1).YearToDate, drill.TotalYearToDate);

        var invalid = await _h.Client.GetAsync($"{reports}/income-statement/drilldown?year=2026&month=3&line=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("报表行次不合法", await invalid.Content.ReadAsStringAsync());
    }

    private sealed record DrilldownModel(int Line, string PeriodText, List<DrilldownAccountModel> Accounts,
        decimal TotalCurrent, decimal TotalYearToDate);

    private sealed record DrilldownAccountModel(Guid AccountId, string Code, string Name, decimal Current, decimal YearToDate);

    private sealed record BalanceSheetModel(List<SheetLineModel> Lines);

    private sealed record IncomeStatementModel(List<StatementLineModel> Lines);

    private sealed record SheetLineModel(int Line, decimal Ending, decimal Opening);

    private sealed record StatementLineModel(int Line, decimal Current, decimal YearToDate);

    /// <summary>播 1-3 月各一张已过账凭证（现金 ↔ 主营业务收入），另挂一个费用与税费科目供费用/纳税报表取数。</summary>
    private async Task<(Guid AccountSetId, Guid Cash, Guid Revenue)> SeedVouchersAsync(string name)
    {
        var accountSetId = await _h.CreateAccountSetAsync(name);
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = _h.AddRootAccount(accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);
        var expense = _h.AddRootAccount(accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);
        var taxPayable = _h.AddRootAccount(accountSetId, "2221", "应交增值税", AccountCategory.Liability, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10",
            (cash, 300m, 0m), (revenue, 0m, 300m));
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-02-15",
            (expense, 200m, 0m), (cash, 0m, 200m));
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-03-20",
            (cash, 100m, 0m), (revenue, 0m, 100m), (taxPayable, 0m, 30m), (expense, 30m, 0m));

        return (accountSetId, cash, revenue);
    }

    /// <summary>取 Content-Disposition 里的文件名（优先 RFC 5987 的 filename*=，回退正则解）。</summary>
    private static string FileNameOf(HttpResponseMessage response)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        return disposition?.FileNameStar
               ?? System.Text.RegularExpressions.Regex.Match(
                   disposition?.ToString() ?? string.Empty,
                   "filename\\*=UTF-8''([^;]+)",
                   System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value;
    }

    /// <summary>把端点产出的 PDF 落盘到 YUNCAI_PDF_ARTIFACTS 指定目录，便于肉眼核对版式（CI 不写文件）。</summary>
    private static void WriteArtifacts(string kind, byte[] pdf)
    {
        var dir = Environment.GetEnvironmentVariable("YUNCAI_PDF_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(dir)) return;

        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{kind}.pdf"), pdf);
    }

    public void Dispose() => _h.Dispose();
}
