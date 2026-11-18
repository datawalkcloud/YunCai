using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Funds.Application;
using YunCai.Modules.Funds.Infrastructure;
using YunCai.SharedKernel.Infrastructure.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.Funds.Api
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
        private readonly IAccountSetRepository _accountSetRepository;

        public FundReportsController(FundReportService fundReportService, IAccountSetRepository accountSetRepository)
        {
            _fundReportService = fundReportService;
            _accountSetRepository = accountSetRepository;
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

        /// <summary>
        /// 打印资金报表为 PDF：分段标题行、小计行与合计行按页面表格 1:1 还原，
        /// 查询参数与 GET / 导出保持一致，组合维度的「显示主类型合计」由 showPrimarySubtotal 带入。
        /// </summary>
        [HttpGet("pdf")]
        public async Task<IActionResult> Pdf(
            Guid accountSetId,
            [FromQuery] string? dimension,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] Guid? journalId,
            [FromQuery] string? keyword,
            [FromQuery] string? secondary,
            [FromQuery] bool? showPrimarySubtotal,
            CancellationToken cancellationToken)
        {
            try
            {
                var report = await _fundReportService.GetReportAsync(
                    accountSetId, dimension, startDate, endDate, journalId, keyword, secondary, cancellationToken);
                var context = await PrintContextAsync(accountSetId, cancellationToken);
                var sheet = FundsPdfProjector.Report(report, showPrimarySubtotal ?? false, context);
                var bytes = PrintSheetPdfBuilder.Build(sheet);
                return File(bytes, PrintSheetPdfBuilder.ContentType,
                    PrintSheetPdfBuilder.BuildFileName(sheet.Title, context.AccountSetName, PeriodSuffixOf(startDate, endDate)));
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

        /// <summary>
        /// 打印出纳核对表为 PDF：与页面表格同列（不平账户标注「（不平）」与差异额），
        /// onlyUnbalanced 对应页面的「只显示不平」勾选。
        /// </summary>
        [HttpGet("reconciliation/pdf")]
        public async Task<IActionResult> PdfReconciliation(
            Guid accountSetId,
            [FromQuery] DateOnly? date,
            [FromQuery] Guid? journalId,
            [FromQuery] bool? onlyUnbalanced,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _fundReportService.GetReconciliationAsync(accountSetId, date, journalId, cancellationToken);
                var context = await PrintContextAsync(accountSetId, cancellationToken);
                var sheet = FundsPdfProjector.Reconciliation(data, onlyUnbalanced ?? false, context);
                var bytes = PrintSheetPdfBuilder.Build(sheet);
                return File(bytes, PrintSheetPdfBuilder.ContentType,
                    PrintSheetPdfBuilder.BuildFileName(sheet.Title, context.AccountSetName, date?.ToString("yyyyMM")));
            }
            catch (FundReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>页头与文件名需要的账套名 / 编制单位。</summary>
        private async Task<FundPrintContext> PrintContextAsync(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            return new FundPrintContext(accountSet?.Name ?? string.Empty, accountSet?.CompanyName);
        }

        /// <summary>文件名里的期间后缀，如 202601-202603；未选区间时为空。</summary>
        private static string PeriodSuffixOf(DateOnly? startDate, DateOnly? endDate)
        {
            if (startDate is not null && endDate is not null)
            {
                return $"{startDate.Value:yyyyMM}-{endDate.Value:yyyyMM}";
            }

            var single = startDate ?? endDate;
            return single is null ? string.Empty : $"{single.Value:yyyyMM}";
        }
    }
}
