using YunCai.Modules.Assist.Application;
using YunCai.Modules.Assist.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.Assist.Api
{
    /// <summary>
    /// 辅助核算档案（核算项目）接口。档案按类别（客户/供应商/职员/部门/项目/存货/现金流）归属于某一账套，
    /// 供凭证分录挂载，是核算项目账页与现金流量表取数的维度来源。
    /// 类别的启用开关在账套上维护（PUT /accountsets/{id}/assist-types），未启用的类别不可建档案。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/[controller]")]
    [Authorize]
    public class AssistItemsController : ControllerBase
    {
        private readonly AssistItemService _assistItemService;

        public AssistItemsController(AssistItemService assistItemService)
        {
            _assistItemService = assistItemService;
        }

        /// <summary>获取七个辅助核算类别的概览：中文名、本账套是否启用、该类别下的档案数。</summary>
        [HttpGet("categories")]
        public async Task<ActionResult<IReadOnlyList<AssistCategoryStatDto>>> GetCategories(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assistItemService.GetCategoriesAsync(accountSetId, cancellationToken));
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>获取指定类别的全部档案（按编码排序，含非末级与已停用档案，前端自行组树）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AssistItemDto>>> GetAll(
            Guid accountSetId,
            [FromQuery] AssistType type,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assistItemService.GetAllAsync(accountSetId, type, cancellationToken));
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>获取指定类别下的直接下级档案；不传 parentId 时返回顶级档案（仅层级型类别有下级）。</summary>
        [HttpGet("children")]
        public async Task<ActionResult<IReadOnlyList<AssistItemDto>>> GetChildren(
            Guid accountSetId,
            [FromQuery] AssistType type,
            [FromQuery] Guid? parentId,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assistItemService.GetChildrenAsync(accountSetId, type, parentId, cancellationToken));
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 取下一个建议编码，供新增表单预填：层级型类别在上级编码后补一段流水，
        /// 客户/供应商/职员返回 4 位流水号。
        /// </summary>
        [HttpGet("next-code")]
        public async Task<ActionResult<string>> NextCode(
            Guid accountSetId,
            [FromQuery] AssistType type,
            [FromQuery] Guid? parentId,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assistItemService.NextCodeAsync(accountSetId, type, parentId, cancellationToken));
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新增档案。类别须已在账套启用，编码长度须符合该类别的编码方案且在同类别内唯一。</summary>
        [HttpPost]
        public async Task<ActionResult<AssistItemDto>> Create(Guid accountSetId, [FromBody] CreateAssistItemRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assistItemService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改档案的名称、联系方式与备注（编码与所属类别不可变更）。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AssistItemDto>> Update(Guid accountSetId, Guid id, [FromBody] UpdateAssistItemRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var item = await _assistItemService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return item is null ? NotFound() : Ok(item);
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>启用/停用档案。停用后新凭证不可选用，历史凭证与账簿仍保留该档案。</summary>
        [HttpPatch("{id:guid}/enabled")]
        public async Task<ActionResult<AssistItemDto>> SetEnabled(
            Guid accountSetId,
            Guid id,
            [FromBody] SetAssistItemEnabledRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var item = await _assistItemService.SetEnabledAsync(accountSetId, id, request.Enabled, cancellationToken);
                return item is null ? NotFound() : Ok(item);
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除档案。存在下级或已被凭证分录引用时不可删除（改提示为停用）。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _assistItemService.DeleteAsync(accountSetId, id, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
