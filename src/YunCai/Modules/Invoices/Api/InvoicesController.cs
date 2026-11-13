using System.Globalization;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Invoices.Application;
using YunCai.Modules.Invoices.Domain;
using YunCai.Modules.Invoices.Infrastructure;
using YunCai.SharedKernel.Infrastructure.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace YunCai.Modules.Invoices.Api
{
    /// <summary>
    /// 发票接口：进销项发票池（列表 / 新建 / 修改 / 删除 / 认证 / 状态流转），
    /// 附列表合计与「发票资金一览表」。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/invoices")]
    [Authorize]
    public class InvoicesController : ControllerBase
    {
        private readonly InvoiceService _invoiceService;
        private readonly IAccountSetRepository _accountSetRepository;

        public InvoicesController(InvoiceService invoiceService, IAccountSetRepository accountSetRepository)
        {
            _invoiceService = invoiceService;
            _accountSetRepository = accountSetRepository;
        }

        /// <summary>按方向查询发票列表（含明细行；1 销项 / 2 进项）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<InvoiceDto>>> GetRange(
            Guid accountSetId,
            [FromQuery] int direction,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? status,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _invoiceService.GetRangeAsync(
                    accountSetId,
                    (InvoiceDirection)direction,
                    startDate,
                    endDate,
                    status is { } s ? (InvoiceStatus)s : null,
                    cancellationToken));
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>列表合计（金额 / 税额 / 价税合计）。</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<InvoiceSummaryDto>> GetSummary(
            Guid accountSetId,
            [FromQuery] int direction,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? status,
            CancellationToken cancellationToken)
        {
            return Ok(await _invoiceService.GetSummaryAsync(
                accountSetId,
                (InvoiceDirection)direction,
                startDate,
                endDate,
                status is { } s ? (InvoiceStatus)s : null,
                cancellationToken));
        }

        /// <summary>发票资金一览表（销项开票 vs 资金收款，按往来单位核对）。</summary>
        [HttpGet("fund-overview")]
        public async Task<ActionResult<IReadOnlyList<InvoiceFundOverviewRowDto>>> GetFundOverview(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _invoiceService.GetFundOverviewAsync(accountSetId, startDate, endDate, cancellationToken));
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出发票（.xlsx，按商品明细行展开）。</summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(
            Guid accountSetId,
            [FromQuery] int direction,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? status,
            [FromQuery] string? period,
            CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _invoiceService.ExportAsync(
                    accountSetId,
                    (InvoiceDirection)direction,
                    startDate,
                    endDate,
                    status is { } s ? (InvoiceStatus)s : null,
                    cancellationToken);
                var fileName = InvoiceExportBuilder.BuildFileName((InvoiceDirection)direction, period);
                return File(bytes, InvoiceExportBuilder.ContentType, fileName);
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出「发票资金一览表」（.xlsx）。</summary>
        [HttpGet("fund-overview/export")]
        public async Task<IActionResult> ExportFundOverview(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] string? period,
            CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _invoiceService.ExportFundOverviewAsync(accountSetId, startDate, endDate, cancellationToken);
                var fileName = string.IsNullOrWhiteSpace(period)
                    ? "发票资金一览表.xlsx"
                    : $"发票资金一览表_{period}.xlsx";
                return File(bytes, InvoiceExportBuilder.ContentType, fileName);
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 打印发票清单为 PDF：列与合计均按销项 / 进项列表页的表格 1:1 复刻，
        /// 参数与 <see cref="Export"/> 同名同口径（<c>period</c> 只用于文件名后缀，形如 2026-10）。
        /// </summary>
        [HttpGet("pdf")]
        public async Task<IActionResult> Pdf(
            Guid accountSetId,
            [FromQuery] int direction,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int? status,
            [FromQuery] string? period,
            CancellationToken cancellationToken)
        {
            if (!TryDirection(direction, out var dir))
            {
                return BadRequest(new { message = "发票方向只能是 1（销项）或 2（进项）。" });
            }

            var statusFilter = status is { } raw ? (InvoiceStatus?)raw : null;
            try
            {
                var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                                 ?? throw new InvoiceException("指定的账套不存在。");
                var context = new InvoicePdfProjector.PrintContext(accountSet.Name, accountSet.CompanyName);
                var invoices = await _invoiceService.GetRangeAsync(accountSetId, dir, startDate, endDate, statusFilter, cancellationToken);
                var summary = await _invoiceService.GetSummaryAsync(accountSetId, dir, startDate, endDate, statusFilter, cancellationToken);
                var sheet = InvoicePdfProjector.List(invoices, summary, dir, context, startDate, endDate);
                return File(
                    PrintSheetPdfBuilder.Build(sheet),
                    PrintSheetPdfBuilder.ContentType,
                    PrintSheetPdfBuilder.BuildFileName(sheet.Title, accountSet.Name, period));
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建发票（1 销项 / 2 进项，发票号同方向唯一）。</summary>
        [HttpPost]
        public async Task<ActionResult<InvoiceDto>> Create(Guid accountSetId, [FromQuery] int direction, [FromBody] SaveInvoiceRequest request, CancellationToken cancellationToken)
        {
            if (!TryDirection(direction, out var dir))
            {
                return BadRequest(new { message = "发票方向只能是 1（销项）或 2（进项）。" });
            }

            try
            {
                return Ok(await _invoiceService.CreateAsync(accountSetId, dir, request, cancellationToken));
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改发票。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<InvoiceDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveInvoiceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return invoice is null ? NotFound() : Ok(invoice);
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除发票。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _invoiceService.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>导入发票（.xlsx，模板与导出一致；按发票号码聚合明细行）。</summary>
        [HttpPost("import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<InvoiceImportResult>> Import(
            Guid accountSetId,
            [FromQuery] int direction,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的文件。" });
            }

            if (!TryDirection(direction, out var dir))
            {
                return BadRequest(new { message = "发票方向只能是 1（销项）或 2（进项）。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var rows = ReadImportRows(stream);
                return Ok(await _invoiceService.ImportAsync(
                    accountSetId,
                    dir,
                    rows,
                    cancellationToken));
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (ex is IOException
                                          or NPOI.OpenXml4Net.Exceptions.InvalidFormatException
                                          or ICSharpCode.SharpZipLib.Zip.ZipException)
            {
                return BadRequest(new { message = "文件读取失败，请确认是 .xlsx 格式的导入模板。" });
            }
        }

        /// <summary>下载发票导入模板（仅含表头，列与导出一致）。</summary>
        [HttpGet("import-template")]
        public async Task<IActionResult> DownloadImportTemplate(
            Guid accountSetId,
            [FromQuery] int direction,
            CancellationToken cancellationToken)
        {
            var bytes = await _invoiceService.ExportAsync(
                accountSetId,
                (InvoiceDirection)direction,
                DateOnly.MaxValue,
                DateOnly.MaxValue,
                null,
                cancellationToken);
            var fileName = (InvoiceDirection)direction == InvoiceDirection.Sales
                ? "销项发票导入模板.xlsx"
                : "进项发票导入模板.xlsx";
            return File(bytes, InvoiceExportBuilder.ContentType, fileName);
        }

        /// <summary>批量删除发票；不存在的计入跳过。</summary>
        [HttpPost("bulk-delete")]
        public async Task<ActionResult<BulkInvoiceResultDto>> BulkDelete(
            Guid accountSetId,
            [FromBody] BulkInvoiceIdsRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _invoiceService.DeleteManyAsync(accountSetId, request.Ids ?? [], cancellationToken));
        }

        /// <summary>批量变更入账状态（1 未入账 / 2 已入账 / 3 暂不入账）。</summary>
        [HttpPost("bulk-status")]
        public async Task<ActionResult<BulkInvoiceResultDto>> BulkChangeStatus(
            Guid accountSetId,
            [FromBody] BulkInvoiceStatusRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _invoiceService.ChangeStatusManyAsync(
                    accountSetId,
                    request.Ids ?? [],
                    (InvoiceStatus)request.Status,
                    cancellationToken));
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>批量认证进项发票；销项发票与不存在的计入跳过。</summary>
        [HttpPost("bulk-authenticate")]
        public async Task<ActionResult<BulkInvoiceResultDto>> BulkAuthenticate(
            Guid accountSetId,
            [FromBody] BulkAuthenticateRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _invoiceService.AuthenticateManyAsync(
                accountSetId,
                request.Ids ?? [],
                request.AuthDate,
                request.TaxPeriod,
                cancellationToken));
        }

        /// <summary>进项发票认证（登记认证日期与税款所属期）。</summary>
        [HttpPost("{id:guid}/authenticate")]
        public async Task<ActionResult<InvoiceDto>> Authenticate(Guid accountSetId, Guid id, [FromBody] AuthenticateInvoiceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.AuthenticateAsync(accountSetId, id, request, cancellationToken);
                return invoice is null ? NotFound() : Ok(invoice);
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 由发票生成凭证：销项 借 对方科目 价税合计 / 贷 收入科目 金额、贷 销项税额科目 税额；
        /// 进项 借 商品科目 金额、借 进项税额科目 税额 / 贷 对方科目 价税合计。
        /// 三个科目均可省略，省略时按方向取默认科目（销项 1122/5001/222100105，进项 2202/1405/222100101）。
        /// 生成后发票转为「已入账」并回填关联凭证。
        /// </summary>
        [HttpPost("{id:guid}/voucher")]
        public async Task<ActionResult<InvoiceVoucherResultDto>> GenerateVoucher(
            Guid accountSetId,
            Guid id,
            [FromBody] GenerateInvoiceVoucherRequest request,
            CancellationToken cancellationToken)
        {
            // 制单人取自当前登录用户的显示名（JWT 的 displayName 声明）。
            var preparer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "未知";
            try
            {
                return Ok(await _invoiceService.GenerateVoucherAsync(accountSetId, id, preparer, request, cancellationToken));
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 批量生成发票凭证：逐条出票，已入账 / 暂不入账的计入跳过，
        /// 单条失败不影响其余，返回成功张数与失败明细。
        /// </summary>
        [HttpPost("vouchers")]
        public async Task<ActionResult<InvoiceVoucherBatchResultDto>> GenerateVouchers(
            Guid accountSetId,
            [FromBody] GenerateInvoiceVouchersRequest request,
            CancellationToken cancellationToken)
        {
            var preparer = User.FindFirst("displayName")?.Value
                           ?? User.FindFirst("unique_name")?.Value
                           ?? "未知";
            return Ok(await _invoiceService.GenerateVouchersAsync(
                accountSetId,
                request.Ids ?? [],
                preparer,
                request.Options ?? new GenerateInvoiceVoucherRequest(),
                cancellationToken));
        }

        /// <summary>变更入账状态（1 未入账 / 2 已入账 / 3 暂不入账）。</summary>
        [HttpPost("{id:guid}/status")]
        public async Task<ActionResult<InvoiceDto>> ChangeStatus(Guid accountSetId, Guid id, [FromBody] ChangeInvoiceStatusRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var invoice = await _invoiceService.ChangeStatusAsync(accountSetId, id, request, cancellationToken);
                return invoice is null ? NotFound() : Ok(invoice);
            }
            catch (InvoiceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 按表头定位各列后逐行读取（表头顺序不限，列与导出一致）。
        /// 多行商品共用同一发票号码时，由服务层按号码聚合为一张发票。
        /// </summary>
        private static List<InvoiceImportRow> ReadImportRows(Stream stream)
        {
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0) ?? throw new InvoiceException("文件中没有工作表。");
            var headerRow = sheet.GetRow(0) ?? throw new InvoiceException("文件缺少表头行。");

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < headerRow.LastCellNum; i++)
            {
                var text = headerRow.GetCell(i)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    index[text] = i;
                }
            }

            if (!index.ContainsKey("发票号码"))
            {
                throw new InvoiceException("模板不正确：缺少「发票号码」列。");
            }

            string? Cell(IRow row, params string[] columns)
            {
                foreach (var column in columns)
                {
                    if (!index.TryGetValue(column, out var i))
                    {
                        continue;
                    }

                    var value = row.GetCell(i) switch
                    {
                        null => null,
                        var cell => cell.CellType switch
                        {
                            CellType.Numeric => cell.NumericCellValue.ToString("0.##", CultureInfo.InvariantCulture),
                            _ => cell.ToString()?.Trim(),
                        },
                    };

                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                return null;
            }

            var rows = new List<InvoiceImportRow>();
            for (var r = 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                // 整行为空时跳过（模板占位行 / 用户留空行）。
                if (Cell(row, "发票号码") is null && Cell(row, "商品名称") is null)
                {
                    continue;
                }

                rows.Add(new InvoiceImportRow
                {
                    InvoiceType = Cell(row, "发票种类"),
                    InputDate = ParseDate(Cell(row, "录入日期")),
                    IssueDate = ParseDate(Cell(row, "开票日期")),
                    InvoiceNo = Cell(row, "发票号码"),
                    PartnerName = Cell(row, "客户名称", "供应商名称"),
                    BizType = Cell(row, "业务类型"),
                    GoodsName = Cell(row, "商品名称"),
                    Amount = ParseDecimal(Cell(row, "不含税金额")) ?? 0m,
                    TaxRate = ParseDecimal(Cell(row, "税率(%)", "税率")) ?? 0m,
                    TaxAmount = ParseDecimal(Cell(row, "税额")),
                    AuthDate = ParseDate(Cell(row, "认证日期")),
                    TaxPeriod = (int?)ParseDecimal(Cell(row, "税款所属期")),
                });
            }

            if (rows.Count == 0)
            {
                throw new InvoiceException("文件中没有可导入的数据行。");
            }

            return rows;
        }

        private static DateOnly? ParseDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // 兼容 yyyy-MM-dd 与 Excel 序列化后的 yyyy/M/d。
            var normalized = text.Replace("/", "-");
            return DateOnly.TryParseExact(normalized, "yyyy-M-d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                   || DateOnly.TryParse(normalized, CultureInfo.InvariantCulture, out d)
                ? d
                : null;
        }

        private static decimal? ParseDecimal(string? text)
            => decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

        /// <summary>
        /// ?direction= 缺失或写错时裸转枚举会得到 0，落库后既不是销项也不是进项，
        /// 列表按方向查询永远看不到它，生成凭证还会静默按进项模板走（借贷方向相反）。
        /// </summary>
        private static bool TryDirection(int raw, out InvoiceDirection direction)
        {
            direction = (InvoiceDirection)raw;
            return direction is InvoiceDirection.Sales or InvoiceDirection.Purchase;
        }
    }
}
