using YunCai.Modules.Assist.Application;
using YunCai.Modules.Accounting.Application;
using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Accounting.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;

namespace YunCai.Modules.Accounting.Api
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
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly ICurrencyRepository _currencyRepository;
        private readonly OpeningBalanceService _openingBalanceService;
        private readonly OpeningBalanceImportService _openingBalanceImportService;
        private readonly AssistItemService _assistItemService;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(AccountImportService importService, IAccountRepository accountRepository, IAccountSetRepository accountSetRepository, ICurrencyRepository currencyRepository, OpeningBalanceService openingBalanceService, OpeningBalanceImportService openingBalanceImportService, AssistItemService assistItemService, ILogger<AccountsController> logger)
        {
            _importService = importService;
            _accountRepository = accountRepository;
            _accountSetRepository = accountSetRepository;
            _currencyRepository = currencyRepository;
            _openingBalanceService = openingBalanceService;
            _openingBalanceImportService = openingBalanceImportService;
            _assistItemService = assistItemService;
            _logger = logger;
        }

        /// <summary>
        /// 获取期初余额表：全部科目按编码排序，末级可录入、非末级由下级汇总，含试算平衡合计。
        /// </summary>
        [HttpGet("opening-balances")]
        public async Task<ActionResult<OpeningBalancesDto>> GetOpeningBalances(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _openingBalanceService.GetAsync(accountSetId, cancellationToken));
            }
            catch (OpeningBalanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 批量保存期初余额：仅末级科目可录入，金额按科目余额方向为正（反方向余额用负数）。
        /// 返回保存后的最新期初余额表与试算平衡结果。
        /// </summary>
        [HttpPut("opening-balances")]
        public async Task<ActionResult<OpeningBalancesDto>> SaveOpeningBalances(Guid accountSetId, [FromBody] SaveOpeningBalancesRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _openingBalanceService.SaveAsync(accountSetId, request, cancellationToken));
            }
            catch (OpeningBalanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 旧账导入模板：按当前账套末级科目生成「科目编码 / 科目名称 / 余额方向 / 借方余额 / 贷方余额」，
        /// 填入旧账金额后可直接回传导入。
        /// </summary>
        [HttpGet("opening-balances/template")]
        public async Task<IActionResult> GetOpeningBalanceTemplate(Guid accountSetId, CancellationToken cancellationToken)
        {
            try
            {
                var balances = await _openingBalanceService.GetAsync(accountSetId, cancellationToken);
                var rows = balances.Rows
                    .Where(r => r.IsLeaf)
                    .Select(r => new Dictionary<string, object?>
                    {
                        ["科目编码"] = r.Code,
                        ["科目名称"] = r.Name,
                        ["余额方向"] = r.Direction == BalanceDirection.Debit ? "借" : "贷",
                        ["借方余额"] = r.Debit,
                        ["贷方余额"] = r.Credit,
                    })
                    .ToList();

                using var stream = new MemoryStream();
                MiniExcel.SaveAs(stream, new Dictionary<string, object> { ["科目余额表"] = rows }, excelType: ExcelType.XLSX);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "旧账导入_科目余额表.xlsx");
            }
            catch (OpeningBalanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 旧账导入：上传旧系统「科目余额表」Excel，按科目编码写入本账套期初余额。
        /// 支持借贷两列、单列余额 + 方向列、单列余额三种表头。
        /// </summary>
        [HttpPost("opening-balances/import")]
        [RequestSizeLimit(MaxUploadBytes)]
        public async Task<ActionResult<OpeningBalanceImportResultDto>> ImportOpeningBalances(Guid accountSetId, IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "请选择要导入的 Excel 文件。" });
            }

            if (file.Length > MaxUploadBytes)
            {
                return BadRequest(new { message = "文件过大，请控制在 10MB 以内。" });
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _openingBalanceImportService.ImportAsync(accountSetId, stream, cancellationToken);
                return Ok(result);
            }
            catch (OpeningBalanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return BadRequest(new { message = "文件解析失败：" + ex.Message });
            }
        }

        /// <summary>
        /// 获取指定账套的全部会计科目（按编码排序）。
        /// 可选 leafOnly=true 只返回启用中的末级科目，供凭证录入选择使用。
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<AccountDto>>> GetAll(Guid accountSetId, [FromQuery] bool leafOnly, CancellationToken cancellationToken)
        {
            // 编辑弹窗直接吃列表里的 currencyIds：少了 Include 会把外币配置读成空，保存时覆盖掉真实数据。
            var accounts = await _accountRepository.GetAllWithSettingsAsync(accountSetId, cancellationToken);
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
            var account = await _accountRepository.GetByIdWithSettingsAsync(accountSetId, id, cancellationToken);
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

            // 加载账套以获取编码方案进行校验。
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return BadRequest(new { message = "账套不存在。" });
            }

            var scheme = AccountSet.ParseCodingScheme(accountSet.AccountCodingScheme);
            var maxLevel = scheme.Length;

            // 外币核算的币别必须取自本账套币别字典。
            var currencyIds = (request.CurrencyIds ?? []).Distinct().ToList();
            if (request.IsForeignCurrencyAccounted)
            {
                var currencyError = await GuardCurrenciesAsync(accountSetId, currencyIds, cancellationToken);
                if (currencyError is not null)
                {
                    return BadRequest(new { message = currencyError });
                }
            }

            Account account;
            try
            {
                if (request.ParentId is { } parentId)
                {
                    // 走 WithSettings 载入，明细科目才能从上级复制已有的币别列表。
                    var parent = await _accountRepository.GetByIdWithSettingsAsync(accountSetId, parentId, cancellationToken);
                    if (parent is null)
                    {
                        return BadRequest(new { message = "指定的上级科目不存在。" });
                    }

                    var newLevel = parent.Level + 1;
                    if (newLevel > maxLevel)
                    {
                        return BadRequest(new { message = $"科目编码方案「{accountSet.AccountCodingScheme}」最多支持 {maxLevel} 级，无法再创建第 {newLevel} 级科目。" });
                    }

                    var expectedLength = scheme.Take(newLevel).Sum();
                    if (code.Length != expectedLength)
                    {
                        return BadRequest(new { message = $"第 {newLevel} 级科目编码应为 {expectedLength} 位（编码方案：{accountSet.AccountCodingScheme}），当前编码「{code}」为 {code.Length} 位。" });
                    }

                    // 二级类别与各项核算属性由 AddChild 从上级继承，请求中的同名值不参与。
                    account = parent.AddChild(code, name, request.Direction);
                }
                else
                {
                    var expectedLength = scheme[0];
                    if (code.Length != expectedLength)
                    {
                        return BadRequest(new { message = $"一级科目编码应为 {expectedLength} 位（编码方案：{accountSet.AccountCodingScheme}），当前编码「{code}」为 {code.Length} 位。" });
                    }

                    account = Account.CreateRoot(accountSetId, code, name, request.Category, request.Direction);
                    if (request.SubCategory is { } subCategory)
                    {
                        account.SetSubCategory(subCategory);
                    }

                    account.SetCashAccount(request.IsCashAccount);
                    account.SetQuantityAccounting(request.IsQuantityAccounted, request.Unit);
                    account.SetForeignCurrencyAccounting(request.IsForeignCurrencyAccounted, currencyIds);
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            // 设置助记码和启用状态。
            if (!string.IsNullOrWhiteSpace(request.Mnemonic))
            {
                account.SetMnemonic(request.Mnemonic);
            }

            if (!request.IsEnabled)
            {
                account.Disable();
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

            var parentId = account.ParentId;

            _accountRepository.Remove(account);
            await _accountRepository.SaveChangesAsync(cancellationToken);

            // 删掉最后一个下级后，上级要退回末级，否则它会被「非末级不可记账」永久挡住。
            if (parentId is { } pid && !await _accountRepository.HasChildrenAsync(accountSetId, pid, cancellationToken))
            {
                var parent = await _accountRepository.GetByIdAsync(accountSetId, pid, cancellationToken);
                if (parent is { IsLeaf: false })
                {
                    parent.RestoreLeaf();
                    await _accountRepository.SaveChangesAsync(cancellationToken);
                }
            }

            return NoContent();
        }

        /// <summary>
        /// 修改科目：名称、助记码、启用状态、二级类别、现金/数量/外币核算属性均可改；
        /// 科目编码与余额方向仅末级科目可改，辅助核算维度仍走独立的 assist-types 接口。
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<AccountDto>> Update(Guid accountSetId, Guid id, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
        {
            // 币别列表要被覆盖写入，必须连同导航集合一起载入，否则读不到旧值也删不掉旧行。
            var account = await _accountRepository.GetByIdWithSettingsAsync(accountSetId, id, cancellationToken);
            if (account is null)
            {
                return NotFound();
            }

            var newCode = request.Code?.Trim();
            if (!string.IsNullOrEmpty(newCode) && newCode != account.Code)
            {
                var codeError = await ValidateNewCodeAsync(accountSetId, account, newCode!, cancellationToken);
                if (codeError is not null)
                {
                    return BadRequest(new { message = codeError });
                }
            }

            var currencyIds = (request.CurrencyIds ?? []).Distinct().ToList();
            if (request.IsForeignCurrencyAccounted is true)
            {
                var currencyError = await GuardCurrenciesAsync(accountSetId, currencyIds, cancellationToken);
                if (currencyError is not null)
                {
                    return BadRequest(new { message = currencyError });
                }
            }

            try
            {
                if (!string.IsNullOrEmpty(newCode) && newCode != account.Code)
                {
                    account.ChangeCode(newCode!);
                }

                if (request.Name is not null)
                {
                    account.Rename(request.Name);
                }

                if (request.Direction is { } direction)
                {
                    account.ChangeDirection(direction);
                }

                if (request.Mnemonic is not null)
                {
                    account.SetMnemonic(request.Mnemonic);
                }

                if (request.IsEnabled is { } isEnabled)
                {
                    if (isEnabled)
                        account.Enable();
                    else
                        account.Disable();
                }

                // 二级类别按整体覆盖处理：编辑弹窗总会提交当前选择，缺省即「未指定」。
                account.SetSubCategory(request.SubCategory);

                if (request.IsCashAccount is { } isCashAccount)
                {
                    account.SetCashAccount(isCashAccount);
                }

                if (request.IsQuantityAccounted is { } quantityEnabled)
                {
                    account.SetQuantityAccounting(quantityEnabled, request.Unit);
                }

                if (request.IsForeignCurrencyAccounted is { } foreignEnabled)
                {
                    // 先按入参算出目标集合（不合法就地抛错，此时尚未提交任何改动）。
                    account.SetForeignCurrencyAccounting(foreignEnabled, currencyIds);
                    if (foreignEnabled)
                    {
                        // account_currencies 上有 (account_id, currency_id) 唯一索引，而一次 SaveChanges
                        // 内新增先于删除执行，直接覆盖会让新旧两行撞在同一索引键上，故分两次提交。
                        var wanted = account.Currencies.Select(c => c.CurrencyId).ToList();
                        account.SetForeignCurrencyAccounting(false);
                        await _accountRepository.SaveChangesAsync(cancellationToken);
                        account.SetForeignCurrencyAccounting(true, wanted);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            await _accountRepository.SaveChangesAsync(cancellationToken);
            return Ok(AccountDto.FromDomain(account));
        }

        /// <summary>
        /// 校验改后的科目编码：长度符合编码方案中该级位数、以上级编码为前缀、账套内不重号。
        /// 返回 null 表示通过，否则为面向用户的错误文案。
        /// </summary>
        private async Task<string?> ValidateNewCodeAsync(Guid accountSetId, Account account, string newCode, CancellationToken cancellationToken)
        {
            if (!account.IsLeaf)
            {
                return "非末级科目不允许修改科目编码，否则下级科目编码的前缀会失效。";
            }

            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return "账套不存在。";
            }

            var scheme = AccountSet.ParseCodingScheme(accountSet.AccountCodingScheme);
            if (account.Level <= scheme.Length)
            {
                var expectedLength = scheme.Take(account.Level).Sum();
                if (newCode.Length != expectedLength)
                {
                    return $"第 {account.Level} 级科目编码应为 {expectedLength} 位（编码方案：{accountSet.AccountCodingScheme}），当前编码「{newCode}」为 {newCode.Length} 位。";
                }
            }

            if (account.ParentId is { } parentId)
            {
                var parent = await _accountRepository.GetByIdAsync(accountSetId, parentId, cancellationToken);
                if (parent is not null && !newCode.StartsWith(parent.Code, StringComparison.Ordinal))
                {
                    return $"明细科目编码「{newCode}」必须以上级科目编码「{parent.Code}」为前缀。";
                }
            }

            if (await _accountRepository.ExistsByCodeAsync(accountSetId, newCode, cancellationToken))
            {
                return "科目编码已存在。";
            }

            return null;
        }

        /// <summary>校验币别标识都属于本账套；返回 null 表示通过。</summary>
        private async Task<string?> GuardCurrenciesAsync(Guid accountSetId, IReadOnlyCollection<Guid> currencyIds, CancellationToken cancellationToken)
        {
            if (currencyIds.Count == 0)
            {
                return "开启外币核算的科目至少要选择一个币别。";
            }

            var known = (await _currencyRepository.GetAllAsync(accountSetId, cancellationToken)).Select(c => c.Id).ToHashSet();
            return currencyIds.All(known.Contains)
                ? null
                : "所选币别不属于本账套，请先在账套设置的币别中维护后重试。";
        }

        /// <summary>获取科目已设置的辅助核算维度（含每类是否必录）；未设置则返回空数组。</summary>
        [HttpGet("{id:guid}/assist-types")]
        public async Task<ActionResult<IReadOnlyList<AccountAssistTypeDto>>> GetAssistTypes(Guid accountSetId, Guid id, CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assistItemService.GetAccountAssistTypesAsync(accountSetId, id, cancellationToken));
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 整体覆盖科目的辅助核算维度设置（body 为 [{type, isRequired}]，空数组表示取消辅助核算）。
        /// 只有账套已启用的类别才可设置；改动只影响后续制单校验，历史分录的取值保持不变。
        /// </summary>
        [HttpPut("{id:guid}/assist-types")]
        public async Task<ActionResult<IReadOnlyList<AccountAssistTypeDto>>> PutAssistTypes(
            Guid accountSetId,
            Guid id,
            [FromBody] IReadOnlyList<AccountAssistTypeRequest> settings,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _assistItemService.SetAccountAssistTypesAsync(accountSetId, id, settings, cancellationToken));
            }
            catch (AssistanceException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
        /// 导出账套全部科目（.xlsx）。导出列与导入模板一致，文件可原样回传导入。
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accounts = await _accountRepository.GetAllAsync(accountSetId, cancellationToken);
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken);
            var bytes = AccountExportBuilder.Build(accounts);
            var fileName = AccountExportBuilder.BuildFileName(accountSet?.Name ?? string.Empty);
            return File(bytes, AccountExportBuilder.ContentType, fileName);
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
