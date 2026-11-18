using System.Collections.Generic;

namespace TaxOfficeClient.Model.Manager;

/// <summary>
/// 网页票据换回来的浏览器登录态：服务器上已登录的税局 Cookie、各站点 localStorage，
/// 以及要打开的企业电子税务局地址。助手把这些注入自己的浏览器后再导航，实现免登录。
/// </summary>
public sealed class BrowserHandoff
{
	public string Url { get; set; } = string.Empty;

	public List<BrowserCookie> Cookies { get; set; } = new List<BrowserCookie>();

	public List<BrowserOrigin> Origins { get; set; } = new List<BrowserOrigin>();
}

/// <summary>一条税局 Cookie，字段与 Playwright storage state 对齐。</summary>
public sealed class BrowserCookie
{
	public string Name { get; set; } = string.Empty;

	public string Value { get; set; } = string.Empty;

	public string Domain { get; set; } = string.Empty;

	public string Path { get; set; } = "/";

	/// <summary>Unix 秒；会话级 Cookie 为 -1。</summary>
	public double Expires { get; set; } = -1;

	public bool HttpOnly { get; set; }

	public bool Secure { get; set; }

	/// <summary>Strict / Lax / None。</summary>
	public string SameSite { get; set; } = "Lax";
}

/// <summary>某个税局站点域下的 localStorage 快照；tpass 用它保存登录 token。</summary>
public sealed class BrowserOrigin
{
	public string Origin { get; set; } = string.Empty;

	public Dictionary<string, string> Items { get; set; } = new Dictionary<string, string>();
}
