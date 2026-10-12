using System.Threading;
using System.Threading.Tasks;
using KuaiJi.Modules.Identity.Application;
using KuaiJi.Modules.Identity.Domain;
using KuaiJi.Modules.Identity.Infrastructure;
using MediatR;

namespace KuaiJi.Modules.Identity.Application
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
            var user = _userRepository.FindByUsername(request.Username);
            if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Task.FromResult<LoginResult?>(null);
            }

            var (token, expiresAt) = _tokenService.CreateToken(user);

            var result = new LoginResult
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName
                }
            };

            return Task.FromResult<LoginResult?>(result);
        }
    }
}
