using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using YunCai.Modules.Marketing.Application;

namespace YunCai.Modules.Marketing.Api;

/// <summary>
/// 官网联系表单：对公网匿名开放，把留言推送到站长的企业微信群机器人。
/// 独立按来源 IP 限流（见 Program.cs 的 contact 策略），防止机器人被刷屏。
/// </summary>
[ApiController]
[Route("contact")]
[AllowAnonymous]
public sealed class ContactController : ControllerBase
{
    private readonly ContactService _service;

    public ContactController(ContactService service)
    {
        _service = service;
    }

    /// <summary>是否已配置 webhook。前端据此显示「留言板暂未开放」而不是让用户白填。</summary>
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new { enabled = _service.IsConfigured });

    /// <summary>提交联系表单。</summary>
    [HttpPost]
    [EnableRateLimiting("contact")]
    public async Task<IActionResult> Submit(ContactRequest request, CancellationToken cancellationToken)
    {
        if (!_service.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "站点留言功能暂未开放，请稍后再试。" });
        }

        await _service.SubmitAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return Ok(new { message = "申请已收到，我们会尽快联系你。" });
    }
}
