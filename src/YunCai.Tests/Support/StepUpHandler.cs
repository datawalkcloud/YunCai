using System.Net.Http.Json;

namespace YunCai.Tests.Support;

/// <summary>
/// 集成测试里的「二次确认」：对写请求自动附带 X-Step-Up-Token，等价于前端 StepUpProvider
/// 收到 428 后走一次密码确认并复用令牌。令牌按服务端 5 分钟窗口缓存，提前 1 分钟续期。
/// 需要验证 428 分支的用例不要用这个 handler，直接 CreateAuthenticatedClient 即可。
/// </summary>
internal sealed class StepUpHandler : DelegatingHandler
{
    public const string HeaderName = "X-Step-Up-Token";

    private readonly Func<HttpClient> _authClientFactory;
    private readonly string _password;
    private string? _token;
    private DateTimeOffset _expiresAt;

    public StepUpHandler(Func<HttpClient> authClientFactory, string password)
    {
        _authClientFactory = authClientFactory;
        _password = password;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get && !request.Headers.Contains(HeaderName))
        {
            request.Headers.Add(HeaderName, await GetTokenAsync(cancellationToken));
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _token;
        }

        using var client = _authClientFactory();
        using var response = await client.PostAsJsonAsync(
            "/Auth/step-up",
            new { password = _password },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<StepUpStub>(cancellationToken);
        _token = body!.Token;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, body.ExpiresInSeconds - 60));
        return _token;
    }

    private sealed record StepUpStub(string Token, int ExpiresInSeconds);
}
