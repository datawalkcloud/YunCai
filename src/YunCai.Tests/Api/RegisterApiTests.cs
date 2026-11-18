using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using YunCai.Tests.Support;
using Xunit;

namespace YunCai.Tests.Api;

/// <summary>
/// 手机号 + 密码自助注册：注册即登录、重复手机号拒绝、密码强度沿用统一策略。
/// 与验证码首次登录自动建号的区别是不发短信、一步带密码。
/// </summary>
public sealed class RegisterApiTests : IDisposable
{
    private readonly ApiTestFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Register_CreatesPasswordAccountAndSignsIn()
    {
        const string phone = "13900000101";
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/Auth/register",
            new { phone, password = "Register123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResultStub>();
        Assert.False(string.IsNullOrWhiteSpace(result!.Token));
        Assert.Equal(phone, result.User.Username);
        Assert.Equal(phone, result.User.Phone);
        Assert.True(result.User.HasPassword);

        // 注册返回的令牌可直接访问受保护端点。
        using var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.Token);
        Assert.Equal(HttpStatusCode.OK, (await authed.GetAsync("/Auth/me")).StatusCode);

        // 之后也能用手机号 + 密码登录。
        var login = await client.PostAsJsonAsync(
            "/Auth/login",
            new { username = phone, password = "Register123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicatePhoneRejected()
    {
        const string phone = "13900000102";
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync(
            "/Auth/register",
            new { phone, password = "Register123" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/Auth/register",
            new { phone, password = "OtherPass456" });

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("该手机号已注册", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_WeakPasswordRejected()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/Auth/register",
            new { phone = "13900000103", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("密码至少 8 位", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_InvalidPhoneRejected()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/Auth/register",
            new { phone = "12345", password = "Register123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("手机号格式不正确", await response.Content.ReadAsStringAsync());
    }

    private sealed record UserStub(
        string Id,
        string Username,
        string DisplayName,
        string? Phone,
        bool HasPassword);

    private sealed record LoginResultStub(string Token, UserStub User);
}
