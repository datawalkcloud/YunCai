namespace YunCai.Modules.Identity.Application
{
    /// <summary>短信发送结果；模拟通道会带回 DebugCode，供本地开发时直接查看验证码。</summary>
    public sealed record SmsSendOutcome(bool Success, string Message, string? DebugCode = null);

    /// <summary>
    /// 短信发送通道。真实通道（如阿里云短信）实现该接口后在 Program.cs 中替换注册即可，
    /// 验证码生成、有效期与重发间隔由 SmsCodeService 统一负责。
    /// </summary>
    public interface ISmsSender
    {
        string Name { get; }

        Task<SmsSendOutcome> SendAsync(string phone, string code, CancellationToken cancellationToken = default);
    }
}
