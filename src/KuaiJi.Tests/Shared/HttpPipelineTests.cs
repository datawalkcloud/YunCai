using System.Net;
using System.Net.Http.Json;
using KuaiJi.Tests.Support;
using Xunit;

namespace KuaiJi.Tests.Shared;

/// <summary>
/// HTTP 管道冒烟测试：验证集成装置真的跑起了生产组合根，并锁住运行时基线承诺——
/// JWT 保护、健康检查含数据库连通、Problem Details 中文业务文案、404 与 401 的状态码口径、
/// 登录成功与失败两条路径、启动播种出的默认账套可被查出。
/// </summary>
public sealed class HttpPipelineTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Health_Anonymous_ReturnsHealthyWithDatabaseReachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task AccountSets_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/AccountSets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccountSets_WithToken_ReturnsSeededDefaultAccountSetAsCurrent()
    {
        var client = _factory.CreateAuthenticatedClient();

        var sets = await client.GetFromJsonAsync<List<AccountSetSummary>>("/AccountSets");

        var seed = Assert.Single(sets!);
        Assert.Equal("默认账套", seed.Name);
        Assert.True(seed.IsCurrent);
        // 播种按当前年份建套，编码方案与凭证字取领域默认值。
        Assert.Equal(DateTime.UtcNow.Year, seed.StartYear);
        Assert.Equal(1, seed.StartMonth);
        Assert.Equal("4-2-2-2", seed.AccountCodingScheme);
        Assert.Contains("记", seed.VoucherWords);
    }

    [Fact]
    public async Task AccountSets_UnknownId_Returns404()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync($"/AccountSets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401AndChineseMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/Auth/login",
            new { username = ApiTestFactory.SeededAdminUsername, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("用户名或密码错误", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsTokenUsableOnProtectedEndpoint()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/Auth/login",
            new
            {
                username = ApiTestFactory.SeededAdminUsername,
                password = ApiTestFactory.SeededAdminPassword,
            });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var result = await login.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.False(string.IsNullOrWhiteSpace(result!.Token));
        Assert.Equal(ApiTestFactory.SeededAdminUsername, result.User.Username);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);

        // 令牌必须能直接用于受保护端点，否则说明签发与校验参数没对上。
        client.DefaultRequestHeaders.Authorization = new("Bearer", result.Token);
        var me = await client.GetFromJsonAsync<UserProfile>("/Auth/me");
        Assert.Equal(result.User.Id, me!.Id);
    }

    [Fact]
    public async Task AccountSets_DuplicateName_Returns400WithBusinessMessage()
    {
        var client = _factory.CreateAuthenticatedClient();

        var first = await client.PostAsJsonAsync("/AccountSets", new
        {
            name = "冒烟测试账套",
            companyName = "冒烟测试单位",
            standard = 1,
            taxpayerType = 1,
            baseCurrency = "人民币",
            startYear = 2026,
            startMonth = 1,
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/AccountSets", new
        {
            name = "冒烟测试账套",
            companyName = "另一家单位",
            standard = 1,
            taxpayerType = 1,
            baseCurrency = "人民币",
            startYear = 2026,
            startMonth = 1,
        });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("同名账套已存在", await duplicate.Content.ReadAsStringAsync());
    }

    private sealed record AccountSetSummary(
        Guid Id,
        string Name,
        bool IsCurrent,
        int StartYear,
        int StartMonth,
        string AccountCodingScheme,
        string VoucherWords);

    private sealed record LoginResponse(string Token, DateTime ExpiresAt, UserProfile User);

    private sealed record UserProfile(string Id, string Username, string DisplayName);
}
