using KuaiJi.Modules.AccountSets.Application;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Tax.Application;
using KuaiJi.Modules.Tax.Infrastructure;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KuaiJi.Tests.Tax;

/// <summary>
/// 个税取数后台任务与任务中心台账的衔接（<see cref="TaxIitFetchJobRunner"/>）。
///
/// 锁死一条口径：任务中心只留真正向税局发起过的操作。提交时会先挂一行「进行中」，
/// 如果这一趟连税局都没进去（缺登录态），记录要撤掉，但内存快照仍在，
/// 页面轮询还得靠 needLogin 去弹「个税申报登录」。
/// 后台线程与测试线程会同时读写台账，所以这里用文件库 + 每上下文一条连接，
/// 不能像 <see cref="TaxTaskStoreTests"/> 那样共用一条内存连接。
/// </summary>
public sealed class TaxIitFetchJobRunnerTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kuaiji-job-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task NeedLoginJob_LeavesNoLedgerRowButStillReportsNeedLogin()
    {
        using var host = CreateHost();
        var accountSetId = AddAccountSet(host);
        var key = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Iit);
        var runner = new TaxIitFetchJobRunner(host.Store, NullLogger<TaxIitFetchJobRunner>.Instance);

        var submitted = await runner.SubmitAsync(
            key, "会计002", "获取个税申报记录",
            _ => Task.FromResult<IIitFetchResult>(
                new StubResult(needLogin: true, message: "个税登录状态已过期，请先登录自然人电子税务局。")),
            CancellationToken.None);

        var finished = await WaitForFinishedAsync(runner, key, submitted);
        Assert.Equal("failed", finished.Status);
        Assert.True(finished.NeedLogin);
        Assert.Equal("个税登录状态已过期，请先登录自然人电子税务局。", finished.Message);

        // 页面拿完 needLogin 之后，台账里不该留下这一行：等后台把它撤掉。
        await PollAsync(
            async () => (await host.Store.QueryAsync(accountSetId, null, null, CancellationToken.None)).Rows.Count == 0,
            "任务中心仍然留着这条未执行的记录");
    }

    [Fact]
    public async Task ExecutedJob_BackfillsTheLedgerRowItReservedAtSubmit()
    {
        using var host = CreateHost();
        var accountSetId = AddAccountSet(host);
        var key = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Iit);
        var runner = new TaxIitFetchJobRunner(host.Store, NullLogger<TaxIitFetchJobRunner>.Instance);
        var started = new TaskCompletionSource();

        var submitted = await runner.SubmitAsync(
            key, "会计002", "获取个税申报记录",
            async _ =>
            {
                // 卡在这里，好让测试先看一眼提交时挂上的那行「进行中」。
                await started.Task;
                return new StubResult(rows: 3);
            },
            CancellationToken.None);
        var id = Guid.Parse(WithDashes(submitted.Key));

        var reserved = await host.Store.FindAsync(id, accountSetId, CancellationToken.None);
        started.SetResult();
        Assert.Equal("running", reserved!.Status);
        Assert.Equal("获取个税申报记录", reserved.Title);
        Assert.Equal("会计002", reserved.Operator);

        Assert.Equal("success", (await WaitForFinishedAsync(runner, key, submitted)).Status);

        // 回填发生在内存快照之后，轮询台账直到那一行也变成成功。
        await PollAsync(
            async () => (await host.Store.FindAsync(id, accountSetId, CancellationToken.None))?.Status == "success",
            "台账里的任务没有回填成成功");

        var finished = await host.Store.FindAsync(id, accountSetId, CancellationToken.None);
        Assert.Null(finished!.Suggestion);
        Assert.NotEqual(default, finished.FinishedAt);
    }

    private static async Task<TaxIitJobDto> WaitForFinishedAsync(
        TaxIitFetchJobRunner runner, TaxPortalSessionKey key, TaxIitJobDto submitted)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var job = await runner.GetAsync(key, Guid.Parse(WithDashes(submitted.Key)), CancellationToken.None);
            if (job.Status != "running")
            {
                return job;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("后台任务没有在预期时间内结束");
    }

    /// <summary>撤登记与回填都排在内存快照之后，轮询台账直到条件成立。</summary>
    private static async Task PollAsync(Func<Task<bool>> condition, string becauseNotMet)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException(becauseNotMet);
    }

    private static string WithDashes(string hex)
        => $"{hex[..8]}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex[20..]}";

    private Guid AddAccountSet(StoreHost host) => host.WithDb(db =>
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

    private StoreHost CreateHost() => new($"DataSource={_databasePath}");

    /// <summary>替身取数结果：只关心行数、登录态与说明三件事。</summary>
    private sealed class StubResult(int rows = 0, bool needLogin = false, string? message = null) : IIitFetchResult
    {
        public int RowCount => rows;

        public bool NeedLogin => needLogin;

        public string? Message => message;
    }

    private sealed class StoreHost : IDisposable
    {
        private readonly ServiceProvider _provider;

        public StoreHost(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            // 传连接字符串而不是连接对象：EF 会为每个 DbContext 各开一条连接，后台线程才不会被测试线程关掉连接。
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
            services.AddSingleton<TaxTaskStore>();
            _provider = services.BuildServiceProvider();

            using var scope = _provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        }

        public TaxTaskStore Store => _provider.GetRequiredService<TaxTaskStore>();

        public T WithDb<T>(Func<AppDbContext, T> action)
        {
            using var scope = _provider.CreateScope();
            return action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public void Dispose() => _provider.Dispose();
    }
}
