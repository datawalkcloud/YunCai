using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Identity.Infrastructure;

namespace YunCai.Modules.Identity.Application
{
    /// <summary>
    /// 手机号 + 密码自助注册：用户名即手机号，注册成功直接签发令牌进入系统。
    /// 与验证码首次登录自动建号的区别是不发短信、一步带密码。
    /// </summary>
    public class RegisterCommand : IRequest<LoginResult>
    {
        public string Phone { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;
    }

    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, LoginResult>
    {
        private static readonly Regex PhonePattern = new(@"^1\d{10}$", RegexOptions.Compiled);

        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        public RegisterCommandHandler(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
        }

        public Task<LoginResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var phone = (request.Phone ?? string.Empty).Trim();
            if (!PhonePattern.IsMatch(phone))
            {
                throw new ArgumentException("手机号格式不正确。");
            }

            var password = PasswordPolicy.Validate(request.Password);
            if (_userRepository.FindByPhone(phone) is not null
                || _userRepository.FindByUsername(phone) is not null)
            {
                throw new ArgumentException("该手机号已注册。");
            }

            var user = new User
            {
                Id = Guid.CreateVersion7().ToString("N"),
                Username = phone,
                DisplayName = $"用户{phone[^4..]}",
                Phone = phone
            };
            user.SetPassword(PasswordHasher.Hash(password));
            user.RecordLogin();
            _userRepository.Add(user);

            try
            {
                _userRepository.SaveChanges();
            }
            catch (DbUpdateException)
            {
                // 并发注册同一手机号：唯一索引兜底，转成可读的业务提示。
                throw new ArgumentException("该手机号已注册。");
            }

            var (token, expiresAt) = _tokenService.CreateToken(user);
            return Task.FromResult(new LoginResult
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = UserDto.From(user)
            });
        }
    }
}
