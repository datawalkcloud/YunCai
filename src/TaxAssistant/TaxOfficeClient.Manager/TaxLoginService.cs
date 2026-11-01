using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaxOfficeClient.Controls;
using TaxOfficeClient.Model.Manager;
using TaxOfficeClient.Tools;
using TaxOfficeClient.Windows;

namespace TaxOfficeClient.Manager;

/// <summary>
/// 税局登录执行器：网页把账号密码经本机端口交进来，这里用助手自己的 WebView2
/// 打开电子税务局、填表、触发短信验证码，并把「等验证码」的状态回给网页。
///
/// 滑块与图片验证码不尝试破解：识别到就停下，让用户在助手窗口里点一下，
/// 后续步骤继续自动完成。这样既不假装全自动，也不会把密码交给第三方打码服务。
/// </summary>
public sealed class TaxLoginService : IDisposable
{
	private sealed class LoginSession
	{
		public string Id { get; }

		public WebViewTabItem Tab { get; }

		public TaxLoginRequest Request { get; }

		public StartupParameters Parameters { get; }

		public string Password { get; }

		public DateTime CreatedAt { get; } = DateTime.UtcNow;

		public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();

		public readonly object StateLock = new object();

		public string State = TaxLoginStates.Starting;

		public string Message = string.Empty;

		public LoginSession(string id, WebViewTabItem tab, TaxLoginRequest request, StartupParameters parameters, string password)
		{
			Id = id;
			Tab = tab;
			Request = request;
			Parameters = parameters;
			Password = password;
		}
	}

	private enum LoginOutcome
	{
		None,
		Sms,
		Success
	}

	private const int StartWaitSeconds = 40;

	private const int SmsWaitSeconds = 30;

	private const int SuccessWaitSeconds = 45;

	private const int SessionKeepMinutes = 30;

	private static readonly HttpClient SharedHttpClient = HttpClientManager.Instance;

	private static readonly Dictionary<string, string> RegionSubdomains = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["北京市"] = "beijing",
		["天津市"] = "tianjin",
		["河北省"] = "hebei",
		["山西省"] = "shanxi",
		["内蒙古自治区"] = "neimenggu",
		["辽宁省"] = "liaoning",
		["吉林省"] = "jilin",
		["黑龙江省"] = "heilongjiang",
		["上海市"] = "shanghai",
		["江苏省"] = "jiangsu",
		["浙江省"] = "zhejiang",
		["安徽省"] = "anhui",
		["福建省"] = "fujian",
		["江西省"] = "jiangxi",
		["山东省"] = "shandong",
		["河南省"] = "henan",
		["湖北省"] = "hubei",
		["湖南省"] = "hunan",
		["广东省"] = "guangdong",
		["广西壮族自治区"] = "guangxi",
		["海南省"] = "hainan",
		["重庆市"] = "chongqing",
		["四川省"] = "sichuan",
		["贵州省"] = "guizhou",
		["云南省"] = "yunnan",
		["西藏自治区"] = "xizang",
		["陕西省"] = "shaanxi",
		["甘肃省"] = "gansu",
		["青海省"] = "qinghai",
		["宁夏回族自治区"] = "ningxia",
		["新疆维吾尔自治区"] = "xinjiang",
		["新疆生产建设兵团"] = "xinjiangbt",
		["大连市"] = "dalian",
		["宁波市"] = "ningbo",
		["厦门市"] = "xiamen",
		["青岛市"] = "qingdao",
		["深圳市"] = "shenzhen"
	};

	private readonly ConcurrentDictionary<string, LoginSession> _sessions = new ConcurrentDictionary<string, LoginSession>(StringComparer.Ordinal);

	private int _virtualAreaCode = -1000;

	private int _disposed;

	private static readonly Lazy<TaxLoginService> _instance = new Lazy<TaxLoginService>(() => new TaxLoginService());

	public static TaxLoginService Instance => _instance.Value;

	private TaxLoginService()
	{
	}

	/// <summary>
	/// 网页点「登录税局 / 获取验证码」时调用：打开助手窗口里的税局登录页并自动填表。
	/// 返回时页面停在「等短信验证码」或「等用户完成滑块」，也可能已经直接登录成功。
	/// </summary>
	public async Task<TaxLoginStatus> StartAsync(TaxLoginRequest request)
	{
		request = request ?? new TaxLoginRequest();
		string taxNo = (request.TaxNo ?? string.Empty).Trim();
		string account = (request.Account ?? string.Empty).Trim();
		if (taxNo.Length == 0)
		{
			return Failure(null, "缺少统一社会信用代码，无法登录税局。");
		}
		if (account.Length == 0)
		{
			return Failure(null, "缺少税局实名账号，请在网页上补充后重试。");
		}
		try
		{
			string password = request.Password ?? string.Empty;
			if (password.Length == 0)
			{
				password = await RedeemTicketAsync(request);
			}
			if (string.IsNullOrEmpty(password))
			{
				return Failure(null, "未取得税局密码：请在网页填写密码，或先保存密码后重试。");
			}
			string destination = ResolveLoginUrl(request);
			int areaCode = Interlocked.Decrement(ref _virtualAreaCode);
			StartupParameters parameters = new StartupParameters
			{
				AreaCode = areaCode,
				Uscc = taxNo,
				Region = (request.Region ?? string.Empty).Trim(),
				PersonalAccount = account,
				DestinationUrl = destination,
				CompanyName = (request.CompanyName ?? string.Empty).Trim(),
				SkipCloudLogin = true
			};
			SessionManager.Instance.AddSession(parameters);
			Logger.Info("[税局登录] 准备打开登录页: " + destination, areaCode, taxNo);
			WebViewTabItem tab = await OpenTabAsync(parameters);
			LoginSession session = new LoginSession(Guid.NewGuid().ToString("N"), tab, request, parameters, password);
			_sessions[session.Id] = session;
			PruneSessions();
			_ = Task.Run(() => DriveAsync(session));
			return await WaitForStateAsync(session, TimeSpan.FromSeconds(StartWaitSeconds), TaxLoginStates.SmsRequired, TaxLoginStates.ManualRequired, TaxLoginStates.Success, TaxLoginStates.Failed);
		}
		catch (Exception ex)
		{
			Logger.Error("[税局登录] 发起失败: " + ex.Message, ex);
			return Failure(null, "发起税局登录失败：" + ex.Message);
		}
	}

	/// <summary>网页收到短信后回填验证码，由助手提交给税局。</summary>
	public async Task<TaxLoginStatus> VerifyAsync(TaxLoginVerifyRequest request)
	{
		LoginSession session = Find(request?.SessionId);
		if (session == null)
		{
			return Failure(null, "登录会话已失效，请回到网页重新点击「登录税局」。");
		}
		if (session.State == TaxLoginStates.Success)
		{
			return Snapshot(session);
		}
		string code = (request?.Code ?? string.Empty).Trim();
		if (!Regex.IsMatch(code, "^\\d{4,8}$"))
		{
			return Failure(session.Id, "请输入税局短信里的数字验证码。");
		}
		try
		{
			SetState(session, TaxLoginStates.Starting, "正在提交短信验证码…");
			JObject filled = await EvalJsonAsync(session, BuildFillSmsScript(code));
			if (!(filled?["filled"]?.Value<bool>() ?? false))
			{
				SetState(session, TaxLoginStates.SmsRequired, "页面上没有找到验证码输入框，请在助手窗口中手动完成验证。");
				return Snapshot(session);
			}
			await Task.Delay(200, session.Cancellation.Token);
			await EvalAsync(session, ClickConfirmScript());
			if (await WaitForSuccessAsync(session, TimeSpan.FromSeconds(SuccessWaitSeconds)))
			{
				SetState(session, TaxLoginStates.Success, "已登录电子税务局。");
				return Snapshot(session);
			}
			JObject stage = await EvalJsonAsync(session, StageScript());
			if (stage?["sms"]?.Value<bool>() ?? false)
			{
				SetState(session, TaxLoginStates.SmsRequired, "验证码未通过，请核对后重试或重新获取。");
			}
			else
			{
				SetState(session, TaxLoginStates.ManualRequired, "提交后未确认登录成功，请在助手窗口中查看并完成剩余步骤。");
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Logger.Error("[税局登录] 提交验证码失败: " + ex.Message, ex, session.Parameters.AreaCode, session.Parameters.Uscc);
			SetState(session, TaxLoginStates.Failed, "提交验证码失败：" + ex.Message);
		}
		return Snapshot(session);
	}

	/// <summary>供网页轮询：等滑块/短信期间页面需要知道助手这边的进展。</summary>
	public TaxLoginStatus GetStatus(string? sessionId)
	{
		LoginSession session = Find(sessionId);
		if (session == null)
		{
			return Failure(null, "登录会话已失效，请回到网页重新点击「登录税局」。");
		}
		return Snapshot(session);
	}

	private async Task DriveAsync(LoginSession session)
	{
		try
		{
			SetState(session, TaxLoginStates.Starting, "正在打开电子税务局…");
			// 先挂响应钩子：各省 tpass 首页会请求自己的登录配置，里面带着本省 OAuth 登录地址。
			await InjectLoginHookAsync(session);
			Navigate(session, session.Parameters.DestinationUrl);
			if (!await WaitForPageAsync(session, TimeSpan.FromSeconds(60)))
			{
				SetState(session, TaxLoginStates.Failed, "电子税务局页面加载超时，请检查网络后重试。");
				return;
			}
			await Task.Delay(1500, session.Cancellation.Token);

			// 税局根地址通常只是门户首页，账号密码框在 OAuth 登录页里，需要先点「登录」进去。
			if (!await WaitForLoginFormAsync(session, TimeSpan.Zero))
			{
				SetState(session, TaxLoginStates.Starting, "正在打开税局登录页…");
				await OpenLoginPageAsync(session);
			}

			await EvalAsync(session, BuildSelectTabScript(session.Request.LoginType));
			if (!await WaitForLoginFormAsync(session, TimeSpan.FromSeconds(10)))
			{
				Logger.Warn("[税局登录] 仍未找到账号密码框，页面诊断: " + (await DiagnosePageAsync(session)), session.Parameters.AreaCode, session.Parameters.Uscc);
				SetState(session, TaxLoginStates.ManualRequired, "未识别到税局登录表单，请在助手窗口中手动登录，完成后网页会自动感知。");
				await WatchUntilSuccessAsync(session, TimeSpan.FromMinutes(5));
				return;
			}

			JObject fill = await EvalJsonAsync(session, BuildFillScript(session.Parameters.Uscc, session.Parameters.PersonalAccount, session.Password));
			bool passwordFilled = fill?["password"]?.Value<bool>() ?? false;
			Logger.Info("[税局登录] 填表结果 税号=" + (fill?["taxNo"]?.Value<bool>() ?? false) + " 账号=" + (fill?["account"]?.Value<bool>() ?? false) + " 密码=" + passwordFilled, session.Parameters.AreaCode, session.Parameters.Uscc);
			if (!passwordFilled)
			{
				Logger.Warn("[税局登录] 账号密码框存在但填表失败，页面诊断: " + (await DiagnosePageAsync(session)), session.Parameters.AreaCode, session.Parameters.Uscc);
				SetState(session, TaxLoginStates.ManualRequired, "未识别到税局登录表单，请在助手窗口中手动登录，完成后网页会自动感知。");
				await WatchUntilSuccessAsync(session, TimeSpan.FromMinutes(5));
				return;
			}

			string manualReason = await DetectManualAsync(session);
			if (manualReason != null)
			{
				SetState(session, TaxLoginStates.ManualRequired, manualReason);
				if (!await WaitForManualClearedAsync(session, TimeSpan.FromMinutes(3)))
				{
					SetState(session, TaxLoginStates.Failed, "验证未在规定时间内完成，请回到网页重新点击「登录税局」。");
					return;
				}
				// 用户可能已经在窗口里提交了，先看一眼有没有直接进短信/成功。
				LoginOutcome early = await WaitForSmsOrSuccessAsync(session, TimeSpan.FromSeconds(2));
				if (early == LoginOutcome.Success)
				{
					SetState(session, TaxLoginStates.Success, "已登录电子税务局。");
					return;
				}
				if (early == LoginOutcome.Sms)
				{
					await EvalAsync(session, ClickSendSmsScript());
					SetState(session, TaxLoginStates.SmsRequired, "税局已发送短信验证码，请填写到网页后点击「登录税局」。");
					await WatchUntilSuccessAsync(session, TimeSpan.FromMinutes(5));
					return;
				}
			}

			SetState(session, TaxLoginStates.Starting, "正在提交税局登录…");
			await EvalAsync(session, ClickLoginScript());
			await Task.Delay(1200, session.Cancellation.Token);

			LoginOutcome outcome = await WaitForSmsOrSuccessAsync(session, TimeSpan.FromSeconds(SmsWaitSeconds));
			if (outcome == LoginOutcome.Success)
			{
				SetState(session, TaxLoginStates.Success, "已登录电子税务局。");
				return;
			}
			if (outcome == LoginOutcome.Sms)
			{
				// 有的页面提交登录后不会自动发码，需要点一次「获取验证码」。
				await EvalAsync(session, ClickSendSmsScript());
				SetState(session, TaxLoginStates.SmsRequired, "税局已发送短信验证码，请填写到网页后点击「登录税局」。");
				await WatchUntilSuccessAsync(session, TimeSpan.FromMinutes(5));
				return;
			}

			await EvalAsync(session, DismissDialogScript());
			SetState(session, TaxLoginStates.ManualRequired, "请在助手窗口中完成税局剩余验证，完成后网页会自动感知。");
			await WatchUntilSuccessAsync(session, TimeSpan.FromMinutes(5));
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			Logger.Error("[税局登录] 执行失败: " + ex.Message, ex, session.Parameters.AreaCode, session.Parameters.Uscc);
			SetState(session, TaxLoginStates.Failed, "税局登录失败：" + ex.Message);
		}
	}

	private static async Task<WebViewTabItem> OpenTabAsync(StartupParameters parameters)
	{
		Application app = Application.Current;
		if (app == null)
		{
			throw new InvalidOperationException("助手界面尚未就绪。");
		}
		Task<WebViewTabItem> task = await app.Dispatcher.InvokeAsync(() =>
		{
			if (!(app.MainWindow is MainWindow window))
			{
				throw new InvalidOperationException("找不到助手主窗口。");
			}
			return window.OpenTaxLoginTabAsync(parameters, "正在打开电子税务局…");
		});
		return await task;
	}

	private static void Navigate(LoginSession session, string url)
	{
		Application app = Application.Current;
		if (app == null)
		{
			return;
		}
		app.Dispatcher.Invoke(() =>
		{
			if (!session.Tab.IsDisposed && session.Tab.WebView != null)
			{
				session.Tab.WebView.Source = new Uri(url);
			}
		});
	}

	private static async Task<string?> EvalAsync(LoginSession session, string script)
	{
		if (session.Tab.IsDisposed)
		{
			return null;
		}
		Application app = Application.Current;
		if (app == null)
		{
			return null;
		}
		try
		{
			Task<string> task = await app.Dispatcher.InvokeAsync(() =>
			{
				var core = session.Tab.WebView?.CoreWebView2;
				return core == null ? Task.FromResult("null") : core.ExecuteScriptAsync(script);
			});
			return await task;
		}
		catch (Exception)
		{
			// 页面正在导航、控件正在释放时脚本会执行失败；让调用方按「还没准备好」继续轮询。
			return null;
		}
	}

	private static async Task<JObject?> EvalJsonAsync(LoginSession session, string script)
	{
		string? raw = await EvalAsync(session, script);
		if (string.IsNullOrWhiteSpace(raw) || raw == "null" || raw == "false")
		{
			return null;
		}
		try
		{
			return JToken.Parse(raw) as JObject;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static async Task<string?> RedeemTicketAsync(TaxLoginRequest request)
	{
		string apiBase = (request.ApiBase ?? string.Empty).Trim().TrimEnd('/');
		if (string.IsNullOrWhiteSpace(request.Ticket) || !Uri.TryCreate(apiBase, UriKind.Absolute, out Uri apiUri))
		{
			return null;
		}
		if (apiUri.Scheme != Uri.UriSchemeHttps && !apiUri.IsLoopback)
		{
			throw new InvalidOperationException("云端地址必须是 HTTPS。");
		}
		using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
		using StringContent content = new StringContent(
			JsonConvert.SerializeObject(new { ticket = request.Ticket }),
			Encoding.UTF8,
			"application/json");
		using HttpResponseMessage response = await SharedHttpClient.PostAsync(apiBase + "/TaxLoginTicket/redeem", content, timeout.Token);
		string body = await response.Content.ReadAsStringAsync(timeout.Token);
		if (!response.IsSuccessStatusCode)
		{
			string message = null;
			try
			{
				message = (JsonConvert.DeserializeObject<JObject>(body))?["message"]?.ToString();
			}
			catch (JsonException)
			{
			}
			throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "换取税局密码失败，请回到网页重新点击登录。" : message);
		}
		try
		{
			return (JsonConvert.DeserializeObject<JObject>(body))?["password"]?.ToString();
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static string ResolveLoginUrl(TaxLoginRequest request)
	{
		if (!string.IsNullOrWhiteSpace(request.DestinationUrl))
		{
			if (!Uri.TryCreate(request.DestinationUrl, UriKind.Absolute, out Uri custom) || !IsAllowedTaxHost(custom))
			{
				throw new InvalidOperationException("登录地址不是电子税务局域名，已拒绝打开。");
			}
			return custom.ToString();
		}
		string region = (request.Region ?? string.Empty).Trim();
		if (RegionSubdomains.TryGetValue(region, out string subdomain))
		{
			return "https://tpass." + subdomain + ".chinatax.gov.cn:8443/";
		}
		return "https://tpass.chinatax.gov.cn:8443/";
	}

	private static bool IsAllowedTaxHost(Uri uri)
	{
		string host = uri.Host;
		return host.Equals("chinatax.gov.cn", StringComparison.OrdinalIgnoreCase)
			|| host.EndsWith(".chinatax.gov.cn", StringComparison.OrdinalIgnoreCase);
	}

	private LoginSession Find(string? sessionId)
	{
		if (string.IsNullOrWhiteSpace(sessionId))
		{
			return null;
		}
		_sessions.TryGetValue(sessionId, out LoginSession session);
		return session;
	}

	private void PruneSessions()
	{
		DateTime cutoff = DateTime.UtcNow.AddMinutes(-SessionKeepMinutes);
		foreach (KeyValuePair<string, LoginSession> pair in _sessions)
		{
			if (pair.Value.CreatedAt < cutoff)
			{
				_sessions.TryRemove(pair.Key, out _);
			}
		}
	}

	private static void SetState(LoginSession session, string state, string message)
	{
		lock (session.StateLock)
		{
			session.State = state;
			session.Message = message;
		}
		Logger.Info("[税局登录] 状态=" + state + "，" + message, session.Parameters.AreaCode, session.Parameters.Uscc);
	}

	private static TaxLoginStatus Snapshot(LoginSession session)
	{
		string state;
		string message;
		lock (session.StateLock)
		{
			state = session.State;
			message = session.Message;
		}
		return new TaxLoginStatus
		{
			State = state,
			Message = message,
			SessionId = session.Id
		};
	}

	private static TaxLoginStatus Failure(string? sessionId, string message)
	{
		return new TaxLoginStatus
		{
			State = TaxLoginStates.Failed,
			Message = message,
			SessionId = sessionId
		};
	}

	private static async Task<TaxLoginStatus> WaitForStateAsync(LoginSession session, TimeSpan timeout, params string[] states)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (states.Contains(session.State, StringComparer.Ordinal))
			{
				return Snapshot(session);
			}
			await Task.Delay(250);
		}
		return Snapshot(session);
	}

	private static async Task<bool> WaitForPageAsync(LoginSession session, TimeSpan timeout)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (session.Tab.IsDisposed)
			{
				return false;
			}
			string? ready = await EvalAsync(session, PageReadyScript());
			if (ready == "true")
			{
				return true;
			}
			await Task.Delay(700, session.Cancellation.Token);
		}
		return false;
	}

	/// <summary>
	/// 在页面脚本之前挂响应钩子：税局门户会请求本省登录配置，响应里的 OAuth 登录地址
	/// 带着各省不同的 client_id，比自己拼 tpass 地址可靠。地址同时写进 window.name，
	/// 因为门户首页会跨文档跳转，普通变量带不过去。
	/// </summary>
	private static async Task InjectLoginHookAsync(LoginSession session)
	{
		Application app = Application.Current;
		if (app == null)
		{
			return;
		}
		try
		{
			Task task = await app.Dispatcher.InvokeAsync(() =>
			{
				CoreWebView2? core = session.Tab.WebView?.CoreWebView2;
				return core == null ? Task.CompletedTask : core.AddScriptToExecuteOnDocumentCreatedAsync(LoginHookScript());
			});
			await task;
		}
		catch (Exception ex)
		{
			Logger.Warn("[税局登录] 挂登录地址钩子失败: " + ex.Message, session.Parameters.AreaCode, session.Parameters.Uscc);
		}
	}

	private static async Task<bool> WaitForLoginFormAsync(LoginSession session, TimeSpan timeout)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		do
		{
			if (session.Tab.IsDisposed)
			{
				return false;
			}
			string? has = await EvalAsync(session, HasPasswordScript());
			if (has == "true")
			{
				return true;
			}
			if (DateTime.UtcNow >= deadline)
			{
				break;
			}
			await Task.Delay(500, session.Cancellation.Token);
		}
		while (true);
		return false;
	}

	private static async Task OpenLoginPageAsync(LoginSession session)
	{
		bool clicked = string.Equals(await EvalAsync(session, ClickLoginEntryScript()), "true", StringComparison.Ordinal);
		string? captured = await ReadCapturedLoginUrlAsync(session);
		Logger.Info("[税局登录] 首页登录入口 点击=" + clicked + " 页面下发登录地址=" + (captured ?? string.Empty), session.Parameters.AreaCode, session.Parameters.Uscc);

		await Task.Delay(1200, session.Cancellation.Token);
		if (await WaitForLoginFormAsync(session, TimeSpan.FromSeconds(12)))
		{
			return;
		}
		if (!string.IsNullOrEmpty(captured)
			&& Uri.TryCreate(captured, UriKind.Absolute, out Uri loginUri)
			&& IsAllowedTaxHost(loginUri))
		{
			Logger.Info("[税局登录] 打开页面自己下发的登录地址: " + captured, session.Parameters.AreaCode, session.Parameters.Uscc);
			Navigate(session, captured);
			if (await WaitForLoginFormAsync(session, TimeSpan.FromSeconds(20)))
			{
				return;
			}
		}
		// 有的省点「登录」只是展开登录弹层，再试点一次。
		await EvalAsync(session, ClickLoginEntryScript());
		await WaitForLoginFormAsync(session, TimeSpan.FromSeconds(10));
	}

	private static async Task<string?> ReadCapturedLoginUrlAsync(LoginSession session)
	{
		JObject? result = await EvalJsonAsync(session, CapturedLoginUrlScript());
		string? url = result?["url"]?.ToString();
		return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
	}

	private static async Task<string> DiagnosePageAsync(LoginSession session)
	{
		JObject? result = await EvalJsonAsync(session, DiagnosePageScript());
		return result == null ? "（脚本不可用）" : result.ToString(Formatting.None);
	}

	private static async Task<LoginOutcome> WaitForSmsOrSuccessAsync(LoginSession session, TimeSpan timeout)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (session.Tab.IsDisposed)
			{
				return LoginOutcome.None;
			}
			JObject stage = await EvalJsonAsync(session, StageScript());
			if (stage?["success"]?.Value<bool>() ?? false)
			{
				return LoginOutcome.Success;
			}
			if (stage?["sms"]?.Value<bool>() ?? false)
			{
				return LoginOutcome.Sms;
			}
			await Task.Delay(700, session.Cancellation.Token);
		}
		return LoginOutcome.None;
	}

	private static async Task<bool> WaitForSuccessAsync(LoginSession session, TimeSpan timeout)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (session.Tab.IsDisposed)
			{
				return false;
			}
			JObject stage = await EvalJsonAsync(session, StageScript());
			if (stage?["success"]?.Value<bool>() ?? false)
			{
				return true;
			}
			await Task.Delay(900, session.Cancellation.Token);
		}
		return false;
	}

	private static async Task WatchUntilSuccessAsync(LoginSession session, TimeSpan timeout)
	{
		if (await WaitForSuccessAsync(session, timeout))
		{
			SetState(session, TaxLoginStates.Success, "已登录电子税务局。");
			return;
		}
		// 超时不覆盖页面状态：用户可能还在窗口里操作，网页侧仍显示当前步骤。
	}

	private static async Task<string?> DetectManualAsync(LoginSession session)
	{
		JObject slider = await EvalJsonAsync(session, SliderScript());
		if (slider?["slider"]?.Value<bool>() ?? false)
		{
			return "检测到滑块验证，请在助手窗口中拖动滑块；完成后助手会自动继续。";
		}
		JObject image = await EvalJsonAsync(session, ImageCaptchaScript());
		if (image?["image"]?.Value<bool>() ?? false)
		{
			return "检测到图片验证码，请在助手窗口中填写；填好后助手会自动继续。";
		}
		return null;
	}

	private static async Task<bool> WaitForManualClearedAsync(LoginSession session, TimeSpan timeout)
	{
		DateTime deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (session.Tab.IsDisposed)
			{
				return false;
			}
			JObject pending = await EvalJsonAsync(session, ManualPendingScript());
			if (!(pending?["pending"]?.Value<bool>() ?? false))
			{
				return true;
			}
			await Task.Delay(800, session.Cancellation.Token);
		}
		return false;
	}

	private static string Js(string body)
	{
		return "(() => {\n" + JsHelpers + "\n" + body + "\n})()";
	}

	private static string PageReadyScript()
	{
		return Js("""
if (document.readyState === 'loading') return false;
if (document.querySelector('input[type=password]')) return true;
const body = document.body ? document.body.innerText : '';
return /(电子税务局|统一身份认证|用户登录|企业业务|自然人业务|代理机构)/.test(body);
""");
	}

	private static string LoginHookScript()
	{
		return """
(function () {
  function remember(url) {
    try {
      if (!url || url.indexOf('oauth2/login') < 0) return;
      window.__kqLoginUrl = url;
      window.name = 'kq-login|' + url;
    } catch (e) {}
  }
  function fromText(text) {
    try {
      if (typeof text !== 'string' || text.indexOf('oauth2/login') < 0) return;
      var m = text.match(/https?:\/\/[^"'\s\\<>]+oauth2\/login\?[^"'\s\\<>]+/);
      if (m && m[0]) remember(m[0].replace(/\\u0026/g, '&').replace(/&amp;/g, '&'));
    } catch (e) {}
  }
  try {
    var origFetch = window.fetch;
    if (origFetch) {
      window.fetch = function () {
        var p = origFetch.apply(this, arguments);
        try {
          var reqUrl = '';
          try { reqUrl = String((arguments[0] && arguments[0].url) || arguments[0] || ''); } catch (e) {}
          if (reqUrl.indexOf('login') >= 0 || reqUrl.indexOf('config') >= 0) {
            p.then(function (r) {
              try { r.clone().text().then(fromText, function () {}); } catch (e) {}
            }, function () {});
          }
        } catch (e) {}
        return p;
      };
    }
  } catch (e) {}
  try {
    var origSend = XMLHttpRequest.prototype.send;
    XMLHttpRequest.prototype.send = function () {
      try {
        var xhr = this;
        xhr.addEventListener('load', function () {
          try {
            if ((xhr.responseURL || '').indexOf('login') >= 0) fromText(xhr.responseText);
          } catch (e) {}
        });
      } catch (e) {}
      return origSend.apply(this, arguments);
    };
  } catch (e) {}
  try {
    if (window.name && window.name.indexOf('kq-login|') === 0) window.__kqLoginUrl = window.name.substring(9);
  } catch (e) {}
  try { fromText(document.documentElement ? document.documentElement.innerHTML : ''); } catch (e) {}
})();
""";
	}

	private static string HasPasswordScript()
	{
		return Js("return !!document.querySelector('input[type=password]');");
	}

	private static string ClickLoginEntryScript()
	{
		return Js("""
const exact = ['登录', '登 录', '立即登录', '用户登录', '企业登录'];
const nodes = Array.from(document.querySelectorAll('a,button,span,div')).filter(visible);
const hits = nodes.filter(el => {
  const t = textOf(el);
  return exact.indexOf(t) >= 0 && !/(扫码|短信|忘记|注册|新办)/.test(t);
});
const anchor = hits.find(el => el.tagName === 'A' && /(login|tpass)/i.test(el.href || ''));
const hit = anchor || hits[0];
if (hit) { hit.click(); return true; }
const link = Array.from(document.querySelectorAll('a[href]')).filter(visible).find(a => /oauth2\/login/i.test(a.href || ''));
if (link) { link.click(); return true; }
return false;
""");
	}

	private static string CapturedLoginUrlScript()
	{
		return Js("""
let url = window.__kqLoginUrl || '';
try {
  if (!url && window.name && window.name.indexOf('kq-login|') === 0) url = window.name.substring(9);
} catch (e) {}
if (!url) {
  const link = Array.from(document.querySelectorAll('a[href]')).map(a => a.href).find(h => /oauth2\/login\?/i.test(h));
  if (link) url = link;
}
return { url: url || '' };
""");
	}

	private static string DiagnosePageScript()
	{
		return Js("""
const list = inputs().slice(0, 12).map(el => ({
  type: el.type || '',
  name: el.name || '',
  id: el.id || '',
  placeholder: (el.placeholder || '').slice(0, 40),
  label: describe(el).slice(0, 60)
}));
return {
  url: location.href,
  title: document.title,
  count: inputs().length,
  inputs: list,
  text: (document.body ? document.body.innerText : '').replace(/\s+/g, ' ').slice(0, 200)
};
""");
	}

	private static string BuildSelectTabScript(string? loginType)
	{
		string wanted = JsonConvert.SerializeObject(loginType ?? string.Empty);
		return Js("const wanted = " + wanted + ";\n" + """
			const isAgency = wanted.indexOf('代理') >= 0;
			const re = isAgency ? /(代理.*登录|代理机构|代理业务)/ : /企业业务/;
			const nodes = Array.from(document.querySelectorAll('[role=tab],div,span,a,li,button')).filter(visible);
			const hit = nodes.find(el => re.test(textOf(el)) && textOf(el).length <= 12);
			if (hit) { hit.click(); return true; }
			return false;
			""");
	}

	private static string BuildFillScript(string taxNo, string account, string password)
	{
		string taxNoJson = JsonConvert.SerializeObject(taxNo);
		string accountJson = JsonConvert.SerializeObject(account);
		string passwordJson = JsonConvert.SerializeObject(password);
		return Js("const taxNo = " + taxNoJson + ";\nconst account = " + accountJson + ";\nconst password = " + passwordJson + ";\n" + """
			const excluded = ['hidden', 'checkbox', 'radio', 'button', 'submit', 'file', 'image', 'range', 'color', 'date', 'time', 'search'];
			const fields = inputs().filter(el => excluded.indexOf(el.type) < 0);
			const passwordEl = fields.find(el => el.type === 'password');
			const textEls = fields.filter(el => el.type !== 'password' && !/(图形验证码|图片验证码)/.test(describe(el)));
			const taxNoRe = /(统一社会信用代码|纳税人识别号|纳税人识别|社会信用代码|税号|taxpayer|taxno|nsrsbh|shxydm|creditcode)/i;
			const accountRe = /(用户名|手机号码|手机号|身份证|账号|账户|用户|account|username|loginname|userid|mobile|phone)/i;
			let taxNoEl = textEls.find(el => taxNoRe.test(describe(el)));
			let accountEl = textEls.find(el => el !== taxNoEl && accountRe.test(describe(el)));
			if (!taxNoEl && textEls.length > 0) taxNoEl = textEls[0];
			if (!accountEl) accountEl = textEls.find(el => el !== taxNoEl) || null;
			if (taxNoEl) setValue(taxNoEl, taxNo);
			if (accountEl) setValue(accountEl, account);
			if (passwordEl) setValue(passwordEl, password);
			return { ok: !!passwordEl && !!taxNoEl, taxNo: !!taxNoEl, account: !!accountEl, password: !!passwordEl };
			""");
	}

	private static string SliderScript()
	{
		return Js("""
const hint = /(拖动滑块|按住滑块|向右拖动|滑动验证|请完成.*验证|拖动下方滑块|按住左边滑块|拖动到最右)/;
const hit = Array.from(document.querySelectorAll('div,span,p,button')).filter(visible).find(el => hint.test(textOf(el)) && textOf(el).length < 40);
return { slider: !!hit };
""");
	}

	private static string ImageCaptchaScript()
	{
		return Js("""
const el = inputs().find(i => i.type !== 'password' && /(图形验证码|图片验证码)/.test(describe(i)));
return { image: !!el, empty: !!el && !el.value };
""");
	}

	private static string ManualPendingScript()
	{
		return Js("""
const hint = /(拖动滑块|按住滑块|向右拖动|滑动验证|请完成.*验证|拖动下方滑块|按住左边滑块|拖动到最右)/;
const slider = Array.from(document.querySelectorAll('div,span,p,button')).filter(visible).some(el => hint.test(textOf(el)) && textOf(el).length < 40);
const image = inputs().find(i => i.type !== 'password' && /(图形验证码|图片验证码)/.test(describe(i)));
return { pending: slider || (!!image && !image.value) };
""");
	}

	private static string ClickLoginScript()
	{
		return Js("""
const exact = ['登录', '登 录', '立即登录'];
const nodes = Array.from(document.querySelectorAll('button, a, div, span')).filter(visible);
const hits = nodes.filter(el => {
  const t = textOf(el);
  return exact.indexOf(t) >= 0 && !/扫码|短信|忘记|注册/.test(t);
});
const hit = hits.find(el => el.tagName === 'BUTTON') || hits[hits.length - 1];
if (!hit) return false;
hit.click();
return true;
""");
	}

	private static string ClickSendSmsScript()
	{
		return Js("""
const re = /(获取验证码|获取短信验证码|发送验证码|免费获取|点击获取|获取动态码)/;
const nodes = Array.from(document.querySelectorAll('button, a, span, div')).filter(visible);
const hit = nodes.reverse().find(el => {
  const t = textOf(el);
  return re.test(t) && t.length <= 12 && !/秒|重新获取/.test(t);
});
if (!hit) return false;
hit.click();
return true;
""");
	}

	private static string BuildFillSmsScript(string code)
	{
		string codeJson = JsonConvert.SerializeObject(code);
		return Js("const code = " + codeJson + ";\n" + """
			const el = inputs().find(i => i.type !== 'password' && /(短信验证码|手机验证码|动态码|验证码)/.test(describe(i)) && !/(图形|图片)/.test(describe(i)));
			if (!el) return { filled: false };
			setValue(el, code);
			return { filled: true };
			""");
	}

	private static string ClickConfirmScript()
	{
		return Js("""
const exact = ['确定', '确认', '提交', '验证', '登录', '下一步', '完成'];
const nodes = Array.from(document.querySelectorAll('button, a, span, div')).filter(visible);
const hit = nodes.reverse().find(el => {
  const t = textOf(el);
  return exact.indexOf(t) >= 0 && t.length <= 4;
});
if (!hit) return false;
hit.click();
return true;
""");
	}

	private static string DismissDialogScript()
	{
		return Js("""
const exact = ['我知道了', '知道了', '同意', '关闭', '继续登录'];
const nodes = Array.from(document.querySelectorAll('button, a, span, div')).filter(visible);
const hit = nodes.reverse().find(el => exact.indexOf(textOf(el)) >= 0 && textOf(el).length <= 6);
if (!hit) return false;
hit.click();
return true;
""");
	}

	private static string StageScript()
	{
		return Js("""
const body = document.body ? document.body.innerText : '';
const hasPassword = inputs().some(i => i.type === 'password');
const successByText = !hasPassword && /(退出登录|退出|我的信息|我要办税|办税中心|用户中心)/.test(body);
const successByUrl = !hasPassword && /(userCenter|dashboard|\/home)/i.test(location.pathname);
const smsInput = inputs().find(i => i.type !== 'password' && /(短信验证码|手机验证码|动态码|验证码)/.test(describe(i)) && !/(图形|图片)/.test(describe(i)));
return { success: successByText || successByUrl, sms: !!smsInput };
""");
	}

	private const string JsHelpers = """
const visible = (el) => {
  if (!el) return false;
  const r = el.getBoundingClientRect();
  if (r.width <= 0 || r.height <= 0) return false;
  const s = getComputedStyle(el);
  return s.display !== 'none' && s.visibility !== 'hidden' && s.opacity !== '0';
};
const textOf = (el) => ((el.innerText || el.textContent || '').replace(/\s+/g, ''));
const describe = (el) => {
  let t = '';
  if (el.id) {
    const label = document.querySelector('label[for="' + CSS.escape(el.id) + '"]');
    if (label) t += ' ' + textOf(label);
  }
  const wrap = el.closest('.ant-form-item, .el-form-item, .form-item, .form-group, .input-group, .ivu-form-item, tr');
  if (wrap && wrap !== el) t += ' ' + textOf(wrap).slice(0, 80);
  t += ' ' + (el.getAttribute('placeholder') || '') + ' ' + (el.getAttribute('aria-label') || '') + ' ' + (el.name || '') + ' ' + (el.id || '');
  return t;
};
const inputs = () => Array.from(document.querySelectorAll('input')).filter(visible).filter(el => !el.disabled && !el.readOnly);
const setValue = (el, value) => {
  const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  const setter = Object.getOwnPropertyDescriptor(proto, 'value').set;
  setter.call(el, value);
  el.dispatchEvent(new Event('input', { bubbles: true }));
  el.dispatchEvent(new Event('change', { bubbles: true }));
  el.dispatchEvent(new Event('blur', { bubbles: true }));
};
""";

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
		{
			return;
		}
		foreach (KeyValuePair<string, LoginSession> pair in _sessions)
		{
			try
			{
				pair.Value.Cancellation.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
		}
		_sessions.Clear();
	}
}
