using System.Threading;
using System.Threading.Tasks;
using YunCai.Modules.Identity.Application;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Identity.Infrastructure;
using MediatR;

namespace YunCai.Modules.Identity.Application
{
    public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult?>
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        public LoginCommandHandler(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
        }

        public Task<LoginResult?> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            // 账号标识以手机号为主（自动注册账号的用户名即手机号），保留用户名兜底以兼容历史账号。
            var identifier = request.Username.Trim();
            var user = _userRepository.FindByPhone(identifier)
                       ?? _userRepository.FindByUsername(identifier);
            if (user is null || user.PasswordHash is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Task.FromResult<LoginResult?>(null);
            }

            if (!user.IsActive)
            {
                throw new ArgumentException("账号已停用，请联系系统管理员。");
            }

            user.RecordLogin();
            _userRepository.SaveChanges();

            var (token, expiresAt) = _tokenService.CreateToken(user);

            var result = new LoginResult
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = UserDto.From(user)
            };

            return Task.FromResult<LoginResult?>(result);
        }
    }
}
