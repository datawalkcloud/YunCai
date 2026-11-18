namespace YunCai.Modules.Identity.Application
{
    /// <summary>短信验证码配置，对应配置节 "YunCai:Sms"。</summary>
    public class SmsOptions
    {
        /// <summary>短信通道；目前仅 "Mock"（本地模拟，验证码回显在响应里）。</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>验证码有效期（秒）。</summary>
        public int CodeExpireSeconds { get; set; } = 300;

        /// <summary>同一手机号两次发送的最小间隔（秒）。</summary>
        public int RetrySeconds { get; set; } = 60;
    }
}
