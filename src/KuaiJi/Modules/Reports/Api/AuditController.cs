using KuaiJi.Modules.Reports.Application;
using KuaiJi.Modules.Reports.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Reports.Api
{
    /// <summary>审计对接接口：一键提取审计取数包（多页签 Excel）。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/audit")]
    [Authorize]
    public class AuditController : ControllerBase
    {
        private const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly AuditExtractService _auditExtractService;
        private readonly AccountSetArchiveService _archiveService;

        public AuditController(AuditExtractService auditExtractService, AccountSetArchiveService archiveService)
        {
            _auditExtractService = auditExtractService;
            _archiveService = archiveService;
        }

        /// <summary>归档 Excel：整账套核心账簿（归档说明 / 科目余额表 / 凭证清单 / 凭证分录）。</summary>
        [HttpGet("archive/excel")]
        public async Task<IActionResult> ArchiveExcel(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var (content, fileName) = await _archiveService.BuildExcelAsync(accountSetId, cancellationToken);
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>归档 PDF：整账套核心账簿（封面 + 科目余额表 + 凭证分录总表）。</summary>
        [HttpGet("archive/pdf")]
        public async Task<IActionResult> ArchivePdf(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var (content, fileName) = await _archiveService.BuildPdfAsync(accountSetId, cancellationToken);
                return File(content, "application/pdf", fileName);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出审计取数工作簿（科目余额表 / 凭证清单 / 凭证分录 / 取数说明）。</summary>
        [HttpGet("extract")]
        public async Task<IActionResult> Extract(Guid accountSetId, [FromQuery] int? year, [FromQuery] int? month, CancellationToken cancellationToken)
        {
            try
            {
                var today = DateTime.Today;
                var (content, fileName) = await _auditExtractService.ExtractAsync(
                    accountSetId, year ?? today.Year, month ?? today.Month, cancellationToken);
                return File(content, ContentType, fileName);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
