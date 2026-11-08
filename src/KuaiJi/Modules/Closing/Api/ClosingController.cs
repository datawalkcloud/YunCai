using KuaiJi.Modules.Auditing.Application;
using KuaiJi.Modules.Closing.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Closing.Api
{
    /// <summary>
    /// 期末结转与结账接口。按自然月顺序执行「期末检查 → 结转损益 → 结账」，
    /// 并支持反结账与撤销结转；结转生成的凭证自动过账并进入账簿与报表。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/closing")]
    [Authorize]
    public class ClosingController : ControllerBase
    {
        private readonly ClosingService _closingService;
        private readonly ClosingTemplateService _templateService;
        private readonly OperationLogService _operationLogService;

        public ClosingController(
            ClosingService closingService,
            ClosingTemplateService templateService,
            OperationLogService operationLogService)
        {
            _closingService = closingService;
            _templateService = templateService;
            _operationLogService = operationLogService;
        }

        /// <summary>写一条期末结转模块的操作日志（审计失败不影响主流程）。</summary>
        private Task LogAsync(Guid accountSetId, string type, string log, CancellationToken cancellationToken)
        {
            var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("nameid")?.Value ?? string.Empty;
            var userName = User.FindFirst("displayName")?.Value ?? User.FindFirst("unique_name")?.Value ?? "未知";
            return _operationLogService.RecordAsync(accountSetId, userId, userName, type, "期末结转", log, cancellationToken);
        }

        /// <summary>获取期末结转总体状态（含逐月状态卡片与下一步待处理期间）。</summary>
        [HttpGet]
        public async Task<ActionResult<ClosingStatusDto>> GetStatus(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _closingService.GetStatusAsync(accountSetId, cancellationToken));
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>期末检查：返回检查项明细与「可否结转 / 可否结账」结论。</summary>
        [HttpGet("checks")]
        public async Task<ActionResult<ClosingCheckResultDto>> Check(
            Guid accountSetId,
            [FromQuery] int year,
            [FromQuery] int month,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _closingService.CheckAsync(accountSetId, year, month, cancellationToken));
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>期末检查（第 1 步）：预置结转项的本期金额、可用性与已生成状态。</summary>
        [HttpGet("pre-close")]
        public async Task<ActionResult<PreCloseListDto>> GetPreClose(
            Guid accountSetId,
            [FromQuery] int year,
            [FromQuery] int month,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _closingService.GetPreCloseAsync(accountSetId, year, month, cancellationToken));
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>生成单个预置结转项（账簿类）的凭证并自动过账。</summary>
        [HttpPost("pre-close/generate")]
        public async Task<ActionResult<GeneratePreCloseResultDto>> GeneratePreClose(
            Guid accountSetId,
            [FromBody] GeneratePreCloseRequest request,
            CancellationToken cancellationToken)
        {
            var operatorName = User.FindFirst("displayName")?.Value
                               ?? User.FindFirst("unique_name")?.Value
                               ?? "未知";
            try
            {
                return Ok(await _closingService.GeneratePreCloseVoucherAsync(accountSetId, operatorName, request, cancellationToken));
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>结转损益预览：返回将要生成的结转凭证（含模板凭证），不落库。</summary>
        [HttpPost("carry-forward/preview")]
        public async Task<ActionResult<CarryForwardPreviewDto>> PreviewCarryForward(
            Guid accountSetId,
            [FromBody] CarryForwardRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _closingService.PreviewCarryForwardAsync(accountSetId, request, cancellationToken));
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>结转损益：生成结转凭证（损益类 → 本年利润，可选 → 未分配利润）并自动过账。</summary>
        [HttpPost("carry-forward")]
        public async Task<ActionResult<CarryForwardResultDto>> CarryForward(
            Guid accountSetId,
            [FromBody] CarryForwardRequest request,
            CancellationToken cancellationToken)
        {
            // 制单/审核人取当前登录用户的显示名（与凭证接口一致）。
            var operatorName = User.FindFirst("displayName")?.Value
                               ?? User.FindFirst("unique_name")?.Value
                               ?? "未知";
            try
            {
                var result = await _closingService.CarryForwardAsync(accountSetId, operatorName, request, cancellationToken);
                await LogAsync(accountSetId, "结转", $"结转损益 {result.PeriodText}", cancellationToken);
                return Ok(result);
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 年末补做结转未分配利润：为已结转损益的 12 月生成「本年利润 → 未分配利润」凭证并自动过账。
        /// 不改变结转进度；已存在同类凭证或本年利润无余额时直接返回说明。
        /// </summary>
        [HttpPost("retained-earnings")]
        public async Task<ActionResult<TransferRetainedResultDto>> TransferRetainedEarnings(
            Guid accountSetId,
            [FromBody] ClosingPeriodRequest request,
            CancellationToken cancellationToken)
        {
            var operatorName = User.FindFirst("displayName")?.Value
                               ?? User.FindFirst("unique_name")?.Value
                               ?? "未知";
            try
            {
                var result = await _closingService.TransferRetainedEarningsAsync(
                    accountSetId, operatorName, request.Year, request.Month, cancellationToken);
                if (result.Created)
                {
                    await LogAsync(
                        accountSetId, "结转", $"补做结转未分配利润 {request.Year}-{request.Month:D2}", cancellationToken);
                }

                return Ok(result);
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>结账：把下一个待结期间标记为已结账（须先完成结转，且期间内凭证全部已过账）。</summary>
        [HttpPost("close")]
        public async Task<ActionResult<ClosingStatusDto>> Close(
            Guid accountSetId,
            [FromBody] ClosingPeriodRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _closingService.CloseAsync(accountSetId, request.Year, request.Month, cancellationToken);
                await LogAsync(accountSetId, "结账", $"结账 {request.Year}-{request.Month:D2}", cancellationToken);
                return Ok(result);
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>反结账：从指定已结期间起撤销结账（该期间及其后全部退回，可指定任意已结期间）；不传期间时退回最近一次结账。结转进度保持不变。</summary>
        [HttpPost("reopen")]
        public async Task<ActionResult<ClosingStatusDto>> Reopen(
            Guid accountSetId,
            [FromQuery] int? year,
            [FromQuery] int? month,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _closingService.ReopenAsync(accountSetId, year, month, cancellationToken);
                var detail = year is { } y && month is { } m
                    ? $"从 {y}-{m:D2} 起反结账"
                    : "反结账（退回最近一次结账）";
                await LogAsync(accountSetId, "反结账", detail, cancellationToken);
                return Ok(result);
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>撤销结转：删除指定期间及其后已结转期间的系统结转凭证并退回结转进度（已结账的期间须先反结账）。</summary>
        [HttpPost("cancel-carry")]
        public async Task<ActionResult<CancelCarryResultDto>> CancelCarry(
            Guid accountSetId,
            [FromBody] ClosingPeriodRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var message = await _closingService.CancelCarryAsync(accountSetId, request.Year, request.Month, cancellationToken);
                await LogAsync(accountSetId, "撤销结转", $"撤销结转 {request.Year}-{request.Month:D2}", cancellationToken);
                return Ok(new CancelCarryResultDto { Message = message });
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /* ---------------- 结转模板 ---------------- */

        /// <summary>获取账套下全部结转模板（含行明细）。</summary>
        [HttpGet("templates")]
        public async Task<ActionResult<IReadOnlyList<ClosingTemplateDto>>> GetTemplates(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _templateService.ListAsync(accountSetId, cancellationToken));
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建结转模板。</summary>
        [HttpPost("templates")]
        public async Task<ActionResult<ClosingTemplateDto>> CreateTemplate(
            Guid accountSetId,
            [FromBody] SaveClosingTemplateRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _templateService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>编辑结转模板（整体替换行）。</summary>
        [HttpPut("templates/{templateId:guid}")]
        public async Task<ActionResult<ClosingTemplateDto>> UpdateTemplate(
            Guid accountSetId,
            Guid templateId,
            [FromBody] SaveClosingTemplateRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var template = await _templateService.UpdateAsync(accountSetId, templateId, request, cancellationToken);
                return template is null ? NotFound() : Ok(template);
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除结转模板。</summary>
        [HttpDelete("templates/{templateId:guid}")]
        public async Task<IActionResult> DeleteTemplate(Guid accountSetId, Guid templateId, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _templateService.DeleteAsync(accountSetId, templateId, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>按月预览结转模板：计算各模板将生成的金额与平衡情况。</summary>
        [HttpGet("templates/preview")]
        public async Task<ActionResult<ClosingTemplatePreviewDto>> PreviewTemplates(
            Guid accountSetId,
            [FromQuery] int year,
            [FromQuery] int month,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _templateService.PreviewAsync(accountSetId, year, month, cancellationToken));
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /* ---------------- 批量结账 ---------------- */

        /// <summary>批量结账：连续结账至最新已结转期间（中途受阻时保留已结部分并说明原因）。</summary>
        [HttpPost("close-batch")]
        public async Task<ActionResult<CloseBatchResultDto>> CloseBatch(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _closingService.CloseBatchAsync(accountSetId, cancellationToken);
                if (result.ClosedPeriods.Count > 0)
                {
                    await LogAsync(
                        accountSetId, "结账", $"批量结账：{string.Join("、", result.ClosedPeriods)}", cancellationToken);
                }

                return Ok(result);
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>批量标记结转：把损益余额已为 0 的连续期间批量标记为已结转（不生成凭证）。</summary>
        [HttpPost("carry-batch")]
        public async Task<ActionResult<CarryBatchResultDto>> CarryBatch(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _closingService.CarryBatchAsync(accountSetId, cancellationToken);
                if (result.MarkedPeriods.Count > 0)
                {
                    await LogAsync(
                        accountSetId, "结转", $"批量标记结转：{string.Join("、", result.MarkedPeriods)}", cancellationToken);
                }

                return Ok(result);
            }
            catch (ClosingException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
