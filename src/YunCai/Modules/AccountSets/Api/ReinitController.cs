using YunCai.Modules.AccountSets.Application;
using YunCai.SharedKernel.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.AccountSets.Api
{
    /// <summary>重新初始化接口（高危）：清空账套全部业务数据并退回启用期间。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/reinit")]
    [Authorize]
    [RequireAccountSetAdmin]
    public class ReinitController : ControllerBase
    {
        private readonly ReinitService _reinitService;

        public ReinitController(ReinitService reinitService)
        {
            _reinitService = reinitService;
        }

        /// <summary>执行重新初始化。须显式提交账套名称作为确认口令。</summary>
        [HttpPost]
        [RequireStepUp]
        public async Task<ActionResult<ReinitResultDto>> Wipe(Guid accountSetId, [FromBody] WipeRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _reinitService.WipeAsync(
                    accountSetId,
                    request.ConfirmName,
                    request.OperatorUserId,
                    request.OperatorUserName,
                    cancellationToken));
            }
            catch (ReinitException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    /// <summary>重新初始化请求。</summary>
    public sealed class WipeRequest
    {
        /// <summary>确认口令：账套名称（必须完全一致）。</summary>
        public string ConfirmName { get; init; } = null!;

        public string OperatorUserId { get; init; } = string.Empty;

        public string OperatorUserName { get; init; } = string.Empty;
    }
}
