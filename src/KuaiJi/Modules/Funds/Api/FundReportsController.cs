using KuaiJi.Modules.Funds.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Funds.Api
{
    /// <summary>
    /// 资金报表接口：按 账户 / 收支类别 / 往来单位 维度汇总区间内收付，
    /// 并提供出纳账与总账的余额核对（核对总账）。只读接口。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/fund-reports")]
    [Authorize]
    public class FundReportsController : ControllerBase
    {
        private readonly FundReportService _fundReportService;

        public FundReportsController(FundReportService fundReportService)
        {
            _fundReportService = fundReportService;
        }

        /// <summary>资金报表：dimension 取 account（默认）/ category / counterparty。</summary>
        [HttpGet]
        public async Task<ActionResult<FundReportDto>> GetReport(
            Guid accountSetId,
            [FromQuery] string? dimension,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _fundReportService.GetReportAsync(accountSetId, dimension, startDate, endDate, cancellationToken));
            }
            catch (FundReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>核对总账：截至 date（默认今天）逐账户对照出纳余额与总账科目余额。</summary>
        [HttpGet("reconciliation")]
        public async Task<ActionResult<FundReconciliationDto>> GetReconciliation(
            Guid accountSetId,
            [FromQuery] DateOnly? date,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _fundReportService.GetReconciliationAsync(accountSetId, date, cancellationToken));
            }
            catch (FundReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
