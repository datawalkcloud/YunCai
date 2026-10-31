using System.Net.Http.Headers;
using KuaiJi.Modules.Identity.Domain;
using KuaiJi.Modules.Identity.Infrastructure;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KuaiJi.Tests.Support;

/// <summary>
/// HTTP 集成测试装置：跑真实的 <c>Program.cs</c> 组合根，认证、限流、模型绑定、
/// Problem Details 全部生效，只把数据库换成内存 SQLite（生产注册的最后一条生效）。
/// 打开播种以覆盖 DatabaseBootstrap 的初始化路径。
/// 每个实例一套独立库与宿主，测试类各自 new，用完即弃。
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    /// <summary>签名密钥仅测试用；生产环境缺失会直接启动失败，这里给足 32 位避免踩闸门。</summary>
    public const string TestJwtKey = "kuaiji-integration-test-signing-key-not-for-production";

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
            ["ConnectionStrings__DefaultConnection"] = "Host=localhost;Database=kuaiji_tests;Username=test;Password=test",
            ["Jwt__Key"] = TestJwtKey,
            ["KuaiJi__Database__AutoMigrate"] = "false",
            ["KuaiJi__Seed__Enabled"] = "true",
            ["KuaiJi__Seed__AdminUsername"] = SeededAdminUsername,
            ["KuaiJi__Seed__AdminPassword"] = SeededAdminPassword,
            ["KuaiJi__Networking__HttpsRedirection"] = "false",
            ["KuaiJi__Networking__UseHsts"] = "false",
            ["KuaiJi__Logging__File"] = "",
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

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
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
    public string CreateToken(string userId = "integration-test-user", string username = "tester", string displayName = "集成测试")
    {
        var user = new User { Id = userId, Username = username, DisplayName = displayName, PasswordHash = "unused" };
        return Services.GetRequiredService<ITokenService>().CreateToken(user).Token;
    }

    /// <summary>带 Bearer 令牌的客户端，用于访问受 <c>[Authorize]</c> 保护的端点。</summary>
    public HttpClient CreateAuthenticatedClient(string userId = "integration-test-user")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId));
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
