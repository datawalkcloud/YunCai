using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace YunCai.Modules.Identity.Application
{
    /// <summary>
    /// 登录验证码：6 位数字、限时有效、同一手机号限频发送。验证码只存进程内存缓存，
    /// 应用重启后失效；未通过前不消耗，校验成功后立即删除，避免重放。
    /// </summary>
    public sealed class SmsCodeService
    {
        private const string CodeKeyPrefix = "sms:code:";
        private const string SendKeyPrefix = "sms:send:";

        private static readonly Regex PhonePattern = new(@"^1\d{10}$", RegexOptions.Compiled);

        private readonly IMemoryCache _cache;
        private readonly ISmsSender _sender;
        private readonly SmsOptions _options;

        public SmsCodeService(IMemoryCache cache, ISmsSender sender, IOptions<SmsOptions> options)
        {
            _cache = cache;
            _sender = sender;
            _options = options.Value;
        }

        /// <summary>发送登录验证码；格式错误或发送过于频繁时抛 ArgumentException（对应 400）。</summary>
        public async Task<SendSmsCodeResult> SendAsync(string phone, CancellationToken cancellationToken = default)
        {
            phone = (phone ?? string.Empty).Trim();
            if (!PhonePattern.IsMatch(phone))
            {
                throw new ArgumentException("手机号格式不正确。");
            }

            var sendKey = SendKeyPrefix + phone;
            if (_cache.TryGetValue(sendKey, out _))
            {
                throw new ArgumentException("验证码发送过于频繁，请稍后再试。");
            }

            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var outcome = await _sender.SendAsync(phone, code, cancellationToken);
            if (!outcome.Success)
            {
                throw new InvalidOperationException(outcome.Message);
            }

            _cache.Set(sendKey, true, TimeSpan.FromSeconds(_options.RetrySeconds));
            _cache.Set(CodeKeyPrefix + phone, code, TimeSpan.FromSeconds(_options.CodeExpireSeconds));

            return new SendSmsCodeResult
            {
                Success = true,
                RetrySeconds = _options.RetrySeconds,
                Message = outcome.Message,
                DebugCode = outcome.DebugCode
            };
        }

        /// <summary>校验验证码；成功后立即作废，失败不消耗（仍可在有效期内重试）。</summary>
        public bool Verify(string phone, string code)
        {
            phone = (phone ?? string.Empty).Trim();
            code = (code ?? string.Empty).Trim();
            if (!PhonePattern.IsMatch(phone) || code.Length == 0)
            {
                return false;
            }

            var key = CodeKeyPrefix + phone;
            if (!_cache.TryGetValue(key, out string? saved) || string.IsNullOrEmpty(saved))
            {
                return false;
            }

            var matches = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(saved),
                Encoding.UTF8.GetBytes(code));
            if (matches)
            {
                _cache.Remove(key);
            }

            return matches;
        }
    }
}
