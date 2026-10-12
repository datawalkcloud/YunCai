using KuaiJi.Modules.Funds.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Funds.Api
{
    /// <summary>
    /// 收支类别接口。类别是日记账记账时对流水归类的字典项（分收入 / 支出两类），
    /// 归属于某一账套；分录按类别名称关联，删除类别不影响既有流水。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/entrycategories")]
    [Authorize]
    public class EntryCategoriesController : ControllerBase
    {
        private readonly EntryCategoryService _categoryService;

        public EntryCategoriesController(EntryCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// <summary>获取指定账套的全部收支类别（按类型、名称排列）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<EntryCategoryDto>>> GetAll(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _categoryService.ListAsync(accountSetId, cancellationToken));
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>新建收支类别。同一账套同一类型内名称不可重复。</summary>
        [HttpPost]
        public async Task<ActionResult<EntryCategoryDto>> Create(Guid accountSetId, [FromBody] CreateEntryCategoryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _categoryService.CreateAsync(accountSetId, request, cancellationToken);
                return Ok(category);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改类别名称。既有流水中的类别文本不随之变更。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<EntryCategoryDto>> Update(Guid accountSetId, Guid id, [FromBody] UpdateEntryCategoryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var category = await _categoryService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return category is null ? NotFound() : Ok(category);
            }
            catch (JournalException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除类别。既有流水中的类别文本保留不变。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _categoryService.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}
