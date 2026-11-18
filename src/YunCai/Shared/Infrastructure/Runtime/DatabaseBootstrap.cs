using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.SharedKernel.Infrastructure.Runtime;

/// <summary>
/// 数据库结构迁移。失败一律向上抛出，让进程带着明确日志退出，
/// 而不是「迁移没成功也照常对外提供服务」。
/// </summary>
public static class DatabaseBootstrap
{
    /// <summary>命令行是否请求「只迁移不启动」，供部署时的一次性迁移任务使用。</summary>
    public static bool IsMigrateOnly(IReadOnlyList<string> args)
        => args.Contains("--migrate-only", StringComparer.OrdinalIgnoreCase);

    public static async Task MigrateAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsRelational())
        {
            return;
        }

        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("数据库迁移已应用到最新。");
    }

    /// <summary>
    /// 不迁移的进程（生产）启动前确认库结构已跟上程序版本。
    /// 库落后时 Postgres 只对查询点到的列报 42703，表现为 /health 正常而接口按路径随机 500，
    /// 所以这里宁可启动即失败，并把待应用的迁移点名出来。
    /// </summary>
    public static async Task EnsureSchemaUpToDateAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        // 只校验 PostgreSQL：集成测试用 SQLite + EnsureCreated 建库，不写迁移历史表，按此口径必然误判。
        if (!context.Database.IsNpgsql())
        {
            return;
        }

        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
        var names = pending.ToList();
        if (names.Count == 0)
        {
            return;
        }

        logger.LogError("数据库结构落后于当前程序版本，待应用迁移：{Migrations}", string.Join(", ", names));
        throw new InvalidOperationException(
            $"数据库有 {names.Count} 个迁移未应用（{string.Join(", ", names)}）。" +
            "先执行一次性迁移任务 dotnet YunCai.dll --migrate-only（Compose 的 migrate 服务），再启动接口进程。");
    }
}
