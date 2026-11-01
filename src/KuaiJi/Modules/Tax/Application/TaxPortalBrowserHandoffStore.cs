using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace KuaiJi.Modules.Tax.Application
{
    /// <summary>
    /// 浏览器会话交接票据：网页持登录态时把服务器上已登录的税局 Cookie 换成短时票据，
    /// 本机助手用票据匿名取回并注入自己的浏览器，打开企业电子税务局时就已经是登录状态。
    /// 票据 2 分钟过期、只能兑换一次；Cookie 只留在服务端内存里等这次兑换。
    /// </summary>
    public sealed class TaxPortalBrowserHandoffStore
    {
        /// <summary>与网页上「换取票据」的倒计时共用这个有效期。</summary>
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

        private readonly ConcurrentDictionary<string, Entry> _tickets = new(StringComparer.Ordinal);

        /// <summary>为一份浏览器登录态签发票据；返回的票据交给网页，再转给本机助手。</summary>
        public string Issue(TaxPortalBrowserHandoffDto handoff)
        {
            PurgeExpired();
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            _tickets[token] = new Entry(handoff, DateTimeOffset.UtcNow.Add(Ttl));
            return token;
        }

        /// <summary>兑换并立即作废票据；过期或不存在时返回 null。</summary>
        public TaxPortalBrowserHandoffDto? Redeem(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || !_tickets.TryRemove(token, out var entry))
            {
                return null;
            }

            return entry.ExpiresAt > DateTimeOffset.UtcNow ? entry.Handoff : null;
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

        private sealed record Entry(TaxPortalBrowserHandoffDto Handoff, DateTimeOffset ExpiresAt);
    }
}
