using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using YunCai.Modules.Identity.Domain;
using Microsoft.IdentityModel.Tokens;

namespace YunCai.Modules.Identity.Infrastructure
{
    /// <summary>
    /// 从配置节 "Jwt" 绑定的令牌选项。
    /// </summary>
    public class JwtOptions
    {
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int ExpireMinutes { get; set; } = 120;
    }

    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) CreateToken(User user);
    }

    /// <summary>
    /// 生成 JWT 访问令牌。
    /// </summary>
    public class JwtTokenService : ITokenService
    {
        private readonly JwtOptions _options;

        public JwtTokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        public (string Token, DateTime ExpiresAt) CreateToken(User user)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpireMinutes);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim("displayName", user.DisplayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
