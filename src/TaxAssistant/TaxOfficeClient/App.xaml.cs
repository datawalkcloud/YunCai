using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using TaxOfficeClient.Manager;
using TaxOfficeClient.Tools;
using TaxOfficeClient.Windows;

namespace TaxOfficeClient;

[SupportedOSPlatform("windows")]
public partial class App : Application
{
	private const string MUTEX_NAME = "KuaijiTaxAssistant";

	private const int HTTP_SERVER_PORT = 41731;

	private static Mutex _mutex;

	private static readonly MultiOpenTool _multiOpenTool = new MultiOpenTool();

	private readonly HttpServerManager _httpServerManager = new HttpServerManager();

	private bool _isCleanedUp;

	private readonly object _cleanupLock = new object();

	private bool _shutdownRequested;

	private int _fatalErrorShown;

	public event Action<int> UrlReceived;

	public App()
	{
		DispatcherUnhandledException += (object _, DispatcherUnhandledExceptionEventArgs e) =>
		{
			e.Handled = true;
			HandleFatalError("未处理的调度程序异常", e.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += (object _, UnhandledExceptionEventArgs e) =>
		{
			HandleFatalError("未处理的域异常", e.ExceptionObject as Exception);
		};
		TaskScheduler.UnobservedTaskException += (object? _, UnobservedTaskExceptionEventArgs e) =>
		{
			e.SetObserved();
			HandleFatalError("未观察到的任务异常", e.Exception);
		};
		AppDomain.CurrentDomain.ProcessExit += (object? _, EventArgs _) =>
		{
			Logger.Info("进程即将退出");
			CleanupResources();
		};
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		try
		{
			Logger.Info("应用程序启动开始");
			if (!TryCreateMutex())
			{
				HandleMultipleInstances(e);
				return;
			}
			// 免安装分发：每次启动都校正当前目录下的协议注册，移动安装目录后仍能唤起。
			ProtocolRegistrar.EnsureRegistered();
			// 用户装完没勾开机自启时，助手第一次启动就自己补上，之后网页才一直探测得到。
			ProtocolRegistrar.EnsureAutoStart();
			if (!CheckWebView2Runtime())
			{
				ShowErrorAndExit("WebView2运行时未安装或版本不兼容,应用程序无法启动。\n请安装最新的WebView2运行时。");
				return;
			}
			int areaCode = ProcessStartupArguments(e);
			Task task = InitializeServicesAsync();
			base.OnStartup(e);
			CreateAndShowMainWindow(areaCode);
			Logger.Info("应用程序启动完成");
			task.ContinueWith((Task t) =>
			{
				if (t.IsFaulted)
				{
					Logger.Error("后台服务初始化失败", t.Exception);
				}
			});
		}
		catch (Exception ex)
		{
			HandleStartupException(ex);
		}
	}

	private async Task InitializeServicesAsync()
	{
		Task[] tasks = new Task[2]
		{
			StartHttpServerAsync(),
			Task.Run(() =>
			{
				LogSystemEnvironment();
				_multiOpenTool.StartNamedPipeServer(OnUrlReceived);
			})
		};
		try
		{
			await Task.WhenAll(tasks);
			Logger.Info("所有后台服务初始化完成");
		}
		catch (Exception ex)
		{
			Logger.Warn("部分后台服务初始化失败: " + ex.Message, ex);
		}
	}

	protected override void OnExit(ExitEventArgs e)
	{
		CleanupResources();
		base.OnExit(e);
		Logger.Info("应用程序已退出");
	}

	internal void HandleFatalError(string message, Exception exception)
	{
		Logger.Fatal(message, exception);
		if (Interlocked.Exchange(ref _fatalErrorShown, 1) != 0 || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
		{
			return;
		}
		try
		{
			if (Dispatcher.CheckAccess())
			{
				ShowAndExit();
			}
			else
			{
				Dispatcher.Invoke(ShowAndExit);
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("退出中无法显示错误提示: " + ex.Message, ex);
		}
		void ShowAndExit()
		{
			try
			{
				MessageBoxHelper.Error("客户端发生错误，即将退出");
			}
			finally
			{
				RequestShutdownAsync();
			}
		}
	}

	public async Task RequestShutdownAsync()
	{
		if (_shutdownRequested)
		{
			return;
		}
		_shutdownRequested = true;
		try
		{
			if (MainWindow is MainWindow mainWindow)
			{
				await mainWindow.PrepareForShutdownAsync();
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("退出前清理失败: " + ex.Message, ex);
		}
		finally
		{
			Shutdown();
		}
	}

	private static bool TryCreateMutex()
	{
		try
		{
			_mutex = new Mutex(initiallyOwned: true, MUTEX_NAME, out var createdNew);
			Logger.Info($"互斥体创建结果：{createdNew}");
			return createdNew;
		}
		catch (Exception exception)
		{
			Logger.Error("创建互斥体失败", exception);
			return false;
		}
	}

	private static void HandleMultipleInstances(StartupEventArgs e)
	{
		Logger.Info("检测到应用程序已在运行，传递参数到现有实例");
		bool flag = true;
		if (e.Args.Length != 0)
		{
			flag = MultiOpenTool.SendArgsToRunningInstance(e.Args[0]);
		}
		if (!flag)
		{
			MessageBoxHelper.Error("登录请求未能发送到已运行的客户端，请重新点击一键登录。");
		}
		_mutex = null;
		Application.Current.Shutdown();
	}

	private static int ProcessStartupArguments(StartupEventArgs e)
	{
		int result = -1;
		Logger.Info("启动参数：" + SessionManager.RedactTicket(JsonConvert.SerializeObject(e.Args)));
		if (e.Args.Length != 0)
		{
			result = AddSession(e.Args[0]);
		}
		return result;
	}

	private static int AddSession(string url)
	{
		try
		{
			return SessionManager.Instance.AddSession(url);
		}
		catch (Exception ex)
		{
			Logger.Error("URL参数解析失败：" + ex.Message, ex);
			MessageBox.Show("添加会话失败\n\nURL: " + url + "\n错误信息: " + ex.Message + "\n\n请再次点击一键登录进行重试", "参数解析错误", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return -1;
		}
	}

	private static void CreateAndShowMainWindow(int areaCode)
	{
		try
		{
			new MainWindow(areaCode).Show();
			Logger.Info("主窗口已创建并显示");
		}
		catch (Exception exception)
		{
			Logger.Error("创建主窗口失败", exception);
			throw;
		}
	}

	private void HandleStartupException(Exception ex)
	{
		Logger.Fatal("应用程序启动失败", ex);
		MessageBox.Show("应用程序启动失败\n\n错误信息：" + ex.Message, "启动错误", MessageBoxButton.OK, MessageBoxImage.Hand);
		CleanupResources();
		Application.Current?.Shutdown();
	}

	private async Task StartHttpServerAsync()
	{
		try
		{
			await _httpServerManager.StartAsync(HTTP_SERVER_PORT);
			Logger.Info($"HTTP服务器已启动，端口：{HTTP_SERVER_PORT}");
		}
		catch (Exception ex)
		{
			Logger.Error("HTTP服务器启动失败: " + ex.Message, ex);
		}
	}

	private static bool CheckWebView2Runtime()
	{
		try
		{
			string availableBrowserVersionString = CoreWebView2Environment.GetAvailableBrowserVersionString();
			Logger.Info("检测到WebView2运行时版本: " + availableBrowserVersionString);
			return !string.IsNullOrEmpty(availableBrowserVersionString);
		}
		catch (Exception exception)
		{
			Logger.Fatal("未检测到WebView2运行时", exception);
			return false;
		}
	}

	private static void ShowErrorAndExit(string message)
	{
		Logger.Error(message);
		MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
		Application.Current.Shutdown();
	}

	private void OnUrlReceived(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			Logger.Warn("接收到空的URL参数");
			return;
		}
		Logger.Info("接收到URL参数: " + url);
		int num = AddSession(url);
		if (num != -1)
		{
			UrlReceived?.Invoke(num);
		}
	}

	private void CleanupResources()
	{
		lock (_cleanupLock)
		{
			if (_isCleanedUp)
			{
				Logger.Info("资源已清理，跳过重复清理");
				return;
			}
			try
			{
				Logger.Info("开始清理应用程序资源");
				CleanupMultiOpenTool();
				CleanupHttpServer();
				CleanupTaxLoginService();
				CleanupSessionManager();
				CleanupHttpClient();
				CleanupMutex();
				_isCleanedUp = true;
				Logger.Info("应用程序资源清理完成");
			}
			catch (Exception ex)
			{
				Logger.Error("清理资源时发生异常: " + ex.Message, ex);
			}
		}
	}

	private void CleanupHttpServer()
	{
		try
		{
			_httpServerManager?.Dispose();
			Logger.Info("HTTP服务器已停止");
		}
		catch (Exception ex)
		{
			Logger.Warn("停止HTTP服务器时出错: " + ex.Message, ex);
		}
	}

	private static void CleanupTaxLoginService()
	{
		try
		{
			TaxLoginService.Instance.Dispose();
			Logger.Info("税局登录执行器已停止");
		}
		catch (Exception ex)
		{
			Logger.Warn("停止税局登录执行器时出错: " + ex.Message, ex);
		}
	}

	private static void CleanupSessionManager()
	{
		try
		{
			SessionManager.Instance.Dispose();
			Logger.Info("会话管理器已释放");
		}
		catch (Exception ex)
		{
			Logger.Warn("释放会话管理器时出错: " + ex.Message, ex);
		}
	}

	private static void CleanupHttpClient()
	{
		try
		{
			HttpClientManager.Dispose();
			Logger.Info("HttpClient已释放");
		}
		catch (Exception ex)
		{
			Logger.Warn("释放HttpClient时出错: " + ex.Message, ex);
		}
	}

	private static void CleanupMutex()
	{
		if (_mutex == null)
		{
			return;
		}
		try
		{
			try
			{
				_mutex.ReleaseMutex();
				Logger.Info("互斥体已释放");
			}
			catch (ApplicationException ex)
			{
				Logger.Warn("释放互斥体时出现ApplicationException: " + ex.Message);
			}
			_mutex.Close();
			_mutex.Dispose();
		}
		catch (ObjectDisposedException ex2)
		{
			Logger.Warn("互斥体已被释放: " + ex2.Message);
		}
		catch (Exception ex3)
		{
			Logger.Error("清理互斥体时发生未预期异常: " + ex3.GetType().Name + ", " + ex3.Message, ex3);
		}
		finally
		{
			_mutex = null;
		}
	}

	private static void CleanupMultiOpenTool()
	{
		try
		{
			_multiOpenTool?.Dispose();
			Logger.Info("多开工具已释放");
		}
		catch (Exception ex)
		{
			Logger.Warn("释放多开工具时出错: " + ex.Message, ex);
		}
	}

	private static void LogSystemEnvironment()
	{
		try
		{
			string oSDescription = RuntimeInformation.OSDescription;
			var value = new
			{
				OSVersion = Environment.OSVersion.ToString(),
				OSDescription = oSDescription,
				OSName = GetOsFriendlyName(oSDescription),
				Is64BitOS = Environment.Is64BitOperatingSystem,
				BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
				AppVersion = typeof(App).Assembly.GetName().Version?.ToString(),
				UserName = Environment.UserName,
				MachineName = Environment.MachineName,
				ProcessorCount = Environment.ProcessorCount,
				SystemPageSize = Environment.SystemPageSize,
				WorkingSetMB = Environment.WorkingSet / 1048576
			};
			Logger.Info("系统环境信息: " + JsonConvert.SerializeObject(value, Formatting.Indented));
		}
		catch (Exception ex)
		{
			Logger.Warn("记录系统环境信息失败: " + ex.Message, ex);
		}
	}

	private static string GetOsFriendlyName(string osDesc)
	{
		if (string.IsNullOrWhiteSpace(osDesc))
		{
			return "未知操作系统";
		}
		try
		{
			string[] array = osDesc.Replace("Microsoft Windows ", "").Split('.');
			string result3;
			if (array.Length >= 3 && int.TryParse(array[0], out var result) && int.TryParse(array[2], out var result2))
			{
				int num = result;
				if (num != 6)
				{
					if (num != 10)
					{
						goto IL_0097;
					}
					if (result2 >= 22000)
					{
						result3 = "Windows 11";
					}
					else
					{
						if (result2 < 10240)
						{
							goto IL_0097;
						}
						result3 = "Windows 10";
					}
				}
				else if (result2 >= 9200)
				{
					result3 = "Windows 8";
				}
				else
				{
					if (result2 < 7600)
					{
						goto IL_0097;
					}
					result3 = "Windows 7";
				}
				goto IL_00ae;
			}
			return "Windows (未知版本)";
			IL_00ae:
			return result3;
			IL_0097:
			result3 = $"Windows (版本 {result}.{result2})";
			goto IL_00ae;
		}
		catch
		{
			return "Windows (解析失败)";
		}
	}
}
