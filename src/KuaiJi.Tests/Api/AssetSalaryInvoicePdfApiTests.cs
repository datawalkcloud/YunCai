using System.Net;
using System.Net.Http.Json;
using System.Text;
using KuaiJi.Modules.Accounting.Domain;
using Xunit;

namespace KuaiJi.Tests.Api;

/// <summary>
/// 固定资产 / 工资 / 发票的打印 PDF 端点测试：走真实组合根取数并渲染，
/// 校验响应是可用 PDF、文件名带账套名与期间（页面上的打印按钮全部指向这些端点）。
/// 设 KUAIJI_PDF_ARTIFACTS=目录 时额外落盘 pdf，供人工核对版式（CI 不写文件）。
/// </summary>
public sealed class AssetSalaryInvoicePdfApiTests : IDisposable
{
    private readonly ApiHarness _h = new();

    // —— 固定资产 ——

    [Fact]
    public async Task AssetsPdf_ReturnsPdfWithAccountSetAndPeriodInFileName()
    {
        var (accountSetId, _) = await SeedAssetsAsync("资产清单打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "fixedassets")}/assets/pdf?period=202603");

        await AssertPdfAsync(response, "固定资产清单", "资产清单打印", "202603");
        WriteArtifacts("assets", response);
    }

    [Fact]
    public async Task InitialAssetsPdf_ReturnsPdf()
    {
        var (accountSetId, _) = await SeedAssetsAsync("期初资产打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "fixedassets")}/initial-assets/pdf?period=202601");

        await AssertPdfAsync(response, "资产初始化清单", "期初资产打印");
        WriteArtifacts("initial-assets", response);
    }

    [Fact]
    public async Task DepreciationLedgerPdf_AfterDepreciate_ReturnsPdfWithRows()
    {
        var (accountSetId, _) = await SeedAssetsAsync("折旧明细打印");

        // 先计提 202603，让明细表有行可印（合计行、期间列都依赖真实数据）
        var depreciate = await _h.Client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "fixedassets")}/depreciate", new { period = 202603 });
        Assert.True(depreciate.IsSuccessStatusCode, await depreciate.Content.ReadAsStringAsync());

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "fixedassets")}/depreciation-ledger/pdf?period=202603");

        await AssertPdfAsync(response, "折旧摊销明细表", "折旧明细打印", "202603");
        WriteArtifacts("depreciation-ledger", response);
    }

    [Fact]
    public async Task CheckPdf_ReturnsPdf()
    {
        var (accountSetId, _) = await SeedAssetsAsync("资产核对打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "fixedassets")}/check/pdf?period=202603");

        await AssertPdfAsync(response, "资产核对总账", "资产核对打印");
        WriteArtifacts("asset-check", response);
    }

    // —— 工资 ——

    [Fact]
    public async Task SalaryBillsPdf_ReturnsPdfWithPeriodInFileName()
    {
        var (accountSetId, _) = await SeedSalaryAsync("工资表打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "salary")}/bills/pdf?period=202603");

        await AssertPdfAsync(response, "工资表", "工资表打印", "202603");
        WriteArtifacts("salary-bills", response);
    }

    [Fact]
    public async Task SalarySummaryPdf_ReturnsPdfWithPeriodRangeInFileName()
    {
        var (accountSetId, _) = await SeedSalaryAsync("工资统计打印");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "salary")}/summary/pdf?startPeriod=202601&endPeriod=202603");

        await AssertPdfAsync(response, "工资统计表", "工资统计打印", "202601-202603");
        WriteArtifacts("salary-summary", response);
    }

    // —— 发票 ——

    /// <summary>销项 / 进项列表页的打印按钮同一条路由，只是 direction 与列不同。</summary>
    [Theory]
    [InlineData(1, "销项发票清单")]
    [InlineData(2, "进项发票清单")]
    public async Task InvoicesPdf_BothDirections_ReturnPdf(int direction, string title)
    {
        var accountSetId = await _h.CreateAccountSetAsync("发票清单打印");
        await SeedInvoiceAsync(accountSetId, direction, "INV-PDF-01");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "invoices")}/pdf?direction={direction}"
            + "&startDate=2026-01-01&endDate=2026-01-31&period=2026-01");

        await AssertPdfAsync(response, title, "发票清单打印", "2026-01");
        WriteArtifacts($"invoices-{direction}", response);
    }

    [Fact]
    public async Task InvoicesPdf_BadDirection_BadRequestWithChineseMessage()
    {
        var accountSetId = await _h.CreateAccountSetAsync("发票打印参数");

        var response = await _h.Client.GetAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "invoices")}/pdf?direction=9");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("发票方向", await response.Content.ReadAsStringAsync());
    }

    // —— 账套不存在：三处打印端点都按各自模块口径回 400 中文提示 ——

    [Theory]
    [InlineData("fixedassets/assets/pdf")]
    [InlineData("fixedassets/check/pdf")]
    [InlineData("salary/bills/pdf?period=202601")]
    [InlineData("salary/summary/pdf?startPeriod=202601&endPeriod=202601")]
    [InlineData("invoices/pdf?direction=1")]
    public async Task Pdf_UnknownAccountSet_BadRequestWithChineseMessage(string tail)
    {
        var response = await _h.Client.GetAsync(
            $"/accountsets/{Guid.NewGuid()}/{tail}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("指定的账套不存在", await response.Content.ReadAsStringAsync());
    }

    // —— 播种 ——

    /// <summary>建账套 + 资产类别 + 两张卡片（一张带期初累计折旧），返回账套与类别标识。</summary>
    private async Task<(Guid AccountSetId, Guid CategoryId)> SeedAssetsAsync(string name)
    {
        var accountSetId = await _h.CreateAccountSetAsync(name);
        var assetAccount = _h.AddRootAccount(accountSetId, "1601", "固定资产", AccountCategory.Asset, BalanceDirection.Debit);
        _h.AddRootAccount(accountSetId, "1602", "累计折旧", AccountCategory.Asset, BalanceDirection.Credit);
        _h.AddRootAccount(accountSetId, "6602", "管理费用", AccountCategory.ProfitAndLoss, BalanceDirection.Debit);

        var category = await _h.Client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "fixedassets")}/categories", new
            {
                code = "01",
                name = "电子设备",
                method = 1,
                attr = 1,
                subjectAccountId = assetAccount,
                months = 120,
                residualRate = 5m,
            });
        Assert.True(category.IsSuccessStatusCode, await category.Content.ReadAsStringAsync());
        var categoryId = (await category.Content.ReadFromJsonAsync<IdStub>()).Id;

        foreach (var (code, assetName, origin, initial) in new[]
                 {
                     ("FA-001", "笔记本电脑", 12000m, 0m),
                     ("FA-002", "打印机", 6000m, 600m),
                 })
        {
            var created = await _h.Client.PostAsJsonAsync(
                $"{ApiHarness.ResourcePath(accountSetId, "fixedassets")}/assets", new
                {
                    code,
                    categoryId,
                    name = assetName,
                    model = "型号X",
                    dept = "行政部",
                    startDate = "2026-01-01",
                    originValue = origin,
                    initialAccumulatedDepreciation = initial,
                });
            Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        }

        return (accountSetId, categoryId);
    }

    /// <summary>建账套 + 两名员工 + 1-3 月工资单（不同部门，让统计表有多行）。</summary>
    private async Task<(Guid AccountSetId, Guid EmployeeId)> SeedSalaryAsync(string name)
    {
        var accountSetId = await _h.CreateAccountSetAsync(name);
        var zhang = await CreateEmployeeAsync(accountSetId, "E001", "张三", "销售部");
        var li = await CreateEmployeeAsync(accountSetId, "E002", "李四", "行政部");

        foreach (var period in new[] { 202601, 202602, 202603 })
        {
            await SaveBillAsync(accountSetId, period, zhang, 8000m);
            await SaveBillAsync(accountSetId, period, li, 6000m);
        }

        return (accountSetId, zhang);
    }

    private async Task<Guid> CreateEmployeeAsync(Guid accountSetId, string code, string name, string dept)
    {
        var response = await _h.Client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "salary")}/employees",
            new { code, name, gender = 1, idType = 1, dept, status = 1 });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<IdStub>()).Id;
    }

    private async Task SaveBillAsync(Guid accountSetId, int period, Guid employeeId, decimal gross)
    {
        var response = await _h.Client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "salary")}/bills", new
            {
                period,
                employeeId,
                gross,
                deductible = gross * 0.1m,
                personalDeduction = gross * 0.105m,
                taxThreshold = 5000m,
                tax = 30m,
                adjust = 0m,
                companyPart = gross * 0.2m,
            });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>播一张日期落在 2026-01 的发票（direction 1 销项 / 2 进项）。</summary>
    private async Task SeedInvoiceAsync(Guid accountSetId, int direction, string invoiceNo)
    {
        var response = await _h.Client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "invoices")}?direction={direction}", new
            {
                invoiceType = "增值税专用发票",
                inputDate = "2026-01-10",
                issueDate = "2026-01-10",
                invoiceNo,
                partnerName = direction == 2 ? "乙供应商" : "甲公司",
                lines = new[] { new { goodsName = "服务", amount = 1000m, taxRate = 6m, taxAmount = 60m } },
            });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    // —— 断言 ——

    /// <summary>打印端点的共同契约：200 + application/pdf + %PDF 头 + 文件名含标题、账套名与期间。</summary>
    private static async Task AssertPdfAsync(HttpResponseMessage response, string title, string accountSetName, params string[] fileNameParts)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var fileName = FileNameOf(response);
        Assert.Contains(title, fileName);
        Assert.Contains(accountSetName, fileName);
        foreach (var part in fileNameParts) Assert.Contains(part, fileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 1_000, $"PDF 体积异常偏小：{bytes.Length} 字节");
    }

    /// <summary>取 Content-Disposition 里的文件名（优先 RFC 5987 的 filename*=，回退正则解）。</summary>
    private static string FileNameOf(HttpResponseMessage response)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        return disposition?.FileNameStar
               ?? Uri.UnescapeDataString(System.Text.RegularExpressions.Regex.Match(
                   disposition?.ToString() ?? string.Empty,
                   "filename\\*=UTF-8''([^;]+)",
                   System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value);
    }

    /// <summary>把端点产出的 PDF 落盘到 KUAIJI_PDF_ARTIFACTS 指定目录，便于肉眼核对版式（CI 不写文件）。</summary>
    private void WriteArtifacts(string kind, HttpResponseMessage response)
    {
        var dir = Environment.GetEnvironmentVariable("KUAIJI_PDF_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(dir)) return;

        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, $"{kind}.pdf"), response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
    }

    private sealed record IdStub(Guid Id);

    public void Dispose() => _h.Dispose();
}
