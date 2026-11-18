using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.AccountSets.Infrastructure;
using YunCai.Modules.Tax.Application;
using YunCai.Modules.Tax.Infrastructure;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace YunCai.Tests.Tax;

/// <summary>
/// 税局登录态入库的测试：按「账套 + 用户 + 税局通道」各存一行，库里只有加密后的密文，
/// 换过密钥（密文解不开）时按「没保存过」处理而不是抛异常，退出时能删干净。
/// </summary>
public sealed class TaxPortalSessionStoreTests : IDisposable
{
    private const string HomeUrl = "https://etax.zhejiang.chinatax.gov.cn:8443/";
    private const string StorageState =
        "{\"cookies\":[{\"name\":\"SESSION\",\"value\":\"abc123\"}],\"origins\":[{\"origin\":\"https://etax.zhejiang.chinatax.gov.cn:8443\",\"localStorage\":[]}]}";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TaxPortalSessionStoreTests()
    {
        _connection.Open();
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsAndStoresOnlyCiphertext()
    {
        using var host = CreateHost("key-a");
        var key = new TaxPortalSessionKey(AddAccountSet(host), "user-1", TaxPortalKind.Enterprise);

        await host.Store.SaveAsync(key, StorageState, HomeUrl, CancellationToken.None);
        var loaded = await host.Store.LoadAsync(key, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(StorageState, loaded!.StorageState);
        Assert.Equal(HomeUrl, loaded.HomeUrl);

        var cipher = host.WithDb(db => db.TaxPortalSessions.Single().PayloadCipher);
        Assert.DoesNotContain("SESSION", cipher);
        Assert.DoesNotContain("chinatax", cipher);
    }

    [Fact]
    public async Task SaveAgain_UpdatesTheSameRow()
    {
        using var host = CreateHost("key-a");
        var key = new TaxPortalSessionKey(AddAccountSet(host), "user-1", TaxPortalKind.Enterprise);
        const string refreshed = "{\"cookies\":[{\"name\":\"SESSION\",\"value\":\"refreshed\"}]}";

        await host.Store.SaveAsync(key, StorageState, HomeUrl, CancellationToken.None);
        await host.Store.SaveAsync(key, refreshed, HomeUrl, CancellationToken.None);

        Assert.Equal(1, host.WithDb(db => db.TaxPortalSessions.Count()));
        Assert.Equal(refreshed, (await host.Store.LoadAsync(key, CancellationToken.None))!.StorageState);
    }

    [Fact]
    public async Task Load_ForKeyOfAnotherAccountSetOrUser_IsNull()
    {
        using var host = CreateHost("key-a");
        var accountSetId = AddAccountSet(host);
        var otherAccountSetId = AddAccountSet(host);
        var saved = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Enterprise);

        await host.Store.SaveAsync(saved, StorageState, HomeUrl, CancellationToken.None);

        Assert.Null(await host.Store.LoadAsync(new TaxPortalSessionKey(accountSetId, "user-2", TaxPortalKind.Enterprise), CancellationToken.None));
        Assert.Null(await host.Store.LoadAsync(new TaxPortalSessionKey(otherAccountSetId, "user-1", TaxPortalKind.Enterprise), CancellationToken.None));
    }

    [Fact]
    public async Task Save_ForEachPortalKind_KeepsSeparateRows()
    {
        using var host = CreateHost("key-a");
        var accountSetId = AddAccountSet(host);
        var enterprise = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Enterprise);
        var iit = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Iit);
        const string iitState = "{\"cookies\":[{\"name\":\"IIT\",\"value\":\"xyz789\"}],\"origins\":[]}";

        await host.Store.SaveAsync(enterprise, StorageState, HomeUrl, CancellationToken.None);
        await host.Store.SaveAsync(iit, iitState, "https://etax.chinatax.gov.cn/", CancellationToken.None);

        // 同一账套同一用户的两套登录态必须各存一行，否则个税登录会把企业门户的存档顶掉。
        Assert.Equal(2, host.WithDb(db => db.TaxPortalSessions.Count()));
        Assert.Equal(StorageState, (await host.Store.LoadAsync(enterprise, CancellationToken.None))!.StorageState);
        Assert.Equal(iitState, (await host.Store.LoadAsync(iit, CancellationToken.None))!.StorageState);

        await host.Store.DeleteAsync(iit);
        Assert.Null(await host.Store.LoadAsync(iit, CancellationToken.None));
        Assert.NotNull(await host.Store.LoadAsync(enterprise, CancellationToken.None));
    }

    [Fact]
    public async Task Load_WithDifferentKey_ReturnsNullInsteadOfThrowing()
    {
        using var writer = CreateHost("key-old");
        var key = new TaxPortalSessionKey(AddAccountSet(writer), "user-1", TaxPortalKind.Enterprise);
        await writer.Store.SaveAsync(key, StorageState, HomeUrl, CancellationToken.None);

        // 换了 Jwt:Key 后旧密文解不开，应当按「没保存过」处理。
        using var reader = CreateHost("key-new");
        Assert.Null(await reader.Store.LoadAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_RemovesPersistedState()
    {
        using var host = CreateHost("key-a");
        var key = new TaxPortalSessionKey(AddAccountSet(host), "user-1", TaxPortalKind.Enterprise);

        await host.Store.SaveAsync(key, StorageState, HomeUrl, CancellationToken.None);
        await host.Store.DeleteAsync(key);

        Assert.Null(await host.Store.LoadAsync(key, CancellationToken.None));
        Assert.Equal(0, host.WithDb(db => db.TaxPortalSessions.Count()));
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private Guid AddAccountSet(StoreHost host)
    {
        return host.WithDb(db =>
        {
            var accountSet = AccountSet.Create(
                "测试账套-" + Guid.NewGuid().ToString("N"),
                "测试单位",
                AccountingStandard.Enterprise,
                TaxpayerType.General,
                "人民币",
                2026,
                1);
            db.AccountSets.Add(accountSet);
            db.SaveChanges();
            return accountSet.Id;
        });
    }

    private StoreHost CreateHost(string jwtKey)
    {
        return new StoreHost(_connection, jwtKey);
    }

    /// <summary>每次新建一个容器，模拟服务重启或换密钥后的独立密钥环境。</summary>
    private sealed class StoreHost : IDisposable
    {
        private readonly ServiceProvider _provider;

        public StoreHost(SqliteConnection connection, string jwtKey)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = jwtKey
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            services.AddSingleton<ITaxCredentialProtector, AesTaxCredentialProtector>();
            services.AddSingleton<TaxPortalSessionStore>();
            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        }

        public TaxPortalSessionStore Store => _provider.GetRequiredService<TaxPortalSessionStore>();

        public T WithDb<T>(Func<AppDbContext, T> action)
        {
            using var scope = _provider.CreateScope();
            return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public void Dispose()
        {
            _provider.Dispose();
        }
    }
}
