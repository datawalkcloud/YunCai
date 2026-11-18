using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Identity.Infrastructure;
using YunCai.SharedKernel.Infrastructure.Security;

namespace YunCai.Modules.Identity.Application
{
    /// <summary>
    /// 高危操作二次确认：要求当前登录用户重新提供密码（已设密）或手机验证码，
    /// 换取 5 分钟有效、一次性使用的确认令牌。令牌用 Data Protection 签名，无需落库。
    /// </summary>
    public sealed class StepUpService : IStepUpValidator
    {
        private const string NonceKeyPrefix = "step-up:";
        private const int ValidSeconds = 300;

        private readonly IUserRepository _users;
        private readonly SmsCodeService _smsCodes;
        private readonly IDataProtector _protector;
        private readonly IMemoryCache _cache;

        public StepUpService(
            IUserRepository users,
            SmsCodeService smsCodes,
            IDataProtectionProvider dataProtection,
            IMemoryCache cache)
        {
            _users = users;
            _smsCodes = smsCodes;
            _protector = dataProtection.CreateProtector("yuncai.step-up.v1");
            _cache = cache;
        }

        /// <summary>校验密码或验证码并签发一次性确认令牌；两者都不正确时抛 ArgumentException。</summary>
        public StepUpResult Issue(string userId, string? password, string? smsCode)
        {
            var user = _users.FindById(userId) ?? throw new ArgumentException("用户不存在。");
            if (!user.IsActive)
            {
                throw new ArgumentException("账号已停用。");
            }

            var hasPassword = !string.IsNullOrEmpty(user.PasswordHash);
            var passwordOk = hasPassword
                             && !string.IsNullOrEmpty(password)
                             && PasswordHasher.Verify(password, user.PasswordHash!);
            var smsOk = !string.IsNullOrWhiteSpace(smsCode)
                        && !string.IsNullOrWhiteSpace(user.Phone)
                        && _smsCodes.Verify(user.Phone!, smsCode);
            if (!passwordOk && !smsOk)
            {
                throw new ArgumentException(hasPassword ? "密码不正确。" : "验证码无效或已过期。");
            }

            var nonce = Guid.NewGuid().ToString("N");
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(ValidSeconds);
            _cache.Set(NonceKeyPrefix + nonce, userId, TimeSpan.FromSeconds(ValidSeconds));

            var payload = _protector.Protect($"{userId}|{nonce}|{expiresAt.ToUnixTimeSeconds()}");
            return new StepUpResult { Token = payload, ExpiresInSeconds = ValidSeconds };
        }

        /// <summary>校验确认令牌；5 分钟窗口内可重复使用（批量删除等连续操作只确认一次）。</summary>
        public bool Validate(string? token, string userId)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
            {
                return false;
            }

            try
            {
                var raw = _protector.Unprotect(token);
                var parts = raw.Split('|');
                if (parts.Length != 3
                    || !string.Equals(parts[0], userId, StringComparison.Ordinal)
                    || !long.TryParse(parts[2], out var expires)
                    || expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    return false;
                }

                var key = NonceKeyPrefix + parts[1];
                return _cache.TryGetValue(key, out string? owner)
                       && string.Equals(owner, userId, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public sealed class StepUpRequest
    {
        public string? Password { get; init; }

        public string? SmsCode { get; init; }
    }

    public sealed class StepUpResult
    {
        public required string Token { get; init; }

        public int ExpiresInSeconds { get; init; }
    }
}
