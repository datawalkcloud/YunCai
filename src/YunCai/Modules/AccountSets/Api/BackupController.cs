using YunCai.Modules.AccountSets.Application;
using YunCai.SharedKernel.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.AccountSets.Api
{
    /// <summary>
    /// 账套备份恢复接口：导出整账套 JSON 数据包 / 从数据包恢复。
    /// </summary>
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/backup")]
    [Authorize]
    public class BackupController : ControllerBase
    {
        private readonly AccountSetBackupService _backupService;

        public BackupController(AccountSetBackupService backupService)
        {
            _backupService = backupService;
        }

        /// <summary>导出账套备份数据包（JSON）。</summary>
        [HttpGet]
        public async Task<IActionResult> Export(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var bytes = await _backupService.ExportAsync(accountSetId, cancellationToken);
                var name = $"账套备份_{DateTime.Now:yyyyMMddHHmm}.json";
                return File(bytes, "application/json", name);
            }
            catch (ReinitException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>从备份数据包恢复（清空账套现有业务数据后写回，高危操作）。</summary>
        [HttpPost("restore")]
        [RequestSizeLimit(200 * 1024 * 1024)]
        public async Task<ActionResult<BackupRestoreResultDto>> Restore(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要恢复的备份数据包。" });
            }

            try
            {
                var preparer = User.FindFirst("displayName")?.Value ?? User.FindFirst("unique_name")?.Value ?? "系统";
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
                await using var stream = file.OpenReadStream();
                return Ok(await _backupService.RestoreAsync(accountSetId, stream, userId, preparer, cancellationToken));
            }
            catch (ReinitException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
