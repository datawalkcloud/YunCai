using KuaiJi.Modules.Funds.Application;
using KuaiJi.Modules.Funds.Domain;
using KuaiJi.Modules.Funds.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Funds.Api
{
    /// <summary>票据管理接口：应收 / 应付票据台账、生成凭证、导入导出与票面附件。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/bills")]
    [Authorize]
    public class BillsController : ControllerBase
    {
        /// <summary>附件上传上限（字节），与柠檬云「单个文件不超过 100M」一致。</summary>
        private const long MaxAttachmentBytes = 100L * 1024 * 1024;

        private readonly BillService _billService;
        private readonly BillImportService _billImportService;

        public BillsController(BillService billService, BillImportService billImportService)
        {
            _billService = billService;
            _billImportService = billImportService;
        }

        /// <summary>获取票据台账（按出票日倒序，可按日期区间过滤）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<BillDto>>> GetRange(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _billService.GetRangeAsync(accountSetId, startDate, endDate, cancellationToken));
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建票据（票据号唯一）。</summary>
        [HttpPost]
        public async Task<ActionResult<BillDto>> Create(Guid accountSetId, [FromBody] SaveBillRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _billService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改票据。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<BillDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveBillRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var bill = await _billService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return bill is null ? NotFound() : Ok(bill);
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>流转票据状态（贴现 / 背书 / 托收 / 兑现 / 作废）。</summary>
        [HttpPost("{id:guid}/status")]
        public async Task<ActionResult<BillDto>> ChangeStatus(Guid accountSetId, Guid id, [FromBody] ChangeBillStatusRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var bill = await _billService.ChangeStatusAsync(accountSetId, id, request, cancellationToken);
                return bill is null ? NotFound() : Ok(bill);
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>收款：票据置为「票据已结清」。</summary>
        [HttpPost("{id:guid}/collect")]
        public async Task<ActionResult<BillDto>> Collect(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var bill = await _billService.CollectAsync(accountSetId, id, cancellationToken);
                return bill is null ? NotFound() : Ok(bill);
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>转让（背书）：不可转让或已结清的票据会被拒绝。</summary>
        [HttpPost("{id:guid}/endorse")]
        public async Task<ActionResult<BillDto>> Endorse(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var bill = await _billService.EndorseAsync(accountSetId, id, cancellationToken);
                return bill is null ? NotFound() : Ok(bill);
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除票据。已生成凭证的票据会被拒绝。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _billService.DeleteAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>批量删除票据，返回实际删除条数。</summary>
        [HttpPost("bulk-delete")]
        public async Task<ActionResult<int>> BulkDelete(Guid accountSetId, [FromBody] BulkBillRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _billService.DeleteManyAsync(accountSetId, request.Ids, cancellationToken));
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>批量生成票据凭证（收款凭证 / 贴现凭证）。</summary>
        [HttpPost("vouchers")]
        public async Task<ActionResult<BulkBillVoucherResult>> GenerateVouchers(Guid accountSetId, [FromBody] BulkBillRequest request, CancellationToken cancellationToken)
        {
            var preparer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "系统";
            try
            {
                return Ok(await _billService.GenerateVouchersAsync(accountSetId, request.Ids, request.VoucherKind ?? "collect", preparer, null, cancellationToken));
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出票据台账（.xlsx，表头与导入模板一致）。</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(Guid accountSetId, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, CancellationToken cancellationToken)
        {
            try
            {
                var bills = await _billService.GetRangeAsync(accountSetId, startDate, endDate, cancellationToken);
                var content = BillExportBuilder.Build(bills);
                return File(content, BillExportBuilder.ContentType, $"票据台账_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载票据导入模板。</summary>
        [HttpGet("import/template")]
        public IActionResult DownloadTemplate()
        {
            var content = BillImportService.BuildTemplate();
            return File(content, BillExportBuilder.ContentType, "票据导入模板.xlsx");
        }

        /// <summary>导入票据（按表头解析，非法行跳过并返回告警）。</summary>
        [HttpPost("import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<BillImportResult>> Import(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                return Ok(await _billImportService.ImportAsync(accountSetId, stream, cancellationToken));
            }
            catch (BillImportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>列出票据附件（票面 / 回单等）。</summary>
        [HttpGet("{id:guid}/attachments")]
        public async Task<ActionResult<IReadOnlyList<BillAttachmentInfo>>> ListAttachments(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _billService.ListAttachmentsAsync(accountSetId, id, cancellationToken));
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>上传票据附件。</summary>
        [HttpPost("{id:guid}/attachments")]
        [RequestSizeLimit(MaxAttachmentBytes)]
        public async Task<ActionResult<BillAttachmentInfo>> UploadAttachment(Guid accountSetId, Guid id, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要上传的文件。" });
            }

            if (file.Length > MaxAttachmentBytes)
            {
                return BadRequest(new { message = "单个文件不超过 100M。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                return Ok(await _billService.AddAttachmentAsync(
                    accountSetId, id, file.FileName, file.ContentType, buffer.ToArray(), cancellationToken));
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载票据附件。</summary>
        [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
        public async Task<IActionResult> DownloadAttachment(Guid accountSetId, Guid id, Guid attachmentId, CancellationToken cancellationToken)
        {
            try
            {
                var attachment = await _billService.GetAttachmentAsync(accountSetId, id, attachmentId, cancellationToken);
                return attachment is null
                    ? NotFound()
                    : File(attachment.Content, attachment.ContentType, attachment.FileName);
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除票据附件。</summary>
        [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
        public async Task<IActionResult> DeleteAttachment(Guid accountSetId, Guid id, Guid attachmentId, CancellationToken cancellationToken)
        {
            try
            {
                return await _billService.DeleteAttachmentAsync(accountSetId, id, attachmentId, cancellationToken)
                    ? NoContent()
                    : NotFound();
            }
            catch (BillException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
