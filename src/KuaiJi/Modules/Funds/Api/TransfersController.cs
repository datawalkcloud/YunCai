using KuaiJi.Modules.Funds.Application;
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

        public TransfersController(TransferService transferService)
        {
            _transferService = transferService;
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
    }
}
