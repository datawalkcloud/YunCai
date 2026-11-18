using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using Newtonsoft.Json;
using TaxOfficeClient.Manager;
using TaxOfficeClient.Model.Manager;
using TaxOfficeClient.Tools;
using TaxOfficeClient.Windows;

namespace TaxOfficeClient.Controls;

public sealed class WebViewTabItem : TabItem, IDisposable
{
	internal const CoreWebView2BrowsingDataKinds LoginDataToClear = CoreWebView2BrowsingDataKinds.LocalStorage | CoreWebView2BrowsingDataKinds.Cookies;

	private const int MAX_UNRESPONSIVE_EVENTS = 5;

	private const int UNRESPONSIVE_EVENT_RESET_MINUTES = 1;

	private const int NAVIGATION_TIMEOUT_MS = 8000;

	private const int TEMP_WEBVIEW_TIMEOUT_MS = 3000;

	private const int PAGE_LOAD_DELAY_MS = 1000;

	private readonly SessionManager _sessionManager = SessionManager.Instance;

	private bool _isClosing;

	private bool _errorDialogShown;

	private WebView2 _loginWebView;

	private bool _allowInitialBlankNavigation;

	private int _unresponsiveEventCount;

	private DateTime _lastUnresponsiveEventTime = DateTime.MinValue;

	public WebView2 WebView { get; private set; }

	public int AreaCode { get; }

	private string Uscc { get; }

	public bool IsDisposed { get; private set; }

	private bool IsActive
	{
		get
		{
			if (!IsDisposed && !_isClosing && !_errorDialogShown)
			{
				MainWindow obj = Application.Current.MainWindow as MainWindow;
				if (obj == null)
				{
					return true;
				}
				return !obj.IsClosing;
			}
			return false;
		}
	}

	public WebViewTabItem(int areaCode, string header = "加载中...")
	{
		AreaCode = areaCode;
		Header = header;
		StartupParameters startupParameters = _sessionManager.GetStartupParameters(areaCode);
		Uscc = startupParameters.Uscc;
		WebView = new WebView2();
		Content = WebView;
		_sessionManager.AddTabToSession(AreaCode, this);
		Logger.Info($"WebViewTabItem已创建，AreaCode: {areaCode}", AreaCode, Uscc);
	}

	public async Task<bool> InitializeAsync(CoreWebView2Environment sharedEnvironment = null)
	{
		string initializationStage = "创建或复用WebView2环境";
		try
		{
			Logger.Info($"开始初始化WebView2，使用共享环境: {sharedEnvironment != null}", AreaCode, Uscc);
			CoreWebView2Environment coreWebView2Environment = sharedEnvironment;
			if (coreWebView2Environment == null)
			{
				coreWebView2Environment = await CreateNewEnvironmentAsync();
			}
			CoreWebView2Environment coreWebView2Environment2 = coreWebView2Environment;
			if (!IsActive)
			{
				return false;
			}
			Logger.Info("WebView2环境已就绪，运行时版本: " + coreWebView2Environment2.BrowserVersionString + "，用户数据目录: " + coreWebView2Environment2.UserDataFolder, AreaCode, Uscc);
			initializationStage = "EnsureCoreWebView2Async";
			Logger.Info("开始创建WebView2控件", AreaCode, Uscc);
			await WebView.EnsureCoreWebView2Async(coreWebView2Environment2);
			if (!IsActive)
			{
				return false;
			}
			Logger.Info("WebView2控件创建成功", AreaCode, Uscc);
			initializationStage = "ConfigureWebViewSettings";
			ConfigureWebViewSettings();
			initializationStage = "AddEventHandlers";
			await AddEventHandlers();
			if (!IsActive)
			{
				return false;
			}
			Logger.Info($"WebViewTabItem已初始化，AreaCode: {AreaCode}", AreaCode, Uscc);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("配置WebView2控件失败，阶段: " + initializationStage, ex, AreaCode, Uscc);
			ShowErrorAndClose("WebView2初始化失败: " + ex.Message);
			return false;
		}
	}

	public async Task LoadUrlAsync(string destinationUrl, StartupParameters startupParameters)
	{
		try
		{
			if (!CanContinueLogin(startupParameters))
			{
				return;
			}
			// 云财页面用票据从后端换回已登录的税局登录态；没有票据时才直接打开税局页面由用户登录。
			if (!string.IsNullOrWhiteSpace(startupParameters.Ticket) && !string.IsNullOrWhiteSpace(startupParameters.ApiBase))
			{
				await OpenWithBrowserHandoffAsync(destinationUrl, startupParameters);
				return;
			}
			Navigate(destinationUrl);
			return;
		}
		catch (Exception ex)
		{
			Logger.Error("加载URL失败", ex, AreaCode, Uscc);
			if (CanContinueLogin(startupParameters))
			{
				ShowErrorAndClose("加载页面失败: " + ex.Message);
			}
		}
		finally
		{
			if (IsActive && !_sessionManager.IsCurrentStartupParameters(startupParameters))
			{
				Logger.Info("本次登录已被同地区的新请求替代，关闭未完成的标签页", AreaCode, Uscc);
				Close(clearBrowsingData: false);
			}
		}
	}

	private bool CanContinueLogin(StartupParameters parameters)
	{
		if (IsActive)
		{
			return _sessionManager.IsCurrentStartupParameters(parameters);
		}
		return false;
	}

	/// <summary>
	/// 用网页换来的登录态直接打开税局：先把 Cookie 与各站点 localStorage 注入当前浏览器，
	/// 再导航到企业电子税务局。登录态来自服务器上已完成的那次登录，窗口里不再走短信/扫码。
	/// </summary>
	private async Task OpenWithBrowserHandoffAsync(string destinationUrl, StartupParameters startupParameters)
	{
		BrowserHandoff handoff = await _sessionManager.FetchBrowserHandoffAsync(startupParameters);
		if (!CanContinueLogin(startupParameters))
		{
			return;
		}

		await _sessionManager.ApplyLoginStateAsync(startupParameters, async () =>
		{
			if (!CanContinueLogin(startupParameters))
			{
				return;
			}
			await ApplyBrowserHandoffAsync(handoff);
			string target = string.IsNullOrWhiteSpace(handoff.Url) ? destinationUrl : handoff.Url;
			Logger.Info($"已注入税局登录态（Cookie {handoff.Cookies.Count} 项，站点 {handoff.Origins.Count} 个），打开 {target}", AreaCode, Uscc);
			Navigate(target);
		});
	}

	/// <summary>把登录态写入 WebView2：Cookie 交给浏览器自身，localStorage 由文档创建脚本按站点写入。</summary>
	private async Task ApplyBrowserHandoffAsync(BrowserHandoff handoff)
	{
		CoreWebView2 coreWebView2 = WebView?.CoreWebView2 ?? throw new InvalidOperationException("WebView尚未初始化，无法注入登录态。");
		CoreWebView2CookieManager cookieManager = coreWebView2.CookieManager;
		foreach (BrowserCookie item in handoff.Cookies)
		{
			CoreWebView2Cookie cookie = cookieManager.CreateCookie(
				item.Name,
				item.Value,
				item.Domain,
				string.IsNullOrWhiteSpace(item.Path) ? "/" : item.Path);
			cookie.IsHttpOnly = item.HttpOnly;
			cookie.IsSecure = item.Secure;
			if (item.Expires > 0)
			{
				// 服务器给的是 UNIX 秒；WebView2 要 DateTime，MinValue 表示会话 Cookie。
				cookie.Expires = DateTimeOffset.FromUnixTimeSeconds((long)item.Expires).LocalDateTime;
			}
			cookie.SameSite = ParseSameSite(item.SameSite);
			cookieManager.AddOrUpdateCookie(cookie);
		}

		if (handoff.Origins.Count > 0)
		{
			await coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildHandoffStorageScript(handoff.Origins));
		}
	}

	private static CoreWebView2CookieSameSiteKind ParseSameSite(string sameSite)
	{
		if (string.Equals(sameSite, "None", StringComparison.OrdinalIgnoreCase))
		{
			return CoreWebView2CookieSameSiteKind.None;
		}
		if (string.Equals(sameSite, "Strict", StringComparison.OrdinalIgnoreCase))
		{
			return CoreWebView2CookieSameSiteKind.Strict;
		}
		return CoreWebView2CookieSameSiteKind.Lax;
	}

	/// <summary>按站点原样恢复 localStorage；只在 origin 完全一致时写入，避免 token 落到别的域。</summary>
	private static string BuildHandoffStorageScript(IReadOnlyList<BrowserOrigin> origins)
	{
		StringBuilder builder = new StringBuilder();
		builder.AppendLine("(function () {");
		foreach (BrowserOrigin origin in origins)
		{
			if (string.IsNullOrWhiteSpace(origin.Origin) || origin.Items == null || origin.Items.Count == 0)
			{
				continue;
			}
			builder.AppendLine("if (location.origin === " + JsonConvert.SerializeObject(origin.Origin) + ") {");
			foreach (KeyValuePair<string, string> item in origin.Items)
			{
				builder.AppendLine("try { localStorage.setItem(" + JsonConvert.SerializeObject(item.Key) + ", " + JsonConvert.SerializeObject(item.Value) + "); } catch (e) {}");
			}
			builder.AppendLine("}");
		}
		builder.AppendLine("})();");
		return builder.ToString();
	}

	public void RefreshWebView()
	{
		try
		{
			if (WebView?.CoreWebView2 != null && !IsDisposed)
			{
				WebView.CoreWebView2.Reload();
				Logger.Info($"WebView已刷新，AreaCode: {AreaCode}", AreaCode, Uscc);
			}
			else
			{
				Logger.Warn($"WebView未初始化或已释放，无法刷新，AreaCode: {AreaCode}", AreaCode, Uscc);
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("刷新WebView时发生异常: " + ex.Message, AreaCode, Uscc);
			MessageBoxHelper.Error("刷新页面时发生异常: " + ex.Message);
		}
	}

	internal void StopNavigationForCleanup()
	{
		if (_loginWebView != null)
		{
			WebView2 loginWebView = _loginWebView;
			_loginWebView = null;
			CleanupTempWebView(loginWebView.Parent as Panel, loginWebView, null);
		}
		try
		{
			WebView?.CoreWebView2?.Stop();
		}
		catch (Exception arg)
		{
			Logger.Warn($"清理前停止页面导航失败: {arg}", AreaCode, Uscc);
		}
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (GetTemplateChild("CloseButton") is Button button)
		{
			button.Click += OnCloseBtnClick;
		}
	}

	private void OnCloseBtnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private async void Close(bool clearBrowsingData = true)
	{
		try
		{
			if (IsDisposed || _isClosing)
			{
				Logger.Info($"标签页已释放，跳过关闭操作，AreaCode: {AreaCode}", AreaCode, Uscc);
				return;
			}
			_isClosing = true;
			StopNavigationForCleanup();
			if (!(Parent is TabControl tabControl))
			{
				Logger.Warn($"标签页没有父TabControl，AreaCode: {AreaCode}", AreaCode, Uscc);
				_sessionManager.RemoveTabFromSession(AreaCode, this);
				DisposeResources();
				return;
			}
			try
			{
				_sessionManager.RemoveTabFromSession(AreaCode, this);
			}
			catch (Exception ex)
			{
				Logger.Warn("从SessionManager移除标签页时出错: " + ex.Message, AreaCode, Uscc);
			}
			try
			{
				tabControl.Items.Remove(this);
			}
			catch (Exception ex2)
			{
				Logger.Warn("从TabControl移除标签页时出错: " + ex2.Message, AreaCode, Uscc);
			}
			if (clearBrowsingData && tabControl.Items.Count == 0)
			{
				try
				{
					await ClearBrowsingDataAsync();
				}
				catch (Exception ex3)
				{
					Logger.Warn("清理浏览数据时出错: " + ex3.Message, AreaCode, Uscc);
				}
			}
			DisposeResources();
			Logger.Info($"WebViewTabItem已关闭，AreaCode: {AreaCode}", AreaCode, Uscc);
		}
		catch (Exception ex4)
		{
			Logger.Warn("关闭标签页时出错: " + ex4.Message, AreaCode, Uscc);
		}
	}

	private async Task<CoreWebView2Environment> CreateNewEnvironmentAsync()
	{
		List<string> values = new List<string> { "--ignore-certificate-errors", "--no-sandbox" };
		CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions(string.Join(" ", values));
		// 会话数据放用户目录，免安装目录可随时删除、换位置而不丢登录态。
		string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YunCai", "TaxAssistant", "WebView2");
		CoreWebView2Environment result = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
		Logger.Info("创建新的CoreWebView2Environment实例", AreaCode, Uscc);
		return result;
	}

	private void ConfigureWebViewSettings()
	{
		if (WebView?.CoreWebView2?.Settings != null)
		{
			CoreWebView2Settings settings = WebView.CoreWebView2.Settings;
			settings.IsGeneralAutofillEnabled = false;
			settings.IsPasswordAutosaveEnabled = false;
			settings.IsPinchZoomEnabled = false;
			settings.IsSwipeNavigationEnabled = false;
			settings.AreDefaultContextMenusEnabled = false;
			Logger.Info("WebView2设置已配置", AreaCode, Uscc);
		}
	}

	private async Task AddEventHandlers()
	{
		if (WebView?.CoreWebView2 == null)
		{
			Logger.Warn("尝试添加事件处理器时WebView.CoreWebView2为null", AreaCode, Uscc);
			return;
		}
		WebView.NavigationStarting += OnNavigationStarting;
		WebView.NavigationCompleted += OnNavigationCompleted;
		UnRegisterCoreWebView2Events();
		await WebView.EnsureCoreWebView2Async();
		WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
		WebView.CoreWebView2.WindowCloseRequested += CoreWebView2_WindowCloseRequested;
		WebView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
		WebView.CoreWebView2.ProcessFailed += CoreWebView2_ProcessFailed;
		WebView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
		Logger.Info("已添加WebView2事件处理", AreaCode, Uscc);
	}

	private void UnRegisterCoreWebView2Events()
	{
		if (WebView?.CoreWebView2 != null)
		{
			TryUnregisterCoreWebView2Event(() =>
			{
				WebView.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
			}, "NewWindowRequested");
			TryUnregisterCoreWebView2Event(() =>
			{
				WebView.CoreWebView2.WindowCloseRequested -= CoreWebView2_WindowCloseRequested;
			}, "WindowCloseRequested");
			TryUnregisterCoreWebView2Event(() =>
			{
				WebView.CoreWebView2.WebResourceResponseReceived -= CoreWebView2_WebResourceResponseReceived;
			}, "WebResourceResponseReceived");
			TryUnregisterCoreWebView2Event(() =>
			{
				WebView.CoreWebView2.ProcessFailed -= CoreWebView2_ProcessFailed;
			}, "ProcessFailed");
			TryUnregisterCoreWebView2Event(() =>
			{
				WebView.CoreWebView2.DownloadStarting -= CoreWebView2_DownloadStarting;
			}, "DownloadStarting");
		}
	}

	private static void TryUnregisterCoreWebView2Event(Action unsubscribeAction, string eventName)
	{
		try
		{
			unsubscribeAction();
		}
		catch (Exception ex)
		{
			Logger.Warn("取消" + eventName + "事件失败: " + ex.Message);
		}
	}

	private async Task SetupCookiesAndStorage(ApiParameters parameters, string destinationUrl, StartupParameters startupParameters)
	{
		if (parameters == null)
		{
			throw new ArgumentNullException("parameters");
		}
		string host = new Uri(parameters.LoginUrl).Host;
		if (string.IsNullOrEmpty(host))
		{
			throw new InvalidOperationException("LoginUrl的域名不能为空");
		}
		try
		{
			string text = ExtractRegionName(host);
			string tpassDomain = "tpass." + text + ".chinatax.gov.cn";
			await SetupCookies(parameters, tpassDomain, ".chinatax.gov.cn");
			if (!CanContinueLogin(startupParameters))
			{
				return;
			}
			await SetupTpassLocalStorageAsync(parameters, tpassDomain, startupParameters);
			if (CanContinueLogin(startupParameters))
			{
				Logger.Info("tpass Cookie已设置，已尝试补充其他Cookie和LocalStorage", AreaCode, Uscc);
				if (destinationUrl.Contains("userCenter/baseInfoEE"))
				{
					await NavigateToUserCenterAsync(parameters.LoginUrl, startupParameters);
				}
				else
				{
					Navigate(destinationUrl);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("设置Cookie和LocalStorage失败", ex, AreaCode, Uscc);
			if (CanContinueLogin(startupParameters))
			{
				ShowErrorAndClose("设置Cookie和LocalStorage失败: " + ex.Message);
			}
		}
	}

	private async Task SetupCookies(ApiParameters parameters, string tpassDomain, string etaxDomain)
	{
		if (string.IsNullOrWhiteSpace(parameters.Token))
		{
			throw new InvalidOperationException("tpass token为空，无法建立登录状态。");
		}
		await AddCookieForDomainAsync(tpassDomain, "token", parameters.Token);
		await ProcessCookieString(parameters.EtaxCookie, etaxDomain, "EtaxCookie", "tpass_");
		await ProcessCookieString(parameters.EtaxSearchCookie, etaxDomain, "EtaxSearchCookie", "tpass_");
		await ProcessDpptCookie(parameters.DpptCookie, etaxDomain);
	}

	private async Task ProcessCookieString(string cookieString, string domain, string logName, string cookiePrefix = null)
	{
		if (string.IsNullOrEmpty(cookieString))
		{
			Logger.Info(logName + "为空", AreaCode, Uscc);
			return;
		}
		string[] array = cookieString.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
		Logger.Info($"开始处理{logName}，共{array.Length}项", AreaCode, Uscc);
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].Trim();
			if (string.IsNullOrEmpty(cookiePrefix) || text.StartsWith(cookiePrefix))
			{
				int num = text.IndexOf('=');
				if (num > 0)
				{
					string name = text.Substring(0, num);
					string value = text.Substring(num + 1);
					await AddCookieForDomainAsync(domain, name, value);
					Logger.Info(name + " 已添加到" + domain + "域", AreaCode, Uscc);
				}
			}
		}
		Logger.Info("已处理" + logName, AreaCode, Uscc);
	}

	private async Task ProcessDpptCookie(string dpptCookieString, string domain)
	{
		if (string.IsNullOrEmpty(dpptCookieString))
		{
			Logger.Info("DpptCookie为空", AreaCode, Uscc);
			return;
		}
		string[] array = dpptCookieString.Split(new char[1] { ';' }, StringSplitOptions.RemoveEmptyEntries);
		Logger.Info($"开始处理DpptCookie，共{array.Length}项", AreaCode, Uscc);
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].Trim();
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			int num = text.IndexOf('=');
			if (num > 0)
			{
				string name = text.Substring(0, num);
				string value = text.Substring(num + 1);
				string text2 = name;
				if ((text2 == "dzfp-ssotoken" || text2 == "SSO_SECURITY_CHECK_TOKEN") ? true : false)
				{
					await AddCookieForDomainAsync(domain, name, value);
					Logger.Info("已添加DpptCookie中的" + name + "到" + domain + "域", AreaCode, Uscc);
				}
			}
		}
		Logger.Info("已处理DpptCookie", AreaCode, Uscc);
	}

	private Task AddCookieForDomainAsync(string domain, string name, string value, string path = "/")
	{
		try
		{
			CoreWebView2CookieManager obj = WebView?.CoreWebView2?.CookieManager ?? throw new InvalidOperationException("WebView尚未初始化或已关闭，无法设置Cookie。");
			CoreWebView2Cookie cookie = obj.CreateCookie(name, value, domain, path);
			obj.AddOrUpdateCookie(cookie);
		}
		catch (Exception ex)
		{
			string text = (domain.Contains("tpass") ? "Error" : "Warn");
			string message = "为域名 " + domain + " 添加Cookie " + name + " 失败: " + ex.Message;
			if (text == "Error")
			{
				Logger.Error(message, AreaCode, Uscc);
				throw;
			}
			Logger.Warn(message, AreaCode, Uscc);
		}
		return Task.CompletedTask;
	}

	private async Task SetupTpassLocalStorageAsync(ApiParameters parameters, string tpassDomain, StartupParameters startupParameters)
	{
		WebView2 tempWebView = null;
		Panel mainContentPanel = null;
		string scriptId = null;
		try
		{
			_ = 2;
			try
			{
				if (WebView?.CoreWebView2 == null)
				{
					throw new InvalidOperationException("主WebView尚未初始化，无法共享环境。");
				}
				CoreWebView2Environment environment = WebView.CoreWebView2.Environment;
				mainContentPanel = (Application.Current.MainWindow?.FindName("MainContentGrid") as Panel) ?? throw new InvalidOperationException("在主窗口中找不到名为 'MainContentGrid' 的面板。");
				tempWebView = (_loginWebView = new WebView2
				{
					Visibility = Visibility.Collapsed
				});
				mainContentPanel.Children.Add(tempWebView);
				await tempWebView.EnsureCoreWebView2Async(environment);
				if (!CanContinueLogin(startupParameters))
				{
					goto end_IL_0054;
				}
				if (tempWebView.CoreWebView2 == null)
				{
					throw new InvalidOperationException("临时WebView2未能正确初始化。");
				}
				string targetUrl = "https://" + tpassDomain + ":8443/#/userCenter/baseInfoEE";
				string leftPart = new Uri(targetUrl).GetLeftPart(UriPartial.Authority);
				string javaScript = BuildLocalStorageScript(parameters, leftPart);
				scriptId = await tempWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(javaScript);
				if (!CanContinueLogin(startupParameters))
				{
					goto end_IL_0054;
				}
				Logger.Info("已为域名 " + tpassDomain + " 准备LocalStorage脚本", AreaCode, Uscc);
				await NavigateTempWebViewAsync(tempWebView, targetUrl, startupParameters);
				goto end_IL_003d;
				end_IL_0054:;
			}
			catch (Exception exception)
			{
				Logger.Error("使用临时WebView设置LocalStorage失败", exception, AreaCode, Uscc);
				goto end_IL_003d;
			}
			end_IL_003d:;
		}
		finally
		{
			if (CanContinueLogin(startupParameters))
			{
				await Task.Delay(3000);
			}
			if (_loginWebView == tempWebView)
			{
				_loginWebView = null;
				CleanupTempWebView(mainContentPanel, tempWebView, scriptId);
			}
		}
	}

	private static string BuildLocalStorageScript(ApiParameters parameters, string targetOrigin)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("if (location.origin === " + JsonConvert.SerializeObject(targetOrigin) + ") {");
		stringBuilder.AppendLine("localStorage.setItem('new_key16', " + JsonConvert.SerializeObject(parameters.NewKey16 ?? string.Empty) + ");");
		stringBuilder.AppendLine("localStorage.setItem('timestamp', " + JsonConvert.SerializeObject(parameters.Timestamp ?? string.Empty) + ");");
		stringBuilder.AppendLine("localStorage.setItem('token', " + JsonConvert.SerializeObject(parameters.Token ?? string.Empty) + ");");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private async Task NavigateTempWebViewAsync(WebView2 tempWebView, string targetUrl, StartupParameters startupParameters)
	{
		TaskCompletionSource<bool> navigationCompleted = new TaskCompletionSource<bool>();
		tempWebView.NavigationCompleted += OnTempNavigationCompleted;
		try
		{
			tempWebView.Source = new Uri(targetUrl);
			if (await Task.WhenAny(new Task[2]
			{
				navigationCompleted.Task,
				Task.Delay(3000)
			}) == navigationCompleted.Task)
			{
				Logger.Info("临时WebView导航到 " + targetUrl + " 已完成", AreaCode, Uscc);
			}
			else
			{
				Logger.Warn("临时WebView导航到 " + targetUrl + " 超时，继续尝试加载主页面", AreaCode, Uscc);
			}
		}
		finally
		{
			tempWebView.NavigationCompleted -= OnTempNavigationCompleted;
		}
		async void OnTempNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
		{
			try
			{
				if (CanContinueLogin(startupParameters))
				{
					if (e.IsSuccess)
					{
						CoreWebView2 coreWebView = ((WebView2)sender)?.CoreWebView2;
						if (coreWebView != null)
						{
							string leftPart = new Uri(targetUrl).GetLeftPart(UriPartial.Authority);
							await coreWebView.ExecuteScriptAsync("if (location.origin === " + JsonConvert.SerializeObject(leftPart) + ") { " + GetButtonClickScript() + " }");
						}
					}
					else
					{
						Logger.Warn("临时WebView页面导航失败", AreaCode, Uscc);
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error("执行页面按钮点击脚本时发生错误: " + ex.Message, AreaCode, Uscc);
			}
			finally
			{
				navigationCompleted.TrySetResult(result: true);
			}
		}
	}

	private static string GetButtonClickScript()
	{
		return "(function() {\r\n    return new Promise((resolve) => {\r\n        const interval = setInterval(() => {\r\n            const backButton = document.querySelector('#app > div > div.box > div > div.btns > button');\r\n            if (backButton) {\r\n                backButton.click();\r\n                clearInterval(interval);\r\n                resolve('已找到并点击按钮');\r\n            }\r\n        }, 300);\r\n        setTimeout(() => {\r\n            clearInterval(interval);\r\n            resolve('超时未找到按钮');\r\n        }, 5000);\r\n    });\r\n})();";
	}

	private void CleanupTempWebView(Panel mainContentPanel, WebView2 tempWebView, string scriptId)
	{
		if (tempWebView == null)
		{
			return;
		}
		try
		{
			if (scriptId != null)
			{
				tempWebView.CoreWebView2?.RemoveScriptToExecuteOnDocumentCreated(scriptId);
			}
		}
		catch (Exception arg)
		{
			Logger.Warn($"移除临时WebView认证脚本失败: {arg}", AreaCode, Uscc);
		}
		try
		{
			tempWebView.CoreWebView2?.Stop();
		}
		catch (Exception arg2)
		{
			Logger.Warn($"停止临时WebView导航失败: {arg2}", AreaCode, Uscc);
		}
		try
		{
			mainContentPanel?.Children.Remove(tempWebView);
		}
		catch (Exception arg3)
		{
			Logger.Warn($"移除临时WebView控件失败: {arg3}", AreaCode, Uscc);
		}
		finally
		{
			try
			{
				tempWebView.Dispose();
			}
			catch (Exception arg4)
			{
				Logger.Warn($"释放临时WebView失败: {arg4}", AreaCode, Uscc);
			}
		}
		Logger.Info("临时WebView资源已清理", AreaCode, Uscc);
	}

	private void Navigate(string url)
	{
		if (WebView != null && !string.IsNullOrEmpty(url))
		{
			WebView.Source = new Uri(url);
			Logger.Info("开始导航到URL: " + url, AreaCode, Uscc);
		}
	}

	private async Task NavigateToUserCenterAsync(string loginUrl, StartupParameters startupParameters)
	{
		if (!CanContinueLogin(startupParameters))
		{
			return;
		}
		Header = "正在导航到用户中心 请勿点击...";
		WebView.NavigationCompleted -= OnNavigationCompleted;
		EventHandler<CoreWebView2NavigationCompletedEventArgs> tempNavigationHandler = null;
		try
		{
			if (WebView?.CoreWebView2 == null)
			{
				Logger.Error("主WebView尚未初始化", AreaCode, Uscc);
				return;
			}
			Uri uri = new Uri(loginUrl);
			string targetUrl = uri.Scheme + "://" + uri.Authority + "/loginb/";
			Logger.Info("开始导航到 loginb 页面: " + targetUrl, AreaCode, Uscc);
			TaskCompletionSource<bool> navigationCompleted = new TaskCompletionSource<bool>();
			tempNavigationHandler = (object? sender, CoreWebView2NavigationCompletedEventArgs e) =>
			{
				Logger.Info(e.IsSuccess ? ("成功导航到 loginb 页面: " + targetUrl) : ("导航到 loginb 页面失败: " + targetUrl), AreaCode, Uscc);
				navigationCompleted.TrySetResult(result: true);
			};
			WebView.NavigationCompleted += tempNavigationHandler;
			WebView.Source = new Uri(targetUrl);
			if (await Task.WhenAny(new Task[2]
			{
				navigationCompleted.Task,
				Task.Delay(8000)
			}) != navigationCompleted.Task)
			{
				throw new TimeoutException("导航到税局首页超时");
			}
			await Task.Delay(1000);
			if (CanContinueLogin(startupParameters))
			{
				await TriggerHoverAndClickAsync();
			}
		}
		finally
		{
			if (WebView != null)
			{
				WebView.NavigationCompleted -= tempNavigationHandler;
				if (IsActive)
				{
					WebView.NavigationCompleted += OnNavigationCompleted;
				}
			}
		}
	}

	private async Task TriggerHoverAndClickAsync()
	{
		try
		{
			Logger.Info("开始执行鼠标悬停并点击操作", AreaCode, Uscc);
			Logger.Info("鼠标悬停操作结果: " + await WebView.CoreWebView2.ExecuteScriptAsync("(function() {\r\n    return new Promise((resolve) => {\r\n        try {\r\n            const hoverTargetSelector = '#NavTopPopup > span';\r\n            const hiddenButtonSelector = '#NavTopPopup > div:nth-child(3) > div > div > div > div.clearfix > div:nth-child(1) > span';\r\n            \r\n            const hoverTarget = document.querySelector(hoverTargetSelector);\r\n            if (!hoverTarget) {\r\n                resolve('未找到悬停目标元素');\r\n                return;\r\n            }\r\n\r\n            const rect = hoverTarget.getBoundingClientRect();\r\n            const centerX = rect.left + rect.width / 2;\r\n            const centerY = rect.top + rect.height / 2;\r\n\r\n            const events = [\r\n                new MouseEvent('mouseover', { bubbles: true, cancelable: true, view: window, clientX: centerX, clientY: centerY }),\r\n                new MouseEvent('mouseenter', { bubbles: true, cancelable: true, view: window, clientX: centerX, clientY: centerY }),\r\n                new MouseEvent('mousemove', { bubbles: true, cancelable: true, view: window, clientX: centerX, clientY: centerY })\r\n            ];\r\n\r\n            events.forEach(event => hoverTarget.dispatchEvent(event));\r\n            hoverTarget.style.pointerEvents = 'auto';\r\n            if (hoverTarget.parentElement) {\r\n                hoverTarget.parentElement.classList.add('hover');\r\n            }\r\n\r\n            setTimeout(() => {\r\n                const hiddenButton = document.querySelector(hiddenButtonSelector);\r\n                if (hiddenButton) {\r\n                    hiddenButton.click();\r\n                    hiddenButton.dispatchEvent(new MouseEvent('click', { bubbles: true }));\r\n                    \r\n                    if (hiddenButton.tagName.toLowerCase() === 'a' && hiddenButton.href) {\r\n                        window.location.href = hiddenButton.href;\r\n                    }\r\n                    \r\n                    resolve('成功执行悬停和点击操作');\r\n                } else {\r\n                    resolve('悬停后仍未找到隐藏按钮');\r\n                }\r\n            }, 800);\r\n\r\n        } catch (error) {\r\n            resolve('悬停操作发生错误: ' + error.message);\r\n        }\r\n    });\r\n})();"), AreaCode, Uscc);
		}
		catch (Exception ex)
		{
			Logger.Error("执行鼠标悬停操作时发生错误: " + ex.Message, AreaCode, Uscc);
		}
	}

	private string ExtractRegionName(string domain)
	{
		try
		{
			int num = domain.IndexOf(':');
			if (num > 0)
			{
				domain = domain.Substring(0, num);
			}
			string[] array = domain.Split('.');
			for (int i = 0; i < array.Length - 1; i++)
			{
				if (array[i + 1].Equals("chinatax", StringComparison.OrdinalIgnoreCase))
				{
					return array[i];
				}
			}
			if (array.Length >= 2)
			{
				return array[1];
			}
			Logger.Warn("无法从域名 " + domain + " 提取地区名", AreaCode, Uscc);
			return string.Empty;
		}
		catch (Exception ex)
		{
			Logger.Warn("提取地区名时出错: " + ex.Message, AreaCode, Uscc);
			return string.Empty;
		}
	}

	private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
	{
		if (e.Uri == "about:blank")
		{
			if (_allowInitialBlankNavigation)
			{
				Logger.Info("新窗口初始空白页，保留标签页", AreaCode, Uscc);
				return;
			}
			Logger.Info("导航到空白页，关闭页面", AreaCode, Uscc);
			e.Cancel = true;
			Close();
		}
		else
		{
			_allowInitialBlankNavigation = false;
			Logger.Info("监控到导航到URL：" + e.Uri, AreaCode, Uscc);
		}
	}

	private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		if (WebView?.CoreWebView2 != null)
		{
			Header = WebView.CoreWebView2.DocumentTitle ?? "无标题";
		}
	}

	private async void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
	{
		CoreWebView2Deferral deferral = null;
		try
		{
			if (!IsActive)
			{
				e.Handled = true;
				return;
			}
			if (HandleLocalPdfOpen(e))
			{
				Logger.Info("本地 PDF 文件已用外部程序打开", AreaCode, Uscc);
				return;
			}
			deferral = e.GetDeferral();
			string uri = e.Uri;
			Logger.Info("收到新窗口请求：" + uri, AreaCode, Uscc);
			await HandleNewWindowAsync(e, uri);
		}
		catch (Exception ex)
		{
			Logger.Error("处理新窗口请求时发生异常: " + ex.Message, ex, AreaCode, Uscc);
			e.Handled = true;
			ShowErrorAndClose("处理新窗口请求时发生异常: " + ex.Message);
		}
		finally
		{
			if (deferral != null)
			{
				CompleteDeferral(deferral);
			}
		}
	}

	private bool HandleLocalPdfOpen(CoreWebView2NewWindowRequestedEventArgs e)
	{
		if (!e.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase) || !e.Uri.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		Logger.Info("拦截本地 PDF 文件导航: " + e.Uri, AreaCode, Uscc);
		e.Handled = true;
		string localPath = new Uri(e.Uri).LocalPath;
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = localPath,
				UseShellExecute = true
			});
			Logger.Info("已使用外部程序打开 PDF 文件: " + localPath, AreaCode, Uscc);
			return true;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			Logger.Error("无法打开 PDF 文件: " + ex3.Message, ex3, AreaCode, Uscc);
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				MessageBoxHelper.Error("无法打开 PDF 文件：" + ex3.Message);
			});
			return true;
		}
	}

	private async Task HandleNewWindowAsync(CoreWebView2NewWindowRequestedEventArgs e, string targetUri)
	{
		e.Handled = true;
		if (!IsActive || !(Parent is TabControl tabControl))
		{
			return;
		}
		WebViewTabItem newTab = null;
		try
		{
			newTab = new WebViewTabItem(AreaCode)
			{
				_allowInitialBlankNavigation = (targetUri == "about:blank")
			};
			tabControl.Items.Add(newTab);
			tabControl.SelectedItem = newTab;
			if (await newTab.InitializeAsync(WebView.CoreWebView2.Environment))
			{
				if (!IsActive || !newTab.IsActive)
				{
					newTab.Close(clearBrowsingData: false);
					return;
				}
				e.NewWindow = newTab.WebView.CoreWebView2;
				Logger.Info("新窗口创建成功: " + targetUri, AreaCode, Uscc);
			}
		}
		catch (Exception ex)
		{
			Logger.Error("创建新标签页失败", ex, AreaCode, Uscc);
			(newTab ?? this).ShowErrorAndClose("创建新标签页失败: " + ex.Message);
		}
	}

	private void CoreWebView2_WindowCloseRequested(object sender, object args)
	{
		Close();
	}

	private void CoreWebView2_WebResourceResponseReceived(object sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
	{
		try
		{
			// 只留请求方法、路径和状态码：查询串与 Cookie 里带税局会话凭据，不能落盘。
			string uri = e.Request.Uri;
			int queryIndex = uri.IndexOf('?');
			if (queryIndex >= 0)
			{
				uri = uri.Substring(0, queryIndex);
			}
			if (!IsNoiseUrl(uri))
			{
				Logger.Http("监控到响应：[" + e.Request.Method + "] URL：[" + uri + "] 状态码：[" + e.Response.StatusCode + "] " + e.Response.ReasonPhrase, AreaCode, Uscc);
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("处理Web响应时发生错误: " + ex.Message, AreaCode, Uscc);
		}
	}

	private static bool IsNoiseUrl(string uri)
	{
		if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return new string[12] { ".js", ".css", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf", ".map" }.Any((string ext) => uri.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
	}

	private void CoreWebView2_ProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
	{
		if (!IsActive)
		{
			return;
		}
		Logger.Error($"WebView2进程异常: 类型={e.ProcessFailedKind}, 原因={e.Reason}, 退出码={e.ExitCode}, " + $"描述={e.ProcessDescription}, 当前URL：{WebView?.Source}, 详细错误={e}", new Exception(), AreaCode, Uscc);
		Application.Current.Dispatcher.InvokeAsync(() =>
		{
			try
			{
				if (IsActive)
				{
					switch (e.ProcessFailedKind)
					{
					case CoreWebView2ProcessFailedKind.BrowserProcessExited:
						ShowErrorAndClose("网页进程意外中断，请重新一键登录。");
						break;
					case CoreWebView2ProcessFailedKind.RenderProcessExited:
						ShowErrorAndClose("页面渲染进程崩溃，请重新打开页面。");
						break;
					case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive:
						HandleRenderProcessUnresponsive();
						break;
					case CoreWebView2ProcessFailedKind.UtilityProcessExited:
					case CoreWebView2ProcessFailedKind.SandboxHelperProcessExited:
					case CoreWebView2ProcessFailedKind.GpuProcessExited:
					case CoreWebView2ProcessFailedKind.PpapiPluginProcessExited:
					case CoreWebView2ProcessFailedKind.PpapiBrokerProcessExited:
					case CoreWebView2ProcessFailedKind.UnknownProcessExited:
						Logger.Info($"非致命进程退出: {e.ProcessFailedKind}，WebView2将自动恢复", AreaCode, Uscc);
						break;
					default:
						ShowErrorAndClose($"WebView组件遇到问题，类型：{e.ProcessFailedKind}");
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.Error("处理WebView崩溃事件时发生异常", ex, AreaCode, Uscc);
				ShowErrorAndClose("处理WebView崩溃事件时发生异常: " + ex.Message);
			}
		});
	}

	private async void HandleRenderProcessUnresponsive()
	{
		try
		{
			DateTime now = DateTime.Now;
			if (now.Subtract(_lastUnresponsiveEventTime).TotalMinutes > 1.0)
			{
				_unresponsiveEventCount = 0;
				Logger.Info("重置无响应事件计数器", AreaCode, Uscc);
			}
			_unresponsiveEventCount++;
			_lastUnresponsiveEventTime = now;
			Logger.Warn($"检测到渲染进程无响应 (第{_unresponsiveEventCount}次)", AreaCode, Uscc);
			if (_unresponsiveEventCount >= 5)
			{
				switch (ShowUnresponsiveDialog())
				{
				case MessageBoxResult.Yes:
					Logger.Info("用户选择继续等待页面响应", AreaCode, Uscc);
					_unresponsiveEventCount = 0;
					_lastUnresponsiveEventTime = now;
					break;
				case MessageBoxResult.No:
					Logger.Info("用户选择关闭标签页", AreaCode, Uscc);
					Close();
					break;
				default:
					Logger.Info("默认等待行为", AreaCode, Uscc);
					_unresponsiveEventCount = 0;
					_lastUnresponsiveEventTime = now;
					break;
				}
			}
			else
			{
				Header = $"页面响应缓慢({_unresponsiveEventCount}/{5}) - " + (WebView?.CoreWebView2?.DocumentTitle ?? "无标题");
				await Task.Delay(5000);
				if (IsActive && _unresponsiveEventCount < 5)
				{
					Header = WebView?.CoreWebView2?.DocumentTitle ?? "无标题";
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("处理渲染进程无响应事件时发生异常", ex, AreaCode, Uscc);
			ShowErrorAndClose("处理页面无响应时发生异常: " + ex.Message);
		}
	}

	private MessageBoxResult ShowUnresponsiveDialog()
	{
		try
		{
			string message = "页面 " + (WebView?.CoreWebView2?.DocumentTitle ?? "无标题") + " 无响应，可能原因：\n• 长时间运行的脚本\n• 系统资源繁忙\n\n是否等待页面响应？选择“否”将关闭该标签页。";
			return Application.Current.Dispatcher.Invoke(() => MessageBoxHelper.Confirm(message));
		}
		catch (Exception exception)
		{
			Logger.Error("显示无响应对话框时发生异常", exception, AreaCode, Uscc);
			return MessageBoxResult.Cancel;
		}
	}

	private void CoreWebView2_DownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
	{
		try
		{
			string fileName = Path.GetFileName(e.ResultFilePath) ?? "";
			string text = Path.GetExtension(e.ResultFilePath) ?? "";
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.FileName = fileName;
			saveFileDialog.Filter = "文件 (*" + text + ")|*" + text + "|所有文件 (*.*)|*.*";
			saveFileDialog.DefaultExt = text;
			saveFileDialog.Title = "请选择下载保存路径";
			SaveFileDialog saveFileDialog2 = saveFileDialog;
			if (saveFileDialog2.ShowDialog() == true)
			{
				string text2 = saveFileDialog2.FileName;
				if (!string.IsNullOrEmpty(text) && !Path.HasExtension(text2))
				{
					text2 += text;
				}
				e.ResultFilePath = text2;
			}
			else
			{
				e.Cancel = true;
			}
		}
		catch (Exception exception)
		{
			Logger.Error("下载路径选择出错", exception, AreaCode, Uscc);
			e.Cancel = true;
		}
	}

	private void CompleteDeferral(CoreWebView2Deferral deferral)
	{
		try
		{
			deferral.Complete();
			Logger.Info("Deferral已完成", AreaCode, Uscc);
		}
		catch (Exception ex)
		{
			Logger.Error("完成Deferral时发生异常: " + ex.Message, ex, AreaCode, Uscc);
		}
	}

	private void ShowErrorAndClose(string message)
	{
		if (!IsActive || _errorDialogShown)
		{
			return;
		}
		_errorDialogShown = true;
		Logger.Error("由于错误关闭标签页: " + message, AreaCode, Uscc);
		try
		{
			MessageBoxHelper.Error(message);
		}
		finally
		{
			Close();
		}
	}

	private async Task ClearBrowsingDataAsync()
	{
		try
		{
			if (WebView?.CoreWebView2 != null && !IsDisposed)
			{
				Logger.Info("清理WebView2 Cookie和LocalStorage", AreaCode, Uscc);
				await WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.LocalStorage | CoreWebView2BrowsingDataKinds.Cookies);
				Logger.Info("已清理WebView2 Cookie和LocalStorage", AreaCode, Uscc);
			}
			else
			{
				Logger.Warn("WebView已释放或CoreWebView2无效，跳过清理Cookie和LocalStorage", AreaCode, Uscc);
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("清理WebView2 Cookie和LocalStorage时发生异常: " + ex.Message, AreaCode, Uscc);
		}
	}

	public void Dispose()
	{
		Close();
	}

	internal void DisposeResources()
	{
		if (IsDisposed)
		{
			return;
		}
		try
		{
			if (WebView != null)
			{
				try
				{
					WebView.NavigationStarting -= OnNavigationStarting;
					WebView.NavigationCompleted -= OnNavigationCompleted;
					try
					{
						UnRegisterCoreWebView2Events();
					}
					catch (Exception ex)
					{
						Logger.Warn("解除CoreWebView2事件时出错（可能是浏览器进程已崩溃）: " + ex.Message, AreaCode, Uscc);
					}
				}
				catch (Exception ex2)
				{
					Logger.Warn("取消事件订阅时出错: " + ex2.Message, AreaCode, Uscc);
				}
				try
				{
					WebView.Dispose();
				}
				catch (Exception ex3)
				{
					Logger.Warn("释放WebView时出错（浏览器进程已崩溃）: " + ex3.Message, AreaCode, Uscc);
				}
				finally
				{
					WebView = null;
				}
			}
			Logger.Info($"WebViewTabItem资源已释放，AreaCode: {AreaCode}", AreaCode, Uscc);
		}
		catch (Exception ex4)
		{
			Logger.Error("释放WebViewTabItem资源时出错: " + ex4.Message, ex4, AreaCode, Uscc);
		}
		IsDisposed = true;
	}
}
