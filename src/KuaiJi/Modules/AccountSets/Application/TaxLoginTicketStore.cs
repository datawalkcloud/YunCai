using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace KuaiJi.Modules.AccountSets.Application
{
    /// <summary>
    /// 税局密码的一次性领取票据：网页持有登录态时把已保存的密码换成短时票据，
    /// 本机助手再用票据匿名换回明文。这样密码既不出现在 URL 里，也不经前端接口下发。
    /// 票据 2 分钟过期、只能兑换一次。
    /// </summary>
    public sealed class TaxLoginTicketStore
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

        private readonly ConcurrentDictionary<string, Entry> _tickets = new(StringComparer.Ordinal);

        /// <summary>为一份明文密码签发票据；返回的票据交给网页，再转给本机助手。</summary>
        public string Issue(string password)
        {
            PurgeExpired();
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            _tickets[token] = new Entry(password, DateTimeOffset.UtcNow.Add(Ttl));
            return token;
        }

        /// <summary>兑换并立即作废票据；过期或不存在时返回 null。</summary>
        public string? Redeem(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || !_tickets.TryRemove(token, out var entry))
            {
                return null;
            }

            return entry.ExpiresAt > DateTimeOffset.UtcNow ? entry.Password : null;
        }

        private void PurgeExpired()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _tickets)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    _tickets.TryRemove(pair.Key, out _);
                }
            }
        }

        private sealed record Entry(string Password, DateTimeOffset ExpiresAt);
    }
}
