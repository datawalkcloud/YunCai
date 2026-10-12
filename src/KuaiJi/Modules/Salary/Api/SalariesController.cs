using KuaiJi.Modules.Salary.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
