using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.Assist.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Assist.Domain;
using YunCai.Modules.Funds.Domain;
using YunCai.Modules.Vouchers.Domain;
using YunCai.SharedKernel.Infrastructure.Data;
using YunCai.SharedKernel.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace YunCai.Modules.AccountSets.Api
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
        private readonly IReinitRepository _reinitRepository;
        private readonly AssistItemService _assistItemService;
        private readonly ITaxCredentialProtector _taxCredentials;
        private readonly TaxLoginTicketStore _taxLoginTickets;
        private readonly MemberService _memberService;
        private readonly IAccountSetMemberRepository _memberRepository;
        private readonly AppDbContext _context;

        public AccountSetsController(
            IAccountSetRepository accountSetRepository,
            IAccountRepository accountRepository,
            IVoucherRepository voucherRepository,
            IJournalRepository journalRepository,
            IEntryCategoryRepository entryCategoryRepository,
            IReinitRepository reinitRepository,
            AssistItemService assistItemService,
            ITaxCredentialProtector taxCredentials,
            TaxLoginTicketStore taxLoginTickets,
            MemberService memberService,
            IAccountSetMemberRepository memberRepository,
            AppDbContext context)
        {
            _accountSetRepository = accountSetRepository;
            _accountRepository = accountRepository;
            _voucherRepository = voucherRepository;
            _journalRepository = journalRepository;
            _entryCategoryRepository = entryCategoryRepository;
            _reinitRepository = reinitRepository;
            _assistItemService = assistItemService;
            _taxCredentials = taxCredentials;
            _taxLoginTickets = taxLoginTickets;
            _memberService = memberService;
            _memberRepository = memberRepository;
            _context = context;
        }

        /// <summary>当前登录用户标识；令牌由 [Authorize] 保护，缺失时视为不可用。</summary>
        private string? CurrentUserId =>
            User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

        private string? CurrentPhone => User.FindFirst("phone")?.Value;

        private string CurrentDisplayName =>
            User.FindFirst("displayName")?.Value ?? "账套管理员";

        /// <summary>
        /// 把当前账套已保存的税局密码换成 2 分钟内有效的一次性票据。
        /// 网页把票据交给本机助手，助手兑换后在自己的窗口里自动登录税局；密码不进入前端响应。
        /// </summary>
        [HttpPost("{id:guid}/tax-login-ticket")]
        [RequireAccountSetAdmin]
        public async Task<ActionResult<TaxLoginTicketDto>> CreateTaxLoginTicket(Guid id, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(accountSet.TaxLoginPasswordCipher))
            {
                return BadRequest(new { message = "当前账套尚未保存税局实名密码，请先填写密码。" });
            }

            var password = _taxCredentials.Decrypt(accountSet.TaxLoginPasswordCipher);
            if (string.IsNullOrEmpty(password))
            {
                return BadRequest(new { message = "税局密码无法解密（签名密钥可能已更换），请重新填写密码。" });
            }

            return Ok(new TaxLoginTicketDto
            {
                Ticket = _taxLoginTickets.Issue(password),
                ExpiresInSeconds = 120
            });
        }

        /// <summary>获取当前登录用户可访问的账套（仅其作为启用成员加入的账套）。</summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AccountSetDto>>> GetAll(CancellationToken cancellationToken)
        {
            if (CurrentUserId is not { } userId)
            {
                return Unauthorized();
            }

            var allowed = await _memberRepository.GetAccountSetIdsAsync(userId, CurrentPhone, cancellationToken);
            var sets = await _accountSetRepository.GetAllAsync(cancellationToken);
            return Ok(sets.Where(s => allowed.Contains(s.Id)).Select(AccountSetDto.FromDomain).ToList());
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
            if (CurrentUserId is not { } userId)
            {
                return Unauthorized();
            }

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
                    request.AccountCodingScheme ?? AccountSet.NewAccountSetCodingScheme,
                    request.Industry);
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

            // 创建者自动成为账套管理员成员，后续按账套路由的接口据此放行。
            try
            {
                await _memberService.EnsureOwnerAsync(
                    accountSet.Id, CurrentDisplayName, userId, CurrentPhone, cancellationToken);
            }
            catch (MemberException ex)
            {
                // 没有成员就该账套谁都进不去，创建失败时撤掉刚落的账套，避免留下孤儿数据。
                _accountSetRepository.Remove(accountSet);
                await _accountSetRepository.SaveChangesAsync(cancellationToken);
                return BadRequest(new { message = ex.Message });
            }

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
        [RequireAccountSetAdmin]
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
                accountSet.UpdateProfile(
                    request.CompanyName,
                    request.Standard,
                    request.TaxpayerType,
                    request.BaseCurrency,
                    request.Industry ?? accountSet.Industry);
                accountSet.UpdateFunctionParams(
                    request.AssetsModuleEnabled ?? accountSet.AssetsModuleEnabled,
                    request.FundsModuleEnabled ?? accountSet.FundsModuleEnabled,
                    request.VoucherAuditRequired ?? accountSet.VoucherAuditRequired,
                    request.BalanceAssistScope ?? accountSet.BalanceAssistScope,
                    request.UnitDecimalPlaces ?? accountSet.UnitDecimalPlaces);
                accountSet.UpdateTaxInfo(
                    request.TaxpayerNumber ?? accountSet.TaxpayerNumber,
                    request.VatRate ?? accountSet.VatRate,
                    request.IncomeTaxRate ?? accountSet.IncomeTaxRate,
                    request.IsExportEnterprise ?? accountSet.IsExportEnterprise,
                    request.CityBuildTaxRate ?? accountSet.CityBuildTaxRate,
                    request.EducationSurchargeRate ?? accountSet.EducationSurchargeRate,
                    request.LocalEducationSurchargeRate ?? accountSet.LocalEducationSurchargeRate);
                accountSet.UpdateTaxLoginInfo(
                    request.TaxRegion ?? accountSet.TaxRegion,
                    request.TaxLoginType ?? accountSet.TaxLoginType,
                    request.TaxIdentityType ?? accountSet.TaxIdentityType,
                    request.TaxLoginAccount ?? accountSet.TaxLoginAccount,
                    // 明文只在参数绑定到这一行的瞬间存在；不传保持原密文，传空串清除。
                    request.TaxLoginPassword is null
                        ? accountSet.TaxLoginPasswordCipher
                        : string.IsNullOrWhiteSpace(request.TaxLoginPassword)
                            ? null
                            : _taxCredentials.Encrypt(request.TaxLoginPassword));
                accountSet.UpdateIitLoginInfo(
                    request.IitChannel ?? accountSet.IitChannel,
                    request.IitLoginType ?? accountSet.IitLoginType,
                    request.IitLoginAccount ?? accountSet.IitLoginAccount,
                    request.IitLoginPassword is null
                        ? accountSet.IitLoginPasswordCipher
                        : string.IsNullOrWhiteSpace(request.IitLoginPassword)
                            ? null
                            : _taxCredentials.Encrypt(request.IitLoginPassword));
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
        [RequireAccountSetAdmin]
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
        /// 已结期间内禁止新增、修改、删除与反过账凭证。
        /// </summary>
        [HttpPost("{id:guid}/close-period")]
        [RequireStepUp]
        public async Task<ActionResult<AccountSetDto>> ClosePeriod(Guid id, [FromBody] ClosePeriodRequest request, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            // 待结期间内的凭证须全部过账，避免暂存/待审凭证被锁进已结期间。
            var vouchers = await _voucherRepository.GetAllAsync(id, cancellationToken);
            var pending = vouchers.Count(v =>
                v.Date.Year == request.Year && v.Date.Month == request.Month && v.Status != VoucherStatus.Posted);
            if (pending > 0)
            {
                return BadRequest(new { message = $"{request.Year:D4}-{request.Month:D2} 期间尚有 {pending} 张暂存凭证，请先处理后再操作。" });
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
        [RequireStepUp]
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

        /// <summary>
        /// 删除账套：连同其下全部业务数据（科目、凭证、账簿、资金、发票、工资、资产、档案等）一并删除，不可撤销。
        /// 删除的是当前账套时，自动把剩余账套中按名称排序的第一套设为当前。
        /// </summary>
        [HttpDelete("{id:guid}")]
        [RequireStepUp]
        [RequireAccountSetAdmin]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(id, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var wasCurrent = accountSet.IsCurrent;
            await _reinitRepository.DeleteAccountSetAsync(id, cancellationToken);

            if (wasCurrent)
            {
                // GetAllAsync 走 AsNoTracking，标记当前账套须重新取可追踪实例。
                var next = (await _accountSetRepository.GetAllAsync(cancellationToken)).FirstOrDefault();
                if (next is not null)
                {
                    var tracked = await _accountSetRepository.GetByIdAsync(next.Id, cancellationToken);
                    tracked?.MarkAsCurrent();
                    await _accountSetRepository.SaveChangesAsync(cancellationToken);
                }
            }

            return NoContent();
        }
    }
}
