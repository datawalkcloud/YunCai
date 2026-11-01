using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TaxOfficeClient.Manager;

public static class GuidManager
{
	private static readonly string GuidFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "nmyswzs_appGuid.txt");

	public static readonly string RunId = Guid.NewGuid().ToString("N").Substring(0, 8);

	private static string _appGuid;

	public static string AppGuid
	{
		get
		{
			if (_appGuid == null)
			{
				if (File.Exists(GuidFilePath))
				{
					string text = File.ReadAllText(GuidFilePath).Trim();
					if (IsValidGuid(text))
					{
						_appGuid = text;
					}
					else
					{
						_appGuid = Guid.NewGuid().ToString("N").Substring(0, 12);
						File.WriteAllText(GuidFilePath, _appGuid);
					}
				}
				else
				{
					_appGuid = Guid.NewGuid().ToString("N").Substring(0, 12);
					File.WriteAllText(GuidFilePath, _appGuid);
				}
			}
			return _appGuid;
		}
	}

	public static string AppVersion => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

	private static bool IsValidGuid(string content)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			return false;
		}
		if (content.Length == 12)
		{
			return Regex.IsMatch(content, "^[0-9a-fA-F]{12}$");
		}
		return false;
	}
}
