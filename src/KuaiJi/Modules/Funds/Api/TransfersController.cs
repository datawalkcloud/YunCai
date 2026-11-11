using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Funds.Application;
using KuaiJi.Modules.Funds.Infrastructure;
using KuaiJi.SharedKernel.Infrastructure.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Funds.Api
{
    /// <summary>
    /// 内部转账接口：账套下资金账户之间的划转台账。创建时自动在两端日记账登记
    /// 转出 / 转入流水；可生成转账凭证（借 转入科目 / 贷 转出科目）。
    /// 已生成凭证的转账不允许修改或删除。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/transfers")]
    [Authorize]
    public class TransfersController : ControllerBase
    {
        private readonly TransferService _transferService;
        private readonly TransferImportService _transferImportService;
        private readonly IAccountSetRepository _accountSetRepository;

        public TransfersController(
            TransferService transferService,
            TransferImportService transferImportService,
            IAccountSetRepository accountSetRepository)
        {
            _transferService = transferService;
            _transferImportService = transferImportService;
            _accountSetRepository = accountSetRepository;
        }

        /// <summary>获取账套内全部内部转账。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<TransferDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _transferService.ListAsync(accountSetId, cancellationToken));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建内部转账（自动登记两端流水）。</summary>
        [HttpPost]
        public async Task<ActionResult<TransferDto>> Create(Guid accountSetId, [FromBody] SaveTransferRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _transferService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改内部转账（未生成凭证时允许）。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<TransferDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveTransferRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var transfer = await _transferService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return transfer is null ? NotFound() : Ok(transfer);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除内部转账（含两端流水；已生成凭证时不允许）。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _transferService.DeleteAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出转账台账（.xlsx）。</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var list = await _transferService.ListAsync(accountSetId, cancellationToken);
                return File(TransferExportBuilder.Build(list), TransferExportBuilder.ContentType,
                    $"内部转账_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 打印转账台账为 PDF：列、筛选结果与合计行按页面表格 1:1 还原，
        /// 「显示全部」与列头漏斗条件由查询参数原样带入。
        /// </summary>
        [HttpGet("pdf")]
        public async Task<IActionResult> Pdf(
            Guid accountSetId,
            [FromQuery] TransferPrintQuery? query,
            CancellationToken cancellationToken)
        {
            try
            {
                var transfers = await _transferService.ListAsync(accountSetId, cancellationToken);
                var context = await PrintContextAsync(accountSetId, cancellationToken);
                var sheet = FundsPdfProjector.Transfers(transfers, query, context);
                var bytes = PrintSheetPdfBuilder.Build(sheet);
                return File(bytes, PrintSheetPdfBuilder.ContentType,
                    PrintSheetPdfBuilder.BuildFileName(sheet.Title, context.AccountSetName, PeriodSuffixOf(query)));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载内部转账导入模板（.xlsx）。</summary>
        [HttpGet("import/template")]
        public IActionResult DownloadTemplate()
        {
            return File(TransferImportService.BuildTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "内部转账导入模板.xlsx");
        }

        /// <summary>导入内部转账（.xls / .xlsx），非法行跳过并告警。</summary>
        [HttpPost("import")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<TransferImportResult>> Import(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的内部转账文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                return Ok(await _transferImportService.ImportAsync(accountSetId, stream, cancellationToken));
            }
            catch (TransferImportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>批量删除（已生成凭证的跳过），返回删除条数。</summary>
        [HttpPost("bulk-delete")]
        public async Task<ActionResult<int>> BulkDelete(Guid accountSetId, [FromBody] BulkTransferRequest request, CancellationToken cancellationToken)
        {
            return Ok(await _transferService.DeleteManyAsync(accountSetId, request.Ids, cancellationToken));
        }

        /// <summary>批量生成转账凭证（已生成凭证的跳过），返回成功张数。</summary>
        [HttpPost("vouchers")]
        public async Task<ActionResult<int>> GenerateVouchers(Guid accountSetId, [FromBody] BulkTransferRequest request, CancellationToken cancellationToken)
        {
            var preparer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "未知";
            return Ok(await _transferService.GenerateVouchersAsync(accountSetId, request.Ids, preparer, null, cancellationToken));
        }

        /// <summary>由转账生成一张凭证（借 转入科目 / 贷 转出科目）。</summary>
        [HttpPost("{id:guid}/voucher")]
        public async Task<ActionResult<GenerateVoucherResultDto>> GenerateVoucher(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var preparer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "未知";
            try
            {
                var voucherId = await _transferService.GenerateVoucherAsync(accountSetId, id, preparer, null, cancellationToken);
                return Ok(new GenerateVoucherResultDto { VoucherId = voucherId });
            }
            catch (JournalException ex)
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

        /// <summary>文件名里的期间后缀，如 202601-202603；勾选「显示全部」或未选区间时为空。</summary>
        private static string PeriodSuffixOf(TransferPrintQuery? query)
        {
            if (query?.ShowAll == true)
            {
                return string.Empty;
            }

            var start = query?.StartDate;
            var end = query?.EndDate;
            if (start is not null && end is not null)
            {
                return $"{start.Value:yyyyMM}-{end.Value:yyyyMM}";
            }

            var single = start ?? end;
            return single is null ? string.Empty : $"{single.Value:yyyyMM}";
        }
    }
}
