namespace YunCai.Modules.Marketing.Application;

/// <summary>官网联系表单配置：企业微信机器人 webhook 地址。</summary>
public sealed class ContactOptions
{
    /// <summary>配置节名称。webhook 属机密，只从环境变量 / 本机 Development 配置读取，不入库。</summary>
    public const string SectionName = "YunCai:Contact";

    /// <summary>企业微信机器人 webhook 完整地址；为空时联系接口直接返回「未开放」。</summary>
    public string Webhook { get; set; } = string.Empty;
}
