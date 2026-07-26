using KuaiJi.Application.Vouchers;
using KuaiJi.Domain.AccountSets;
using KuaiJi.Infrastructure.Vouchers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Controllers
{
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/[controller]")]
    [Authorize]
    public class VouchersController : ControllerBase
    {
        // 上传文件大小上限：10 MB，足以覆盖常见的凭证导出量。
        private const long MaxUploadBytes = 10 * 1024 * 1024;

        private readonly VoucherImportService _importService;
        private readonly VoucherService _voucherService;
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly ILogger<VouchersController> _logger;

        public VouchersController(VoucherImportService importService, VoucherService voucherService, IAccountSetRepository accountSetRepository, ILogger<VouchersController> logger)
        {
            _importService = importService;
            _voucherService = voucherService;
            _accountSetRepository = accountSetRepository;
            _logger = logger;
        }

        /// <summary>获取指定账套的凭证（可选 startDate/endDate 日期范围过滤，格式 yyyy-MM-dd，含起止日）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<VoucherListItemDto>>> GetAll(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                var vouchers = await _voucherService.ListAsync(accountSetId, startDate, endDate, cancellationToken);
                return Ok(vouchers);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>按标识获取单张凭证详情（含分录及科目编码/名称）。</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<VoucherDetailDto>> GetById(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var voucher = await _voucherService.GetAsync(accountSetId, id, cancellationToken);
            return voucher is null ? NotFound() : Ok(voucher);
        }

        /// <summary>制单：新建一张草稿凭证。凭证号省略（为 0）时按凭证字自动续号。</summary>
        [HttpPost]
        public async Task<ActionResult<VoucherDetailDto>> Create(Guid accountSetId, [FromBody] CreateVoucherRequest request, CancellationToken cancellationToken)
        {
            // 制单人取自当前登录用户的显示名（JWT 的 displayName 声明）。
            var preparer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "未知";
            try
            {
                var voucher = await _voucherService.CreateAsync(accountSetId, preparer, request, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { accountSetId, id = voucher.Id }, voucher);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>编辑凭证（仅草稿可改）。凭证字与号保持不变，可修改日期、附单据数与全部分录。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<VoucherDetailDto>> Update(Guid accountSetId, Guid id, [FromBody] UpdateVoucherRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var voucher = await _voucherService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return voucher is null ? NotFound() : Ok(voucher);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除凭证（含其全部分录）。已过账凭证不允许删除。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _voucherService.DeleteAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>审核凭证：草稿 → 已审核。要求借贷平衡，审核人取当前登录用户的显示名。</summary>
        [HttpPost("{id:guid}/review")]
        public async Task<ActionResult<VoucherDetailDto>> Review(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            // 审核人取自当前登录用户的显示名（JWT 的 displayName 声明）。
            var reviewer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "未知";
            try
            {
                var voucher = await _voucherService.ReviewAsync(accountSetId, id, reviewer, cancellationToken);
                return voucher is null ? NotFound() : Ok(voucher);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>反审核凭证：已审核 → 草稿，清空审核人。</summary>
        [HttpPost("{id:guid}/unreview")]
        public async Task<ActionResult<VoucherDetailDto>> Unreview(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var voucher = await _voucherService.UnreviewAsync(accountSetId, id, cancellationToken);
                return voucher is null ? NotFound() : Ok(voucher);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>过账凭证：已审核 → 已过账（登记入账锁定）。</summary>
        [HttpPost("{id:guid}/post")]
        public async Task<ActionResult<VoucherDetailDto>> Post(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var voucher = await _voucherService.PostAsync(accountSetId, id, cancellationToken);
                return voucher is null ? NotFound() : Ok(voucher);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 导出账套全部凭证（.xlsx）。每条分录一行，列与凭证导入解析列同构，文件可回传导入。
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _voucherService.ExportRowsAsync(accountSetId, cancellationToken);
                var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
                var bytes = VoucherExportBuilder.Build(rows);
                var fileName = VoucherExportBuilder.BuildFileName(accountSet?.Name ?? string.Empty);
                return File(bytes, VoucherExportBuilder.ContentType, fileName);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        /// <summary>
        /// 导入外部平台导出的记账凭证文件（.xls / .xlsx）。按凭证号聚合分录、自动补建缺失科目，
        /// 借贷金额原样保留（含负数），返回导入统计与告警信息。
        /// </summary>
        [HttpPost("import")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<VoucherImportResult>> Import(
            Guid accountSetId, IFormFile? file, [FromForm] VoucherImportConflict conflict = VoucherImportConflict.Skip, CancellationToken cancellationToken = default)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的凭证文件。" });
            }

            if (file.Length > MaxUploadBytes)
            {
                return BadRequest(new { message = "文件过大，请上传不超过 10MB 的凭证文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _importService.ImportAsync(accountSetId, stream, conflict, cancellationToken);
                return Ok(result);
            }
            catch (VoucherImportException ex)
            {
                // 可预期的业务错误（格式不符、内容缺失等），返回友好提示。
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入凭证文件「{FileName}」时发生未预期的错误。", file.FileName);
                return StatusCode(500, new { message = "导入凭证时发生错误，请稍后重试。" });
            }
        }
    }
}
