using KuaiJi.Application.AccountSets;
using KuaiJi.Domain.AccountSets;
using KuaiJi.Domain.Accounting;
using KuaiJi.Domain.Journals;
using KuaiJi.Domain.Vouchers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Controllers
{
    /// <summary>
    /// 账套管理接口。账套是科目、日记账、凭证、报表的归属边界与数据隔离单位，
    /// 这些实体均须挂靠在某一账套之下，不能脱离账套独立存在。
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class AccountSetsController : ControllerBase
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IVoucherRepository _voucherRepository;
        private readonly IJournalRepository _journalRepository;

        public AccountSetsController(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IJournalRepository journalRepository)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _journalRepository = journalRepository;
        }

        /// <summary>获取全部账套。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AccountSetDto>>> GetAll(CancellationToken cancellationToken)
        {
            var sets = await _accountSetRepository.GetAllAsync(cancellationToken);
            return Ok(sets.Select(AccountSetDto.FromDomain).ToList());
        }

        /// <summary>按标识获取账套。</summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AccountSetDto>> GetById(Guid id, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            return accountSet is null ? NotFound() : Ok(AccountSetDto.FromDomain(accountSet));
        }

        /// <summary>新建账套。名称需全局唯一；若当前无任何账套，则新建者自动设为当前账套。</summary>
        [HttpPost]
        public async Task<ActionResult<AccountSetDto>> Create([FromBody] CreateAccountSetRequest request, CancellationToken cancellationToken)
        {
            if (await _accountSetRepository.ExistsByNameAsync(request.Name?.Trim() ?? string.Empty, cancellationToken))
            {
                return BadRequest(new { message = "同名账套已存在。" });
            }

            AccountSet accountSet;
            try
            {
                accountSet = AccountSet.Create(
                    request.Name,
                    request.CompanyName,
                    request.Standard,
                    request.TaxpayerType,
                    request.BaseCurrency,
                    request.StartYear,
                    request.StartMonth);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            // 首个账套自动置为当前账套，便于后续按账套路由的接口直接使用。
            var existing = await _accountSetRepository.GetAllAsync(cancellationToken);
            if (existing.Count == 0)
            {
                accountSet.MarkAsCurrent();
            }

            await _accountSetRepository.AddAsync(accountSet, cancellationToken);
            await _accountSetRepository.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = accountSet.Id }, AccountSetDto.FromDomain(accountSet));
        }

        /// <summary>更新账套：改名并更新单位、准则、纳税人性质、本位币等基础信息。</summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AccountSetDto>> Update(Guid id, [FromBody] UpdateAccountSetRequest request, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var newName = request.Name?.Trim() ?? string.Empty;
            if (!string.Equals(newName, accountSet.Name, StringComparison.Ordinal)
                && await _accountSetRepository.ExistsByNameAsync(newName, cancellationToken))
            {
                return BadRequest(new { message = "同名账套已存在。" });
            }

            try
            {
                accountSet.Rename(request.Name);
                accountSet.UpdateProfile(request.CompanyName, request.Standard, request.TaxpayerType, request.BaseCurrency);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return Ok(AccountSetDto.FromDomain(accountSet));
        }

        /// <summary>设为当前账套：先取消原当前账套标记，再将目标账套置为当前（全局唯一）。</summary>
        [HttpPost("{id:guid}/current")]
        public async Task<ActionResult<AccountSetDto>> SetCurrent(Guid id, CancellationToken cancellationToken)
        {
            var target = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            if (target is null)
            {
                return NotFound();
            }

            var current = await _accountSetRepository.GetCurrentAsync(cancellationToken);
            if (current is not null && current.Id != target.Id)
            {
                current.UnmarkCurrent();
            }

            target.MarkAsCurrent();
            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return Ok(AccountSetDto.FromDomain(target));
        }

        /// <summary>删除账套：仅当其下没有任何科目、凭证、日记账时允许，以保证下属数据不脱离账套。</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var hasAccounts = (await _accountRepository.GetAllAsync(id, cancellationToken)).Count > 0;
            var hasVouchers = (await _voucherRepository.GetAllAsync(id, cancellationToken)).Count > 0;
            var hasJournals = (await _journalRepository.GetAllAsync(id, cancellationToken)).Count > 0;
            if (hasAccounts || hasVouchers || hasJournals)
            {
                return BadRequest(new { message = "账套下仍有科目、凭证或日记账，无法删除。" });
            }

            _accountSetRepository.Remove(accountSet);
            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
