using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace TaxOfficeClient.Tools;

/// <summary>
/// 把 kuaiji: 自定义协议注册到当前用户（HKCU，无需管理员）。
/// 注册后浏览器访问 kuaiji:tax?... 会以完整 URL 作为参数唤起本程序。
/// </summary>
[SupportedOSPlatform("windows")]
public static class ProtocolRegistrar
{
	public const string Scheme = "kuaiji";

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
				Logger.Info("已注册协议 kuaiji: -> " + command);
			}
		}
		catch (Exception ex)
		{
			Logger.Warn("注册 kuaiji 协议失败: " + ex.Message, ex);
		}
	}
}
