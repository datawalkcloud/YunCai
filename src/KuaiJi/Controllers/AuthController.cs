using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KuaiJi.Models;
using KuaiJi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserStore _users;
        private readonly ITokenService _tokens;

        public AuthController(IUserStore users, ITokenService tokens)
        {
            _users = users;
            _tokens = tokens;
        }

        /// <summary>
        /// 使用用户名/密码登录，成功后返回 JWT 令牌。
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
        {
            var user = _users.FindByUsername(request.Username);
            if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "用户名或密码错误" });
            }

            var (token, expiresAt) = _tokens.CreateToken(user);

            return Ok(new LoginResponse
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName
                }
            });
        }

        /// <summary>
        /// 返回当前登录用户信息，用于前端校验令牌有效性。
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public ActionResult<UserInfo> Me()
        {
            var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                           ?? User.Identity?.Name;
            var displayName = User.FindFirstValue("displayName");

            if (id is null || username is null)
            {
                return Unauthorized();
            }

            return Ok(new UserInfo
            {
                Id = id,
                Username = username,
                DisplayName = displayName ?? username
            });
        }
    }
}
