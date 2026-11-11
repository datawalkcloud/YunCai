using KuaiJi.Modules.Accounting.Domain;
using Xunit;

namespace KuaiJi.Tests.Api;

/// <summary>
/// 账页打印 PDF 端点测试：走真实组合根取数并渲染，校验响应是可用的 PDF。
/// 设 KUAIJI_PDF_ARTIFACTS=目录 时额外落盘 pdf/png，供人工核对版式（CI 不写文件）。
/// </summary>
public sealed class LedgerPdfApiTests : IDisposable
{
    private readonly ApiHarness _h = new();

    [Fact]
    public async Task SubsidiaryPdf_ReturnsPdfWithAccountSetAndPeriodInFileName()
    {
        var (accountSetId, cash, revenue) = await SeedVouchersAsync("明细账打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "Ledgers")}/subsidiary/pdf?startDate=2026-01-01&endDate=2026-03-31");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar
            ?? Uri.UnescapeDataString(System.Text.RegularExpressions.Regex.Match(
                disposition?.ToString() ?? string.Empty,
                "filename\\*=UTF-8''([^;]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value);
        Assert.Contains("明细账打印", fileName);
        Assert.Contains("202601-202603", fileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1_000, $"PDF 体积异常偏小：{bytes.Length} 字节");

        WriteArtifacts("subsidiary", bytes);
    }

    [Fact]
    public async Task GeneralPdf_RendersYearCumulativeRow()
    {
        var (accountSetId, _, _) = await SeedVouchersAsync("总账打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "Ledgers")}/general/pdf?startDate=2026-02-01&endDate=2026-03-31");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        WriteArtifacts("general", bytes);
    }

    [Fact]
    public async Task Pdf_UnprintedKind_BadRequestWithChineseMessage()
    {
        var accountSetId = await _h.CreateAccountSetAsync("未做打印版式");

        // 序时账只有 xlsx 导出，页面没有打印按钮，故打印版式未实现
        var response = await _h.Client.GetAsync($"{ApiHarness.ResourcePath(accountSetId, "Ledgers")}/journal/pdf");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("暂不支持打印", await response.Content.ReadAsStringAsync());
    }

    /// <summary>播 1-3 月各一张已过账凭证（现金 ↔ 主营业务收入），返回账套与两个科目。</summary>
    private async Task<(Guid AccountSetId, Guid Cash, Guid Revenue)> SeedVouchersAsync(string name)
    {
        var accountSetId = await _h.CreateAccountSetAsync(name);
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金", AccountCategory.Asset, BalanceDirection.Debit);
        var revenue = _h.AddRootAccount(accountSetId, "6001", "主营业务收入", AccountCategory.ProfitAndLoss, BalanceDirection.Credit);

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-01-10", (cash, 300m, 0m), (revenue, 0m, 300m));
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-02-15", (cash, 200m, 0m), (revenue, 0m, 200m));
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-03-20", (cash, 100m, 0m), (revenue, 0m, 100m));

        return (accountSetId, cash, revenue);
    }

    /// <summary>把端点产出的 PDF 落盘到 KUAIJI_PDF_ARTIFACTS 指定目录，便于肉眼核对版式（CI 不写文件）。</summary>
    private static void WriteArtifacts(string kind, byte[] pdf)
    {
        var dir = Environment.GetEnvironmentVariable("KUAIJI_PDF_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(dir)) return;

        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{kind}.pdf"), pdf);
    }

    public void Dispose() => _h.Dispose();
}
