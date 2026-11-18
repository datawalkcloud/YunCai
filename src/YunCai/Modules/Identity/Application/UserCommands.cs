using System.Threading;
using System.Threading.Tasks;
using MediatR;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Identity.Infrastructure;

namespace YunCai.Modules.Identity.Application
{
    /// <summary>按用户标识读取当前资料（/Auth/me 用，确保 hasPassword 状态是最新的）。</summary>
    public class GetUserQuery : IRequest<UserDto?>
    {
        public string UserId { get; init; } = string.Empty;
    }

    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto?>
    {
        private readonly IUserRepository _userRepository;

        public GetUserQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public Task<UserDto?> Handle(GetUserQuery request, CancellationToken cancellationToken)
            => Task.FromResult(_userRepository.FindById(request.UserId) is { } user ? UserDto.From(user) : null);
    }

    /// <summary>登录密码强度校验（首次设密、改密、重置共用）。</summary>
    internal static class PasswordPolicy
    {
        public const int MinLength = 8;
        public const int MaxLength = 64;

        public static string Validate(string? password)
        {
            var value = password ?? string.Empty;
            if (value.Length < MinLength)
            {
                throw new ArgumentException($"密码至少 {MinLength} 位。");
            }

            if (value.Length > MaxLength)
            {
                throw new ArgumentException($"密码不能超过 {MaxLength} 位。");
            }

            return value;
        }
    }

    /// <summary>
    /// 设置登录密码。只允许尚未设密的账号调用（手机号验证码首次登录自动建号），
    /// 已设密账号拒绝，避免仅凭登录令牌静默改掉既有密码。
    /// 设置成功后返回新令牌：密码变更会吊销旧令牌，前端需要立即换用新令牌。
    /// </summary>
    public class SetPasswordCommand : IRequest<LoginResult>
    {
        public string UserId { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
    }

    public class SetPasswordCommandHandler : IRequestHandler<SetPasswordCommand, LoginResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        public SetPasswordCommandHandler(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
        }

        public Task<LoginResult> Handle(SetPasswordCommand request, CancellationToken cancellationToken)
        {
            var password = PasswordPolicy.Validate(request.Password);
            var user = _userRepository.FindById(request.UserId)
                       ?? throw new ArgumentException("用户不存在。");
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                throw new ArgumentException("密码已设置，如需修改请使用修改密码功能。");
            }

            user.SetPassword(PasswordHasher.Hash(password));
            _userRepository.SaveChanges();

            var (token, expiresAt) = _tokenService.CreateToken(user);
            return Task.FromResult(new LoginResult
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = UserDto.From(user)
            });
        }
    }

    /// <summary>已登录用户修改密码：校验当前密码，改后吊销全部旧令牌并返回新令牌。</summary>
    public class ChangePasswordCommand : IRequest<LoginResult>
    {
        public string UserId { get; init; } = string.Empty;
        public string CurrentPassword { get; init; } = string.Empty;
        public string NewPassword { get; init; } = string.Empty;
    }

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, LoginResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        public ChangePasswordCommandHandler(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
        }

        public Task<LoginResult> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
        {
            var newPassword = PasswordPolicy.Validate(request.NewPassword);
            var user = _userRepository.FindById(request.UserId)
                       ?? throw new ArgumentException("用户不存在。");

            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                if (string.IsNullOrEmpty(request.CurrentPassword)
                    || !PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
                {
                    throw new ArgumentException("当前密码不正确。");
                }

                if (PasswordHasher.Verify(newPassword, user.PasswordHash))
                {
                    throw new ArgumentException("新密码不能与当前密码相同。");
                }
            }

            user.SetPassword(PasswordHasher.Hash(newPassword));
            _userRepository.SaveChanges();

            var (token, expiresAt) = _tokenService.CreateToken(user);
            return Task.FromResult(new LoginResult
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = UserDto.From(user)
            });
        }
    }

    /// <summary>
    /// 忘记密码：手机验证码校验通过后重置密码。手机号不存在时同样返回成功，
    /// 避免把「该手机号是否注册」暴露给未登录调用方。
    /// </summary>
    public class ResetPasswordCommand : IRequest
    {
        public string Phone { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string NewPassword { get; init; } = string.Empty;
    }

    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly SmsCodeService _smsCodes;

        public ResetPasswordCommandHandler(IUserRepository userRepository, SmsCodeService smsCodes)
        {
            _userRepository = userRepository;
            _smsCodes = smsCodes;
        }

        public Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            var password = PasswordPolicy.Validate(request.NewPassword);
            var phone = (request.Phone ?? string.Empty).Trim();
            if (!_smsCodes.Verify(phone, request.Code))
            {
                throw new ArgumentException("验证码无效或已过期。");
            }

            var user = _userRepository.FindByPhone(phone);
            if (user is null)
            {
                return Task.CompletedTask;
            }

            if (!user.IsActive)
            {
                throw new ArgumentException("账号已停用，请联系系统管理员。");
            }

            user.SetPassword(PasswordHasher.Hash(password));
            _userRepository.SaveChanges();
            return Task.CompletedTask;
        }
    }
}
