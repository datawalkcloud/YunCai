using System.Globalization;
using KuaiJi.Modules.Salary.Application;
using KuaiJi.Modules.Salary.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KuaiJi.Modules.Salary.Api
{
    /// <summary>工资接口：员工档案 / 五险一金配置 / 月度工资单 / 统计表 / 计提与发放生成凭证。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/salary")]
    [Authorize]
    public class SalariesController : ControllerBase
    {
        private readonly SalaryService _salaryService;

        public SalariesController(SalaryService salaryService)
        {
            _salaryService = salaryService;
        }

        // —— 员工 ——

        /// <summary>获取全部员工。</summary>
        [HttpGet("employees")]
        public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetEmployees(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.GetEmployeesAsync(accountSetId, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建员工（编号唯一）。</summary>
        [HttpPost("employees")]
        public async Task<ActionResult<EmployeeDto>> CreateEmployee(Guid accountSetId, [FromBody] SaveEmployeeRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.CreateEmployeeAsync(accountSetId, request, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改员工档案。</summary>
        [HttpPut("employees/{id:guid}")]
        public async Task<ActionResult<EmployeeDto>> UpdateEmployee(Guid accountSetId, Guid id, [FromBody] SaveEmployeeRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var employee = await _salaryService.UpdateEmployeeAsync(accountSetId, id, request, cancellationToken);
                return employee is null ? NotFound() : Ok(employee);
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除员工。</summary>
        [HttpDelete("employees/{id:guid}")]
        public async Task<IActionResult> DeleteEmployee(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _salaryService.DeleteEmployeeAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        // —— 五险一金 ——

        /// <summary>获取五险一金配置（首访补种默认六险种）。</summary>
        [HttpGet("insurance")]
        public async Task<ActionResult<IReadOnlyList<InsuranceItemDto>>> GetInsurance(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.GetInsuranceAsync(accountSetId, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建五险一金项。</summary>
        [HttpPost("insurance")]
        public async Task<ActionResult<InsuranceItemDto>> CreateInsurance(Guid accountSetId, [FromBody] SaveInsuranceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.CreateInsuranceAsync(accountSetId, request, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改五险一金项。</summary>
        [HttpPut("insurance/{id:guid}")]
        public async Task<ActionResult<InsuranceItemDto>> UpdateInsurance(Guid accountSetId, Guid id, [FromBody] SaveInsuranceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var item = await _salaryService.UpdateInsuranceAsync(accountSetId, id, request, cancellationToken);
                return item is null ? NotFound() : Ok(item);
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除五险一金项。</summary>
        [HttpDelete("insurance/{id:guid}")]
        public async Task<IActionResult> DeleteInsurance(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _salaryService.DeleteInsuranceAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        // —— 月度工资单 ——

        /// <summary>获取某期间工资单（period = yyyyMM）。</summary>
        [HttpGet("bills")]
        public async Task<ActionResult<IReadOnlyList<SalaryBillDto>>> GetBills(Guid accountSetId, [FromQuery] int period, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.GetBillsAsync(accountSetId, period, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>保存工资单（同期间同员工唯一，存在即更新）。</summary>
        [HttpPost("bills")]
        public async Task<ActionResult<SalaryBillDto>> SaveBill(Guid accountSetId, [FromBody] SaveSalaryBillRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.SaveBillAsync(accountSetId, request, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除工资单。</summary>
        [HttpDelete("bills/{id:guid}")]
        public async Task<IActionResult> DeleteBill(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _salaryService.DeleteBillAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>工资统计表（按部门汇总期间区间）。</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<IReadOnlyList<SalarySummaryRowDto>>> GetSummary(
            Guid accountSetId,
            [FromQuery] int startPeriod,
            [FromQuery] int endPeriod,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.GetSummaryAsync(accountSetId, startPeriod, endPeriod, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>计提 / 发放卡片金额（应发合计 / 实发合计）。</summary>
        [HttpGet("accrue-view")]
        public async Task<ActionResult<AccrueViewDto>> GetAccrueView(Guid accountSetId, [FromQuery] int period, CancellationToken cancellationToken)
        {
            try
            {
                var (payable, net) = await _salaryService.GetAccrueViewAsync(accountSetId, period, cancellationToken);
                return Ok(new AccrueViewDto { Payable = payable, Net = net });
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>计提指定期间工资（生成计提凭证）。</summary>
        [HttpPost("generate-accrue")]
        public async Task<ActionResult<GenerateCountDto>> GenerateAccrue(Guid accountSetId, [FromBody] GenerateSalaryVoucherRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var count = await _salaryService.GenerateAccrueVouchersAsync(accountSetId, request.Period, request.Preparer, cancellationToken);
                return Ok(new GenerateCountDto { Count = count });
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>发放指定期间工资（生成发放凭证）。</summary>
        [HttpPost("generate-pay")]
        public async Task<ActionResult<GenerateCountDto>> GeneratePay(Guid accountSetId, [FromBody] GenerateSalaryVoucherRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var count = await _salaryService.GeneratePayVouchersAsync(accountSetId, request.Period, request.Preparer, cancellationToken);
                return Ok(new GenerateCountDto { Count = count });
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // —— 员工导入导出 / 批量 ——

        /// <summary>导出员工名册为 .xlsx（列与导入模板一致，可改完原样导回）。</summary>
        [HttpGet("employees/export")]
        public async Task<IActionResult> ExportEmployees(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _salaryService.ExportEmployeesAsync(accountSetId, cancellationToken);
                return File(bytes, SalaryExportBuilder.ContentType, "员工信息.xlsx");
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>下载员工导入模板（仅表头）。</summary>
        [HttpGet("employees/import-template")]
        public IActionResult DownloadEmployeeTemplate()
            => File(_salaryService.BuildEmployeeTemplate(), SalaryExportBuilder.ContentType, "员工导入模板.xlsx");

        /// <summary>导入员工（按编号匹配，存在则更新）。</summary>
        [HttpPost("employees/import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<EmployeeImportResultDto>> ImportEmployees(
            Guid accountSetId,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var rows = ReadEmployeeRows(stream);
                return Ok(await _salaryService.ImportEmployeesAsync(accountSetId, rows, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return BadRequest(new { message = "文件读取失败，请确认是 .xlsx 格式的导入模板。" });
            }
        }

        /// <summary>批量删除员工；有工资单的员工计入跳过并给出原因。</summary>
        [HttpPost("employees/bulk-delete")]
        public async Task<ActionResult<SalaryBulkResultDto>> BulkDeleteEmployees(
            Guid accountSetId,
            [FromBody] SalaryBulkIdsRequest request,
            CancellationToken cancellationToken)
            => Ok(await _salaryService.DeleteEmployeesAsync(accountSetId, request.Ids ?? [], cancellationToken));

        // —— 工资表导入导出 / 工资条 ——

        /// <summary>导出工资统计表。</summary>
        [HttpGet("summary/export")]
        public async Task<IActionResult> ExportSummary(
            Guid accountSetId,
            [FromQuery] int startPeriod,
            [FromQuery] int endPeriod,
            CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _salaryService.ExportSummaryAsync(accountSetId, startPeriod, endPeriod, cancellationToken);
                return File(bytes, SalaryExportBuilder.ContentType, $"工资统计表_{startPeriod}-{endPeriod}.xlsx");
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导出当期工资表。</summary>
        [HttpGet("bills/export")]
        public async Task<IActionResult> ExportBills(Guid accountSetId, [FromQuery] int period, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _salaryService.ExportBillsAsync(accountSetId, period, cancellationToken);
                return File(bytes, SalaryExportBuilder.ContentType, $"工资表_{period}.xlsx");
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>导入工资表（按编号匹配员工，同期间存在则更新）。</summary>
        [HttpPost("bills/import")]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<ActionResult<SalaryBillImportResultDto>> ImportBills(
            Guid accountSetId,
            [FromQuery] int period,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var rows = ReadBillRows(stream);
                return Ok(await _salaryService.ImportBillsAsync(accountSetId, period, rows, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (IsFileError(ex))
            {
                return BadRequest(new { message = "文件读取失败，请确认是 .xlsx 格式的导入模板。" });
            }
        }

        /// <summary>生成当期工资条 PDF（一页 3 条，带裁切线）。</summary>
        [HttpGet("payslips/pdf")]
        public async Task<IActionResult> BuildPayslips(Guid accountSetId, [FromQuery] int period, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _salaryService.BuildPayslipsAsync(accountSetId, period, cancellationToken);
                return File(bytes, PayslipPdfBuilder.ContentType, $"工资条_{period}.pdf");
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // —— 工资凭证科目设置 ——

        /// <summary>读取工资凭证科目映射（未配置项返回自动推断结果）。</summary>
        [HttpGet("voucher-setting")]
        public async Task<ActionResult<SalaryVoucherSettingDto>> GetVoucherSetting(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.GetVoucherSettingAsync(accountSetId, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>保存工资凭证科目映射；留空的项回落到自动推断。</summary>
        [HttpPut("voucher-setting")]
        public async Task<ActionResult<SalaryVoucherSettingDto>> SaveVoucherSetting(
            Guid accountSetId,
            [FromBody] SaveSalaryVoucherSettingRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _salaryService.SaveVoucherSettingAsync(accountSetId, request, cancellationToken));
            }
            catch (SalaryException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>把工作表读成「列名 → 单元格文本」的行集合（列名取自首行）。</summary>
        private static List<Dictionary<string, string?>> ReadSheet(Stream stream)
        {
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0) ?? throw new SalaryException("文件中没有工作表。");
            var headerRow = sheet.GetRow(0) ?? throw new SalaryException("文件缺少表头行。");

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
                throw new SalaryException("文件缺少表头行。");
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

        private static List<EmployeeImportRow> ReadEmployeeRows(Stream stream)
        {
            var sheet = ReadSheet(stream);
            if (sheet.Count > 0 && !sheet[0].ContainsKey("编号") && !sheet[0].ContainsKey("姓名"))
            {
                throw new SalaryException("模板不正确：缺少「编号」或「姓名」列。");
            }

            string? Get(Dictionary<string, string?> row, string key)
                => row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

            return sheet.Select(row => new EmployeeImportRow
            {
                Row = int.TryParse(Get(row, "__row"), out var n) ? n : 0,
                Code = Get(row, "编号"),
                Name = Get(row, "姓名"),
                GenderText = Get(row, "性别"),
                IdTypeText = Get(row, "证件类型"),
                IdNo = Get(row, "证件号码"),
                Dept = Get(row, "部门"),
                Phone = Get(row, "手机"),
                Email = Get(row, "邮箱"),
                HireDate = Get(row, "入职日期"),
                Post = Get(row, "岗位"),
                Title = Get(row, "职称"),
                SubjectAccountCode = Get(row, "计提工资科目编码") ?? CodePrefix(Get(row, "计提工资科目")),
                StatusText = Get(row, "员工状态"),
                Remark = Get(row, "备注"),
            }).ToList();
        }

        private static List<SalaryBillImportRow> ReadBillRows(Stream stream)
        {
            var sheet = ReadSheet(stream);
            if (sheet.Count > 0 && !sheet[0].ContainsKey("编号") && !sheet[0].ContainsKey("姓名"))
            {
                throw new SalaryException("模板不正确：缺少「编号」或「姓名」列。");
            }

            string? Get(Dictionary<string, string?> row, string key)
                => row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

            return sheet.Select(row => new SalaryBillImportRow
            {
                Row = int.TryParse(Get(row, "__row"), out var n) ? n : 0,
                EmployeeCode = Get(row, "编号"),
                EmployeeName = Get(row, "姓名"),
                Gross = Get(row, "工资总额"),
                Deductible = Get(row, "应扣工资"),
                PersonalDeduction = Get(row, "代扣个人款项"),
                TaxThreshold = Get(row, "个税免征额"),
                Tax = Get(row, "本月应缴个税"),
                Adjust = Get(row, "实发工资调整"),
                CompanyPart = Get(row, "公司承担款项"),
            }).ToList();
        }

        /// <summary>「5602001 管理人员职工薪酬」→ 5602001。</summary>
        private static string? CodePrefix(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var trimmed = text.Trim();
            var index = trimmed.IndexOf(' ', StringComparison.Ordinal);
            return index < 0 ? trimmed : trimmed[..index];
        }

        private static bool IsFileError(Exception ex)
            => ex is IOException
                or NPOI.OpenXml4Net.Exceptions.InvalidFormatException
                or ICSharpCode.SharpZipLib.Zip.ZipException;
    }

    /// <summary>计提 / 发放视图 DTO。</summary>
    public sealed class AccrueViewDto
    {
        public decimal Payable { get; init; }

        public decimal Net { get; init; }
    }

    /// <summary>生成凭证请求（preparer 取当前登录人展示名）。</summary>
    public sealed class GenerateSalaryVoucherRequest
    {
        /// <summary>工资期间（yyyyMM）。</summary>
        public int Period { get; init; }

        public string Preparer { get; init; } = "系统";
    }

    /// <summary>生成条数 DTO。</summary>
    public sealed class GenerateCountDto
    {
        public int Count { get; init; }
    }
}
