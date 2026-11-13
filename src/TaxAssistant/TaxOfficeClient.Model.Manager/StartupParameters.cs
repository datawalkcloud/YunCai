namespace TaxOfficeClient.Model.Manager;

public class StartupParameters
{
	public int AreaCode { get; set; }

	public string Uscc { get; set; }

	public string DestinationUrl { get; set; }

	/// <summary>报税地区（省级税务局名称），用于选择电子税务局入口。</summary>
	public string Region { get; set; } = string.Empty;

	public string Identifier { get; set; }

	public string PersonalAccount { get; set; }

	public string AccountId { get; set; } = string.Empty;

	/// <summary>云财页面传来的公司名称，仅用于窗口显示。</summary>
	public string CompanyName { get; set; } = string.Empty;

	/// <summary>
	/// 来自云财页面（yuncai: 协议）的请求没有云端登录态，跳过换取 token 的接口。
	/// 带了 Ticket + ApiBase 时先用票据向云财后端取回服务器上的税局登录态并注入浏览器，
	/// 实现免登录；没带票据时直接打开税局页面由用户登录。
	/// </summary>
	public bool SkipCloudLogin { get; set; }

	/// <summary>网页用登录态换取的一次性票据，用来取回服务器上已登录的税局 Cookie。</summary>
	public string Ticket { get; set; } = string.Empty;

	/// <summary>云财后端 API 根地址（形如 https://站点/api），助手凭它兑换票据。</summary>
	public string ApiBase { get; set; } = string.Empty;
}
