using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YunCai.Modules.Auditing.Domain;

namespace YunCai.Modules.Auditing.Infrastructure
{
    /// <summary>
    /// 实体级审计拦截器：与业务写入同一次 SaveChanges 落库，
    /// 记录是谁对哪个账套的哪条记录做了什么（含脱敏后的改前 / 改后快照）。
    /// 没有登录上下文的写入（迁移、播种、后台任务）不记；单次超过 50 条时只记汇总，避免导入撑爆日志。
    /// </summary>
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private const int BulkThreshold = 50;

        // 属性名命中这些片段时按敏感字段脱敏，避免密码 / 密钥进审计表。
        private static readonly string[] SensitiveFragments =
            ["password", "passwordhash", "secret", "cipher", "token", "apikey", "privatekey"];

        private static readonly Dictionary<string, string> ModuleByType = new(StringComparer.Ordinal)
        {
            ["Voucher"] = "凭证",
            ["VoucherEntry"] = "凭证",
            ["VoucherAttachment"] = "凭证",
            ["Journal"] = "资金",
            ["Bill"] = "资金",
            ["Transfer"] = "资金",
            ["EntryCategory"] = "资金",
            ["Invoice"] = "发票",
            ["ExpenseBill"] = "费用",
            ["SalaryBill"] = "工资",
            ["Employee"] = "工资",
            ["FixedAsset"] = "资产",
            ["ArchiveItem"] = "电子档案",
            ["ArchiveCategory"] = "电子档案",
            ["AccountSetMember"] = "账套设置",
            ["AccountSetRole"] = "账套设置",
            ["AccountSet"] = "账套设置",
            ["Currency"] = "账套设置",
            ["VoucherWord"] = "账套设置",
            ["Budget"] = "预算",
            ["BudgetLine"] = "预算",
        };

        private readonly IHttpContextAccessor _http;

        public AuditSaveChangesInterceptor(IHttpContextAccessor http)
        {
            _http = http;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Record(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Record(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void Record(DbContext? context)
        {
            var http = _http.HttpContext;
            var user = http?.User;
            var userId = user?.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (context is null || http is null || string.IsNullOrEmpty(userId) || user?.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => e.Entity is not OperationLog)
                .Where(e => e.State != EntityState.Modified || e.Properties.Any(p => p.IsModified))
                .ToList();
            if (entries.Count == 0)
            {
                return;
            }

            var accountSetId = ResolveAccountSetId(entries, http);
            if (accountSetId is not { } setId)
            {
                return;
            }

            var userName = user!.FindFirstValue("displayName")
                ?? user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                ?? userId;
            var ip = http.Connection.RemoteIpAddress?.ToString();

            if (entries.Count > BulkThreshold)
            {
                var added = entries.Count(e => e.State == EntityState.Added);
                var modified = entries.Count(e => e.State == EntityState.Modified);
                var deleted = entries.Count(e => e.State == EntityState.Deleted);
                context.Add(OperationLog.Create(
                    setId, userId, userName, "数据变更", "批量",
                    $"批量数据变更：新增 {added} 条、修改 {modified} 条、删除 {deleted} 条",
                    ipAddress: ip));
                return;
            }

            foreach (var entry in entries)
            {
                var entityType = entry.Metadata.ClrType.Name;
                var entityId = entry.Properties
                    .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                    ?.CurrentValue?.ToString();
                var before = entry.State == EntityState.Added ? null : Snapshot(entry, useOriginal: true);
                var after = entry.State == EntityState.Deleted ? null : Snapshot(entry, useOriginal: false);
                var verb = entry.State switch
                {
                    EntityState.Added => "新增",
                    EntityState.Modified => "修改",
                    _ => "删除"
                };
                var module = ModuleByType.TryGetValue(entityType, out var name) ? name : entityType;
                context.Add(OperationLog.Create(
                    setId,
                    userId,
                    userName,
                    "数据变更",
                    module,
                    $"{verb}{module}记录 {entityType}",
                    entityType,
                    entityId,
                    before,
                    after,
                    ip));
            }
        }

        private static Guid? ResolveAccountSetId(IReadOnlyList<EntityEntry> entries, HttpContext http)
        {
            if (http.Request.RouteValues.TryGetValue("accountSetId", out var raw)
                && Guid.TryParse(raw?.ToString(), out var routeId))
            {
                return routeId;
            }

            foreach (var entry in entries)
            {
                var property = entry.Metadata.FindProperty("AccountSetId");
                if (property is not null)
                {
                    var value = entry.State == EntityState.Deleted
                        ? entry.Property("AccountSetId").OriginalValue
                        : entry.Property("AccountSetId").CurrentValue;
                    if (value is Guid id && id != Guid.Empty)
                    {
                        return id;
                    }
                }

                // 账套实体自身：主键就是账套标识。
                if (entry.Metadata.ClrType.Name == "AccountSet")
                {
                    var pk = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue;
                    if (pk is Guid setId && setId != Guid.Empty)
                    {
                        return setId;
                    }
                }
            }

            return null;
        }

        private static string Snapshot(EntityEntry entry, bool useOriginal)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in entry.Properties)
            {
                if (property.Metadata.IsPrimaryKey() && !property.Metadata.IsForeignKey())
                {
                    values[property.Metadata.Name] = property.CurrentValue;
                    continue;
                }

                if (entry.State == EntityState.Modified && !property.IsModified)
                {
                    continue;
                }

                var name = property.Metadata.Name;
                var value = useOriginal ? property.OriginalValue : property.CurrentValue;
                values[name] = IsSensitive(name) ? "***" : value;
            }

            return JsonSerializer.Serialize(values);
        }

        private static bool IsSensitive(string propertyName)
        {
            var lower = propertyName.ToLowerInvariant();
            return SensitiveFragments.Any(fragment => lower.Contains(fragment, StringComparison.Ordinal));
        }
    }
}
