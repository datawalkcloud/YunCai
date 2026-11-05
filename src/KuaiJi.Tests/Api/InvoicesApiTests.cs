using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace KuaiJi.Tests.Api;

/// <summary>
/// 发票 HTTP 集成测试：?direction= 是查询串里的裸 int，缺省时模型绑定给 0，
/// 而 InvoiceDirection 只有 1（销项）/ 2（进项）——0 落库后两侧列表都查不到，
/// 生成凭证还会静默按进项模板走（借贷方向与销项相反）。写入口必须直接拒掉。
/// </summary>
public sealed class InvoicesApiTests : IDisposable
{
    private readonly ApiHarness _h = new();
    private readonly HttpClient _client;

    public InvoicesApiTests() => _client = _h.Client;

    public void Dispose() => _h.Dispose();

    private static object SalesInvoiceBody(string invoiceNo) => new
    {
        invoiceType = "增值税专用发票",
        inputDate = "2026-01-10",
        issueDate = "2026-01-10",
        invoiceNo,
        partnerName = "甲公司",
        lines = new[] { new { goodsName = "服务", amount = 1000m, taxRate = 6m, taxAmount = 60m } },
    };

    [Fact]
    public async Task Create_MissingDirection_IsRejected()
    {
        var accountSetId = await _h.CreateAccountSetAsync("发票方向缺省");
        var response = await _client.PostAsJsonAsync(
            ApiHarness.ResourcePath(accountSetId, "invoices"), SalesInvoiceBody("NO-DIR"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("发票方向", await response.Content.ReadAsStringAsync());

        // 被拒的那张不得半落库：按两个方向都查不到它
        foreach (var direction in new[] { 1, 2 })
        {
            var list = await _client.GetAsync(
                $"{ApiHarness.ResourcePath(accountSetId, "invoices")}?direction={direction}");
            list.EnsureSuccessStatusCode();
            Assert.DoesNotContain("NO-DIR", await list.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Create_UnknownDirection_IsRejected()
    {
        var accountSetId = await _h.CreateAccountSetAsync("发票方向越界");
        var response = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "invoices")}?direction=9", SalesInvoiceBody("NO-NINE"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("发票方向", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_SalesDirection_AppearsOnlyInSalesList()
    {
        var accountSetId = await _h.CreateAccountSetAsync("发票销项");
        var created = await _client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "invoices")}?direction=1", SalesInvoiceBody("SALES-01"));
        Assert.True(created.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            await created.Content.ReadAsStringAsync());

        var sales = await _client.GetStringAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "invoices")}?direction=1");
        Assert.Contains("SALES-01", sales);
        var purchase = await _client.GetStringAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "invoices")}?direction=2");
        Assert.DoesNotContain("SALES-01", purchase);

        // 反向对照：删除后两张列表都不该再出现，否则上面的 Contains 可能只是恒真
        var body = await created.Content.ReadAsStringAsync();
        var id = System.Text.Json.JsonSerializer.Deserialize<InvoiceStub>(body, JsonWeb)!.Id;
        await _client.DeleteAsync($"{ApiHarness.ResourcePath(accountSetId, "invoices")}/{id}");
        Assert.DoesNotContain("SALES-01", await _client.GetStringAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "invoices")}?direction=1"));
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonWeb =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private sealed record InvoiceStub(Guid Id);
}
