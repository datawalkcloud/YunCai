using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 反结账的 HTTP 集成测试：可以从任意已结期间起整体回退到目标月的上一个月，
/// 结转进度保持不变；未结账的期间不能作为反结账起点。
/// </summary>
public sealed class ClosingReopenApiTests : IDisposable
{
    private readonly ApiHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Reopen_FromEarlierClosedPeriod_RollsBackAllLaterPeriods()
    {
        var accountSetId = await _h.CreateAccountSetAsync("反结账任意位置");
        using (var db = _h.NewContext())
        {
            var accountSet = db.AccountSets.Single(s => s.Id == accountSetId);
            foreach (var month in new[] { 1, 2, 3 })
            {
                accountSet.MarkCarried(2026, month);
                accountSet.ClosePeriod(2026, month);
            }

            db.SaveChanges();
        }

        var response = await _h.Client.PostAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/reopen?year=2026&month=2", null);
        response.EnsureSuccessStatusCode();
        var status = await response.Content.ReadFromJsonAsync<ClosingStatusStub>();

        Assert.Equal("2026-01", status!.ClosedPeriod);
        Assert.Equal("2026-03", status.CarriedPeriod);
    }

    [Fact]
    public async Task Reopen_PeriodNotYetClosed_ReturnsBadRequest()
    {
        var accountSetId = await _h.CreateAccountSetAsync("反结账未结期间");
        using (var db = _h.NewContext())
        {
            var accountSet = db.AccountSets.Single(s => s.Id == accountSetId);
            accountSet.MarkCarried(2026, 1);
            accountSet.ClosePeriod(2026, 1);
            db.SaveChanges();
        }

        var response = await _h.Client.PostAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "closing")}/reopen?year=2026&month=2", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("尚未结账", await response.Content.ReadAsStringAsync());
    }

    private sealed record ClosingStatusStub(string? ClosedPeriod, string? CarriedPeriod);
}
