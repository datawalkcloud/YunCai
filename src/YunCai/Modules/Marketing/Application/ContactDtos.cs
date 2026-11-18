namespace YunCai.Modules.Marketing.Application;

/// <summary>官网体验申请表单提交内容。</summary>
/// <param name="Name">您的称呼。</param>
/// <param name="Contact">联系方式（手机或微信）。</param>
/// <param name="Message">需求描述（选填）。</param>
/// <param name="Website">蜜罐字段：正常用户看不到这个输入框，机器人填了就静默丢弃。</param>
public sealed record ContactRequest(
    string? Name,
    string? Contact,
    string? Message,
    string? Website);
