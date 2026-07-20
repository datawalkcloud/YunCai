using KuaiJi.Application.Accounts;
using KuaiJi.Domain.Accounting;
using KuaiJi.Infrastructure.Accounting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Controllers
{
    [ApiController]
    [Route("accountsets/{accountSetId:guid}/[controller]")]
    [Authorize]
    public class AccountsController : ControllerBase
    {
        // 上传文件大小上限：10 MB，足以覆盖常见的科目表导入量。
        private const long MaxUploadBytes = 10 * 1024 * 1024;

        private readonly AccountImportService _importService;
        private readonly IAccountRepository _accountRepository;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(AccountImportService importService, IAccountRepository accountRepository, ILogger<AccountsController> logger)
        {
            _importService = importService;
            _accountRepository = accountRepository;
            _logger = logger;
        }

        /// <summary>
        /// 获取指定账套的全部会计科目（按编码排序）。
        /// 可选 leafOnly=true 只返回启用中的末级科目，供凭证录入选择使用。
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AccountDto>>> GetAll(Guid accountSetId, [FromQuery] bool leafOnly, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            IEnumerable<Account> query = accounts;
            if (leafOnly)
            {
                query = query.Where(a => a.IsLeaf && a.IsEnabled);
            }

            var result = query
                .OrderBy(a => a.Code, StringComparer.Ordinal)
                .Select(AccountDto.FromDomain)
                .ToList();
            return Ok(result);
        }

        /// <summary>按标识获取单个科目（限定账套）。</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AccountDto>> GetById(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            return account is null ? NotFound() : Ok(AccountDto.FromDomain(account));
        }

        /// <summary>
        /// 新增科目。指定上级科目时创建明细科目（类别、方向继承上级，编码须以上级编码为前缀）；
        /// 否则创建顶级科目。编码在账套内须唯一。
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<AccountDto>> Create(Guid accountSetId, [FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
        {
            var code = request.Code?.Trim() ?? string.Empty;
            var name = request.Name?.Trim() ?? string.Empty;
            if (code.Length == 0 || name.Length == 0)
            {
                return BadRequest(new { message = "请填写科目编码和名称。" });
            }

            if (await _accountRepository.ExistsByCodeAsync(accountSetId, code, cancellationToken))
            {
                return BadRequest(new { message = "科目编码已存在。" });
            }

            Account account;
            try
            {
                if (request.ParentId is { } parentId)
                {
                    // 明细科目：上级须受跟踪，以便 AddChild 对上级 IsLeaf 的修改被持久化。
                    var parent = await _accountRepository.GetByIdAsync(accountSetId, parentId, cancellationToken);
                    if (parent is null)
                    {
                        return BadRequest(new { message = "指定的上级科目不存在。" });
                    }

                    account = parent.AddChild(code, name, request.Direction);
                }
                else
                {
                    account = Account.CreateRoot(accountSetId, code, name, request.Category, request.Direction);
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            await _accountRepository.AddAsync(account, cancellationToken);
            await _accountRepository.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetById), new { accountSetId, id = account.Id }, AccountDto.FromDomain(account));
        }

        /// <summary>删除科目：预置科目及存在下级明细的科目不可删除。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByIdAsync(accountSetId, id, cancellationToken);
            if (account is null)
            {
                return NotFound();
            }

            if (account.IsPreset)
            {
                return BadRequest(new { message = "预置科目不可删除。" });
            }

            if (await _accountRepository.HasChildrenAsync(accountSetId, id, cancellationToken))
            {
                return BadRequest(new { message = "该科目下仍有明细科目，请先删除下级科目。" });
            }

            _accountRepository.Remove(account);
            await _accountRepository.SaveChangesAsync(cancellationToken);
            return NoContent();
        }


        /// <summary>
        /// 下载科目导入模板（.xlsx）。模板含表头、示例数据与填写说明，供用户填写后回传导入。
        /// </summary>
        [HttpGet("import/template")]
        public IActionResult DownloadTemplate()
        {
            var bytes = AccountImportTemplateBuilder.Build();
            return File(bytes, AccountImportTemplateBuilder.ContentType, AccountImportTemplateBuilder.FileName);
        }

        /// <summary>
        /// 导入科目模板文件（.xlsx）。先父后子逐行建立科目，编码已存在者跳过并告警，
        /// 返回导入统计与告警信息。
        /// </summary>
        [HttpPost("import")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<AccountImportResult>> Import(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的科目模板文件。" });
            }

            if (file.Length > MaxUploadBytes)
            {
                return BadRequest(new { message = "文件过大，请上传不超过 10MB 的科目模板文件。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _importService.ImportAsync(accountSetId, stream, cancellationToken);
                return Ok(result);
            }
            catch (AccountImportException ex)
            {
                // 可预期的业务错误（格式不符、内容缺失等），返回友好提示。
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入科目模板文件「{FileName}」时发生未预期的错误。", file.FileName);
                return StatusCode(500, new { message = "导入科目时发生错误，请稍后重试。" });
            }
        }
    }
}
