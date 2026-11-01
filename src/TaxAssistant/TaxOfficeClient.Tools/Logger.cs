using System;
using System.IO;
using System.Text;

namespace TaxOfficeClient.Tools;

/// <summary>
/// 本地文件日志，写入 %LOCALAPPDATA%\Kuaiji\TaxAssistant\logs，仅保留最近 7 天。
/// 助手不再向任何外部服务上传日志。
/// </summary>
public static class Logger
{
	private static readonly object SyncRoot = new object();

	private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kuaiji", "TaxAssistant", "logs");

	private static bool _pruned;

	public static void Info(string message)
	{
		Write("INFO", message, 0, null);
	}

	public static void Info(string message, int areaCode, string uscc)
	{
		Write("INFO", message, areaCode, uscc);
	}

	public static void Http(string message, int areaCode, string uscc)
	{
		Write("HTTP", message, areaCode, uscc);
	}

	public static void Warn(string message)
	{
		Write("WARN", message, 0, null);
	}

	public static void Warn(string message, int areaCode, string uscc)
	{
		Write("WARN", message, areaCode, uscc);
	}

	public static void Warn(string message, Exception exception)
	{
		Write("WARN", message + Describe(exception), 0, null);
	}

	public static void Error(string message)
	{
		Write("ERROR", message, 0, null);
	}

	public static void Error(string message, Exception exception)
	{
		Write("ERROR", message + Describe(exception), 0, null);
	}

	public static void Error(string message, int areaCode, string uscc)
	{
		Write("ERROR", message, areaCode, uscc);
	}

	public static void Error(string message, Exception exception, int areaCode, string uscc)
	{
		Write("ERROR", message + Describe(exception), areaCode, uscc);
	}

	public static void Fatal(string message, Exception exception)
	{
		Write("FATAL", message + Describe(exception), 0, null);
	}

	private static string Describe(Exception exception)
	{
		return $"，HResult=0x{exception.HResult:X8}，报错：{exception}";
	}

	private static void Write(string level, string message, int areaCode, string uscc)
	{
		try
		{
			DateTime now = DateTime.Now;
			StringBuilder line = new StringBuilder();
			line.Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(" [").Append(level).Append("] ");
			if (areaCode != 0)
			{
				line.Append("地区 ").Append(areaCode).Append(' ');
			}
			if (!string.IsNullOrWhiteSpace(uscc))
			{
				line.Append("税号 ").Append(uscc).Append(' ');
			}
			line.Append(message).Append(Environment.NewLine);
			lock (SyncRoot)
			{
				Directory.CreateDirectory(LogDirectory);
				if (!_pruned)
				{
					_pruned = true;
					PruneExpiredLogs(now);
				}
				File.AppendAllText(Path.Combine(LogDirectory, "assistant-" + now.ToString("yyyyMMdd") + ".log"), line.ToString(), Encoding.UTF8);
			}
		}
		catch
		{
			// 写日志失败不能影响主流程
		}
	}

	private static void PruneExpiredLogs(DateTime now)
	{
		foreach (string file in Directory.GetFiles(LogDirectory, "assistant-*.log"))
		{
			try
			{
				if (now - File.GetLastWriteTime(file) > TimeSpan.FromDays(7.0))
				{
					File.Delete(file);
				}
			}
			catch
			{
				// 单个日志文件删不掉（被占用）不影响其他文件
			}
		}
	}
}
