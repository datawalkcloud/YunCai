namespace TaxOfficeClient.Model.Manager;

/// <summary>网页经本机端口提交的税局登录请求；密码只在内存里经过，不写日志。</summary>
public sealed class TaxLoginRequest
{
	public string CompanyName { get; set; } = string.Empty;

	public string TaxNo { get; set; } = string.Empty;

	public string Region { get; set; } = string.Empty;

	public string LoginType { get; set; } = string.Empty;

	public string IdentityType { get; set; } = string.Empty;

	public string Account { get; set; } = string.Empty;

	/// <summary>本次填写的明文密码；留空时改用 Ticket 向后端兑换。</summary>
	public string? Password { get; set; }

	/// <summary>网页用登录态换到的一次性票据，兑换后即失效。</summary>
	public string? Ticket { get; set; }

	/// <summary>云财后端地址（含 /api 前缀前的主机路径），用于兑换票据。</summary>
	public string? ApiBase { get; set; }

	public string? AccountId { get; set; }

	/// <summary>可选的税局登录页地址；只接受 chinatax.gov.cn 域，避免被任意网页带去钓鱼站。</summary>
	public string? DestinationUrl { get; set; }
}

/// <summary>网页回填短信验证码。</summary>
public sealed class TaxLoginVerifyRequest
{
	public string? SessionId { get; set; }

	public string? Code { get; set; }
}

/// <summary>登录过程对外暴露的状态。</summary>
public static class TaxLoginStates
{
	public const string Starting = "starting";

	/// <summary>税局登录页已提交，等用户把短信验证码填回网页。</summary>
	public const string SmsRequired = "sms_required";

	/// <summary>出现滑块/图片验证码等无法自动完成的一步，需要用户在助手窗口里操作。</summary>
	public const string ManualRequired = "manual_required";

	public const string Success = "success";

	public const string Failed = "failed";
}

/// <summary>本机接口统一的返回体。</summary>
public sealed class TaxLoginStatus
{
	public string State { get; set; } = TaxLoginStates.Starting;

	public string Message { get; set; } = string.Empty;

	public string? SessionId { get; set; }
}
