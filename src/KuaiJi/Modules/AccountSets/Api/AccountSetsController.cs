using KuaiJi.Modules.AccountSets.Application;
using KuaiJi.Modules.Assist.Application;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Accounting.Domain;
using KuaiJi.Modules.Assist.Domain;
using KuaiJi.Modules.Funds.Domain;
using KuaiJi.Modules.Vouchers.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KuaiJi.Modules.AccountSets.Api
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
        private readonly IEntryCategoryRepository _entryCategoryRepository;
        private readonly AssistItemService _assistItemService;
        private readonly AppDbContext _context;

        public AccountSetsController(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IJournalRepository journalRepository,
            IEntryCategoryRepository entryCategoryRepository,
            AssistItemService assistItemService,
            AppDbContext context)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _journalRepository = journalRepository;
            _entryCategoryRepository = entryCategoryRepository;
            _assistItemService = assistItemService;
            _context = context;
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
                    request.StartMonth,
                    request.AccountCodingScheme ?? AccountSet.NewAccountSetCodingScheme);
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

            // 建账预置：企业会计准则账套按 2007 版科目表生成科目（与柠檬云一致，建完即有整套科目）。
            if (request.Preset && accountSet.Standard == AccountingStandard.Enterprise)
            {
                await Accounting.Application.AccountPresetSeeder.SeedAsync(
                    accountSet.Id, _accountRepository, cancellationToken);

                // 建账预置（资金）：19 条收支类别 + 001 现金账户
                await Funds.Application.FundPresetSeeder.SeedAsync(
                    accountSet.Id, _entryCategoryRepository, _journalRepository, _accountRepository, cancellationToken);
            }

            return CreatedAtAction(nameof(GetById), new { id = accountSet.Id }, AccountSetDto.FromDomain(accountSet));
        }

        /// <summary>更新账套：改名并更新单位、准则、纳税人性质、本位币等基础信息。更改编码方案时自动重建所有科目编码。</summary>
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

            // 记录编码方案是否发生变更。
            var schemeChanged = request.AccountCodingScheme is not null
                && !string.Equals(request.AccountCodingScheme, accountSet.AccountCodingScheme, StringComparison.Ordinal);
            var newCodingScheme = request.AccountCodingScheme;

            try
            {
                accountSet.Rename(request.Name);
                accountSet.UpdateProfile(request.CompanyName, request.Standard, request.TaxpayerType, request.BaseCurrency);
                accountSet.UpdateTaxInfo(
                    request.TaxpayerNumber ?? accountSet.TaxpayerNumber,
                    request.VatRate ?? accountSet.VatRate,
                    request.IncomeTaxRate ?? accountSet.IncomeTaxRate);
                if (request.VoucherWords is not null)
                {
                    accountSet.SetVoucherWords(request.VoucherWords);
                }
                if (newCodingScheme is not null)
                {
                    accountSet.SetCodingScheme(newCodingScheme);
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            // 编码方案变更时，按新方案重建所有科目编码（凭证通过ID关联，不受编码变更影响）。
            if (schemeChanged)
            {
                var existingAccounts = await _context.Accounts
                    .Where(a => a.AccountSetId == id)
                    .OrderBy(a => a.Level)
                    .ToListAsync(cancellationToken);

                var newScheme = AccountSet.ParseCodingScheme(newCodingScheme!);

                // 保存旧编码与新编码的映射，用于子级提取自身片段。
                var oldCodeById = existingAccounts.ToDictionary(a => a.Id, a => a.Code);
                var newCodeById = new Dictionary<Guid, string>();

                foreach (var acc in existingAccounts)
                {
                    string newCode;

                    if (acc.Level <= 1 || acc.ParentId == null
                        || !newCodeById.TryGetValue(acc.ParentId.Value, out var parentNewCode)
                        || !oldCodeById.TryGetValue(acc.ParentId.Value, out var parentOldCode))
                    {
                        // 一级科目或孤儿子科：保留原编码不变。
                        newCode = acc.Code;
                    }
                    else
                    {
                        // 从旧编码中去掉上级前缀，得到本级原始片段。
                        var ownSegment = acc.Code.StartsWith(parentOldCode, StringComparison.Ordinal)
                            ? acc.Code.Substring(parentOldCode.Length)
                            : acc.Code;

                        // 按新方案的该级位数对齐：不足补零，超出取末尾 targetWidth 位。
                        var segIndex = acc.Level - 1;
                        var targetWidth = segIndex < newScheme.Length ? newScheme[segIndex] : ownSegment.Length;
                        var aligned = ownSegment.PadLeft(targetWidth, '0');
                        if (aligned.Length > targetWidth)
                            aligned = aligned.Substring(aligned.Length - targetWidth);

                        newCode = parentNewCode + aligned;
                    }

                    // 防止同账套内编码重复（极少见的边界情况，附加后缀消解）。
                    int suffix = 1;
                    var candidate = newCode;
                    while (newCodeById.Values.Contains(candidate))
                    {
                        candidate = newCode + "_" + (suffix++);
                    }

                    acc.RebaseCode(candidate, acc.ParentId, acc.Level);
                    newCodeById[acc.Id] = candidate;
                }

                await _context.SaveChangesAsync(cancellationToken);
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

        /// <summary>
        /// 设置账套启用的辅助核算类别（整体覆盖，传空数组表示关闭辅助核算）。
        /// 已建档案的类别不允许关闭，否则历史分录会失去维度。
        /// </summary>
        [HttpPut("{id:guid}/assist-types")]
        public async Task<ActionResult<AccountSetDto>> SetAssistTypes(Guid id, [FromBody] IReadOnlyList<AssistType> types, CancellationToken cancellationToken)
        {
            try
            {
                var accountSet = await _assistItemService.SetAccountSetAssistTypesAsync(id, types, cancellationToken);
                return Ok(AccountSetDto.FromDomain(accountSet));
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 月末结账：将下一个待结期间标记为已结账。要求该期间内全部凭证均已过账；
        /// 已结期间内禁止新增、修改、删除与反审核凭证。
        /// </summary>
        [HttpPost("{id:guid}/close-period")]
        public async Task<ActionResult<AccountSetDto>> ClosePeriod(Guid id, [FromBody] ClosePeriodRequest request, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            // 待结期间内的凭证须全部过账，避免草稿/待审凭证被锁进已结期间。
            var vouchers = await _voucherRepository.GetAllAsync(id, cancellationToken);
            var pending = vouchers.Count(v =>
                v.Date.Year == request.Year && v.Date.Month == request.Month && v.Status != VoucherStatus.Posted);
            if (pending > 0)
            {
                return BadRequest(new { message = $"{request.Year:D4}-{request.Month:D2} 期间尚有 {pending} 张凭证未过账，请先完成审核与过账。" });
            }

            try
            {
                accountSet.ClosePeriod(request.Year, request.Month);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return Ok(AccountSetDto.FromDomain(accountSet));
        }

        /// <summary>反结账：撤销最近一次结账，已结期间退回一个月。</summary>
        [HttpPost("{id:guid}/reopen-period")]
        public async Task<ActionResult<AccountSetDto>> ReopenPeriod(Guid id, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            try
            {
                accountSet.ReopenPeriod();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return Ok(AccountSetDto.FromDomain(accountSet));
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

            // 收支类别仅为记账归类的字典数据，随账套一并清理，不阻止删除。
            var categories = await _context.EntryCategories.Where(c => c.AccountSetId == id).ToListAsync(cancellationToken);
            _context.EntryCategories.RemoveRange(categories);

            _accountSetRepository.Remove(accountSet);
            await _accountSetRepository.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
