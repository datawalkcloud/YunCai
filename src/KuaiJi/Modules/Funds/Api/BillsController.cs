using KuaiJi.Modules.Funds.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Funds.Api
{
    /// <summary>票据管理接口：应收 / 应付票据台账。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/bills")]
    [Authorize]
    public class BillsController : ControllerBase
    {
        private readonly BillService _billService;

        public BillsController(BillService billService)
        {
            _billService = billService;
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

        /// <summary>删除票据。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _billService.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}
