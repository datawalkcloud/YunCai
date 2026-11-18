using System;
using System.Windows;
using HandyControl.Controls;

namespace TaxOfficeClient.Tools;

public static class MessageBoxHelper
{
	public static MessageBoxResult Warning(string message, string title = "警告")
	{
		return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
	}

	public static MessageBoxResult Error(string message, string title = "错误")
	{
		return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Hand);
	}

	public static MessageBoxResult Confirm(string message, string title = "确认")
	{
		return Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
	}

	private static MessageBoxResult Show(string message, string title, MessageBoxButton button, MessageBoxImage icon)
	{
		try
		{
			return HandyControl.Controls.MessageBox.Show(message, title, button, icon);
		}
		catch (Exception ex)
		{
			Logger.Error("显示消息框时发生错误: " + ex.Message);
			return System.Windows.MessageBox.Show(message, title, button, icon);
		}
	}
}
