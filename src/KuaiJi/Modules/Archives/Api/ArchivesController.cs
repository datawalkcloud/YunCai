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

        public ArchivesController(ArchiveService archiveService)
        {
            _archiveService = archiveService;
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

        /// <summary>批量归档。</summary>
        [HttpPost("archive")]
        public async Task<ActionResult<int>> Archive(Guid accountSetId, [FromBody] ArchiveIdsRequest request, CancellationToken cancellationToken)
        {
            return Ok(await _archiveService.ArchiveAsync(accountSetId, request, "会计002", cancellationToken));
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
