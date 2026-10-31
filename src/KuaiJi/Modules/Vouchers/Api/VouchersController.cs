using KuaiJi.Modules.Vouchers.Application;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Vouchers.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Vouchers.Api
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
        private readonly VoucherAttachmentService _attachmentService;
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly ILogger<VouchersController> _logger;

        public VouchersController(
            VoucherImportService importService,
            VoucherService voucherService,
            VoucherAttachmentService attachmentService,
            IAccountSetRepository accountSetRepository,
            ILogger<VouchersController> logger)
        {
            _importService = importService;
            _voucherService = voucherService;
            _attachmentService = attachmentService;
            _accountSetRepository = accountSetRepository;
            _logger = logger;
        }

        /// <summary>
        /// 获取指定账套的凭证列表。支持期间、凭证字、制单人、审核状态、辅助核算、摘要、备注、
        /// 科目（含下级）、借贷方向、金额区间、凭证号区间等筛选，以及按凭证号/日期升降序排序。
        /// deleted=true 时返回回收站中的凭证。
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<VoucherListItemDto>>> GetAll(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? word,
            [FromQuery] string? preparer,
            [FromQuery] bool? reviewed,
            [FromQuery] bool? hasAssists,
            [FromQuery] string? summary,
            [FromQuery] string? remark,
            [FromQuery] Guid? accountId,
            [FromQuery] bool? direction,
            [FromQuery] decimal? amountFrom,
            [FromQuery] decimal? amountTo,
            [FromQuery] int? numberFrom,
            [FromQuery] int? numberTo,
            [FromQuery] string? sortBy,
            [FromQuery] bool sortDesc = false,
            [FromQuery] bool deleted = false,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var query = new VoucherQuery
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Word = word,
                    Preparer = preparer,
                    Reviewed = reviewed,
                    HasAssists = hasAssists,
                    SummaryContains = summary,
                    RemarkContains = remark,
                    AccountId = accountId,
                    Direction = direction,
                    AmountFrom = amountFrom,
                    AmountTo = amountTo,
                    NumberFrom = numberFrom,
                    NumberTo = numberTo,
                    SortBy = sortBy,
                    SortDesc = sortDesc,
                    Deleted = deleted,
                };
                return Ok(await _voucherService.ListAsync(accountSetId, query, cancellationToken));
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

        /// <summary>反过账凭证：已过账 → 已审核（撤销登记入账），结账期间内禁止。</summary>
        [HttpPost("{id:guid}/unpost")]
        public async Task<ActionResult<VoucherDetailDto>> Unpost(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var voucher = await _voucherService.UnpostAsync(accountSetId, id, cancellationToken);
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
        /// 单张凭证 PDF（服务端用 PaperSharp 渲染）。版式与前端「凭证打印」一致，用于下载/归档。
        /// </summary>
        [HttpGet("{id:guid}/pdf")]
        public async Task<IActionResult> Pdf(
            Guid accountSetId,
            Guid id,
            [FromQuery] string paper = "A4x2",
            [FromQuery] string orientation = "portrait",
            [FromQuery] string valign = "top",
            [FromQuery] int rows = 5,
            [FromQuery] int fontSize = 10,
            [FromQuery] bool mergePrint = false,
            [FromQuery] bool showAccountCode = true,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var voucher = await _voucherService.GetAsync(accountSetId, id, cancellationToken);
                if (voucher is null) return NotFound(new { message = "凭证不存在或已删除。" });

                var options = new VoucherPdfOptions
                {
                    Paper = paper,
                    Orientation = orientation,
                    Valign = valign,
                    Rows = rows,
                    FontSize = fontSize,
                    MergePrint = mergePrint,
                    ShowAccountCode = showAccountCode,
                    PrintedAt = DateTimeOffset.Now,
                };

                var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
                var bytes = VoucherPdfBuilder.Build([voucher], options);
                return File(bytes, VoucherPdfBuilder.ContentType, VoucherPdfBuilder.BuildFileName(accountSet?.Name ?? string.Empty, voucher));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 按选中清单或查询条件批量导出凭证 PDF（服务端用 PaperSharp 渲染，多张凭证按版式分页）。
        /// </summary>
        [HttpPost("pdf")]
        public async Task<IActionResult> PdfBatch(
            Guid accountSetId, [FromBody] VoucherPdfRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var vouchers = await LoadVouchersForPdfAsync(accountSetId, request, cancellationToken);
                if (vouchers.Count == 0) return BadRequest(new { message = "没有可打印的凭证。" });

                var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
                var bytes = VoucherPdfBuilder.Build(vouchers, ToPdfOptions(request));
                var single = vouchers.Count == 1 ? vouchers[0] : null;
                return File(bytes, VoucherPdfBuilder.ContentType, VoucherPdfBuilder.BuildFileName(accountSet?.Name ?? string.Empty, single));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>按选中 id 或查询条件取出待打印的凭证明细（保持传入顺序）。</summary>
        private async Task<List<VoucherDetailDto>> LoadVouchersForPdfAsync(
            Guid accountSetId, VoucherPdfRequest request, CancellationToken cancellationToken)
        {
            var result = new List<VoucherDetailDto>();
            if (request.Ids is { Count: > 0 })
            {
                foreach (var id in request.Ids)
                {
                    var detail = await _voucherService.GetAsync(accountSetId, id, cancellationToken);
                    if (detail is not null) result.Add(detail);
                }

                return result;
            }

            var list = await _voucherService.ListAsync(accountSetId, request.Query ?? new VoucherQuery(), cancellationToken);
            foreach (var item in list)
            {
                var detail = await _voucherService.GetAsync(accountSetId, item.Id, cancellationToken);
                if (detail is not null) result.Add(detail);
            }

            return result;
        }

        private static VoucherPdfOptions ToPdfOptions(VoucherPdfRequest request) => new()
        {
            Paper = request.Paper,
            Orientation = request.Orientation,
            Valign = request.Valign,
            Rows = request.Rows,
            FontSize = request.FontSize,
            MarginLeft = request.MarginLeft,
            MarginRight = request.MarginRight,
            MarginTop = request.MarginTop,
            MarginBottom = request.MarginBottom,
            SignPreparer = request.SignPreparer,
            PreparerText = request.PreparerText,
            SignReviewer = request.SignReviewer,
            ReviewerText = request.ReviewerText,
            SignManager = request.SignManager,
            ManagerText = request.ManagerText,
            SignBookkeeper = request.SignBookkeeper,
            BookkeeperText = request.BookkeeperText,
            SignCashier = request.SignCashier,
            CashierText = request.CashierText,
            SignTime = request.SignTime,
            ShowAccountCode = request.ShowAccountCode,
            ZeroAttachment = request.ZeroAttachment,
            MergePrint = request.MergePrint,
            PrintedAt = DateTimeOffset.Now,
        };

        /// <summary>
        /// 导入外部平台导出的记账凭证文件（.xls / .xlsx）。按凭证号聚合分录、自动补建缺失科目，
        /// 借贷金额原样保留（含负数），返回导入统计与告警信息。
        /// </summary>
        [HttpPost("import")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<VoucherImportResult>> Import(
            Guid accountSetId, IFormFile? file, [FromForm] VoucherImportConflict? conflict = null, CancellationToken cancellationToken = default)
        {
            var conflictMode = conflict ?? VoucherImportConflict.Skip;
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
                var result = await _importService.ImportAsync(accountSetId, stream, conflictMode, cancellationToken);
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
        /// <summary>账套内出现过的分录摘要（去重，最近使用优先），供摘要输入框做候选提示。</summary>
        [HttpGet("summaries")]
        public async Task<ActionResult<IReadOnlyList<string>>> GetSummaryHistory(
            Guid accountSetId, [FromQuery] int limit = 80, CancellationToken cancellationToken = default)
        {
            try
            {
                return Ok(await _voucherService.ListSummaryHistoryAsync(accountSetId, limit, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 按条件或选中清单导出凭证（Excel）。前端「导出 → 选中凭证 / 当前查询凭证 / 所有凭证」三条路径共用此端点。
        /// </summary>
        [HttpPost("export")]
        public async Task<IActionResult> ExportSelected(
            Guid accountSetId, [FromBody] ExportVouchersRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var rows = await _voucherService.ExportRowsAsync(
                    accountSetId, request.Query ?? new VoucherQuery(), request.Ids, cancellationToken);
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

        /// <summary>复制一张凭证：生成同凭证字下的新草稿凭证。</summary>
        [HttpPost("{id:guid}/copy")]
        public async Task<ActionResult<VoucherDetailDto>> Copy(
            Guid accountSetId, Guid id, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
        {
            try
            {
                var preparer = User.FindFirst("displayName")?.Value
                               ?? User.FindFirst("unique_name")?.Value
                               ?? "未知";
                return Ok(await _voucherService.CopyAsync(accountSetId, id, preparer, date, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>红冲一张凭证：生成金额取反的草稿凭证，摘要带「冲销…」前缀。</summary>
        [HttpPost("{id:guid}/reverse")]
        public async Task<ActionResult<VoucherDetailDto>> Reverse(
            Guid accountSetId, Guid id, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
        {
            try
            {
                var preparer = User.FindFirst("displayName")?.Value
                               ?? User.FindFirst("unique_name")?.Value
                               ?? "未知";
                return Ok(await _voucherService.ReverseAsync(accountSetId, id, preparer, date, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>从回收站还原一张凭证。</summary>
        [HttpPost("{id:guid}/restore")]
        public async Task<ActionResult<VoucherDetailDto>> Restore(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var restored = await _voucherService.RestoreAsync(accountSetId, id, cancellationToken);
                return restored is null ? NotFound() : Ok(restored);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>从回收站彻底删除一张凭证（不可恢复）。</summary>
        [HttpDelete("{id:guid}/purge")]
        public async Task<IActionResult> Purge(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                return await _voucherService.PurgeAsync(accountSetId, id, cancellationToken) ? NoContent() : NotFound();
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>清空回收站。</summary>
        [HttpDelete("recycle-bin")]
        public async Task<ActionResult<object>> ClearRecycleBin(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(new { removed = await _voucherService.ClearRecycleBinAsync(accountSetId, cancellationToken) });
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>整理凭证：按凭证号或凭证日期重新顺次编号，返回实际变更的凭证张数。</summary>
        [HttpPost("renumber")]
        public async Task<ActionResult<object>> Renumber(
            Guid accountSetId, [FromBody] RenumberVouchersRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var changed = await _voucherService.RenumberAsync(
                    accountSetId, request.Word, request.PeriodStart, request.PeriodEnd, request.ByDate, cancellationToken);
                return Ok(new { changed });
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>列出某张凭证的附件。</summary>
        [HttpGet("{id:guid}/attachments")]
        public async Task<ActionResult<IReadOnlyList<VoucherAttachmentDto>>> ListAttachments(
            Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _attachmentService.ListAsync(accountSetId, id, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>上传凭证附件（原始单据影像），单文件上限 10 MB。</summary>
        [HttpPost("{id:guid}/attachments")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<VoucherAttachmentDto>> UploadAttachment(
            Guid accountSetId, Guid id, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要上传的附件。" });
            }

            if (file.Length > VoucherAttachmentService.MaxAttachmentBytes)
            {
                return BadRequest(new { message = "附件过大，请上传不超过 10MB 的文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, cancellationToken);
                var dto = await _attachmentService.UploadAsync(
                    accountSetId, id, file.FileName, file.ContentType, buffer.ToArray(), cancellationToken);
                return Ok(dto);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载（预览）某个附件。</summary>
        [HttpGet("{id:guid}/attachments/{attachmentId:guid}/file")]
        public async Task<IActionResult> DownloadAttachment(
            Guid accountSetId, Guid id, Guid attachmentId, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var found = await _attachmentService.DownloadAsync(accountSetId, id, attachmentId, cancellationToken);
                if (found is null)
                {
                    return NotFound();
                }

                var (attachment, content) = found.Value;
                // 预览时不加 attachment 头，避免浏览器直接下载；?download=true 时强制下载。
                if (download)
                {
                    return File(content, attachment.ContentType, attachment.FileName);
                }

                Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(attachment.FileName)}";
                return File(content, attachment.ContentType);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除某个附件。</summary>
        [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
        public async Task<IActionResult> DeleteAttachment(Guid accountSetId, Guid id, Guid attachmentId, CancellationToken cancellationToken)
        {
            try
            {
                return await _attachmentService.DeleteAsync(accountSetId, id, attachmentId, cancellationToken)
                    ? NoContent()
                    : NotFound();
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
