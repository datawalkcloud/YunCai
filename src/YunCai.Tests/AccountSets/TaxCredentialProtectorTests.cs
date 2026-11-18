using YunCai.Modules.AccountSets.Infrastructure;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace YunCai.Tests.AccountSets;

/// <summary>
/// 税局实名密码的存储保护（<see cref="AesTaxCredentialProtector"/>）。
/// 领域依据（AGENTS.md「账套是数据隔离单位」+ 一键登录税局）：
/// - 库里只存密文，接口只回「是否已填写」，明文只在请求处理过程中存在；
/// - 密钥由 Jwt:Key 派生，所以换签名密钥等价于让已存密码失效——必须解不开时返回 null
///   让界面提示重填，而不是抛异常把整个账套接口打挂。
/// </summary>
public static class TaxCredentialProtectorTests
{
    private const string Key = "0123456789abcdef0123456789abcdef";
    private const string OtherKey = "fedcba9876543210fedcba9876543210";

    private static AesTaxCredentialProtector Protector(string jwtKey = Key)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = jwtKey })
            .Build();
        return new AesTaxCredentialProtector(config);
    }

    [Theory]
    [InlineData("Secret#2026")]
    [InlineData("税局密码123")]
    [InlineData("a")]
    public static void Encrypt_then_decrypt_returns_the_original_password(string plaintext)
    {
        var protector = Protector();
        Assert.Equal(plaintext, protector.Decrypt(protector.Encrypt(plaintext)));
    }

    [Fact]
    public static void Cipher_does_not_contain_the_plaintext_and_differs_each_time()
    {
        var protector = Protector();
        var first = protector.Encrypt("Secret#2026");
        var second = protector.Encrypt("Secret#2026");

        Assert.DoesNotContain("Secret", first);
        // 每次随机 nonce，所以同一密码两次密文不同；库里存的不是可比对指纹。
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plaintext-not-wrapped")]
    [InlineData("v1:not-base64!!!")]
    public static void Unreadable_cipher_is_reported_as_not_filled_in(string? cipher)
    {
        // 脏数据、旧版本、被截断的密文都不能让账套接口 500。
        Assert.Null(Protector().Decrypt(cipher));
    }

    [Fact]
    public static void Tampered_cipher_does_not_decrypt()
    {
        var protector = Protector();
        var cipher = protector.Encrypt("Secret#2026");
        var chars = cipher.ToCharArray();
        // 改动最后一个字符：GCM 认证标签校验必然失败。
        chars[^1] = chars[^1] == 'A' ? 'B' : 'A';
        Assert.Null(protector.Decrypt(new string(chars)));
    }

    [Fact]
    public static void Rotating_the_jwt_key_invalidates_stored_passwords()
    {
        var cipher = Protector(Key).Encrypt("Secret#2026");
        // 换 Jwt:Key 后按「未填写」处理，界面提示重填一次，这是密钥来源的既定代价。
        Assert.Null(Protector(OtherKey).Decrypt(cipher));
    }
}
