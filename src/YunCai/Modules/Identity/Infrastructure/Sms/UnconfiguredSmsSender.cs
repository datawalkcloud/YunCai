using YunCai.Modules.Identity.Application;

namespace YunCai.Modules.Identity.Infrastructure.Sms
{
    /// <summary>
    /// 未配置真实短信通道时的占位实现：明确拒绝发送，而不是静默成功。
    /// 接入真实通道（如阿里云短信）后替换 Program.cs 中的注册即可。
    /// </summary>
    public sealed class UnconfiguredSmsSender : ISmsSender
    {
        public string Name => "未配置";

        public Task<SmsSendOutcome> SendAsync(string phone, string code, CancellationToken cancellationToken = default)
            => Task.FromResult(new SmsSendOutcome(false, "短信通道未配置，暂时无法使用手机验证码登录。"));
    }
}
