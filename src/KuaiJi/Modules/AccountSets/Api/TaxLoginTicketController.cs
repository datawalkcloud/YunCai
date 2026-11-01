using KuaiJi.Modules.AccountSets.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.Modules.AccountSets.Api
{
    /// <summary>
    /// 本机税务助手兑换一次性税局密码票据的入口。
    /// 助手没有云财登录态，所以匿名开放；安全性由票据本身保证：32 字节随机、2 分钟过期、只可兑换一次。
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [AllowAnonymous]
    public class TaxLoginTicketController : ControllerBase
    {
        private readonly TaxLoginTicketStore _tickets;

        public TaxLoginTicketController(TaxLoginTicketStore tickets)
        {
            _tickets = tickets;
        }

        /// <summary>用网页转交的票据换回税局密码；票据随即作废。</summary>
        [HttpPost("redeem")]
        public ActionResult<TaxLoginPasswordDto> Redeem([FromBody] TaxLoginTicketRedeemRequest request)
        {
            var password = _tickets.Redeem(request?.Ticket);
            if (password is null)
            {
                return BadRequest(new { message = "登录凭据已失效，请回到网页重新点击「登录税局」。" });
            }

            return Ok(new TaxLoginPasswordDto { Password = password });
        }
    }
}
