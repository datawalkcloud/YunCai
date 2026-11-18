using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// P0 安全基线的 HTTP 集成测试：账套成员与权限点、系统管理员的账号生命周期
/// （停用 / 改密立即吊销令牌）、高危操作的二次确认，以及实体级审计留痕。
/// 每组用例独立账套 / 账号，互不影响。
/// </summary>
public sealed class SecurityApiTests : IDisposable
{
    private readonly ApiHarness _h = new();

    public void Dispose() => _h.Dispose();

    // —— 账套权限 ——

    [Fact]
    public async Task NonMember_CannotAccessAccountSetResources()
    {
        var accountSetId = await _h.CreateAccountSetAsync("越权访问");
        var (userId, _) = await CreateUserAsync("13900000001", "外部人员");

        var outsider = _h.Factory.CreateAuthenticatedClient(userId);
        var response = await outsider.GetAsync($"{ApiHarness.ResourcePath(accountSetId, "Vouchers")}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("无权访问该账套", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ViewerMember_CanReadButCannotWrite()
    {
        var accountSetId = await _h.CreateAccountSetAsync("只读成员");
        var (userId, _) = await CreateUserAsync("13900000002", "只读会计");
        await AddMemberAsync(accountSetId, "只读会计", "13900000002", "查看");

        var viewer = _h.Factory.CreateAuthenticatedClient(userId);

        var read = await viewer.GetAsync($"{ApiHarness.ResourcePath(accountSetId, "Vouchers")}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var write = await viewer.PostAsync(
            ApiHarness.ResourcePath(accountSetId, "Vouchers"),
            JsonContent(new
            {
                word = "记",
                date = "2026-02-15",
                entries = Array.Empty<object>(),
            }));

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Contains("需要权限", await write.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NonAdminMember_CannotCallAdminOnlyEndpoint()
    {
        var accountSetId = await _h.CreateAccountSetAsync("管理员专属");
        var (userId, _) = await CreateUserAsync("13900000003", "普通成员");
        await AddMemberAsync(accountSetId, "普通成员", "13900000003", "制单人");

        var members = await _h.Client.GetFromJsonAsync<List<MemberStub>>(
            $"{ApiHarness.ResourcePath(accountSetId, "members")}");
        var owner = Assert.Single(members!, m => m.Name == "管理员");

        var memberClient = _h.Factory.CreateAuthenticatedClient(userId);
        var response = await memberClient.DeleteAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "members")}/{owner.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("账套管理员权限", await response.Content.ReadAsStringAsync());
    }

    // —— 账号生命周期 ——

    [Fact]
    public async Task DisableUser_InvalidatesExistingToken()
    {
        var (userId, phone) = await CreateUserAsync("13900000004", "待停用账号", "InitPass123");

        var userClient = _h.Factory.CreateAuthenticatedClient(userId);
        Assert.Equal(HttpStatusCode.OK, (await userClient.GetAsync("/Auth/me")).StatusCode);

        var disable = await _h.Client.PutAsJsonAsync($"/users/{userId}/status", new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/Auth/me")).StatusCode);

        var login = await _h.Factory.CreateClient().PostAsJsonAsync(
            "/Auth/login",
            new { username = phone, password = "InitPass123" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RotatesTokenAndOldOneStopsWorking()
    {
        var (userId, phone) = await CreateUserAsync("13900000005", "改密账号", "InitPass123");
        var userClient = _h.Factory.CreateAuthenticatedClient(userId);

        var change = await userClient.PostAsJsonAsync(
            "/Auth/change-password",
            new { currentPassword = "InitPass123", newPassword = "NewPass456" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var payload = await change.Content.ReadFromJsonAsync<LoginResultStub>();
        Assert.False(string.IsNullOrWhiteSpace(payload!.Token));

        // 旧令牌因令牌版本自增立即失效，新令牌可继续访问。
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(payload.Token)).StatusCode);

        var login = await _h.Factory.CreateClient().PostAsJsonAsync(
            "/Auth/login",
            new { username = phone, password = "NewPass456" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    // —— 高危操作二次确认 ——

    [Fact]
    public async Task StepUp_WrongPasswordRejected_CorrectPasswordUnlocksDelete()
    {
        var accountSetId = await _h.CreateAccountSetAsync("二次确认");
        var client = _h.Factory.CreateAuthenticatedClient();

        var blocked = await client.DeleteAsync($"/AccountSets/{accountSetId}");
        Assert.Equal(HttpStatusCode.PreconditionRequired, blocked.StatusCode);
        Assert.Contains("step_up_required", await blocked.Content.ReadAsStringAsync());

        var wrong = await client.PostAsJsonAsync("/Auth/step-up", new { password = "not-the-password" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var stepUp = await client.PostAsJsonAsync(
            "/Auth/step-up",
            new { password = ApiTestFactory.SeededAdminPassword });
        Assert.Equal(HttpStatusCode.OK, stepUp.StatusCode);
        var stepUpToken = (await stepUp.Content.ReadFromJsonAsync<StepUpStub>())!.Token;

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/AccountSets/{accountSetId}");
        request.Headers.Add("X-Step-Up-Token", stepUpToken);
        var deleted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var remaining = await _h.Client.GetFromJsonAsync<List<AccountSetStub>>("/AccountSets");
        Assert.DoesNotContain(remaining!, s => s.Id == accountSetId);
    }

    // —— 审计留痕 ——

    [Fact]
    public async Task VoucherWrite_LeavesEntityLevelAuditRowWithSnapshot()
    {
        var accountSetId = await _h.CreateAccountSetAsync("审计留痕");
        var cash = _h.AddRootAccount(accountSetId, "1001", "库存现金");
        var expense = _h.AddRootAccount(accountSetId, "6602", "管理费用");

        await _h.CreatePostedVoucherAsync(accountSetId, "2026-03-01", (cash, 100m, 0m), (expense, 0m, 100m));

        var logs = await _h.Client.GetFromJsonAsync<List<OperationLogStub>>(
            $"{ApiHarness.ResourcePath(accountSetId, "operationlogs")}"
            + "?start=2026-01-01T00:00:00Z&end=2026-12-31T23:59:59Z");

        var voucherLog = Assert.Single(logs!, l => l.EntityType == "Voucher");
        Assert.Equal("凭证", voucherLog.Module);
        Assert.False(string.IsNullOrEmpty(voucherLog.EntityId));
        Assert.False(string.IsNullOrEmpty(voucherLog.AfterJson));
        Assert.Null(voucherLog.BeforeJson);
        // 写入发生在登录上下文中，审计行同时记录操作人（播种管理员）。
        Assert.Equal(ApiHarness.TokenDisplayName, voucherLog.UserName);
    }

    // —— 辅助 ——

    private async Task<(string UserId, string Phone)> CreateUserAsync(
        string phone,
        string displayName,
        string? password = null)
    {
        var response = await _h.Client.PostAsJsonAsync("/users", new { displayName, phone, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserStub>();
        return (user!.Id, phone);
    }

    private async Task AddMemberAsync(Guid accountSetId, string name, string phone, string roleName)
    {
        var roles = await _h.Client.GetFromJsonAsync<List<RoleStub>>(
            $"{ApiHarness.ResourcePath(accountSetId, "roles")}");
        var role = Assert.Single(roles!, r => r.Name == roleName);

        var response = await _h.Client.PostAsJsonAsync(
            $"{ApiHarness.ResourcePath(accountSetId, "members")}",
            new { name, phone, roleId = role.Id });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<HttpResponseMessage> MeAsync(string token)
    {
        using var client = _h.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync("/Auth/me");
    }

    private static HttpContent JsonContent(object body)
        => new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

    private sealed record UserStub(string Id, string DisplayName, string? Phone);

    private sealed record MemberStub(Guid Id, string Name, string Phone);

    private sealed record RoleStub(Guid Id, string Name);

    private sealed record AccountSetStub(Guid Id, string Name);

    private sealed record StepUpStub(string Token);

    private sealed record LoginResultStub(string Token);

    private sealed record OperationLogStub(
        string UserName,
        string Module,
        string? EntityType,
        string? EntityId,
        string? BeforeJson,
        string? AfterJson);
}
