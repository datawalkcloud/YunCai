using System.Security.Cryptography;
using System.Text;
using YunCai.Modules.AccountSets.Application;
using Microsoft.Extensions.Configuration;

namespace YunCai.Modules.AccountSets.Infrastructure
{
    /// <summary>
    /// AES-GCM 实现的税局密码保护。密钥由 Jwt:Key 经 HKDF 派生（盐与标签固定），
    /// 好处是不必让部署方再多配一个密钥——Jwt:Key 缺失时进程本来就起不来。
    /// 代价是换 Jwt:Key 会让已存密码全部解不开，用户重填一次即可；
    /// 密文带 v1: 前缀，将来换密钥来源时可以按前缀平滑迁移旧密文。
    /// </summary>
    public sealed class AesTaxCredentialProtector : ITaxCredentialProtector
    {
        private const string VersionPrefix = "v1:";
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;

        private static readonly byte[] HkdfSalt = Encoding.UTF8.GetBytes("YunCai.TaxCredential.v1");
        private static readonly byte[] HkdfInfo = Encoding.UTF8.GetBytes("tax-login-password");

        private readonly IConfiguration _configuration;

        public AesTaxCredentialProtector(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string Encrypt(string plaintext)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plaintext);
            var cipherBytes = new byte[plainBytes.Length];
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];

            using (var cipher = CreateCipher())
            {
                cipher.Encrypt(nonce: nonce, plaintext: plainBytes, ciphertext: cipherBytes, tag: tag);
            }

            var payload = new byte[NonceSize + TagSize + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
            Buffer.BlockCopy(cipherBytes, 0, payload, NonceSize + TagSize, cipherBytes.Length);
            return VersionPrefix + Convert.ToBase64String(payload);
        }

        public string? Decrypt(string? cipher)
        {
            if (string.IsNullOrWhiteSpace(cipher) || !cipher.StartsWith(VersionPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                var payload = Convert.FromBase64String(cipher[VersionPrefix.Length..]);
                if (payload.Length < NonceSize + TagSize)
                {
                    return null;
                }

                var plainBytes = new byte[payload.Length - NonceSize - TagSize];
                using (var aes = CreateCipher())
                {
                    // 必须用命名参数：AesGcm 的 byte[] 重载第三个位置是 tag，Span 重载第三个位置是 plaintext，
                    // 按位置传会静默选中另一个重载，报「Plaintext and ciphertext must have the same length」。
                    aes.Decrypt(
                        nonce: payload.AsSpan(0, NonceSize).ToArray(),
                        ciphertext: payload.AsSpan(NonceSize + TagSize).ToArray(),
                        tag: payload.AsSpan(NonceSize, TagSize).ToArray(),
                        plaintext: plainBytes);
                }

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                // 最常见的原因是 Jwt:Key 换过；按「未填写」处理，界面上会要求重填，而不是让账套接口报错。
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private AesGcm CreateCipher()
        {
            var secret = _configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidOperationException("缺少 Jwt:Key，无法加解密税局密码。");
            }

            var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret), KeySize, HkdfSalt, HkdfInfo);
            return new AesGcm(key, TagSize);
        }
    }
}
