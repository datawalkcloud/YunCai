using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace YunCai.Modules.Marketing.Application;

/// <summary>
/// 官网联系表单：把访客留言推送到企业微信群机器人。
/// webhook 地址是机密（含 key），只从配置读取；未配置时不对外暴露是否已配置，统一回「暂未开放」。
/// </summary>
public sealed class ContactService
{
    private const int MaxName = 40;
    private const int MaxContact = 120;
    private const int MaxMessage = 2000;

    private readonly HttpClient _httpClient;
    private readonly ContactOptions _options;
    private readonly ILogger<ContactService> _logger;

    public ContactService(IHttpClientFactory httpClientFactory, IOptions<ContactOptions> options, ILogger<ContactService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>webhook 是否已配置。</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Webhook);

    /// <summary>
    /// 校验并推送留言。参数不合法抛 ArgumentException（=> 400 中文文案）；
    /// 机器人返回失败抛 InvalidOperationException（=> 400，文案不泄露 webhook 地址）。
    /// </summary>
    public async Task SubmitAsync(ContactRequest request, string? clientIp, CancellationToken cancellationToken)
    {
        // 蜜罐：隐藏输入框被填说明是机器人，静默返回成功，不推送、不惊动对方。
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            _logger.LogWarning("联系表单命中蜜罐字段，已静默丢弃（来源 IP：{Ip}）。", clientIp);
            return;
        }

        var name = (request.Name ?? string.Empty).Trim();
        var contact = (request.Contact ?? string.Empty).Trim();
        var message = (request.Message ?? string.Empty).Trim();

        if (name.Length is < 1 or > MaxName)
            throw new ArgumentException($"请填写称呼（1～{MaxName} 个字符）。");
        if (contact.Length is < 1 or > MaxContact)
            throw new ArgumentException($"请填写联系方式（1～{MaxContact} 个字符）。");
        if (message.Length > MaxMessage)
            throw new ArgumentException($"需求描述不能超过 {MaxMessage} 个字符。");

        var content = BuildMessage(name, contact, message, clientIp);

        // 企业微信机器人 markdown 消息体。字段串起来后由文本转义函数处理，避免留言里的特殊字符破坏排版。
        var payload = new
        {
            msgtype = "markdown",
            markdown = new { content }
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                _options.Webhook, payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // 企业微信成功响应：{"errcode":0,"errmsg":"ok"}
            if (!response.IsSuccessStatusCode || !body.Contains("\"errcode\":0", StringComparison.Ordinal))
            {
                _logger.LogWarning("企业微信机器人推送失败：HTTP {Status}，响应 {Body}", (int)response.StatusCode, body);
                throw new InvalidOperationException("留言暂时无法送达，请稍后再试。");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("企业微信机器人响应超时，请稍后再试。");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "企业微信机器人网络请求失败。");
            throw new InvalidOperationException("留言暂时无法送达，请稍后再试或直接邮件联系我们。");
        }
    }

    private static string BuildMessage(string name, string contact, string message, string? clientIp)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 官网体验申请");
        sb.AppendLine($"> 时间：<font color=\"comment\">{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}</font>");
        sb.AppendLine($"**您的称呼**：{Escape(name)}");
        sb.AppendLine($"**联系方式**：{Escape(contact)}");
        sb.AppendLine($"**需求描述**：{(message.Length > 0 ? Escape(message) : "<font color=\"comment\">未填写</font>")}");
        sb.AppendLine($"**来源 IP**：<font color=\"comment\">{Escape(clientIp ?? "未知")}</font>");
        return sb.ToString();
    }

    /// <summary>转义企业微信 markdown 的强调符与反引号，避免留言内容把消息排版冲乱。</summary>
    private static string Escape(string value)
        => value.Replace("*", "＊").Replace("_", "＿").Replace("`", "'").Replace("<", "‹").Replace(">", "›");
}
