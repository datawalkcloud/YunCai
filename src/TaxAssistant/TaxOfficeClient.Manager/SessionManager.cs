using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaxOfficeClient.Controls;
using TaxOfficeClient.Model.Manager;
using TaxOfficeClient.Tools;

namespace TaxOfficeClient.Manager;

public sealed class SessionManager : IDisposable
{
	private class SessionData(StartupParameters startupParameters)
	{
		private readonly object _tabItemsLock = new object();

		public volatile StartupParameters StartupParameters = startupParameters ?? throw new ArgumentNullException("startupParameters");

		public SemaphoreSlim LoginStateLock { get; } = new SemaphoreSlim(1, 1);

		public List<WebViewTabItem> TabItems { get; } = new List<WebViewTabItem>();

		public void AddTab(WebViewTabItem tabItem)
		{
			if (tabItem == null)
			{
				return;
			}
			lock (_tabItemsLock)
			{
				if (!TabItems.Contains(tabItem))
				{
					TabItems.Add(tabItem);
				}
			}
		}

		public bool RemoveTab(WebViewTabItem tabItem)
		{
			if (tabItem == null)
			{
				return false;
			}
			lock (_tabItemsLock)
			{
				return TabItems.Remove(tabItem);
			}
		}
	}

	private const string OLD_API_SUCCESS_CODE = "200";

	private const string NEW_API_SUCCESS_CODE = "0";

	private const string URL_PROTOCOL_PREFIX = "nmyswzs://?";

	private const string URL_PROTOCOL_MALFORMED = "nmyswzs:///?";

	private readonly ConcurrentDictionary<int, SessionData> _sessions = new ConcurrentDictionary<int, SessionData>();

	private bool _disposed;

	private readonly object _disposeLock = new object();

	private static readonly Lazy<SessionManager> _instance = new Lazy<SessionManager>(() => new SessionManager());

	private static HttpClient HttpClient => HttpClientManager.Instance;

	public static SessionManager Instance => _instance.Value;

	public int AddSession(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			throw new ArgumentException("添加session时传入的URL不能为空", "url");
		}
		StartupParameters parameters = ParseUrlParameters(url);
		Logger.Info($"添加会话 AreaCode={parameters.AreaCode}");
		_sessions.AddOrUpdate(parameters.AreaCode, (int _) => CreateNewSession(parameters), (int _, SessionData oldValue) => UpdateExistingSession(oldValue, parameters));
		return parameters.AreaCode;
	}

	/// <summary>
	/// 直接以参数登记会话。本机网页发起的登录没有 kuaiji: URL，
	/// 由登录执行器构造参数后走这里，标签页仍能按 AreaCode 找到会话。
	/// </summary>
	public int AddSession(StartupParameters parameters)
	{
		if (parameters == null)
		{
			throw new ArgumentNullException("parameters");
		}
		Logger.Info($"添加本机会话 AreaCode={parameters.AreaCode}, USCC={parameters.Uscc}");
		_sessions.AddOrUpdate(parameters.AreaCode, (int _) => CreateNewSession(parameters), (int _, SessionData oldValue) => UpdateExistingSession(oldValue, parameters));
		return parameters.AreaCode;
	}

	private static SessionData CreateNewSession(StartupParameters parameters)
	{
		Logger.Info($"创建新会话：AreaCode={parameters.AreaCode}, USCC={parameters.Uscc}");
		return new SessionData(parameters);
	}

	private static SessionData UpdateExistingSession(SessionData oldSession, StartupParameters newParameters)
	{
		if (oldSession.StartupParameters.Uscc != newParameters.Uscc)
		{
			Logger.Info($"更新会话，AreaCode={newParameters.AreaCode}, " + "USCC变更：" + oldSession.StartupParameters.Uscc + " -> " + newParameters.Uscc);
		}
		else
		{
			Logger.Info($"会话AreaCode={newParameters.AreaCode}已存在，USCC={newParameters.Uscc}未变化");
		}
		oldSession.StartupParameters = newParameters;
		return oldSession;
	}

	public void AddTabToSession(int areaCode, WebViewTabItem tabItem)
	{
		SessionData value;
		if (tabItem == null)
		{
			Logger.Warn($"尝试向会话{areaCode}添加空标签页");
		}
		else if (_sessions.TryGetValue(areaCode, out value))
		{
			value.AddTab(tabItem);
			Logger.Info($"已向会话{areaCode}添加标签页，当前标签页数量：{value.TabItems.Count}", value.StartupParameters.AreaCode, value.StartupParameters.Uscc);
		}
		else
		{
			Logger.Warn($"尝试向不存在的会话{areaCode}添加标签页");
		}
	}

	public void RemoveTabFromSession(int areaCode, WebViewTabItem tabItem)
	{
		SessionData value;
		if (tabItem == null)
		{
			Logger.Warn($"尝试从会话{areaCode}移除空标签页");
		}
		else if (_sessions.TryGetValue(areaCode, out value))
		{
			if (value.RemoveTab(tabItem))
			{
				Logger.Info($"已从会话{areaCode}移除标签页，剩余标签页数量：{value.TabItems.Count}", value.StartupParameters.AreaCode, value.StartupParameters.Uscc);
				if (value.TabItems.Count == 0)
				{
					Logger.Info($"会话{areaCode}已无标签页");
				}
			}
			else
			{
				Logger.Warn($"从会话{areaCode}移除标签页失败，标签页可能不存在");
			}
		}
		else
		{
			Logger.Warn($"尝试从不存在的会话{areaCode}移除标签页");
		}
	}

	public StartupParameters GetStartupParameters(int areaCode)
	{
		if (!_sessions.TryGetValue(areaCode, out var value))
		{
			throw new KeyNotFoundException($"会话不存在：AreaCode={areaCode}");
		}
		return value.StartupParameters;
	}

	public bool IsCurrentStartupParameters(StartupParameters parameters)
	{
		if (_sessions.TryGetValue(parameters.AreaCode, out var value))
		{
			return value.StartupParameters == parameters;
		}
		return false;
	}

	public async Task ApplyLoginStateAsync(StartupParameters parameters, Func<Task> applyStateAsync)
	{
		if (!_sessions.TryGetValue(parameters.AreaCode, out var session))
		{
			return;
		}
		await session.LoginStateLock.WaitAsync();
		try
		{
			if (IsCurrentStartupParameters(parameters))
			{
				await applyStateAsync();
			}
		}
		finally
		{
			session.LoginStateLock.Release();
		}
	}

	private static StartupParameters ParseUrlParameters(string url)
	{
		Logger.Info("开始解析URL：" + RedactTicket(url));
		try
		{
			// 云财页面用 kuaiji:tax?... 唤起；这类请求没有云端登录态，不走换取 token 的接口。
			bool fromKuaiji = url.StartsWith("kuaiji:", StringComparison.OrdinalIgnoreCase);
			url = NormalizeUrl(url);
			int queryIndex = url.IndexOf('?');
			NameValueCollection nameValueCollection = HttpUtility.ParseQueryString((queryIndex >= 0) ? url.Substring(queryIndex) : string.Empty);
			if (nameValueCollection.Count == 0)
			{
				throw new ArgumentException("URL不包含任何查询参数");
			}
			StartupParameters startupParameters = ExtractParameters(nameValueCollection);
			startupParameters.SkipCloudLogin = fromKuaiji;
			ValidateParameters(startupParameters);
			Logger.Info($"解析URL成功：AreaCode={startupParameters.AreaCode}, USCC={startupParameters.Uscc}, " + $"Region={startupParameters.Region}, Identifier={startupParameters.Identifier}, PersonalAccount={startupParameters.PersonalAccount}, " + $"DestinationUrl={startupParameters.DestinationUrl}, AccountId={startupParameters.AccountId}, SkipCloudLogin={startupParameters.SkipCloudLogin}");
			return startupParameters;
		}
		catch (Exception ex)
		{
			Logger.Warn("URL参数解析失败，URL：" + url, ex);
			throw new ArgumentException("URL参数解析失败: " + ex.Message, "url", ex);
		}
	}

	private static string NormalizeUrl(string url)
	{
		if (!url.StartsWith("nmyswzs:///?"))
		{
			return url;
		}
		return url.Replace("nmyswzs:///?", "nmyswzs://?");
	}

	private static StartupParameters ExtractParameters(NameValueCollection queryParams)
	{
		return new StartupParameters
		{
			AreaCode = ParseOptionalInt(queryParams["AreaCode"]),
			Uscc = (queryParams["USCC"] ?? queryParams["taxNo"] ?? string.Empty),
			Identifier = (queryParams["Identifier"] ?? string.Empty),
			PersonalAccount = (queryParams["PersonalAccount"] ?? queryParams["account"] ?? string.Empty),
			DestinationUrl = (queryParams["url"] ?? string.Empty),
			Region = (queryParams["region"] ?? string.Empty),
			AccountId = (queryParams["AccountId"] ?? string.Empty),
			CompanyName = (queryParams["company"] ?? string.Empty),
			Ticket = (queryParams["ticket"] ?? string.Empty),
			ApiBase = (queryParams["apiBase"] ?? string.Empty)
		};
	}

	/// <summary>票据是取回登录态的短期凭据，写日志时只留参数名。</summary>
	public static string RedactTicket(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return url;
		}
		return System.Text.RegularExpressions.Regex.Replace(
			url,
			"([?&]ticket=)[^&\"]*",
			"$1***",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);
	}

	private static int ParseOptionalInt(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return 0;
		}
		if (!int.TryParse(value, out var result))
		{
			throw new ArgumentException("参数 AreaCode 必须是有效的整数");
		}
		return result;
	}

	private static void ValidateParameters(StartupParameters parameters)
	{
		if (string.IsNullOrWhiteSpace(parameters.DestinationUrl))
		{
			parameters.DestinationUrl = "https://etax.chinatax.gov.cn/";
		}
		if (!Uri.IsWellFormedUriString(parameters.DestinationUrl, UriKind.Absolute))
		{
			throw new ArgumentException("DestinationUrl格式无效：" + parameters.DestinationUrl);
		}
	}

	public async Task<ApiResult> FetchParametersFromApi(StartupParameters parameters)
	{
		_ = 2;
		try
		{
			string text = BuildApiRequestUrl(parameters);
			Logger.Info("开始从API获取登录参数，请求URL：" + text, parameters.AreaCode, parameters.Uscc);
			using HttpResponseMessage response = await HttpClient.GetAsync(text);
			return await ProcessApiResponse(response, await response.Content.ReadAsStringAsync(), parameters);
		}
		catch (HttpRequestException ex)
		{
			Logger.Warn("HTTP请求失败", ex);
			return CreateErrorResult("网络请求失败: " + ex.Message);
		}
		catch (Exception ex2)
		{
			Logger.Warn("获取登录参数错误", ex2);
			return CreateErrorResult("获取登录参数错误，请检查网络连接或稍后重试。错误信息：" + ex2.Message);
		}
	}

	/// <summary>
	/// 用网页换到的一次性票据，从云财后端取回服务器上已登录的税局登录态。
	/// 票据只能兑换一次；内容只含税局域名下的 Cookie 与 localStorage。
	/// </summary>
	public async Task<BrowserHandoff> FetchBrowserHandoffAsync(StartupParameters parameters)
	{
		string apiBase = (parameters.ApiBase ?? string.Empty).Trim().TrimEnd('/');
		if (string.IsNullOrWhiteSpace(parameters.Ticket) || !Uri.TryCreate(apiBase, UriKind.Absolute, out Uri apiUri))
		{
			throw new InvalidOperationException("缺少登录态票据或云财后端地址。");
		}
		if (apiUri.Scheme != Uri.UriSchemeHttps && !apiUri.IsLoopback)
		{
			throw new InvalidOperationException("云财后端地址必须是 HTTPS。");
		}

		Logger.Info("开始用票据换取税局登录态", parameters.AreaCode, parameters.Uscc);
		using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		using StringContent content = new StringContent(
			JsonConvert.SerializeObject(new { ticket = parameters.Ticket }),
			Encoding.UTF8,
			"application/json");
		using HttpResponseMessage response = await HttpClient.PostAsync(apiBase + "/TaxPortalBrowserSession/redeem", content, timeout.Token);
		string body = await response.Content.ReadAsStringAsync(timeout.Token);
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException(ReadErrorMessage(body) ?? "换取税局登录态失败，请回到网页重新点击「一键登录电子税务局」。");
		}

		BrowserHandoff handoff = JsonConvert.DeserializeObject<BrowserHandoff>(body);
		if (handoff == null || (handoff.Cookies.Count == 0 && handoff.Origins.Count == 0))
		{
			throw new InvalidOperationException("服务器没有返回可用的税局登录态，请回到网页重新登录。");
		}
		Logger.Info($"已取得税局登录态：Cookie {handoff.Cookies.Count} 项，站点 {handoff.Origins.Count} 个", parameters.AreaCode, parameters.Uscc);
		return handoff;
	}

	private static string ReadErrorMessage(string body)
	{
		try
		{
			return JsonConvert.DeserializeObject<JObject>(body)?["message"]?.ToString();
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static string BuildApiRequestUrl(StartupParameters parameters)
	{
		StringBuilder stringBuilder = new StringBuilder((!string.IsNullOrWhiteSpace(parameters.AccountId)) ? "https://jtaxapi.ningmengyun.com/api/TaxTransfer/UserToken" : "https://japi.ningmengyun.com/api/TaxTransfer/UserToken");
		stringBuilder.Append($"?taxArea={parameters.AreaCode}");
		stringBuilder.Append("&taxNumber=" + Uri.EscapeDataString(parameters.Uscc));
		stringBuilder.Append("&identity=" + Uri.EscapeDataString(parameters.Identifier));
		stringBuilder.Append("&personalAccount=" + Uri.EscapeDataString(parameters.PersonalAccount));
		stringBuilder.Append("&accountId=" + Uri.EscapeDataString(parameters.AccountId ?? string.Empty));
		return stringBuilder.ToString();
	}

	private static async Task<ApiResult> ProcessApiResponse(HttpResponseMessage response, string responseBody, StartupParameters parameters)
	{
		try
		{
			JObject jObject = JsonConvert.DeserializeObject<JObject>(responseBody);
			if (jObject == null)
			{
				return CreateErrorResult("API返回数据为空");
			}
			string text = ((!string.IsNullOrWhiteSpace(parameters.AccountId)) ? "0" : "200");
			if (response.IsSuccessStatusCode && jObject["code"]?.ToString() == text)
			{
				return await ProcessSuccessResponse(jObject, parameters);
			}
			return ProcessErrorResponse(jObject);
		}
		catch (JsonException ex)
		{
			Logger.Warn("API响应JSON解析失败", ex);
			return CreateErrorResult("API响应格式错误: " + ex.Message);
		}
	}

	private static Task<ApiResult> ProcessSuccessResponse(JObject responseData, StartupParameters parameters)
	{
		Logger.Info($"获取登录参数成功，响应内容：{responseData}", parameters.AreaCode, parameters.Uscc);
		ApiParameters apiParameters = ExtractApiParameters(responseData);
		Logger.Info("登录URL：" + apiParameters.LoginUrl);
		return Task.FromResult(new ApiResult
		{
			Success = true,
			Message = "获取参数成功",
			Data = apiParameters
		});
	}

	private static ApiParameters ExtractApiParameters(JObject responseData)
	{
		JToken jToken = responseData["data"];
		return new ApiParameters
		{
			NewKey16 = jToken?["newKey16"]?.ToString(),
			Timestamp = jToken?["timestamp"]?.ToString(),
			Token = jToken?["token"]?.ToString(),
			LoginUrl = jToken?["loginUrl"]?.ToString(),
			EtaxCookie = jToken?["etaxCookie"]?.ToString(),
			EtaxSearchCookie = jToken?["etaxSearchCookie"]?.ToString(),
			DpptCookie = jToken?["dpptCookie"]?.ToString()
		};
	}

	private static ApiResult ProcessErrorResponse(JObject responseData)
	{
		string message = BuildErrorMessage(responseData);
		Logger.Warn("获取登录参数错误", new Exception(message));
		return CreateErrorResult(message);
	}

	private static string BuildErrorMessage(JObject responseData)
	{
		if (responseData.TryGetValue("message", out JToken value))
		{
			return $"接口调用错误！错误信息：\n{value}\n请重新一键登录";
		}
		StringBuilder stringBuilder = new StringBuilder("接口返回错误：\n");
		foreach (KeyValuePair<string, JToken> responseDatum in responseData)
		{
			stringBuilder.AppendLine($"{responseDatum.Key}: {responseDatum.Value}");
		}
		return stringBuilder.ToString();
	}

	private static ApiResult CreateErrorResult(string message)
	{
		return new ApiResult
		{
			Success = false,
			Message = message,
			Data = null
		};
	}

	private SessionManager()
	{
		Logger.Info("SessionManager初始化");
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		lock (_disposeLock)
		{
			if (!_disposed)
			{
				_sessions.Clear();
				Logger.Info("SessionManager会话数据已清理");
				_disposed = true;
			}
		}
	}
}
