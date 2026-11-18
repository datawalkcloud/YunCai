using YunCai.Modules.Identity.Application;

namespace YunCai.Modules.Identity.Infrastructure.Sms
{
    /// <summary>
    /// 本地开发用模拟短信通道：不真正发送，把验证码写进日志并在响应里回显，
    /// 前端可直接看到「本地模拟验证码」。仅在 Development 环境注册。
    /// </summary>
    public sealed class MockSmsSender(ILogger<MockSmsSender> logger) : ISmsSender
    {
        public string Name => "模拟短信";

        public Task<SmsSendOutcome> SendAsync(string phone, string code, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("【模拟短信】手机号 {Phone} 的登录验证码为 {Code}", phone, code);
            return Task.FromResult(new SmsSendOutcome(true, "验证码已发送（本地模拟）", code));
        }
    }
}
