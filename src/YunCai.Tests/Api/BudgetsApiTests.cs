using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>预算 HTTP 集成测试：权限矩阵（查看可读、编辑才可写）与执行对比取数。</summary>
public sealed class BudgetsApiTests : IDisposable
{
    private readonly ApiHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Budget_CrudAndExecution_RespectPermissions()
    {
        var accountSetId = await _h.CreateAccountSetAsync("预算接口");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var expense = _h.AddRootAccount(accountSetId, "6602", "管理费用",
            Modules.Accounting.Domain.AccountCategory.ProfitAndLoss);
        await _h.CreatePostedVoucherAsync(accountSetId, "2026-02-01", (expense, 500m, 0m), (cash, 0m, 500m));

        var months = new decimal[12];
        months[1] = 1000m;
        var created = await _h.Client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "budgets")}",
            new { year = 2026, name = "2026 年费用预算", lines = new[] { new { accountId = expense, months } } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var budget = await created.Content.ReadFromJsonAsync<BudgetStub>();
        Assert.NotNull(budget);

        var list = await _h.Client.GetFromJsonAsync<List<BudgetSummaryStub>>(
            $"{ApiHarness.ResourcePath(accountSetId, "budgets")}?year=2026");
        Assert.Single(list!);

        var execution = await _h.Client.GetFromJsonAsync<ExecutionStub>(
            $"{ApiHarness.ResourcePath(accountSetId, "budgets")}/{budget!.Id}/execution");
        Assert.Equal(1000m, execution!.BudgetToDate);
        Assert.Equal(500m, execution.ActualToDate);
        Assert.Equal(50m, execution.Rate);

        // 「查看」角色有 budget.view，只能读。
        var viewerPhone = "13900000011";
        var userResponse = await _h.Client.PostAsJsonAsync(
            "/users", new { displayName = "预算查看员", phone = viewerPhone, password = "ViewerPass123" });
        var viewerUser = await userResponse.Content.ReadFromJsonAsync<UserStub>();
        var roles = await _h.Client.GetFromJsonAsync<List<RoleStub>>(
            $"{ApiHarness.ResourcePath(accountSetId, "roles")}");
        var viewerRole = Assert.Single(roles!, r => r.Name == "查看");
        var member = await _h.Client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "members")}",
            new { name = "预算查看员", phone = viewerPhone, roleId = viewerRole.Id });
        Assert.Equal(HttpStatusCode.Created, member.StatusCode);

        var viewer = _h.Factory.CreateAuthenticatedClient(viewerUser!.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await viewer.GetAsync($"{ApiHarness.ResourcePath(accountSetId, "budgets")}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.PostAsJsonAsync(
                $"{ApiHarness.ResourcePath(accountSetId, "budgets")}",
                new { year = 2026, name = "越权预算", lines = new[] { new { accountId = expense, months } } })).StatusCode);

        var deleted = await _h.Client.DeleteAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "budgets")}/{budget.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    private sealed record BudgetStub(Guid Id, int Year, string Name);

    private sealed record BudgetSummaryStub(Guid Id, int Year, string Name, decimal Total);

    private sealed record ExecutionStub(Guid BudgetId, decimal BudgetToDate, decimal ActualToDate, decimal? Rate);

    private sealed record UserStub(string Id, string DisplayName);

    private sealed record RoleStub(Guid Id, string Name);
}
