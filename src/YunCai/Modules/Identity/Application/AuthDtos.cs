using System.ComponentModel.DataAnnotations;
using MediatR;
using YunCai.Modules.Identity.Domain;

namespace YunCai.Modules.Identity.Application
{
    public class LoginCommand : IRequest<LoginResult?>
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResult
    {
        public required string Token { get; init; }
        public required DateTime ExpiresAt { get; init; }
        public required UserDto User { get; init; }
    }

    public class UserDto
    {
        public required string Id { get; init; }
        public required string Username { get; init; }
        public required string DisplayName { get; init; }
        public string? Phone { get; init; }

        /// <summary>是否已设置登录密码；手机号首次登录自动建号后为 false，前端据此弹出设密流程。</summary>
        public bool HasPassword { get; init; }

        /// <summary>是否系统管理员（可见「用户管理」入口）。</summary>
        public bool IsSystemAdmin { get; init; }

        public bool IsActive { get; init; }

        public DateTime? LastLoginAt { get; init; }

        public DateTime CreatedAt { get; init; }

        public static UserDto From(User user) => new()
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Phone = user.Phone,
            HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
            IsSystemAdmin = user.IsSystemAdmin,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }

    /// <summary>已登录用户修改密码（需要校验当前密码）。</summary>
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>通过手机验证码重置密码（忘记密码）。</summary>
    public class ResetPasswordRequest
    {
        public string Phone { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>系统管理员重置指定账号密码。</summary>
    public class AdminResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
    }

    /// <summary>首次登录后设置登录密码的请求。</summary>
    public class SetPasswordRequest
    {
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>发送登录验证码的请求。</summary>
    public class SendSmsCodeRequest
    {
        public string Phone { get; set; } = string.Empty;
    }

    /// <summary>发送结果；模拟通道会通过 DebugCode 回显验证码，方便本地开发登录。</summary>
    public class SendSmsCodeResult
    {
        public bool Success { get; init; }
        public int RetrySeconds { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? DebugCode { get; init; }
    }

    /// <summary>手机号 + 验证码登录；手机号首次登录时自动创建账号。</summary>
    public class PhoneLoginCommand : IRequest<LoginResult?>
    {
        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = string.Empty;
    }
}
