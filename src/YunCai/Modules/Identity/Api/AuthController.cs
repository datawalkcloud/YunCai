using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using YunCai.Modules.Identity.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace YunCai.Modules.Identity.Api
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly SmsCodeService _smsCodes;
        private readonly StepUpService _stepUp;

        public AuthController(IMediator mediator, SmsCodeService smsCodes, StepUpService stepUp)
        {
            _mediator = mediator;
            _smsCodes = smsCodes;
            _stepUp = stepUp;
        }

        /// <summary>
        /// 发送手机登录验证码；本地开发为模拟发送，验证码通过 debugCode 回显。
        /// </summary>
        [HttpPost("sms-code")]
        [AllowAnonymous]
        [EnableRateLimiting("sms")]
        public async Task<ActionResult<SendSmsCodeResult>> SendSmsCode(
            [FromBody] SendSmsCodeRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _smsCodes.SendAsync(request.Phone, cancellationToken));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 手机号 + 验证码登录；手机号首次登录自动创建账号。
        /// </summary>
        [HttpPost("login-by-phone")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<LoginResult>> LoginByPhone([FromBody] PhoneLoginCommand request)
        {
            try
            {
                return Ok(await _mediator.Send(request));
            }
            catch (ArgumentException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 使用用户名/密码登录，成功后返回 JWT 令牌。
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<LoginResult>> Login([FromBody] LoginCommand request)
        {
            try
            {
                var result = await _mediator.Send(request);
                return result == null
                    ? Unauthorized(new { message = "手机号或密码错误" })
                    : Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 手机号 + 密码自助注册，成功后直接返回 JWT 令牌并完成登录。
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<LoginResult>> Register(
            [FromBody] RegisterRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _mediator.Send(
                    new RegisterCommand { Phone = request.Phone, Password = request.Password },
                    cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>已登录用户修改密码；成功后旧令牌全部失效，响应里带回新令牌。</summary>
        [HttpPost("change-password")]
        [Authorize]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<LoginResult>> ChangePassword(
            [FromBody] ChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            if (CurrentUserId is not { } id)
            {
                return Unauthorized();
            }

            try
            {
                return Ok(await _mediator.Send(
                    new ChangePasswordCommand
                    {
                        UserId = id,
                        CurrentPassword = request.CurrentPassword,
                        NewPassword = request.NewPassword
                    },
                    cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 高危操作二次确认：用当前密码或手机验证码换取 5 分钟有效的一次性令牌，
        /// 请求头 X-Step-Up-Token 携带。短信验证码走 /Auth/sms-code 发送。
        /// </summary>
        [HttpPost("step-up")]
        [Authorize]
        [EnableRateLimiting("login")]
        public ActionResult<StepUpResult> StepUp([FromBody] StepUpRequest request)
        {
            if (CurrentUserId is not { } id)
            {
                return Unauthorized();
            }

            try
            {
                return Ok(_stepUp.Issue(id, request.Password, request.SmsCode));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>忘记密码：手机验证码 + 新密码。短信验证码走 /Auth/sms-code 发送。</summary>
        [HttpPost("reset-password")]
        [AllowAnonymous]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await _mediator.Send(
                    new ResetPasswordCommand
                    {
                        Phone = request.Phone,
                        Code = request.Code,
                        NewPassword = request.NewPassword
                    },
                    cancellationToken);
                return Ok(new { message = "密码已重置，请使用新密码登录。" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 返回当前登录用户信息，用于前端校验令牌有效性。
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
        {
            if (CurrentUserId is not { } id)
            {
                return Unauthorized();
            }

            var user = await _mediator.Send(new GetUserQuery { UserId = id }, cancellationToken);
            return user is null ? Unauthorized() : Ok(user);
        }

        /// <summary>
        /// 手机号首次登录后设置密码；设置成功即完成引导，可直接用密码登录。
        /// </summary>
        [HttpPost("set-password")]
        [Authorize]
        [EnableRateLimiting("login")]
        public async Task<ActionResult<LoginResult>> SetPassword(
            [FromBody] SetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            if (CurrentUserId is not { } id)
            {
                return Unauthorized();
            }

            try
            {
                return Ok(await _mediator.Send(
                    new SetPasswordCommand { UserId = id, Password = request.Password },
                    cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private string? CurrentUserId =>
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
