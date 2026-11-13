using System.Globalization;
using YunCai.Modules.Expenses.Application;
using YunCai.Modules.Expenses.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace YunCai.Modules.Expenses.Api
{
    /// <summary>
    /// 费用单据接口：登记 / 修改 / 删除 / 批量删除 / 导入导出 / 生成凭证。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/expensebills")]
    [Authorize]
    public class ExpensesController : ControllerBase
    {
        private readonly ExpenseService _expenseService;

        public ExpensesController(ExpenseService expenseService)
        {
            _expenseService = expenseService;
        }

        /// <summary>获取全部费用单据。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ExpenseBillDto>>> GetBills(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _expenseService.GetBillsAsync(accountSetId, cancellationToken));
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>费用单据合计。</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<ExpenseBillSummaryDto>> GetSummary(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _expenseService.GetSummaryAsync(accountSetId, cancellationToken));
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建费用单据（编号留空自动生成）。</summary>
        [HttpPost]
        public async Task<ActionResult<ExpenseBillDto>> CreateBill(Guid accountSetId, [FromBody] SaveExpenseBillRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _expenseService.CreateBillAsync(accountSetId, request, cancellationToken));
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改费用单据。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ExpenseBillDto>> UpdateBill(Guid accountSetId, Guid id, [FromBody] SaveExpenseBillRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var bill = await _expenseService.UpdateBillAsync(accountSetId, id, request, cancellationToken);
                return bill is null ? NotFound() : Ok(bill);
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除费用单据（已入账不可删除）。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteBill(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _expenseService.DeleteBillAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>批量删除费用单据。</summary>
        [HttpPost("bulk-delete")]
        public async Task<ActionResult<ExpenseBulkResultDto>> BulkDeleteBills(Guid accountSetId, [FromBody] ExpenseBulkIdsRequest request, CancellationToken cancellationToken)
        {
            var (affected, skipped, messages) = await _expenseService.DeleteBillsAsync(accountSetId, request.Ids ?? [], cancellationToken);
            return Ok(new ExpenseBulkResultDto { Affected = affected, Skipped = skipped, Messages = messages });
        }

        /// <summary>为单张费用单据生成凭证。</summary>
        [HttpPost("{id:guid}/voucher")]
        public async Task<ActionResult<ExpenseVoucherResultDto>> GenerateVoucher(Guid accountSetId, Guid id, [FromBody] GenerateExpenseVoucherRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var preparer = User.FindFirst("displayName")?.Value ?? User.Identity?.Name ?? "系统";
                var body = new GenerateExpenseVoucherRequest
                {
                    ExpenseAccountId = request.ExpenseAccountId,
                    CounterAccountId = request.CounterAccountId,
                    TaxAccountId = request.TaxAccountId,
                    Word = request.Word,
                    Preparer = preparer,
                };
                var text = await _expenseService.GenerateVoucherAsync(accountSetId, id, body, cancellationToken);
                return Ok(new ExpenseVoucherResultDto { VoucherText = text });
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>为多张费用单据批量生成凭证。</summary>
        [HttpPost("vouchers")]
        public async Task<ActionResult<ExpenseVoucherBatchResultDto>> GenerateVouchers(Guid accountSetId, [FromBody] GenerateExpenseVouchersRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var preparer = User.FindFirst("displayName")?.Value ?? User.Identity?.Name ?? "系统";
                var body = new GenerateExpenseVouchersRequest
                {
                    BillIds = request.BillIds,
                    ExpenseAccountId = request.ExpenseAccountId,
                    CounterAccountId = request.CounterAccountId,
                    TaxAccountId = request.TaxAccountId,
                    Word = request.Word,
                    Preparer = preparer,
                };
                return Ok(await _expenseService.GenerateVouchersAsync(accountSetId, body, cancellationToken));
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出费用单据为 .xlsx。</summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportBills(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _expenseService.ExportBillsAsync(accountSetId, cancellationToken);
                return File(bytes, ExpenseExportBuilder.ContentType, "费用单据.xlsx");
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载费用单据导入模板。</summary>
        [HttpGet("import-template")]
        public IActionResult DownloadImportTemplate()
            => File(_expenseService.BuildImportTemplate(), ExpenseExportBuilder.ContentType, "费用单据导入模板.xlsx");

        /// <summary>导入费用单据（按单据编号匹配，存在则更新）。</summary>
        [HttpPost("import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<ExpenseImportResultDto>> ImportBills(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var rows = ReadExpenseRows(stream);
                return Ok(await _expenseService.ImportBillsAsync(accountSetId, rows, cancellationToken));
            }
            catch (ExpenseException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return BadRequest(new { message = "文件读取失败，请确认是 .xlsx 格式的导入模板。" });
            }
        }

        /// <summary>把工作表读成「列名 → 单元格文本」的行集合（列名取自首行）。</summary>
        private static List<Dictionary<string, string?>> ReadSheet(Stream stream)
        {
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0) ?? throw new ExpenseException("文件中没有工作表。");
            var headerRow = sheet.GetRow(0) ?? throw new ExpenseException("文件缺少表头行。");

            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < headerRow.LastCellNum; i++)
            {
                var text = headerRow.GetCell(i)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    index[text] = i;
                }
            }

            if (index.Count == 0)
            {
                throw new ExpenseException("文件缺少表头行。");
            }

            var rows = new List<Dictionary<string, string?>>();
            for (var r = 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row is null)
                {
                    continue;
                }

                var values = new Dictionary<string, string?>(StringComparer.Ordinal);
                var hasValue = false;
                foreach (var (name, column) in index)
                {
                    var text = row.GetCell(column) switch
                    {
                        null => null,
                        var cell => cell.CellType switch
                        {
                            CellType.Numeric when DateUtil.IsCellDateFormatted(cell)
                                => cell.DateCellValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            CellType.Numeric => cell.NumericCellValue.ToString("0.##", CultureInfo.InvariantCulture),
                            CellType.Formula => cell.NumericCellValue.ToString("0.##", CultureInfo.InvariantCulture),
                            _ => cell.ToString()?.Trim(),
                        },
                    };
                    values[name] = text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        hasValue = true;
                    }
                }

                if (hasValue)
                {
                    values["__row"] = (r + 1).ToString(CultureInfo.InvariantCulture);
                    rows.Add(values);
                }
            }

            return rows;
        }

        private static List<ExpenseImportRow> ReadExpenseRows(Stream stream)
        {
            var sheet = ReadSheet(stream);
            if (sheet.Count > 0 && !sheet[0].ContainsKey("单据日期"))
            {
                throw new ExpenseException("模板不正确：缺少「单据日期」列。");
            }

            string? Get(Dictionary<string, string?> row, string key)
                => row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

            return sheet.Select(row => new ExpenseImportRow
            {
                Row = int.TryParse(Get(row, "__row"), out var n) ? n : 0,
                BillNo = Get(row, "单据编号"),
                BillDate = Get(row, "单据日期"),
                Reason = Get(row, "费用事由"),
                ExpenseType = Get(row, "费用类型"),
                Payee = Get(row, "收款人"),
                SettleMethod = Get(row, "结算方式"),
                Amount = Get(row, "单据金额（含税）"),
                DeductibleTax = Get(row, "可抵扣税额"),
            }).ToList();
        }

        private static bool IsFileError(Exception ex)
            => ex is IOException
                or NPOI.OpenXml4Net.Exceptions.InvalidFormatException
                or ICSharpCode.SharpZipLib.Zip.ZipException;
    }
}
