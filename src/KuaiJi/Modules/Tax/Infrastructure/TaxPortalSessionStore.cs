using System.Text.Json;
using KuaiJi.Modules.AccountSets.Application;
using KuaiJi.Modules.Tax.Application;
using KuaiJi.Modules.Tax.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KuaiJi.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 税局登录态的持久化存储：把 Playwright 的 storage state（Cookie + localStorage）加密后
    /// 按「账套 + 用户」各存一行到数据库，服务重启、重建容器、换服务器都能恢复登录，
    /// 用户不必重新收短信。税局自身让会话过期时才需要重新登录。
    /// </summary>
    public sealed class TaxPortalSessionStore
    {
        private readonly ITaxCredentialProtector _protector;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public TaxPortalSessionStore(ITaxCredentialProtector protector, IServiceScopeFactory scopeFactory)
        {
            _protector = protector;
            _scopeFactory = scopeFactory;
        }

        public async Task SaveAsync(
            TaxPortalSessionKey key,
            string storageState,
            string homeUrl,
            CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Serialize(new PersistedSession(storageState, homeUrl));
            var cipher = _protector.Encrypt(payload);
            var now = DateTimeOffset.UtcNow;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var record = await db.TaxPortalSessions.FindAsync(
                    new object[] { key.AccountSetId, key.UserId, key.Kind },
                    cancellationToken);
                if (record is null)
                {
                    db.TaxPortalSessions.Add(TaxPortalSessionRecord.Create(
                        key.AccountSetId, key.UserId, key.Kind, cipher, now));
                }
                else
                {
                    record.Refresh(cipher, now);
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<TaxPortalPersistedSession?> LoadAsync(
            TaxPortalSessionKey key,
            CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var record = await db.TaxPortalSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.AccountSetId == key.AccountSetId
                        && item.UserId == key.UserId
                        && item.PortalKind == key.Kind,
                    cancellationToken);
            if (record is null)
            {
                return null;
            }

            // 换过 Jwt:Key 时密文解不开，按「没保存过」处理，下一次登录会覆盖这一行。
            var payload = _protector.Decrypt(record.PayloadCipher);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                var saved = JsonSerializer.Deserialize<PersistedSession>(payload);
                return saved is null || string.IsNullOrWhiteSpace(saved.StorageState)
                    ? null
                    : new TaxPortalPersistedSession(saved.StorageState, saved.HomeUrl, record.SavedAt);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task DeleteAsync(TaxPortalSessionKey key)
        {
            await _gate.WaitAsync();
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.TaxPortalSessions
                    .Where(item => item.AccountSetId == key.AccountSetId
                        && item.UserId == key.UserId
                        && item.PortalKind == key.Kind)
                    .ExecuteDeleteAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        private sealed record PersistedSession(string StorageState, string HomeUrl);
    }

    /// <summary>从数据库恢复出来的一份税局登录态。</summary>
    public sealed record TaxPortalPersistedSession(string StorageState, string HomeUrl, DateTimeOffset SavedAt);
}
