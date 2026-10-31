using KuaiJi.Modules.Funds.Application;
using KuaiJi.Modules.Funds.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Funds.Api
{
    /// <summary>
    /// 资金报表接口：按 账户 / 收支类别 / 往来单位 / 项目 / 部门 / 组合 六个维度汇总区间内收付，
    /// 并提供出纳账与总账的余额核对（核对总账）。只读接口（导出除外，导出为文件响应）。
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

        /// <summary>
        /// 资金报表：dimension 取 account（默认）/ category / counterparty / project / department / combo；
        /// journalId 按账户筛选，keyword 为维度值名称搜索，secondary 为组合维度的次类别（仅组合维度生效）。
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<FundReportDto>> GetReport(
            Guid accountSetId,
            [FromQuery] string? dimension,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] Guid? journalId,
            [FromQuery] string? keyword,
            [FromQuery] string? secondary,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _fundReportService.GetReportAsync(
                    accountSetId, dimension, startDate, endDate, journalId, keyword, secondary, cancellationToken));
            }
            catch (FundReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出当前维度汇总结果为 .xlsx（分段与小计同页面）。</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(
            Guid accountSetId,
            [FromQuery] string? dimension,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] Guid? journalId,
            [FromQuery] string? keyword,
            [FromQuery] string? secondary,
            CancellationToken cancellationToken)
        {
            try
            {
                var report = await _fundReportService.GetReportAsync(
                    accountSetId, dimension, startDate, endDate, journalId, keyword, secondary, cancellationToken);
                var bytes = FundReportExportBuilder.Build(report);
                var fileName = $"{FundReportExportBuilder.SheetNameOf(report.Dimension)}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(bytes, FundReportExportBuilder.ContentType, fileName);
            }
            catch (FundReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>核对总账：截至 date（默认今天）按会计期间逐账户列示期初/借方/贷方/余额与总账差异。</summary>
        [HttpGet("reconciliation")]
        public async Task<ActionResult<FundReconciliationDto>> GetReconciliation(
            Guid accountSetId,
            [FromQuery] DateOnly? date,
            [FromQuery] Guid? journalId,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _fundReportService.GetReconciliationAsync(accountSetId, date, journalId, cancellationToken));
            }
            catch (FundReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出核对总账为 .xlsx。</summary>
        [HttpGet("reconciliation/export")]
        public async Task<IActionResult> ExportReconciliation(
            Guid accountSetId,
            [FromQuery] DateOnly? date,
            [FromQuery] Guid? journalId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _fundReportService.GetReconciliationAsync(accountSetId, date, journalId, cancellationToken);
                var bytes = FundReportExportBuilder.BuildReconciliation(data);
                var fileName = $"核对总账_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(bytes, FundReportExportBuilder.ContentType, fileName);
            }
            catch (FundReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
