using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using YunCai.Modules.Identity.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.Identity.Api
{
    /// <summary>登录账号管理（仅系统管理员）：开户、改资料、停用 / 启用、重置密码。</summary>
    [ApiController]
    [Route("users")]
    [Authorize(Policy = "SystemAdmin")]
    public class UsersController : ControllerBase
    {
        private readonly UserAdminService _users;

        public UsersController(UserAdminService users)
        {
            _users = users;
        }

        private string? CurrentUserId =>
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        [HttpGet]
        public ActionResult<IReadOnlyList<UserDto>> List([FromQuery] string? keyword)
            => Ok(_users.List(keyword));

        [HttpPost]
        public ActionResult<UserDto> Create([FromBody] CreateUserRequest request)
        {
            try
            {
                return Ok(_users.Create(request));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public ActionResult<UserDto> Update(string id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                return Ok(_users.UpdateProfile(id, request));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>停用 / 启用账号；停用会立即吊销该账号已签发的全部令牌。</summary>
        [HttpPut("{id}/status")]
        public ActionResult<UserDto> SetStatus(string id, [FromBody] SetUserStatusRequest request)
        {
            if (CurrentUserId is not { } operatorId)
            {
                return Unauthorized();
            }

            try
            {
                return Ok(_users.SetEnabled(id, request.Enabled, operatorId));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>管理员重置密码；该账号已签发的令牌随即失效。</summary>
        [HttpPost("{id}/reset-password")]
        public IActionResult ResetPassword(string id, [FromBody] AdminResetPasswordRequest request)
        {
            try
            {
                _users.ResetPassword(id, request.NewPassword);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public sealed class SetUserStatusRequest
    {
        public bool Enabled { get; init; }
    }
}
