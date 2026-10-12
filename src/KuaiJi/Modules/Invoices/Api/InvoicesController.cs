using KuaiJi.Modules.Invoices.Application;
using KuaiJi.Modules.Invoices.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Invoices.Api
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

        public InvoicesController(InvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
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

        /// <summary>新建发票（1 销项 / 2 进项，发票号同方向唯一）。</summary>
        [HttpPost]
        public async Task<ActionResult<InvoiceDto>> Create(Guid accountSetId, [FromQuery] int direction, [FromBody] SaveInvoiceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _invoiceService.CreateAsync(accountSetId, (InvoiceDirection)direction, request, cancellationToken));
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
    }
}
