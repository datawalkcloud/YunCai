using YunCai.Modules.Reports.Domain;
using YunCai.Modules.Reports.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.Reports.Api
{
    /// <summary>自定义报表（数据透视表）接口：报表定义 CRUD 与实时渲染。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/custom-reports")]
    [Authorize]
    public class CustomReportsController : ControllerBase
    {
        private readonly CustomReportService _service;

        public CustomReportsController(CustomReportService service)
        {
            _service = service;
        }

        /// <summary>自定义报表定义列表（按编码排序）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<CustomReportDto>>> GetList(
            Guid accountSetId,
            CancellationToken cancellationToken)
        {
            return Ok(await _service.GetListAsync(accountSetId, cancellationToken));
        }

        /// <summary>新建报表定义。</summary>
        [HttpPost]
        public async Task<ActionResult<CustomReportDto>> Create(
            Guid accountSetId,
            [FromBody] SaveCustomReportRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.CreateAsync(accountSetId, request, CurrentUserName(), cancellationToken);
                return Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>更新报表定义（名称与取值口径）。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<CustomReportDto>> Update(
            Guid accountSetId,
            Guid id,
            [FromBody] SaveCustomReportRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _service.UpdateAsync(accountSetId, id, request, CurrentUserName(), cancellationToken);
                return result is null ? NotFound(new { message = "自定义报表不存在。" }) : Ok(result);
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除报表定义。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            return await _service.DeleteAsync(accountSetId, id, cancellationToken)
                ? NoContent()
                : NotFound(new { message = "自定义报表不存在。" });
        }

        /// <summary>渲染报表：科目 × 12 个月的透视矩阵。可选 year 指定年度。</summary>
        [HttpGet("{id:guid}/render")]
        public async Task<ActionResult<CustomReportRenderDto>> Render(
            Guid accountSetId,
            Guid id,
            [FromQuery] int? year,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _service.RenderAsync(accountSetId, id, year, cancellationToken));
            }
            catch (ReportException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>操作人名称：取 JWT 显示名，缺省回退登录名。</summary>
        private string CurrentUserName()
        {
            return User.FindFirst("displayName")?.Value
                   ?? User.FindFirst("unique_name")?.Value
                   ?? "系统管理员";
        }
    }
}
