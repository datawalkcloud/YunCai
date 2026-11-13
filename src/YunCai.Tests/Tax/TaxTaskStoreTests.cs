using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Tax.Application;
using YunCai.Modules.Tax.Domain;
using YunCai.Modules.Tax.Infrastructure;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace YunCai.Tests.Tax;

/// <summary>
/// 任务中心台账的测试：成功不留「解决建议」、失败把税局回的话带上；
/// 税款所属期与日期区间都按东八区自然日切，不能跟着服务器的系统时区跑；
/// 查询只回本账套的任务，按提交时间倒序。
/// </summary>
public sealed class TaxTaskStoreTests : IDisposable
{
    private static readonly Guid NoAccountSet = Guid.Empty;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TaxTaskStoreTests()
    {
        _connection.Open();
    }

    [Fact]
    public async Task Record_KeepsSuggestionOnlyForFailures()
    {
        using var host = CreateHost();
        var accountSetId = AddAccountSet(host);
        var key = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Enterprise);

        await host.Store.RecordAsync(key, "会计002", "查询税费种认定信息", true, null, Utc(2026, 11, 5), cancellationToken: CancellationToken.None);
        await host.Store.RecordAsync(key, "会计002", "查询税费种认定信息", false, "税局登录状态已过期，请重新登录电子税务局。", Utc(2026, 11, 6), cancellationToken: CancellationToken.None);

        var rows = (await host.Store.QueryAsync(accountSetId, null, null, CancellationToken.None)).Rows;

        Assert.Equal(2, rows.Count);
        // 时间倒序：后提交的那条排在最前面。
        Assert.Equal("failed", rows[0].Status);
        Assert.Equal("税局登录状态已过期，请重新登录电子税务局。", rows[0].Suggestion);
        Assert.Equal("success", rows[1].Status);
        Assert.Null(rows[1].Suggestion);
        Assert.Equal("会计002", rows[0].Operator);
    }

    [Fact]
    public async Task Record_WithoutPeriod_UsesTheBeijingMonthOfSubmission()
    {
        using var host = CreateHost();
        var accountSetId = AddAccountSet(host);
        var key = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Enterprise);

        // UTC 的 11-30 20:00 已是东八区 12-01 凌晨，所属期要跟着税局侧的日历走。
        await host.Store.RecordAsync(key, "会计002", "获取申报记录（2026年12月）", true, null, Utc(2026, 11, 30, 20), cancellationToken: CancellationToken.None);
        await host.Store.RecordAsync(key, "会计002", "获取申报记录（2026年10月）", true, null, Utc(2026, 11, 10), period: 202610, cancellationToken: CancellationToken.None);

        var rows = (await host.Store.QueryAsync(accountSetId, null, null, CancellationToken.None)).Rows;

        Assert.Equal(202612, rows.Single(row => row.Title.Contains("12月")).Period);
        Assert.Equal(202610, rows.Single(row => row.Title.Contains("10月")).Period);
    }

    [Fact]
    public async Task Query_FiltersByBeijingDayNotUtcDay()
    {
        using var host = CreateHost();
        var accountSetId = AddAccountSet(host);
        var key = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Enterprise);

        // 东八区 11-02 的 00:30，UTC 还停在 11-01。
        await host.Store.RecordAsync(key, "会计002", "跨零点提交的任务", true, null, Utc(2026, 11, 1, 16, 30), cancellationToken: CancellationToken.None);
        await host.Store.RecordAsync(key, "会计002", "当天早些提交的任务", true, null, Utc(2026, 11, 1, 1, 0), cancellationToken: CancellationToken.None);

        Assert.Equal(new[] { "跨零点提交的任务" }, RowTitles(await host.Store.QueryAsync(accountSetId, Day(2026, 11, 2), Day(2026, 11, 2), CancellationToken.None)));
        Assert.Equal(new[] { "当天早些提交的任务" }, RowTitles(await host.Store.QueryAsync(accountSetId, Day(2026, 11, 1), Day(2026, 11, 1), CancellationToken.None)));
        Assert.Equal(2, (await host.Store.QueryAsync(accountSetId, Day(2026, 11, 1), Day(2026, 11, 2), CancellationToken.None)).Total);
    }

    [Fact]
    public async Task Query_ReturnsOnlyTheRequestedAccountSet()
    {
        using var host = CreateHost();
        var mine = AddAccountSet(host);
        var others = AddAccountSet(host);

        await host.Store.RecordAsync(new TaxPortalSessionKey(mine, "user-1", TaxPortalKind.Enterprise), "会计002", "本账套的任务", true, null, Utc(2026, 11, 5), cancellationToken: CancellationToken.None);
        await host.Store.RecordAsync(new TaxPortalSessionKey(others, "user-1", TaxPortalKind.Enterprise), "会计002", "别的账套的任务", true, null, Utc(2026, 11, 5), cancellationToken: CancellationToken.None);

        var result = await host.Store.QueryAsync(mine, null, null, CancellationToken.None);

        Assert.Equal(new[] { "本账套的任务" }, RowTitles(result));
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task Record_WithoutAccountSet_OnlyLogsAndKeepsTaxOperationAlive()
    {
        using var host = CreateHost();
        var key = new TaxPortalSessionKey(NoAccountSet, "user-1", TaxPortalKind.Enterprise);

        // 账套外键不存在，写库必然失败；台账是旁路，不能把取数请求带崩，也不能抛回调用方。
        await host.Store.RecordAsync(key, "会计002", "查询税费种认定信息", true, null, Utc(2026, 11, 5), cancellationToken: CancellationToken.None);

        Assert.Equal(0, host.WithDb(db => db.TaxTasks.Count()));
    }

    [Fact]
    public void Ctor_ClipsOverlongTitleAndSuggestionAndFallsBackOnAnonymousUser()
    {
        var title = new string('长', TaxTaskRecord.MaxTitleLength + 20);
        var suggestion = new string('议', TaxTaskRecord.MaxSuggestionLength + 20);
        var failed = new TaxTaskRecord(Guid.NewGuid(), " user-1 ", "", 202611, title, TaxTaskStatus.Failed, suggestion, Utc(2026, 11, 5), Utc(2026, 11, 5));

        Assert.Equal(TaxTaskRecord.MaxTitleLength, failed.Title.Length);
        Assert.Equal(TaxTaskRecord.MaxSuggestionLength, failed.Suggestion!.Length);
        Assert.Equal("未知用户", failed.UserName);
        Assert.Equal("user-1", failed.UserId);
    }

    [Fact]
    public async Task Delete_RemovesTheRunningRowSoUnexecutedJobsLeaveNoTrace()
    {
        using var host = CreateHost();
        var accountSetId = AddAccountSet(host);
        var key = new TaxPortalSessionKey(accountSetId, "user-1", TaxPortalKind.Iit);
        var id = Guid.NewGuid();

        await host.Store.StartAsync(id, key, "会计002", "获取个税申报记录", Utc(2026, 11, 2), cancellationToken: CancellationToken.None);
        Assert.Equal("running", (await host.Store.FindAsync(id, accountSetId, CancellationToken.None))!.Status);

        await host.Store.DeleteAsync(id, CancellationToken.None);

        Assert.Null(await host.Store.FindAsync(id, accountSetId, CancellationToken.None));
        Assert.Empty((await host.Store.QueryAsync(accountSetId, null, null, CancellationToken.None)).Rows);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 9, int minute = 0)
        => new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static string[] RowTitles(TaxTasksResultDto result)
        => result.Rows.Select(row => row.Title).ToArray();

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

    private StoreHost CreateHost() => new(_connection);

    private sealed class StoreHost : IDisposable
    {
        private readonly ServiceProvider _provider;

        public StoreHost(SqliteConnection connection)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
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
