using System.Security.Claims;
using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Tax.Application;
using YunCai.Modules.Tax.Domain;
using YunCai.Modules.Tax.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace YunCai.Modules.Tax.Api
{
    /// <summary>
    /// 智能报税的服务端税局通道：登录、短信验证、税费种 / 申报记录 / 一键取票取数。
    /// 税局口令只从账套里解密后交给服务器上的浏览器，不再下发到前端，也不依赖本机助手。
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class TaxPortalController : ControllerBase
    {
        private readonly IAccountSetRepository _accountSets;
        private readonly ITaxCredentialProtector _credentials;
        private readonly TaxPortalService _portal;
        private readonly TaxPortalBrowserHandoffStore _handoffs;
        private readonly IitReportService _iitReport;
        private readonly TaxTaskStore _tasks;
        private readonly TaxIitFetchJobRunner _iitJobs;
        private readonly TaxInvoiceSyncJobRunner _invoiceJobs;

        public TaxPortalController(
            IAccountSetRepository accountSets,
            ITaxCredentialProtector credentials,
            TaxPortalService portal,
            TaxPortalBrowserHandoffStore handoffs,
            IitReportService iitReport,
            TaxTaskStore tasks,
            TaxIitFetchJobRunner iitJobs,
            TaxInvoiceSyncJobRunner invoiceJobs)
        {
            _accountSets = accountSets;
            _credentials = credentials;
            _portal = portal;
            _handoffs = handoffs;
            _iitReport = iitReport;
            _tasks = tasks;
            _iitJobs = iitJobs;
            _invoiceJobs = invoiceJobs;
        }

        /// <summary>用账套里保存的税局资料发起服务端登录；返回会话后前端轮询状态。</summary>
        [HttpPost("{accountSetId:guid}/login")]
        public async Task<ActionResult<TaxPortalLoginStatusDto>> Login(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(accountSet.TaxpayerNumber)
                || string.IsNullOrWhiteSpace(accountSet.TaxRegion)
                || string.IsNullOrWhiteSpace(accountSet.TaxLoginAccount))
            {
                return BadRequest(new { message = "请先补全统一社会信用代码、报税地区与税局实名账号。" });
            }

            if (!TaxPortalService.IsSupportedRegion(accountSet.TaxRegion))
            {
                return BadRequest(new { message = "当前账套保存的报税地区无法识别，请在「税局登录」弹窗里重新选择报税地区并保存。" });
            }

            var password = _credentials.Decrypt(accountSet.TaxLoginPasswordCipher);
            if (string.IsNullOrEmpty(password))
            {
                return BadRequest(new { message = "当前账套尚未保存可用的税局密码，请重新填写一次密码。" });
            }

            var credentials = new TaxPortalCredentials(
                accountSet.CompanyName,
                accountSet.TaxpayerNumber.Trim(),
                accountSet.TaxRegion.Trim(),
                string.IsNullOrWhiteSpace(accountSet.TaxLoginType) ? "企业业务登录" : accountSet.TaxLoginType,
                string.IsNullOrWhiteSpace(accountSet.TaxIdentityType) ? "办税员" : accountSet.TaxIdentityType,
                accountSet.TaxLoginAccount.Trim(),
                password);

            return Ok(_portal.StartLogin(CurrentKey(accountSetId), credentials));
        }

        /// <summary>
        /// 进入税局页面时确认登录态：能用就返回 success，用不了返回 failed 让网页弹窗重新登录。
        /// 判据与取数一致（内存里正在登录中也算未登录）；内存里没有会话时会先用保存的登录态恢复，
        /// 可能要拉起浏览器，耗时比 status 长。
        /// </summary>
        [HttpPost("{accountSetId:guid}/login/resume")]
        public async Task<ActionResult<TaxPortalLoginStatusDto>> Resume(Guid accountSetId, CancellationToken cancellationToken)
        {
            return Ok(await _portal.ResumeAsync(CurrentKey(accountSetId), cancellationToken));
        }

        /// <summary>查询登录进展：等短信、成功或失败。</summary>
        [HttpGet("{accountSetId:guid}/login/status")]
        public ActionResult<TaxPortalLoginStatusDto> Status(Guid accountSetId)
        {
            return Ok(_portal.GetStatus(CurrentKey(accountSetId)));
        }

        /// <summary>提交税局短信验证码。</summary>
        [HttpPost("{accountSetId:guid}/login/verify")]
        public async Task<ActionResult<TaxPortalLoginStatusDto>> Verify(
            Guid accountSetId,
            [FromBody] TaxPortalVerifyRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _portal.VerifyAsync(CurrentKey(accountSetId), request?.Code ?? string.Empty, cancellationToken);
            return Ok(result);
        }

        /// <summary>提交网页上完成的文字点选验证码。</summary>
        [HttpPost("{accountSetId:guid}/login/captcha")]
        public ActionResult<TaxPortalLoginStatusDto> Captcha(
            Guid accountSetId,
            [FromBody] TaxPortalCaptchaRequest request)
        {
            return Ok(_portal.SubmitCaptcha(CurrentKey(accountSetId), request?.Clicks));
        }

        /// <summary>退出税局登录，丢弃服务器上的登录态。</summary>
        [HttpPost("{accountSetId:guid}/logout")]
        public async Task<IActionResult> Logout(Guid accountSetId)
        {
            await _portal.LogoutAsync(CurrentKey(accountSetId));
            return NoContent();
        }

        /// <summary>
        /// 把服务器上的税局登录态换成一次性票据，交给本机助手注入自己的浏览器。
        /// 助手打开企业电子税务局时就已经是登录状态；票据 2 分钟内只能兑换一次。
        /// </summary>
        [HttpPost("{accountSetId:guid}/browser-session-ticket")]
        public async Task<ActionResult<TaxPortalBrowserTicketDto>> CreateBrowserSessionTicket(
            Guid accountSetId,
            CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            if (!TaxPortalService.IsSupportedRegion(accountSet.TaxRegion))
            {
                return BadRequest(new { message = "当前账套的报税地区无法识别，请先在「税局登录」弹窗里重新选择并保存。" });
            }

            var url = TaxPortalService.ResolveEtaxHomeUrl(accountSet.TaxRegion);
            var handoff = await _portal.CreateBrowserHandoffAsync(CurrentKey(accountSetId), url, cancellationToken);
            if (handoff is null)
            {
                return Conflict(new { message = "税局登录状态已失效，请先重新登录电子税务局。" });
            }

            return Ok(new TaxPortalBrowserTicketDto
            {
                Ticket = _handoffs.Issue(handoff),
                Url = url,
                ExpiresInSeconds = (int)TaxPortalBrowserHandoffStore.Ttl.TotalSeconds
            });
        }

        /// <summary>
        /// 临时诊断：按菜单文字逐步点击，返回页面结构与本轮抓到的 JSON 摘要。
        /// 仅用于摸清税局各业务的菜单路径，摸底完成后删除。
        /// </summary>
        [HttpPost("{accountSetId:guid}/debug/explore")]
        public async Task<ActionResult<object>> Explore(
            Guid accountSetId,
            [FromBody] ExploreRequest request,
            CancellationToken cancellationToken)
        {
            return Ok(await _portal.ExploreAsync(
                CurrentKey(accountSetId, request?.Kind ?? TaxPortalKind.Enterprise), request, cancellationToken));
        }

        /// <summary>
        /// 临时诊断：打开全量发票查询，按给定日期查询并返回抓到的 JSON 摘要。
        /// 摸清查询接口后删除。
        /// </summary>
        [HttpPost("{accountSetId:guid}/debug/invoice-query")]
        public async Task<ActionResult<object>> DebugInvoiceQuery(
            Guid accountSetId,
            [FromBody] InvoiceQueryProbeRequest request,
            CancellationToken cancellationToken)
        {
            var start = request?.StartDate is { } s ? DateOnly.FromDateTime(s) : DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
            var end = request?.EndDate is { } e ? DateOnly.FromDateTime(e) : DateOnly.FromDateTime(DateTime.Today);
            return Ok(await _portal.DebugInvoiceQueryAsync(
                CurrentKey(accountSetId),
                start,
                end,
                cancellationToken,
                request?.ResetFilters ?? false));
        }

        /// <summary>
        /// 临时诊断：在已登录的自然人电子税务局页面里摸清它的加解密落在哪一层，
        /// 并抓一份「页面自己解密后的明文报文」，用来判断个税能不能改直连。摸底完成后删除。
        /// </summary>
        [HttpPost("{accountSetId:guid}/debug/iit-plain")]
        public async Task<ActionResult<object>> DebugIitPlain(
            Guid accountSetId,
            [FromBody] IitPlainProbeRequest? request,
            CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            return Ok(await _portal.DebugIitPlainAsync(
                CurrentKey(accountSetId, TaxPortalKind.Iit),
                accountSet.CompanyName,
                request?.Menu,
                cancellationToken));
        }

        /// <summary>从已登录的税局会话读取税费种认定。</summary>
        [HttpGet("{accountSetId:guid}/tax-kinds")]
        public async Task<ActionResult<TaxKindsResultDto>> TaxKinds(Guid accountSetId, CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var key = CurrentKey(accountSetId);
            var startedAt = DateTimeOffset.UtcNow;
            var profile = new TaxKindProfile(accountSet.TaxpayerType, accountSet.Standard);
            var result = await _portal.FetchTaxKindsAsync(key, profile, cancellationToken);
            if (!result.NeedLogin)
            {
                // 没登录就没办事，任务中心只记真正向税局发起过的操作。
                await _tasks.RecordAsync(
                    key, CurrentUserName(), "查询税费种认定信息", result.Message is null, result.Message, startedAt,
                    cancellationToken: cancellationToken);
            }
            return Ok(result);
        }

        /// <summary>读取所选税款所属期（yyyyMM）在电子税务局里的申报记录。</summary>
        [HttpGet("{accountSetId:guid}/declarations")]
        public async Task<ActionResult<TaxDeclarationsResultDto>> Declarations(
            Guid accountSetId,
            [FromQuery] string? period,
            CancellationToken cancellationToken)
        {
            var (year, month) = ResolvePeriod(period);
            var key = CurrentKey(accountSetId);
            var startedAt = DateTimeOffset.UtcNow;
            var result = await _portal.FetchDeclarationsAsync(key, year, month, cancellationToken);
            if (!result.NeedLogin)
            {
                await _tasks.RecordAsync(
                    key, CurrentUserName(), $"获取申报记录（{year}年{month}月）", result.Message is null, result.Message,
                    startedAt, year * 100 + month, cancellationToken);
            }
            return Ok(result);
        }

        /// <summary>一次性排查「未申报查询」的真实接口与报文。</summary>
        [HttpGet("{accountSetId:guid}/probe-undeclared")]
        public async Task<ActionResult<TaxDeclarationsResultDto>> ProbeUndeclared(
            Guid accountSetId,
            CancellationToken cancellationToken)
        {
            return Ok(await _portal.ProbeUndeclaredAsync(CurrentKey(accountSetId), cancellationToken));
        }

        /// <summary>
        /// 读取「报表查询」页的申报表：按申报日期区间（东八区自然日，含首尾）查询，两端省略即今年年初至今。
        /// 税局的申报查询接口只按所属期过滤，服务端会放宽所属期窗口取数后再按申报日期筛。
        /// </summary>
        [HttpGet("{accountSetId:guid}/declaration-reports")]
        public async Task<ActionResult<TaxDeclarationsResultDto>> DeclarationReports(
            Guid accountSetId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var start = from ?? new DateOnly(today.Year, 1, 1);
            var end = to ?? today;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            var key = CurrentKey(accountSetId);
            var startedAt = DateTimeOffset.UtcNow;
            var result = await _portal.FetchDeclarationReportsAsync(key, start, end, cancellationToken);
            if (!result.NeedLogin)
            {
                await _tasks.RecordAsync(
                    key, CurrentUserName(), $"查询申报表（{start:yyyy-MM-dd}至{end:yyyy-MM-dd}）", result.Message is null,
                    result.Message, startedAt, start.Year * 100 + start.Month, cancellationToken);
            }
            return Ok(result);
        }

        /// <summary>
        /// 读取「缴款信息查询」：按缴款日期区间（东八区自然日，含首尾）返回税局已入库的缴款记录。
        /// 两端省略即今年年初至今。
        /// </summary>
        [HttpGet("{accountSetId:guid}/payments")]
        public async Task<ActionResult<TaxPaymentsResultDto>> Payments(
            Guid accountSetId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var (start, end) = ResolveDateRange(from, to);
            var key = CurrentKey(accountSetId);
            var startedAt = DateTimeOffset.UtcNow;
            var result = await _portal.FetchPaymentsAsync(key, start, end, cancellationToken);
            if (!result.NeedLogin)
            {
                await _tasks.RecordAsync(
                    key, CurrentUserName(), $"查询缴款信息（{start:yyyy-MM-dd}至{end:yyyy-MM-dd}）",
                    result.Message is null, result.Message, startedAt,
                    start.Year * 100 + start.Month, cancellationToken);
            }
            return Ok(result);
        }

        /// <summary>
        /// 读取「缴税」页的应缴税款：按税款所属期（yyyyMM）查询；税局页面取不到时退回申报记录。
        /// </summary>
        [HttpGet("{accountSetId:guid}/payables")]
        public async Task<ActionResult<TaxPayablesResultDto>> Payables(
            Guid accountSetId,
            [FromQuery] string? period,
            CancellationToken cancellationToken)
        {
            var (year, month) = ResolvePeriod(period);
            var key = CurrentKey(accountSetId);
            var startedAt = DateTimeOffset.UtcNow;
            var result = await _portal.FetchPayablesAsync(key, year, month, cancellationToken);
            if (!result.NeedLogin)
            {
                await _tasks.RecordAsync(
                    key, CurrentUserName(), $"查询应缴税款（{year}年{month}月）", result.Message is null,
                    result.Message, startedAt, year * 100 + month, cancellationToken);
            }
            return Ok(result);
        }

        /// <summary>
        /// 读取「下载完税证明」：按开具日期区间（东八区自然日，含首尾）返回税局已开具的完税证明。
        /// 两端省略即今年年初至今。
        /// </summary>
        [HttpGet("{accountSetId:guid}/certificates")]
        public async Task<ActionResult<TaxCertificatesResultDto>> Certificates(
            Guid accountSetId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var (start, end) = ResolveDateRange(from, to);
            var key = CurrentKey(accountSetId);
            var startedAt = DateTimeOffset.UtcNow;
            var result = await _portal.FetchIssuedCertificatesAsync(key, start, end, cancellationToken);
            if (!result.NeedLogin)
            {
                await _tasks.RecordAsync(
                    key, CurrentUserName(), $"查询已开具完税证明（{start:yyyy-MM-dd}至{end:yyyy-MM-dd}）",
                    result.Message is null, result.Message, startedAt,
                    start.Year * 100 + start.Month, cancellationToken);
            }
            return Ok(result);
        }

        /// <summary>发起个税（自然人电子税务局）登录：password=账号密码登录；scan=个人所得税 APP 扫码。</summary>
        [HttpPost("{accountSetId:guid}/iit/login")]
        public async Task<ActionResult<TaxPortalLoginStatusDto>> IitLogin(
            Guid accountSetId,
            [FromBody] TaxIitLoginRequest? request,
            CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var mode = string.IsNullOrWhiteSpace(request?.Mode) ? "password" : request!.Mode!.Trim().ToLowerInvariant();
            // 个税是独立的实名体系，优先用账套「个税申报登录」里填的账号密码；没填时退回企税那套。
            var account = (accountSet.IitLoginAccount ?? accountSet.TaxLoginAccount)?.Trim() ?? string.Empty;
            var password = string.Empty;
            if (mode == "password")
            {
                if (string.IsNullOrWhiteSpace(account))
                {
                    return BadRequest(new { message = "当前账套尚未保存个税实名账号，请先在「账套 - 税务信息」里补全后再试。" });
                }

                var cipher = accountSet.IitLoginPasswordCipher ?? accountSet.TaxLoginPasswordCipher;
                password = _credentials.Decrypt(cipher);
                if (string.IsNullOrEmpty(password))
                {
                    return BadRequest(new { message = "当前账套尚未保存可用的个税密码，请先补全密码或改用「扫码登录」。" });
                }
            }

            return Ok(_portal.StartIitLogin(CurrentKey(accountSetId, TaxPortalKind.Iit), mode, account, password));
        }

        /// <summary>查询个税登录进展：等短信、等扫码、成功或失败。</summary>
        [HttpGet("{accountSetId:guid}/iit/login/status")]
        public ActionResult<TaxPortalLoginStatusDto> IitStatus(Guid accountSetId)
        {
            return Ok(_portal.GetStatus(CurrentKey(accountSetId, TaxPortalKind.Iit)));
        }

        /// <summary>提交个税短信验证码。</summary>
        [HttpPost("{accountSetId:guid}/iit/login/verify")]
        public async Task<ActionResult<TaxPortalLoginStatusDto>> IitVerify(
            Guid accountSetId,
            [FromBody] TaxPortalVerifyRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _portal.VerifyIitAsync(CurrentKey(accountSetId, TaxPortalKind.Iit), request?.Code ?? string.Empty, cancellationToken);
            return Ok(result);
        }

        /// <summary>退出个税登录，丢弃服务器上的个税登录态。</summary>
        [HttpPost("{accountSetId:guid}/iit/logout")]
        public async Task<IActionResult> IitLogout(Guid accountSetId)
        {
            await _portal.LogoutAsync(CurrentKey(accountSetId, TaxPortalKind.Iit));
            return NoContent();
        }

        /// <summary>
        /// 提交一次个税人员信息取数。税局页面要在无头浏览器里一步步点、耗时不可控，
        /// 所以接口立刻返回任务标识并落一行「进行中」台账，页面改轮询 /iit/jobs/{taskId}。
        /// </summary>
        [HttpPost("{accountSetId:guid}/iit/personnel")]
        public async Task<ActionResult<TaxIitJobDto>> IitPersonnel(
            Guid accountSetId,
            CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var key = CurrentKey(accountSetId, TaxPortalKind.Iit);
            return Ok(await _iitJobs.SubmitAsync(
                key,
                CurrentUserName(),
                "同步自然人电子税局人员信息",
                async ct => await _portal.FetchIitPersonnelAsync(key, accountSet.CompanyName, ct),
                cancellationToken));
        }

        /// <summary>提交一次个税「单位办税 → 专项附加扣除信息采集」取数，同样轮询 /iit/jobs/{taskId}。</summary>
        [HttpPost("{accountSetId:guid}/iit/deductions")]
        public async Task<ActionResult<TaxIitJobDto>> IitDeductions(
            Guid accountSetId,
            CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var key = CurrentKey(accountSetId, TaxPortalKind.Iit);
            return Ok(await _iitJobs.SubmitAsync(
                key,
                CurrentUserName(),
                "获取专项附加扣除采集结果",
                async ct => await _portal.FetchIitDeductionsAsync(key, accountSet.CompanyName, ct),
                cancellationToken));
        }

        /// <summary>
        /// 提交一次个税「查询统计 → 申报记录查询」取数。起止所属期形如 2026-01，任一端省略或解析不出
        /// 就沿用税局页面自己的默认区间。
        /// </summary>
        [HttpPost("{accountSetId:guid}/iit/filings")]
        public async Task<ActionResult<TaxIitJobDto>> IitFilings(
            Guid accountSetId,
            [FromQuery] string? start,
            [FromQuery] string? end,
            CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var key = CurrentKey(accountSetId, TaxPortalKind.Iit);
            return Ok(await _iitJobs.SubmitAsync(
                key,
                CurrentUserName(),
                "获取个税申报记录",
                async ct => await _portal.FetchIitFilingsAsync(key, accountSet.CompanyName, MonthOf(start), MonthOf(end), ct),
                cancellationToken));
        }

        /// <summary>
        /// 提交一次个税「查询统计 → 缴税记录查询」取数。dateField=period 按税款所属期过滤，
        /// 否则按缴税日期；起止形如 2026-01（所属期）或 2026-01-01（缴税日期），任一端为空就沿用税局默认区间。
        /// </summary>
        [HttpPost("{accountSetId:guid}/iit/payments")]
        public async Task<ActionResult<TaxIitJobDto>> IitPayments(
            Guid accountSetId,
            [FromQuery] string? dateField,
            [FromQuery] string? start,
            [FromQuery] string? end,
            CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            var key = CurrentKey(accountSetId, TaxPortalKind.Iit);
            return Ok(await _iitJobs.SubmitAsync(
                key,
                CurrentUserName(),
                "获取个税缴税记录",
                async ct => await _portal.FetchIitPaymentsAsync(
                    key, accountSet.CompanyName, dateField, start, end, ct),
                cancellationToken));
        }

        /// <summary>
        /// 轮询后台取数任务：running 表示税局那边还在跑，success 时 data 里就是取到的数据，
        /// failed 时 message 同时写进了任务中心的「解决建议」。
        /// </summary>
        [HttpGet("{accountSetId:guid}/iit/jobs/{taskId:guid}")]
        public async Task<ActionResult<TaxIitJobDto>> IitJob(
            Guid accountSetId,
            Guid taskId,
            CancellationToken cancellationToken)
        {
            return Ok(await _iitJobs.GetAsync(CurrentKey(accountSetId, TaxPortalKind.Iit), taskId, cancellationToken));
        }

        /// <summary>个税「报表填写」：按累计预扣法把本系统工资表算成综合所得（工资薪金）申报表。</summary>
        [HttpGet("{accountSetId:guid}/iit/report")]
        public async Task<ActionResult<TaxIitReportResultDto>> IitReport(
            Guid accountSetId,
            [FromQuery] string? period,
            CancellationToken cancellationToken)
        {
            var (year, month) = ResolvePeriod(period);
            return Ok(await _iitReport.BuildAsync(accountSetId, year, month, cancellationToken));
        }

        /// <summary>
        /// 一键取票：从电子税务局「全量发票查询」取开票日期区间内的发票，判定销项/进项后写进发票池。
        /// 查询与翻页都跑在无头浏览器里，接口立刻返回任务标识，页面轮询 /invoices/sync/{taskId}。
        /// </summary>
        [HttpPost("{accountSetId:guid}/invoices/sync")]
        public async Task<ActionResult<TaxInvoiceSyncJobDto>> SyncInvoices(
            Guid accountSetId,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            CancellationToken cancellationToken)
        {
            var accountSet = await _accountSets.GetByIdAsync(accountSetId, cancellationToken);
            if (accountSet is null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(accountSet.TaxpayerNumber))
            {
                return BadRequest(new { message = "请先在账套的税务信息里补全统一社会信用代码，再一键取票。" });
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var start = startDate ?? new DateOnly(today.Year, today.Month, 1);
            var end = endDate ?? start.AddMonths(1).AddDays(-1);
            if (end < start)
            {
                return BadRequest(new { message = "取票区间的截止日期不能早于起始日期。" });
            }

            return Ok(await _invoiceJobs.SubmitAsync(
                CurrentKey(accountSetId),
                CurrentUserName(),
                accountSet.TaxpayerNumber.Trim(),
                start,
                end,
                cancellationToken));
        }

        /// <summary>轮询一键取票任务：running 时税局还在查，success 时发票已经落库，页面刷新列表即可。</summary>
        [HttpGet("{accountSetId:guid}/invoices/sync/{taskId:guid}")]
        public async Task<ActionResult<TaxInvoiceSyncJobDto>> InvoiceSyncJob(
            Guid accountSetId,
            Guid taskId,
            CancellationToken cancellationToken)
        {
            return Ok(await _invoiceJobs.GetAsync(CurrentKey(accountSetId), taskId, cancellationToken));
        }

        /// <summary>
        /// 任务中心：本账套在服务端办过的税局操作，按提交时间区间（东八区自然日）倒序返回。
        /// 区间两端都可省略，省略即不限。
        /// </summary>
        [HttpGet("{accountSetId:guid}/tasks")]
        public async Task<ActionResult<TaxTasksResultDto>> Tasks(
            Guid accountSetId,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            return Ok(await _tasks.QueryAsync(accountSetId, from, to, cancellationToken));
        }

        private TaxPortalSessionKey CurrentKey(Guid accountSetId, TaxPortalKind kind = TaxPortalKind.Enterprise)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.Identity?.Name
                ?? "anonymous";
            return new TaxPortalSessionKey(accountSetId, userId, kind);
        }

        /// <summary>任务中心「操作人」列显示的名字。</summary>
        private string CurrentUserName()
        {
            return User.FindFirst("displayName")?.Value
                ?? User.Identity?.Name
                ?? "未知用户";
        }

        /// <summary>把日期区间归一到东八区自然日；两端省略即今年年初至今，起止颠倒时自动交换。</summary>
        private static (DateOnly From, DateOnly To) ResolveDateRange(DateOnly? from, DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var start = from ?? new DateOnly(today.Year, 1, 1);
            var end = to ?? today;
            return end < start ? (end, start) : (start, end);
        }

        /// <summary>把 yyyyMM 归一到具体年月；缺省或非法时取当前自然月。</summary>
        private static (int Year, int Month) ResolvePeriod(string? period)
        {
            var now = DateTime.Now;
            if (period is { Length: 6 }
                && int.TryParse(period[..4], out var year)
                && int.TryParse(period[4..], out var month)
                && month is >= 1 and <= 12
                && year is >= 2000 and <= 2100)
            {
                return (year, month);
            }

            return (now.Year, now.Month);
        }

        /// <summary>所属期归一成「yyyy-MM」，兼容 2026-01 / 202601 / 2026-01-31；解析不出返回 null，让税局用默认区间。</summary>
        private static string? MonthOf(string? period)
        {
            var text = period?.Trim();
            if (text is not { Length: >= 6 } || !int.TryParse(text[..4], out var year) || year is < 2000 or > 2100)
            {
                return null;
            }

            var digits = new string(text.Where(char.IsDigit).ToArray());
            if (digits.Length < 6 || !int.TryParse(digits[4..6], out var month) || month is < 1 or > 12)
            {
                return null;
            }

            return $"{year:D4}-{month:D2}";
        }
    }
}
