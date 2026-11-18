using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaxOfficeClient.Manager;

public class CustomTitleBarManager(Window window, FrameworkElement titleBar, Button minimizeButton, Button maximizeRestoreButton, Button closeButton)
{
	public struct Point
	{
		public int X;

		public int Y;
	}

	public struct MinMaxInfo
	{
		public Point ptReserved;

		public Point ptMaxSize;

		public Point ptMaxPosition;

		public Point ptMinTrackSize;

		public Point ptMaxTrackSize;
	}

	private struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct MonitorInfoEx
	{
		public int Size;

		public Rect Monitor;

		public Rect WorkArea;

		public uint Flags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string DeviceName;
	}

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int GetDpiForMonitorDelegate(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

	private struct Osversioninfoex
	{
		public uint dwOSVersionInfoSize;

		public uint dwMajorVersion;

		public uint dwMinorVersion;

		public uint dwBuildNumber;

		public uint dwPlatformId;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szCSDVersion;

		public ushort wServicePackMajor;

		public ushort wServicePackMinor;

		public ushort wSuiteMask;

		public byte wProductType;

		public byte wReserved;
	}

	private const uint MONITOR_DEFAULTTONEAREST = 2u;

	private const int WM_NC_HIT_TEST = 132;

	private const int WM_GET_MIN_MAX_INFO = 36;

	private const int HT_LEFT = 10;

	private const int HT_RIGHT = 11;

	private const int HT_TOP = 12;

	private const int HT_TOP_LEFT = 13;

	private const int HT_TOP_RIGHT = 14;

	private const int HT_BOTTOM = 15;

	private const int HT_BOTTOM_LEFT = 16;

	private const int HT_BOTTOM_RIGHT = 17;

	private bool _isDragging;

	private IntPtr _handle;

	private EventHandler _stateChangedHandler;

	private RoutedEventHandler _loadedHandler;

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out Point lpPoint);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(Point pt, uint dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

	public void Attach()
	{
		window.SourceInitialized += OnSourceInitialized;
		titleBar.MouseLeftButtonDown += OnTitleBarMouseLeftButtonDown;
		titleBar.MouseMove += OnTitleBarMouseMove;
		titleBar.MouseLeftButtonUp += OnTitleBarMouseLeftButtonUp;
		titleBar.MouseLeave += OnTitleBarMouseLeave;
		minimizeButton.Click += OnMinimizeButtonClick;
		maximizeRestoreButton.Click += OnMaximizeRestoreButtonClick;
		closeButton.Click += OnCloseButtonClick;
		_stateChangedHandler = (object? _, EventArgs _) =>
		{
			UpdateMaximizeRestoreButton();
		};
		_loadedHandler = (object _, RoutedEventArgs _) =>
		{
			UpdateMaximizeRestoreButton();
		};
		window.StateChanged += _stateChangedHandler;
		window.Loaded += _loadedHandler;
	}

	public void Detach()
	{
		if (_handle != IntPtr.Zero)
		{
			HwndSource.FromHwnd(_handle)?.RemoveHook(WindowProcedure);
		}
		window.SourceInitialized -= OnSourceInitialized;
		titleBar.MouseLeftButtonDown -= OnTitleBarMouseLeftButtonDown;
		titleBar.MouseMove -= OnTitleBarMouseMove;
		titleBar.MouseLeftButtonUp -= OnTitleBarMouseLeftButtonUp;
		titleBar.MouseLeave -= OnTitleBarMouseLeave;
		minimizeButton.Click -= OnMinimizeButtonClick;
		maximizeRestoreButton.Click -= OnMaximizeRestoreButtonClick;
		closeButton.Click -= OnCloseButtonClick;
		window.StateChanged -= _stateChangedHandler;
		window.Loaded -= _loadedHandler;
	}

	private void OnSourceInitialized(object sender, EventArgs e)
	{
		_handle = new WindowInteropHelper(window).Handle;
		HwndSource.FromHwnd(_handle)?.AddHook(WindowProcedure);
	}

	private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount == 2)
		{
			ToggleMaximizeRestore();
			e.Handled = true;
			return;
		}
		if (window.WindowState != WindowState.Maximized)
		{
			try
			{
				window.DragMove();
				return;
			}
			catch (InvalidOperationException)
			{
				return;
			}
		}
		_isDragging = true;
	}

	private void OnTitleBarMouseMove(object sender, MouseEventArgs e)
	{
		if (!_isDragging || e.LeftButton != MouseButtonState.Pressed || window.WindowState != WindowState.Maximized)
		{
			return;
		}
		_isDragging = false;
		GetCursorPos(out var lpPoint);
		IntPtr hMonitor = MonitorFromPoint(lpPoint, 2u);
		DpiScale dpiForMonitor = GetDpiForMonitor(hMonitor);
		double num = e.GetPosition(window).X / window.ActualWidth;
		double y = e.GetPosition(window).Y;
		window.WindowState = WindowState.Normal;
		window.Left = (double)lpPoint.X / dpiForMonitor.DpiScaleX - window.RestoreBounds.Width * num;
		window.Top = (double)lpPoint.Y / dpiForMonitor.DpiScaleY - y;
		try
		{
			window.DragMove();
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void OnTitleBarMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		_isDragging = false;
	}

	private void OnTitleBarMouseLeave(object sender, MouseEventArgs e)
	{
		_isDragging = false;
	}

	private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
	{
		window.WindowState = WindowState.Minimized;
	}

	private void OnMaximizeRestoreButtonClick(object sender, RoutedEventArgs e)
	{
		ToggleMaximizeRestore();
	}

	private void OnCloseButtonClick(object sender, RoutedEventArgs e)
	{
		window.Close();
	}

	private void ToggleMaximizeRestore()
	{
		window.WindowState = ((window.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void UpdateMaximizeRestoreButton()
	{
		if (!(maximizeRestoreButton?.Content is Image image))
		{
			return;
		}
		try
		{
			if (window.WindowState == WindowState.Maximized)
			{
				image.Source = new BitmapImage(new Uri("/Resources/Icons/restore.png", UriKind.Relative));
				maximizeRestoreButton.ToolTip = "还原";
			}
			else
			{
				image.Source = new BitmapImage(new Uri("/Resources/Icons/maximize.png", UriKind.Relative));
				maximizeRestoreButton.ToolTip = "最大化";
			}
		}
		catch
		{
		}
	}

	private DpiScale GetDpiForMonitor(IntPtr hMonitor)
	{
		if (IsWindows81OrGreater())
		{
			try
			{
				IntPtr intPtr = LoadLibrary("shcore.dll");
				if (intPtr != IntPtr.Zero)
				{
					IntPtr procAddress = GetProcAddress(intPtr, "GetDpiForMonitor");
					if (procAddress != IntPtr.Zero)
					{
						int num = Marshal.GetDelegateForFunctionPointer<GetDpiForMonitorDelegate>(procAddress)(hMonitor, 0, out var dpiX, out var dpiY);
						FreeLibrary(intPtr);
						if (num == 0)
						{
							return new DpiScale((double)dpiX / 96.0, (double)dpiY / 96.0);
						}
					}
					else
					{
						FreeLibrary(intPtr);
					}
				}
			}
			catch
			{
			}
		}
		return VisualTreeHelper.GetDpi(window);
	}

	private static bool IsWindows81OrGreater()
	{
		Osversioninfoex versionInfo = new Osversioninfoex
		{
			dwOSVersionInfoSize = (uint)Marshal.SizeOf<Osversioninfoex>()
		};
		if (RtlGetVersion(ref versionInfo) == 0)
		{
			if (versionInfo.dwMajorVersion <= 6)
			{
				if (versionInfo.dwMajorVersion == 6)
				{
					return versionInfo.dwMinorVersion >= 3;
				}
				return false;
			}
			return true;
		}
		if (Environment.OSVersion.Version.Major <= 6)
		{
			if (Environment.OSVersion.Version.Major == 6)
			{
				return Environment.OSVersion.Version.Minor >= 3;
			}
			return false;
		}
		return true;
	}

	private IntPtr WindowProcedure(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		switch (msg)
		{
		case 36:
			HandleGetMinMaxInfo(hwnd, lParam, ref handled);
			break;
		case 132:
			return HandleNcHitTest(lParam, ref handled);
		}
		return IntPtr.Zero;
	}

	private void HandleGetMinMaxInfo(IntPtr hwnd, IntPtr lParam, ref bool handled)
	{
		try
		{
			MinMaxInfo structure = Marshal.PtrToStructure<MinMaxInfo>(lParam);
			IntPtr hMonitor = MonitorFromWindow(hwnd, 2u);
			MonitorInfoEx lpmi = new MonitorInfoEx
			{
				Size = Marshal.SizeOf(typeof(MonitorInfoEx))
			};
			if (GetMonitorInfo(hMonitor, ref lpmi))
			{
				Rect workArea = lpmi.WorkArea;
				Rect monitor = lpmi.Monitor;
				structure.ptMaxPosition.X = workArea.Left - monitor.Left;
				structure.ptMaxPosition.Y = workArea.Top - monitor.Top;
				structure.ptMaxSize.X = workArea.Right - workArea.Left;
				structure.ptMaxSize.Y = workArea.Bottom - workArea.Top;
				DpiScale dpiForMonitor = GetDpiForMonitor(hMonitor);
				structure.ptMinTrackSize.X = (int)(window.MinWidth * dpiForMonitor.DpiScaleX);
				structure.ptMinTrackSize.Y = (int)(window.MinHeight * dpiForMonitor.DpiScaleY);
				Marshal.StructureToPtr(structure, lParam, fDeleteOld: true);
				handled = true;
			}
		}
		catch
		{
		}
	}

	private IntPtr HandleNcHitTest(IntPtr lParam, ref bool handled)
	{
		if (window.WindowState == WindowState.Maximized)
		{
			return IntPtr.Zero;
		}
		System.Windows.Point point = new System.Windows.Point((short)(lParam.ToInt32() & 0xFFFF), (short)(lParam.ToInt32() >> 16));
		System.Windows.Point point2 = window.PointFromScreen(point);
		bool flag = point2.Y <= 8.0;
		bool flag2 = point2.Y >= window.ActualHeight - 8.0;
		bool flag3 = point2.X <= 8.0;
		bool flag4 = point2.X >= window.ActualWidth - 8.0;
		handled = true;
		if (flag & flag3)
		{
			return new IntPtr(13);
		}
		if (flag & flag4)
		{
			return new IntPtr(14);
		}
		if (flag2 & flag3)
		{
			return new IntPtr(16);
		}
		if (flag2 & flag4)
		{
			return new IntPtr(17);
		}
		if (flag)
		{
			return new IntPtr(12);
		}
		if (flag2)
		{
			return new IntPtr(15);
		}
		if (flag3)
		{
			return new IntPtr(10);
		}
		if (flag4)
		{
			return new IntPtr(11);
		}
		handled = false;
		return IntPtr.Zero;
	}

	[DllImport("ntdll.dll", SetLastError = true)]
	private static extern int RtlGetVersion(ref Osversioninfoex versionInfo);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr LoadLibrary(string lpFileName);

	[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
	private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool FreeLibrary(IntPtr hModule);
}
