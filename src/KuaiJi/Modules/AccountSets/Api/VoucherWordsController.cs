using KuaiJi.Modules.AccountSets.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.AccountSets.Api
{
    /// <summary>凭证字接口。默认凭证字有且仅有一个，设默认时自动取消其他默认。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/voucherwords")]
    [Authorize]
    public class VoucherWordsController : ControllerBase
    {
        private readonly VoucherWordService _wordService;

        public VoucherWordsController(VoucherWordService wordService)
        {
            _wordService = wordService;
        }

        /// <summary>获取全部凭证字（首访按账套默认凭证字补种 记/收/付/转）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<VoucherWordDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _wordService.ListAsync(accountSetId, cancellationToken));
            }
            catch (SettingsException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建凭证字。</summary>
        [HttpPost]
        public async Task<ActionResult<VoucherWordDto>> Create(Guid accountSetId, [FromBody] SaveVoucherWordRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _wordService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (SettingsException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改凭证字。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<VoucherWordDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveVoucherWordRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var word = await _wordService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return word is null ? NotFound() : Ok(word);
            }
            catch (SettingsException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除凭证字（默认不可删）。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _wordService.DeleteAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (SettingsException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
