using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Identity.Domain;
using KuaiJi.Modules.Identity.Infrastructure;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.SharedKernel.Infrastructure.Runtime;

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
        if (await context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var adminUsername = string.IsNullOrWhiteSpace(options.Seed.AdminUsername) ? "admin" : options.Seed.AdminUsername;
        var adminPassword = options.Seed.AdminPassword;

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "已启用数据初始化但未提供初始管理员口令。请设置环境变量 KuaiJi__Seed__AdminPassword，或关闭 KuaiJi__Seed__Enabled。");
            }

            adminPassword = "admin123";
            logger.LogWarning("未配置初始管理员口令，开发环境回退为 {Username}/{Password}，请勿用于生产。", adminUsername, adminPassword);
        }

        context.Users.Add(new User
        {
            Id = "1",
            Username = adminUsername,
            DisplayName = "管理员",
            PasswordHash = PasswordHasher.Hash(adminPassword)
        });

        // 演示用的第二账号只在开发环境创建，生产环境不留下可猜测的口令。
        if (isDevelopment)
        {
            context.Users.Add(new User
            {
                Id = "2",
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
