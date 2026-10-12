using KuaiJi.Modules.Vouchers.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Vouchers.Api
{
    /// <summary>凭证模板接口：分录骨架的套用字典，行整体替换。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/vouchertemplates")]
    [Authorize]
    public class VoucherTemplatesController : ControllerBase
    {
        private readonly VoucherTemplateService _templateService;

        public VoucherTemplatesController(VoucherTemplateService templateService)
        {
            _templateService = templateService;
        }

        /// <summary>获取全部凭证模板（含模板行与科目解析）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<VoucherTemplateDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _templateService.ListAsync(accountSetId, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建凭证模板。</summary>
        [HttpPost]
        public async Task<ActionResult<VoucherTemplateDto>> Create(Guid accountSetId, [FromBody] SaveVoucherTemplateRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _templateService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改凭证模板（整体替换模板行）。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<VoucherTemplateDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveVoucherTemplateRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var template = await _templateService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return template is null ? NotFound() : Ok(template);
            }
            catch (VoucherException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除凭证模板。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _templateService.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}
