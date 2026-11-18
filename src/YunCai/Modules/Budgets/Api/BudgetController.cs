using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using YunCai.Modules.Budgets.Application;
using YunCai.Modules.Budgets.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.Budgets.Api
{
    /// <summary>预算管理：年度科目预算的新增 / 修改 / 删除与执行对比。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/budgets")]
    [Authorize]
    public class BudgetController : ControllerBase
    {
        private readonly BudgetService _service;

        public BudgetController(BudgetService service)
        {
            _service = service;
        }

        private string CurrentUserName =>
            User.FindFirstValue("displayName")
            ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            ?? "未知";

        /// <summary>预算单列表；按年度筛选，缺省返回全部年度。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<BudgetSummaryDto>>> GetList(
            Guid accountSetId,
            [FromQuery] int? year,
            CancellationToken cancellationToken)
        {
            return Ok(await _service.GetListAsync(accountSetId, year, cancellationToken));
        }

        /// <summary>预算单详情（含逐月明细）。</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<BudgetDto>> GetById(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var budget = await _service.GetAsync(accountSetId, id, cancellationToken);
            return budget is null ? NotFound() : Ok(budget);
        }

        /// <summary>新建预算单；同一年度名称不可重复。</summary>
        [HttpPost]
        public async Task<ActionResult<BudgetDto>> Create(
            Guid accountSetId,
            [FromBody] SaveBudgetRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var dto = await _service.CreateAsync(accountSetId, request, CurrentUserName, cancellationToken);
                return CreatedAtAction(nameof(GetById), new { accountSetId, id = dto.Id }, dto);
            }
            catch (BudgetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改预算单：名称 / 备注 / 预算行整体覆盖，年度不可改。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<BudgetDto>> Update(
            Guid accountSetId,
            Guid id,
            [FromBody] SaveBudgetRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var dto = await _service.UpdateAsync(accountSetId, id, request, cancellationToken);
                return dto is null ? NotFound() : Ok(dto);
            }
            catch (BudgetException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除预算单。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _service.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>预算执行对比：按月列出预算与实际发生额（实际数取已过账凭证）。</summary>
        [HttpGet("{id:guid}/execution")]
        public async Task<ActionResult<BudgetExecutionDto>> GetExecution(
            Guid accountSetId,
            Guid id,
            [FromQuery] int? throughMonth,
            CancellationToken cancellationToken)
        {
            var report = await _service.GetExecutionAsync(accountSetId, id, throughMonth, cancellationToken);
            return report is null ? NotFound() : Ok(report);
        }
    }
}
