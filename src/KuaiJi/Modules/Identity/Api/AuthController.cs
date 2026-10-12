using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using KuaiJi.Modules.Identity.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Identity.Api
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// 使用用户名/密码登录，成功后返回 JWT 令牌。
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResult>> Login([FromBody] LoginCommand request)
        {
            var result = await _mediator.Send(request);
            if (result == null)
            {
                return Unauthorized(new { message = "用户名或密码错误" });
            }

            return Ok(result);
        }

        /// <summary>
        /// 返回当前登录用户信息，用于前端校验令牌有效性。
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public ActionResult<UserDto> Me()
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

            return Ok(new UserDto
            {
                Id = id,
                Username = username,
                DisplayName = displayName ?? username
            });
        }
    }
}
