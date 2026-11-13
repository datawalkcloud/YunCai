using YunCai.Modules.Auditing.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.Auditing.Api
{
    /// <summary>操作日志接口：按日期区间 / 用户 / 关键字查询。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/operationlogs")]
    [Authorize]
    public class OperationLogsController : ControllerBase
    {
        private readonly OperationLogService _logService;

        public OperationLogsController(OperationLogService logService)
        {
            _logService = logService;
        }

        /// <summary>查询操作日志（时间倒序）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<OperationLogDto>>> Query(
            Guid accountSetId,
            [FromQuery] DateTime? start,
            [FromQuery] DateTime? end,
            [FromQuery] string? userId,
            [FromQuery] string? keyword,
            CancellationToken cancellationToken)
        {
            return Ok(await _logService.QueryAsync(
                accountSetId,
                new OperationLogQuery { Start = start, End = end, UserId = userId, Keyword = keyword },
                cancellationToken));
        }
    }
}
