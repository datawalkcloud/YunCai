using System.Net.Http.Headers;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Identity.Infrastructure;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace YunCai.Tests.Support;

/// <summary>
/// HTTP 集成测试装置：跑真实的 <c>Program.cs</c> 组合根，认证、限流、模型绑定、
/// Problem Details 全部生效，只把数据库换成内存 SQLite（生产注册的最后一条生效）。
/// 打开播种以覆盖 DatabaseBootstrap 的初始化路径。
/// 每个实例一套独立库与宿主，测试类各自 new，用完即弃。
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    /// <summary>签名密钥仅测试用；生产环境缺失会直接启动失败，这里给足 32 位避免踩闸门。</summary>
    public const string TestJwtKey = "yuncai-integration-test-signing-key-not-for-production";

    public const string SeededAdminUsername = "admin";
    public const string SeededAdminPassword = "integration-test-admin-password";

    private readonly SqliteConnection _connection;

    static ApiTestFactory()
    {
        // Program.cs 用 WebApplicationBuilder 建配置，WAF 的 ConfigureAppConfiguration 注不进去；
        // 它在建完默认源后又调 AddEnvironmentVariables()，所以环境变量是唯一能稳定盖过
        // appsettings*.json 的入口——本机有 appsettings.Local.json（真实连接串与密钥）、CI 没有，
        // 两边都必须能起来，测试不能依赖开发者本机机密文件。
        foreach (var (key, value) in new Dictionary<string, string>
        {
            // 连接串只为过启动闸门，真正的上下文由下面的 SQLite 注册接管。
            ["ConnectionStrings__DefaultConnection"] = "Host=localhost;Database=yuncai_tests;Username=test;Password=test",
            ["Jwt__Key"] = TestJwtKey,
            ["YunCai__Database__AutoMigrate"] = "false",
            ["YunCai__Seed__Enabled"] = "true",
            ["YunCai__Seed__AdminUsername"] = SeededAdminUsername,
            ["YunCai__Seed__AdminPassword"] = SeededAdminPassword,
            ["YunCai__Networking__HttpsRedirection"] = "false",
            ["YunCai__Networking__UseHsts"] = "false",
            ["YunCai__Logging__File"] = "",
            ["Serilog__MinimumLevel__Default"] = "Error",
        })
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public ApiTestFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Program.cs 在 Build() 之后立刻播种，表结构必须赶在宿主启动前建好，
        // 所以这里用一次性上下文建表，而不是等 ConfigureTestServices 里的注册生效。
        using var bootstrapping = new AppDbContext(SqliteOptions());
        bootstrapping.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Production 环境走真实管道（UseExceptionHandler + Problem Details），
        // 开发环境的 OpenAPI 端点由 DevelopmentApiTestFactory 单独覆盖。
        builder.UseEnvironment("Production");

        builder.ConfigureTestServices(services =>
        {
            // 生产的 AddDbContext 已经挂了 Npgsql provider，只追加一条 SQLite 注册会让 EF 抛
            // 「Only a single database provider can be registered」，必须先把上下文与其 Options 描述符摘干净。
            foreach (var descriptor in services.Where(d =>
                         d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType.FullName?.Contains("DbContextOptions") == true).ToList())
            {
                services.Remove(descriptor);
            }

            // 与生产一致：审计拦截器按请求作用域解析，实体级变更自动留痕。
            services.AddDbContext<AppDbContext>((sp, options) => options
                .UseSqlite(_connection)
                .AddInterceptors(
                    sp.GetRequiredService<YunCai.Modules.Auditing.Infrastructure.AuditSaveChangesInterceptor>()));
        });
    }

    private DbContextOptions<AppDbContext> SqliteOptions() =>
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;

    /// <summary>新开一个上下文等于一次新的请求作用域，用于直查数据库做断言或补充种子数据。</summary>
    public AppDbContext NewContext() => new(SqliteOptions());

    /// <summary>
    /// 签发访问令牌。走生产 <see cref="ITokenService"/>，顺带覆盖令牌签发链路；
    /// 不经 /Auth/login 也就不会消耗该端点的按 IP 限流额度。
    /// </summary>
    public string CreateToken(string userId)
    {
        // 鉴权时会按 sub 回查账号状态与令牌版本，令牌必须对应真实存在的启用账号。
        using var db = NewContext();
        var user = db.Users.AsNoTracking().Single(u => u.Id == userId);
        return Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    /// <summary>
    /// 带 Bearer 令牌的客户端，用于访问受 <c>[Authorize]</c> 保护的端点。
    /// 不指定用户时以播种管理员身份访问——账套访问校验按成员身份放行，
    /// 播种出的默认账套只有管理员是成员。
    /// 传 <paramref name="stepUpPassword"/> 时再挂一层自动二次确认（模拟前端 StepUpProvider），
    /// 写请求会带上 X-Step-Up-Token；验证 428 分支的用例不要传。
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string? userId = null, string? stepUpPassword = null)
    {
        // 先建客户端触发宿主启动与播种，账号才在库里。
        var client = CreateClient();
        userId ??= GetSeededAdminUserId();
        var token = CreateToken(userId);

        if (stepUpPassword is not null)
        {
            client.Dispose();
            client = CreateDefaultClient(new StepUpHandler(() =>
            {
                var inner = CreateClient();
                inner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return inner;
            }, stepUpPassword));
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>播种管理员的用户标识；先建客户端触发宿主启动与播种。</summary>
    public string GetSeededAdminUserId()
    {
        using var db = NewContext();
        return db.Users.Single(u => u.Username == SeededAdminUsername).Id;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
