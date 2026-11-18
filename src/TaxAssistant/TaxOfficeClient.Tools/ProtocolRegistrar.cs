using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace TaxOfficeClient.Tools;

/// <summary>
/// 把 yuncai: 自定义协议注册到当前用户（HKCU，无需管理员）。
/// 注册后浏览器访问 yuncai:tax?... 会以完整 URL 作为参数唤起本程序。
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProtocolRegistrar
{
	public const string Scheme = "yuncai";

	/// <summary>与安装包 [Tasks] autostart 写的值名保持一致，两边互相校正。</summary>
	private const string RunValueName = "YunCaiTaxAssistant";

	/// <summary>安装包默认目录 %LOCALAPPDATA%\云财税务助手，只有落在这里才允许写启动项。</summary>
	private const string InstallFolderName = "云财税务助手";

	public static void EnsureRegistered()
	{
		try
		{
			string exePath = Environment.ProcessPath;
			if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
			{
				Logger.Warn("无法确定助手可执行文件路径，跳过协议注册");
				return;
			}
			string command = "\"" + exePath + "\" \"%1\"";
			using RegistryKey schemeKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + Scheme);
			schemeKey.SetValue(null, "URL:云财税务助手");
			schemeKey.SetValue("URL Protocol", string.Empty);
			using RegistryKey commandKey = schemeKey.CreateSubKey("shell\\open\\command");
			if (!string.Equals(commandKey.GetValue(null) as string, command, StringComparison.OrdinalIgnoreCase))
			{
				commandKey.SetValue(null, command);
				Logger.Info("已注册协议 yuncai: -> " + command);
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("注册 yuncai 协议失败: " + ex.Message, ex);
		}
	}

	/// <summary>
	/// 校正当前用户的开机启动项，让助手常驻、网页随时探测得到本机端口。
	/// 只在可执行文件确实位于安装目录下时写入：从 obj\installer-stage 这类构建目录直接运行时
	/// 不能注册，否则目录被 clean 掉之后开机会去启动一个已经不存在的文件。
	/// </summary>
	public static void EnsureAutoStart()
	{
		try
		{
			string exePath = Environment.ProcessPath;
			if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
			{
				return;
			}
			string installDirectory = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				InstallFolderName);
			if (!string.Equals(Path.GetDirectoryName(exePath), installDirectory, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			string command = "\"" + exePath + "\"";
			using RegistryKey runKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
			if (!string.Equals(runKey.GetValue(RunValueName) as string, command, StringComparison.OrdinalIgnoreCase))
			{
				runKey.SetValue(RunValueName, command);
				Logger.Info("已写入开机启动项 " + RunValueName + " -> " + command);
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("写入开机启动项失败: " + ex.Message, ex);
		}
	}
}
