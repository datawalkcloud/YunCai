using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KuaiJi.SharedKernel.Infrastructure.Api;

/// <summary>
/// 存活/就绪探针：确认数据库可连通。AppDbContext 是 Scoped，健康检查单例需临时开作用域取。
/// </summary>
public sealed class DatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("数据库连接正常")
            : HealthCheckResult.Unhealthy("数据库无法连接");
    }
}
