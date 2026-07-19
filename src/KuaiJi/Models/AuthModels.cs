using System.ComponentModel.DataAnnotations;

namespace KuaiJi.Models
{
    /// <summary>
    /// 登录请求体。
    /// </summary>
    public class LoginRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 登录成功后返回的令牌与用户信息。
    /// </summary>
    public class LoginResponse
    {
        public required string Token { get; init; }
        public required DateTime ExpiresAt { get; init; }
        public required UserInfo User { get; init; }
    }

    /// <summary>
    /// 返回给前端的公开用户信息（不含密码）。
    /// </summary>
    public class UserInfo
    {
        public required string Id { get; init; }
        public required string Username { get; init; }
        public required string DisplayName { get; init; }
    }
}
