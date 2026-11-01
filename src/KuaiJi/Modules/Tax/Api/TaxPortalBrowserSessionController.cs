using KuaiJi.Modules.Tax.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.Tax.Api
{
    /// <summary>
    /// 本机税务助手兑换浏览器会话票据的入口。
    /// 助手没有云财登录态，所以匿名开放；安全性由票据保证：32 字节随机、2 分钟过期、只可兑换一次，
    /// 且导出的内容只包含税局域名下的 Cookie 与 localStorage。
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [AllowAnonymous]
    public class TaxPortalBrowserSessionController : ControllerBase
    {
        private readonly TaxPortalBrowserHandoffStore _handoffs;

        public TaxPortalBrowserSessionController(TaxPortalBrowserHandoffStore handoffs)
        {
            _handoffs = handoffs;
        }

        /// <summary>用网页转交的票据换回浏览器登录态；票据随即作废。</summary>
        [HttpPost("redeem")]
        public ActionResult<TaxPortalBrowserHandoffDto> Redeem([FromBody] TaxPortalBrowserTicketRedeemRequest request)
        {
            var handoff = _handoffs.Redeem(request?.Ticket);
            if (handoff is null)
            {
                return BadRequest(new { message = "登录态票据已失效，请回到网页重新点击「一键登录电子税务局」。" });
            }

            return Ok(handoff);
        }
    }
}
