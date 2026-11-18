using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using YunCai.Modules.Identity.Domain;
using YunCai.Modules.Identity.Infrastructure;

namespace YunCai.Modules.Identity.Application
{
    /// <summary>
    /// 手机号 + 验证码登录。验证码校验通过后按手机号查找账号，
    /// 首次登录自动创建账号（用户名即手机号），随后签发与密码登录一致的 JWT。
    /// </summary>
    public class PhoneLoginCommandHandler : IRequestHandler<PhoneLoginCommand, LoginResult?>
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly SmsCodeService _smsCodes;

        public PhoneLoginCommandHandler(
            IUserRepository userRepository,
            ITokenService tokenService,
            SmsCodeService smsCodes)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _smsCodes = smsCodes;
        }

        public Task<LoginResult?> Handle(PhoneLoginCommand request, CancellationToken cancellationToken)
        {
            var phone = (request.Phone ?? string.Empty).Trim();
            if (!_smsCodes.Verify(phone, request.Code))
            {
                throw new ArgumentException("验证码无效或已过期。");
            }

            var user = _userRepository.FindByPhone(phone) ?? CreateUser(phone);
            if (!user.IsActive)
            {
                throw new ArgumentException("账号已停用，请联系系统管理员。");
            }

            user.RecordLogin();
            _userRepository.SaveChanges();

            var (token, expiresAt) = _tokenService.CreateToken(user);

            return Task.FromResult<LoginResult?>(new LoginResult
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = UserDto.From(user)
            });
        }

        private User CreateUser(string phone)
        {
            var created = new User
            {
                Id = Guid.CreateVersion7().ToString("N"),
                Username = phone,
                // 首次登录不生成可用密码，前端收到 hasPassword = false 后引导设置密码。
                PasswordHash = null,
                DisplayName = $"用户{phone[^4..]}",
                Phone = phone
            };

            _userRepository.Add(created);
            try
            {
                _userRepository.SaveChanges();
                return created;
            }
            catch (DbUpdateException)
            {
                // 同一手机号并发首次登录：唯一索引挡住了重复插入，回读另一请求已建好的账号。
                return _userRepository.FindByPhone(phone)
                       ?? throw new InvalidOperationException("手机号账号创建失败，请重试。");
            }
        }
    }
}
