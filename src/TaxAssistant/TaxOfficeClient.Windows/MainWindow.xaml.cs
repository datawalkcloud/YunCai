using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Web.WebView2.Core;
using TaxOfficeClient.Controls;
using TaxOfficeClient.Manager;
using TaxOfficeClient.Model.Manager;
using TaxOfficeClient.Tools;

namespace TaxOfficeClient.Windows;

public partial class MainWindow : Window, IDisposable
{
	public static readonly DependencyProperty TabItemWidthProperty = DependencyProperty.Register("TabItemWidth", typeof(double), typeof(MainWindow), new PropertyMetadata(200.0));

	private readonly SessionManager _sessionManager = SessionManager.Instance;

	private CustomTitleBarManager _customTitleBarManager;

	private bool _disposed;

	private Task _shutdownPreparation;

	public double TabItemWidth
	{
		get
		{
			return (double)GetValue(TabItemWidthProperty);
		}
		set
		{
			SetValue(TabItemWidthProperty, value);
		}
	}

	public bool IsClosing { get; private set; }

	public MainWindow(int areaCode)
	{
		Logger.Info("开始加载MainWindow...");
		InitializeComponent();
		InitializeTitle();
		InitializeCustomTitleBar();
		SubscribeEvents();
		UpdateBackgroundVisibility();
		InitializeDelayedLoad(areaCode);
	}

	private void InitializeTitle()
	{
		Title = "云财税务助手 ";
	}

	private void InitializeCustomTitleBar()
	{
		_customTitleBarManager = new CustomTitleBarManager(this, TitleBar, MinimizeButton, MaximizeRestoreButton, CloseButton);
		_customTitleBarManager.Attach();
	}

	private void SubscribeEvents()
	{
		if (Application.Current is App app)
		{
			app.UrlReceived += OnUrlReceived;
		}
		SizeChanged += UpdateTabItemWidth;
		StateChanged += OnWindowStateChanged;
		Closing += MainWindow_Closing;
		INotifyCollectionChanged items = MainWindowTabControl.Items;
		if (items != null)
		{
			items.CollectionChanged += OnTabControlItemsChanged;
		}
	}

	private void InitializeDelayedLoad(int areaCode)
	{
		Loaded += async (object s, RoutedEventArgs e) =>
		{
			if (!IsClosing)
			{
				if (areaCode != -1)
				{
					StartupParameters startupParameters = _sessionManager.GetStartupParameters(areaCode);
					await AddNewWebViewTab(startupParameters.DestinationUrl, areaCode, startupParameters);
				}
			}
		};
	}

	private void OnTabControlItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		UpdateTabItemWidth(sender, e);
		UpdateBackgroundVisibility();
	}

	private void OnWindowStateChanged(object sender, EventArgs e)
	{
		MainBorder.BorderThickness = ((WindowState == WindowState.Maximized) ? new Thickness(0.0) : new Thickness(1.0));
	}

	private async void OnUrlReceived(int areaCode)
	{
		if (IsClosing)
		{
			return;
		}
		try
		{
			StartupParameters startupParameters = _sessionManager.GetStartupParameters(areaCode);
			ShowWindow();
			if (!IsClosing)
			{
				await AddNewWebViewTab(startupParameters.DestinationUrl, areaCode, startupParameters);
			}
		}
		catch (Exception exception)
		{
			((App)Application.Current).HandleFatalError("接受参数后事件处理出错", exception);
		}
	}

	/// <summary>
	/// 点关闭只把窗口收进托盘，不退出进程。
	/// 网页靠本机 41731 端口判断助手在不在，进程一退探测就失败，页面会把「没在运行」报成「没安装」。
	/// </summary>
	private void MainWindow_Closing(object sender, CancelEventArgs e)
	{
		if (_disposed || IsClosing)
		{
			return;
		}
		Logger.Info("MainWindow收到关闭请求，转为隐藏并保留托盘常驻");
		e.Cancel = true;
		Hide();
	}

	public Task PrepareForShutdownAsync()
	{
		if (_shutdownPreparation != null)
		{
			return _shutdownPreparation;
		}
		IsClosing = true;
		return _shutdownPreparation = PrepareAndDisposeAsync();
	}

	private async Task PrepareAndDisposeAsync()
	{
		try
		{
			foreach (WebViewTabItem item in MainWindowTabControl.Items.OfType<WebViewTabItem>().ToList())
			{
				try
				{
					item.StopNavigationForCleanup();
				}
				catch (Exception ex)
				{
					Logger.Warn("停止标签页导航时出错: " + ex.Message, ex);
				}
			}
			await CleanupBrowsingDataAsync();
		}
		finally
		{
			Dispose();
		}
	}

	private void RefreshButton_Click(object sender, RoutedEventArgs e)
	{
		if (MainWindowTabControl.SelectedItem is WebViewTabItem webViewTabItem)
		{
			webViewTabItem.RefreshWebView();
		}
	}

	private void ShowWindow()
	{
		Show();
		Topmost = true;
		WindowState = WindowState.Maximized;
		Activate();
		Topmost = false;
	}

	/// <summary>把助手窗口带到前台，供本机网页发起的登录复用。</summary>
	public void BringToFront()
	{
		if (!IsClosing)
		{
			ShowWindow();
		}
	}

	private void TrayIcon_MouseDoubleClick(object sender, RoutedEventArgs e)
	{
		BringToFront();
	}

	private void TrayOpenMenuItem_Click(object sender, RoutedEventArgs e)
	{
		BringToFront();
	}

	/// <summary>托盘「退出」才走真正的关闭流程：清理标签页后结束进程，41731 监听随之停掉。</summary>
	private async void TrayExitMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (Application.Current is App app)
		{
			await app.RequestShutdownAsync();
		}
	}

	/// <summary>
	/// 为网页发起的税局登录新建标签页并初始化内置浏览器。
	/// 与 AddNewWebViewTab 的区别：不调用 LoadUrlAsync（那条路径依赖云端 token），
	/// 登录页导航与填表由 TaxLoginService 在本机完成。
	/// </summary>
	public async Task<WebViewTabItem> OpenTaxLoginTabAsync(StartupParameters startupParameters, string header)
	{
		if (IsClosing)
		{
			throw new InvalidOperationException("助手正在退出，无法发起登录。");
		}
		BringToFront();
		WebViewTabItem tabItem = new WebViewTabItem(startupParameters.AreaCode, header);
		MainWindowTabControl.Items.Add(tabItem);
		MainWindowTabControl.SelectedItem = tabItem;
		if (!await tabItem.InitializeAsync())
		{
			MainWindowTabControl.Items.Remove(tabItem);
			tabItem.DisposeResources();
			throw new InvalidOperationException("内置浏览器初始化失败，请重启助手后重试。");
		}
		return tabItem;
	}

	private void UpdateTabItemWidth(object sender, EventArgs e)
	{
		int count = MainWindowTabControl.Items.Count;
		if (count != 0)
		{
			TabItemWidth = (((double)count * 200.0 > MainWindowTabControl.ActualWidth) ? Math.Max(23.0, MainWindowTabControl.ActualWidth / (double)count) : 200.0);
		}
	}

	private void UpdateBackgroundVisibility()
	{
		bool flag = MainWindowTabControl.Items.Count > 0;
		BackgroundImage.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
		RefreshButton.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
	}

	private async Task AddNewWebViewTab(string destinationUrl, int areaCode, StartupParameters startupParameters)
	{
		if (!IsClosing && _sessionManager.IsCurrentStartupParameters(startupParameters))
		{
			WebViewTabItem tabItem = new WebViewTabItem(areaCode);
			MainWindowTabControl.Items.Add(tabItem);
			MainWindowTabControl.SelectedItem = tabItem;
			if (await tabItem.InitializeAsync() && !IsClosing)
			{
				await tabItem.LoadUrlAsync(destinationUrl, startupParameters);
			}
		}
	}

	private void UnsubscribeEvents()
	{
		if (Application.Current is App app)
		{
			app.UrlReceived -= OnUrlReceived;
		}
		SizeChanged -= UpdateTabItemWidth;
		StateChanged -= OnWindowStateChanged;
		Closing -= MainWindow_Closing;
		INotifyCollectionChanged items = MainWindowTabControl.Items;
		if (items != null)
		{
			items.CollectionChanged -= OnTabControlItemsChanged;
		}
	}

	private void CleanupTabItems()
	{
		foreach (WebViewTabItem item in MainWindowTabControl.Items.OfType<WebViewTabItem>().ToList())
		{
			try
			{
				_sessionManager.RemoveTabFromSession(item.AreaCode, item);
				item.DisposeResources();
				Logger.Info($"已清理标签页资源,AreaCode: {item.AreaCode}");
			}
			catch (Exception ex)
			{
				Logger.Warn("清理标签页资源时出错: " + ex.Message);
			}
		}
		MainWindowTabControl.Items.Clear();
	}

	private async Task CleanupBrowsingDataAsync()
	{
		_ = 1;
		try
		{
			WebViewTabItem webViewTabItem = MainWindowTabControl.Items.OfType<WebViewTabItem>().FirstOrDefault((WebViewTabItem t) => t?.WebView?.CoreWebView2 != null && !t.IsDisposed);
			if (webViewTabItem != null)
			{
				Logger.Info("启动WebView2 Cookie和LocalStorage清理...");
				Task clearTask = webViewTabItem.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.LocalStorage | CoreWebView2BrowsingDataKinds.Cookies);
				if (await Task.WhenAny(new Task[2]
				{
					clearTask,
					Task.Delay(3000)
				}) == clearTask)
				{
					await clearTask;
					Logger.Info("WebView2 Cookie和LocalStorage清理完成");
					return;
				}
				Logger.Warn("WebView2 Cookie和LocalStorage清理超过3秒，继续退出");
				clearTask.ContinueWith((Task t) =>
				{
					if (t.IsFaulted)
					{
						Logger.Warn($"WebView2 Cookie和LocalStorage清理失败: {t.Exception}");
					}
				}, TaskScheduler.Default);
			}
			else
			{
				Logger.Info("没有可用的WebView2实例用于清理Cookie和LocalStorage");
			}
		}
		catch (Exception ex)
		{
			Logger.Error("启动清理WebView2 Cookie和LocalStorage时发生异常: " + ex.Message, ex);
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			UnsubscribeEvents();
			_customTitleBarManager?.Detach();
			CleanupTabItems();
			_disposed = true;
			GC.SuppressFinalize(this);
		}
	}
}
