using KuaiJi.Modules.AccountSets.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.AccountSets.Api
{
    /// <summary>币别接口。每个账套必须有且仅有一个本位币，删除本位币前须先转移。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/currencies")]
    [Authorize]
    public class CurrenciesController : ControllerBase
    {
        private readonly CurrencyService _currencyService;

        public CurrenciesController(CurrencyService currencyService)
        {
            _currencyService = currencyService;
        }

        /// <summary>获取全部币别（首访自动补种人民币本位币）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<CurrencyDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _currencyService.ListAsync(accountSetId, cancellationToken));
            }
            catch (SettingsException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建币别。</summary>
        [HttpPost]
        public async Task<ActionResult<CurrencyDto>> Create(Guid accountSetId, [FromBody] SaveCurrencyRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _currencyService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (SettingsException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改币别。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<CurrencyDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveCurrencyRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var currency = await _currencyService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return currency is null ? NotFound() : Ok(currency);
            }
            catch (SettingsException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除币别（本位币不可删）。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _currencyService.DeleteAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (SettingsException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
