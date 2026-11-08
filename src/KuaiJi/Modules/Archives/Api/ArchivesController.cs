using KuaiJi.Modules.Archives.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Archives.Api
{
    /// <summary>会计电子档案接口：登记 / 修改 / 删除 / 审核 / 归档 / 下载 / 空间统计。</summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/archives")]
    [Authorize]
    public class ArchivesController : ControllerBase
    {
        private readonly ArchiveService _archiveService;
        private readonly ArchiveCategoryService _categoryService;

        public ArchivesController(ArchiveService archiveService, ArchiveCategoryService categoryService)
        {
            _archiveService = archiveService;
            _categoryService = categoryService;
        }

        /// <summary>获取未归档档案列表。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ArchiveItemDto>>> GetList(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _archiveService.GetListAsync(accountSetId, cancellationToken));
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>获取已归档档案列表。</summary>
        [HttpGet("archived")]
        public async Task<ActionResult<IReadOnlyList<ArchiveItemDto>>> GetArchivedList(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _archiveService.GetArchivedListAsync(accountSetId, cancellationToken));
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>已用空间（免费版总量 10 MB）。</summary>
        [HttpGet("usage")]
        public async Task<ActionResult<ArchiveUsageDto>> GetUsage(Guid accountSetId, CancellationToken cancellationToken)
        {
            return Ok(await _archiveService.GetUsageAsync(accountSetId, cancellationToken));
        }

        /// <summary>新建档案（可含 Base64 文件内容）。</summary>
        [HttpPost]
        public async Task<ActionResult<ArchiveItemDto>> Create(Guid accountSetId, [FromBody] SaveArchiveRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _archiveService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>修改档案元数据。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ArchiveItemDto>> Update(Guid accountSetId, Guid id, [FromBody] SaveArchiveRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var item = await _archiveService.UpdateAsync(accountSetId, id, request, cancellationToken);
                return item is null ? NotFound() : Ok(item);
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>分类树节点（扁平列表，前端按 ParentId 组树；首次访问自动播种内置分类）。</summary>
        [HttpGet("categories")]
        public async Task<ActionResult<IReadOnlyList<ArchiveCategoryDto>>> GetCategories(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _categoryService.GetListAsync(accountSetId, cancellationToken));
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>在指定节点下新增子分类。</summary>
        [HttpPost("categories")]
        public async Task<ActionResult<ArchiveCategoryDto>> CreateCategory(Guid accountSetId, [FromBody] SaveArchiveCategoryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _categoryService.CreateAsync(accountSetId, request, cancellationToken));
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>重命名自建分类（同步档案分类路径）。</summary>
        [HttpPut("categories/{categoryId:guid}")]
        public async Task<ActionResult<ArchiveCategoryDto>> RenameCategory(Guid accountSetId, Guid categoryId, [FromBody] SaveArchiveCategoryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var dto = await _categoryService.RenameAsync(accountSetId, categoryId, request, cancellationToken);
                return dto is null ? NotFound() : Ok(dto);
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除自建分类（有子节点或已挂档案时拒绝）。</summary>
        [HttpDelete("categories/{categoryId:guid}")]
        public async Task<IActionResult> DeleteCategory(Guid accountSetId, Guid categoryId, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _categoryService.DeleteAsync(accountSetId, categoryId, cancellationToken);
                return deleted ? NoContent() : NotFound();
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>删除档案。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _archiveService.DeleteAsync(accountSetId, id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>批量审核 / 取消审核。</summary>
        [HttpPost("audit")]
        public async Task<ActionResult<int>> SetAudited(Guid accountSetId, [FromBody] AuditArchiveRequest request, CancellationToken cancellationToken)
        {
            return Ok(await _archiveService.SetAuditedAsync(accountSetId, request, cancellationToken));
        }

        /// <summary>批量标记 / 取消标记为纸质档案。</summary>
        [HttpPost("paper")]
        public async Task<ActionResult<int>> SetPaper(Guid accountSetId, [FromBody] PaperArchiveRequest request, CancellationToken cancellationToken)
        {
            return Ok(await _archiveService.SetPaperAsync(accountSetId, request, cancellationToken));
        }

        /// <summary>批量关联 / 取消关联凭证。</summary>
        [HttpPost("link-voucher")]
        public async Task<ActionResult<int>> LinkVoucher(Guid accountSetId, [FromBody] LinkVoucherRequest request, CancellationToken cancellationToken)
        {
            return Ok(await _archiveService.LinkVoucherAsync(accountSetId, request, cancellationToken));
        }

        /// <summary>由档案生成凭证：选中档案合并为一张借贷凭证，凭证字号回写档案。</summary>
        [HttpPost("voucher-from")]
        public async Task<ActionResult<object>> GenerateVoucher(Guid accountSetId, [FromBody] GenerateVoucherFromArchivesRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var preparer = User.FindFirst("displayName")?.Value ?? User.FindFirst("unique_name")?.Value ?? "系统";
                var voucherText = await _archiveService.GenerateVoucherAsync(accountSetId, request, preparer, cancellationToken);
                return Ok(new { voucherText });
            }
            catch (ArchiveException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>批量归档。</summary>
        [HttpPost("archive")]
        public async Task<ActionResult<int>> Archive(Guid accountSetId, [FromBody] ArchiveIdsRequest request, CancellationToken cancellationToken)
        {
            var operatorName = User.FindFirst("displayName")?.Value ?? User.FindFirst("unique_name")?.Value ?? "未知";
            return Ok(await _archiveService.ArchiveAsync(accountSetId, request, operatorName, cancellationToken));
        }

        /// <summary>下载档案文件。</summary>
        [HttpGet("{id:guid}/download")]
        public async Task<IActionResult> Download(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var result = await _archiveService.DownloadAsync(accountSetId, id, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            return File(result.Value.Content, "application/octet-stream", result.Value.FileName);
        }
    }
}
