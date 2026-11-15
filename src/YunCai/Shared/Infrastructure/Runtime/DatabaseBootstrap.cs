using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Identity.Infrastructure;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace YunCai.SharedKernel.Infrastructure.Runtime;

/// <summary>
/// 数据库结构迁移与初始数据。失败一律向上抛出，让进程带着明确日志退出，
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
            "先执行一次性迁移任务 dotnet YunCai.dll --migrate-only（Compose 的 migrate 服务、K8s 的 migrate initContainer），再启动接口进程。");
    }

    public static async Task SeedAsync(AppDbContext context, RuntimeOptions options, bool isDevelopment, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (!options.Seed.Enabled)
        {
            return;
        }

        await SeedUsersAsync(context, options, isDevelopment, logger, cancellationToken);
        await SeedDefaultAccountSetAsync(context, logger, cancellationToken);
    }

    private static async Task SeedUsersAsync(AppDbContext context, RuntimeOptions options, bool isDevelopment, ILogger logger, CancellationToken cancellationToken)
    {
        var adminUsername = string.IsNullOrWhiteSpace(options.Seed.AdminUsername) ? "admin" : options.Seed.AdminUsername;
        var adminPhone = string.IsNullOrWhiteSpace(options.Seed.AdminPhone) ? null : options.Seed.AdminPhone.Trim();

        if (await context.Users.AnyAsync(cancellationToken))
        {
            // 存量库：补录管理员手机号（让手机号 + 密码入口可用），并确保初始账号拥有系统管理员身份。
            var existingAdmin = await context.Users.SingleOrDefaultAsync(u => u.Username == adminUsername, cancellationToken);
            if (existingAdmin is not null)
            {
                var changed = false;
                if (adminPhone is not null
                    && string.IsNullOrWhiteSpace(existingAdmin.Phone)
                    && !await context.Users.AnyAsync(u => u.Phone == adminPhone, cancellationToken))
                {
                    existingAdmin.Phone = adminPhone;
                    changed = true;
                }

                if (!existingAdmin.IsSystemAdmin)
                {
                    existingAdmin.PromoteToSystemAdmin();
                    changed = true;
                }

                if (changed)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    logger.LogInformation("已更新初始账号 {Username} 的手机号 / 系统管理员身份。", adminUsername);
                }
            }

            return;
        }

        var adminPassword = options.Seed.AdminPassword;

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "已启用数据初始化但未提供初始管理员口令。请设置环境变量 YunCai__Seed__AdminPassword，或关闭 YunCai__Seed__Enabled。");
            }

            adminPassword = "admin123";
            logger.LogWarning("未配置初始管理员口令，开发环境回退为 {Username}/{Password}，请勿用于生产。", adminUsername, adminPassword);
        }

        var admin = new User
        {
            Id = Guid.CreateVersion7().ToString("N"),
            Username = adminUsername,
            DisplayName = "管理员",
            PasswordHash = PasswordHasher.Hash(adminPassword),
            Phone = adminPhone
        };
        admin.PromoteToSystemAdmin();
        context.Users.Add(admin);

        // 演示用的第二账号只在开发环境创建，生产环境不留下可猜测的口令。
        if (isDevelopment)
        {
            context.Users.Add(new User
            {
                Id = Guid.CreateVersion7().ToString("N"),
                Username = "user",
                DisplayName = "普通用户",
                PasswordHash = PasswordHasher.Hash("user123")
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("已创建初始账号 {Username}。", adminUsername);
    }

    private static async Task SeedDefaultAccountSetAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        if (await context.AccountSets.AnyAsync(cancellationToken))
        {
            return;
        }

        // 科目、日记账、凭证都归属某一账套，没有账套时系统无法使用。
        var defaultSet = AccountSet.Create(
            "默认账套",
            "默认单位",
            AccountingStandard.Enterprise,
            TaxpayerType.General,
            "人民币",
            DateTime.UtcNow.Year,
            1);
        defaultSet.MarkAsCurrent();
        context.AccountSets.Add(defaultSet);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("已创建默认账套「{Name}」。", defaultSet.Name);
    }
}
