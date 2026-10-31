using KuaiJi.Modules.Funds.Application;
using KuaiJi.Modules.Funds.Infrastructure;
using MiniExcelLibs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Funds.Api
{
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/[controller]")]
    [Authorize]
    public class JournalsController : ControllerBase
    {
        // 上传文件大小上限：10 MB，足以覆盖常见的日记账流水导入量。
        private const long MaxUploadBytes = 10 * 1024 * 1024;

        private readonly JournalService _journalService;
        private readonly JournalImportService _importService;
        private readonly JournalAccountImportService _accountImportService;
        private readonly ILogger<JournalsController> _logger;

        public JournalsController(JournalService journalService, JournalImportService importService, JournalAccountImportService accountImportService, ILogger<JournalsController> logger)
        {
            _journalService = journalService;
            _importService = importService;
            _accountImportService = accountImportService;
            _logger = logger;
        }

        /// <summary>获取指定账套的全部日记账（含收支合计与期初/期末余额，按名称排列）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<JournalListItemDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _journalService.ListAsync(accountSetId, cancellationToken));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>按标识获取单本日记账详情（含全部分录及关联凭证号）。</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<JournalDetailDto>> GetById(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var journal = await _journalService.GetAsync(accountSetId, id, cancellationToken);
            return journal is null ? NotFound() : Ok(journal);
        }

        /// <summary>
        /// 建账（新建资金账户）。指定 accountId 时绑定已有货币资金末级科目；
        /// 省略时系统按类型自动在 1001 / 1002 下创建与账簿同名的明细科目。
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<JournalDetailDto>> Create(Guid accountSetId, [FromBody] CreateJournalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var journal = await _journalService.CreateAsync(accountSetId, request, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { accountSetId, id = journal.Id }, journal);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>改账：修改账簿名称与期初余额。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<JournalDetailDto>> Update(Guid accountSetId, Guid id, [FromBody] UpdateJournalRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var journal = await _journalService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return journal is null ? NotFound() : Ok(journal);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>启用 / 停用资金账户（停用后不能再登记新流水，历史数据与报表照常保留）。</summary>
        [HttpPatch("{id:guid}/enabled")]
        public async Task<ActionResult<JournalDetailDto>> SetEnabled(Guid accountSetId, Guid id, [FromBody] SetJournalEnabledRequest request, CancellationToken cancellationToken)
        {
            var journal = await _journalService.SetEnabledAsync(accountSetId, id, request.Enabled, cancellationToken);
            return journal is null ? NotFound() : Ok(journal);
        }

        /// <summary>删除日记账（含其全部分录）。对应的明细科目保留，可在科目管理中另行处理。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _journalService.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>登记一笔流水，登记后自动重排序号与逐行余额，返回最新账簿详情。</summary>
        [HttpPost("{id:guid}/entries")]
        public async Task<ActionResult<JournalDetailDto>> AddEntry(Guid accountSetId, Guid id, [FromBody] JournalEntryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var journal = await _journalService.AddEntryAsync(accountSetId, id, request, cancellationToken);
                return journal is null ? NotFound() : Ok(journal);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改一笔流水（保留原有关联凭证），返回最新账簿详情。</summary>
        [HttpPut("{id:guid}/entries/{entryId:guid}")]
        public async Task<ActionResult<JournalDetailDto>> UpdateEntry(Guid accountSetId, Guid id, Guid entryId, [FromBody] JournalEntryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var journal = await _journalService.UpdateEntryAsync(accountSetId, id, entryId, request, cancellationToken);
                return journal is null ? NotFound() : Ok(journal);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 分录批量操作：批量删除 / 批量修改摘要 / 指定收支类别 / 指定往来单位 / 指定项目 / 指定部门 / 删除关联凭证。
        /// 返回受影响条数。
        /// </summary>
        [HttpPost("{id:guid}/entries/bulk")]
        public async Task<ActionResult<int>> BulkEntries(Guid accountSetId, Guid id, [FromBody] BulkJournalEntryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var affected = await _journalService.BulkEntriesAsync(accountSetId, id, request, cancellationToken);
                return affected is null ? NotFound() : Ok(affected.Value);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除一笔流水，返回最新账簿详情。</summary>
        [HttpDelete("{id:guid}/entries/{entryId:guid}")]
        public async Task<ActionResult<JournalDetailDto>> RemoveEntry(Guid accountSetId, Guid id, Guid entryId, CancellationToken cancellationToken)
        {
            try
            {
                var journal = await _journalService.RemoveEntryAsync(accountSetId, id, entryId, cancellationToken);
                return journal is null ? NotFound() : Ok(journal);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>由指定流水生成一张记账凭证（一借一贷），返回生成的凭证标识。</summary>
        [HttpPost("{id:guid}/entries/{entryId:guid}/voucher")]
        public async Task<ActionResult<GenerateVoucherResultDto>> GenerateVoucher(Guid accountSetId, Guid id, Guid entryId, [FromBody] GenerateVoucherRequest request, CancellationToken cancellationToken)
        {
            // 制单人取自当前登录用户的显示名（JWT 的 displayName 声明）。
            var preparer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "未知";
            try
            {
                var voucherId = await _journalService.GenerateVoucherAsync(accountSetId, id, entryId, preparer, request, cancellationToken);
                return Ok(new GenerateVoucherResultDto { VoucherId = voucherId });
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 由多笔流水批量生成凭证。按合并方式（全部 / 日期 / 收支类别 / 收支方向）分组出票，
        /// 对方科目取统一科目或各类别的默认对方科目；无法确定对方科目时整单拒绝。
        /// </summary>
        [HttpPost("{id:guid}/entries/vouchers")]
        public async Task<ActionResult<GenerateVouchersBatchResultDto>> GenerateVouchersBatch(Guid accountSetId, Guid id, [FromBody] GenerateVouchersBatchRequest request, CancellationToken cancellationToken)
        {
            var preparer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "未知";
            try
            {
                return Ok(await _journalService.GenerateVouchersBatchAsync(accountSetId, id, preparer, request, cancellationToken));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载日记账流水导入模板（.xlsx）。</summary>
        [HttpGet("import/template")]
        public IActionResult DownloadTemplate()
        {
            var bytes = JournalImportTemplateBuilder.Build();
            return File(bytes, JournalImportTemplateBuilder.ContentType, JournalImportTemplateBuilder.FileName);
        }

        /// <summary>下载资金账户批量导入模板（.xlsx）。</summary>
        [HttpGet("accounts/import-template")]
        public IActionResult DownloadAccountTemplate()
        {
            using var stream = new MemoryStream();
            MiniExcelLibs.MiniExcel.SaveAs(stream, new Dictionary<string, object>
            {
                ["资金账户"] = new List<Dictionary<string, object?>>
                {
                    new() { ["账簿名称"] = "库存现金", ["账户类型"] = "现金", ["账户编码"] = "001", ["币别"] = "人民币", ["期初余额"] = 0 },
                    new() { ["账簿名称"] = "工行基本户", ["账户类型"] = "银行存款", ["账户编码"] = "002", ["币别"] = "人民币", ["期初余额"] = 0, ["开户银行"] = "工商银行", ["银行账号"] = "" },
                },
            }, excelType: ExcelType.XLSX);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "资金账户导入模板.xlsx");
        }

        /// <summary>资金账户批量导入：同名同类型账户更新，否则新建（现金 / 银行存款日记账）。</summary>
        [HttpPost("accounts/import")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<JournalAccountImportResultDto>> ImportAccounts(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的 Excel 文件。" });
            }

            if (file.Length > MaxUploadBytes)
            {
                return BadRequest(new { message = "文件过大，请控制在 10MB 以内。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                return Ok(await _accountImportService.ImportAsync(accountSetId, stream, cancellationToken));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return BadRequest(new { message = "文件解析失败：" + ex.Message });
            }
        }

        /// <summary>导出一本日记账的全部流水（.xlsx）。导出列与导入模板同构，文件可回传导入。</summary>
        [HttpGet("{id:guid}/export")]
        public async Task<IActionResult> Export(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var journal = await _journalService.GetAsync(accountSetId, id, cancellationToken);
            if (journal is null)
            {
                return NotFound();
            }

            var bytes = JournalExportBuilder.Build(journal);
            return File(bytes, JournalExportBuilder.ContentType, JournalExportBuilder.BuildFileName(journal.Name));
        }

        /// <summary>导出所有账户的全部流水（.xlsx，含「账户」列）。</summary>
        [HttpGet("export/all")]
        public async Task<IActionResult> ExportAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            var journals = await _journalService.ListAsync(accountSetId, cancellationToken);
            var details = new List<JournalDetailDto>(journals.Count);
            foreach (var item in journals)
            {
                var detail = await _journalService.GetAsync(accountSetId, item.Id, cancellationToken);
                if (detail is not null)
                {
                    details.Add(detail);
                }
            }

            var bytes = JournalExportBuilder.BuildAll(details);
            return File(bytes, JournalExportBuilder.ContentType, $"日记账全部数据_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
        }

        /// <summary>
        /// 导入日记账流水文件（.xls / .xlsx）。非法行跳过并告警，不阻断整体导入，
        /// 返回导入统计与告警信息。
        /// </summary>
        [HttpPost("{id:guid}/import")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<JournalImportResult>> Import(Guid accountSetId, Guid id, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的日记账流水文件。" });
            }

            if (file.Length > MaxUploadBytes)
            {
                return BadRequest(new { message = "文件过大，请上传不超过 10MB 的日记账流水文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _importService.ImportAsync(accountSetId, id, stream, cancellationToken);
                return Ok(result);
            }
            catch (JournalImportException ex)
            {
                // 可预期的业务错误（格式不符、内容缺失等），返回友好提示。
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入日记账流水文件「{FileName}」时发生未预期的错误。", file.FileName);
                return StatusCode(500, new { message = "导入日记账流水时发生错误，请稍后重试。" });
            }
        }
    }

    /// <summary>由日记账流水生成凭证的返回结果。</summary>
    public sealed class GenerateVoucherResultDto
    {
        /// <summary>生成的凭证标识。</summary>
        public Guid VoucherId { get; init; }
    }
}
