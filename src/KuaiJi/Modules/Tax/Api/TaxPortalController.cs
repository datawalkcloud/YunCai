using System.Security.Claims;
using KuaiJi.Modules.AccountSets.Application;
using KuaiJi.Modules.AccountSets.Domain;
using KuaiJi.Modules.Tax.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Tax.Api
{
    /// <summary>
    /// 智能报税的服务端税局通道：登录、短信验证、税费种取数。
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

        public TaxPortalController(
            IAccountSetRepository accountSets,
            ITaxCredentialProtector credentials,
            TaxPortalService portal,
            TaxPortalBrowserHandoffStore handoffs)
        {
            _accountSets = accountSets;
            _credentials = credentials;
            _portal = portal;
            _handoffs = handoffs;
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
        /// 内存里没有会话时会先用保存的登录态恢复，可能要拉起浏览器，耗时比 status 长。
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
            return Ok(await _portal.ExploreAsync(CurrentKey(accountSetId), request, cancellationToken));
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
            return Ok(await _portal.DebugInvoiceQueryAsync(CurrentKey(accountSetId), start, end, cancellationToken));
        }

        /// <summary>从已登录的税局会话读取税费种认定。</summary>
        [HttpGet("{accountSetId:guid}/tax-kinds")]
        public async Task<ActionResult<TaxKindsResultDto>> TaxKinds(Guid accountSetId, CancellationToken cancellationToken)
        {
            var result = await _portal.FetchTaxKindsAsync(CurrentKey(accountSetId), cancellationToken);
            return Ok(result);
        }

        /// <summary>读取所选税款所属期（yyyyMM）在电子税务局里的申报记录。</summary>
        [HttpGet("{accountSetId:guid}/declarations")]
        public async Task<ActionResult<TaxDeclarationsResultDto>> Declarations(
            Guid accountSetId,
            [FromQuery] string? period,
            CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var year = now.Year;
            var month = now.Month;
            if (period is { Length: 6 }
                && int.TryParse(period[..4], out var parsedYear)
                && int.TryParse(period[4..], out var parsedMonth)
                && parsedMonth is >= 1 and <= 12
                && parsedYear is >= 2000 and <= 2100)
            {
                year = parsedYear;
                month = parsedMonth;
            }

            var result = await _portal.FetchDeclarationsAsync(CurrentKey(accountSetId), year, month, cancellationToken);
            return Ok(result);
        }

        private TaxPortalSessionKey CurrentKey(Guid accountSetId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.Identity?.Name
                ?? "anonymous";
            return new TaxPortalSessionKey(accountSetId, userId);
        }
    }
}
