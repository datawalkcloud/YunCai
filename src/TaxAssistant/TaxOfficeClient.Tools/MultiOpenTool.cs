using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace TaxOfficeClient.Tools;

public sealed class MultiOpenTool : IDisposable
{
	private const string PIPE_NAME = "YunCaiTaxAssistantPipe";

	private const int CONNECTION_TIMEOUT = 5000;

	private const int MESSAGE_READ_TIMEOUT = 5000;

	private const int MAX_INSTANCES = 3;

	private const int RETRY_DELAY_NORMAL = 200;

	private const int RETRY_DELAY_RESOURCE_BUSY = 500;

	private const string PIPE_BUSY_ERROR_MESSAGE = "所有的管道范例都在使用中";

	private CancellationTokenSource _serverCts;

	private bool _disposed;

	private readonly object _disposeLock = new object();

	private bool _isServerRunning;

	public static bool SendArgsToRunningInstance(string args)
	{
		if (string.IsNullOrWhiteSpace(args))
		{
			Logger.Warn("尝试发送空参数到运行实例");
			return false;
		}
		try
		{
			Logger.Info("尝试连接到运行实例，发送参数：" + args);
			using NamedPipeClientStream client = CreateNamedPipeClient();
			if (!TryConnectClient(client))
			{
				Logger.Warn("无法连接到运行实例");
				return false;
			}
			SendMessageToClient(client, args);
			Logger.Info("参数发送成功");
			return true;
		}
		catch (Exception ex)
		{
			Logger.Error("向运行实例发送参数时发生异常：" + ex.Message, ex);
			return false;
		}
	}

	private static NamedPipeClientStream CreateNamedPipeClient()
	{
		return new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out, PipeOptions.None);
	}

	private static bool TryConnectClient(NamedPipeClientStream client)
	{
		try
		{
			client.Connect(5000);
			return client.IsConnected;
		}
		catch (TimeoutException)
		{
			Logger.Warn("连接运行实例超时");
			return false;
		}
		catch (IOException ex2)
		{
			Logger.Warn("连接运行实例失败（IO错误）: " + ex2.Message);
			return false;
		}
		catch (UnauthorizedAccessException ex3)
		{
			Logger.Warn("连接运行实例失败（权限不足）: " + ex3.Message);
			return false;
		}
		catch (Exception ex4)
		{
			Logger.Warn("连接运行实例失败: " + ex4.Message);
			return false;
		}
	}

	private static void SendMessageToClient(NamedPipeClientStream client, string message)
	{
		using StreamWriter streamWriter = new StreamWriter(client);
		streamWriter.AutoFlush = true;
		streamWriter.WriteLine(message);
	}

	public void StartNamedPipeServer(Action<string> onUrlReceived)
	{
		if (onUrlReceived == null)
		{
			throw new ArgumentNullException("onUrlReceived");
		}
		if (_disposed)
		{
			throw new ObjectDisposedException("MultiOpenTool");
		}
		lock (_disposeLock)
		{
			if (_isServerRunning)
			{
				Logger.Info("管道服务器已在运行，先停止旧实例");
				StopNamedPipeServer();
			}
			_serverCts = new CancellationTokenSource();
			_isServerRunning = true;
			StartServerLoop(onUrlReceived, _serverCts.Token);
			Logger.Info("管道服务器已启动");
		}
	}

	private void StopNamedPipeServer()
	{
		lock (_disposeLock)
		{
			if (!_isServerRunning || _serverCts == null)
			{
				return;
			}
			try
			{
				Logger.Info("正在停止管道服务器...");
				_serverCts.Cancel();
				_serverCts.Dispose();
				_serverCts = null;
				_isServerRunning = false;
				Logger.Info("管道服务器已停止");
			}
			catch (Exception ex)
			{
				Logger.Error("停止管道服务器时发生异常: " + ex.Message, ex);
			}
		}
	}

	private void StartServerLoop(Action<string> onUrlReceived, CancellationToken token)
	{
		Task.Run(async () =>
		{
			Logger.Info("管道服务器循环已启动");
			while (!token.IsCancellationRequested)
			{
				await ProcessServerIterationAsync(onUrlReceived, token);
			}
			Logger.Info("管道服务器循环已退出");
		}, token);
	}

	private async Task ProcessServerIterationAsync(Action<string> onUrlReceived, CancellationToken token)
	{
		NamedPipeServerStream server = null;
		try
		{
			server = CreateNamedPipeServer();
			await WaitForConnectionAsync(server, token);
			NamedPipeServerStream pipeServer = server;
			server = null;
			ProcessConnectionAsync(pipeServer, onUrlReceived, token);
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
		}
		catch (IOException ex2)
		{
			await HandleIoException(ex2, token);
		}
		catch (Exception ex3)
		{
			Logger.Warn("管道服务器发生异常: " + ex3.Message);
			await Task.Delay(200, token);
		}
		finally
		{
			await CleanupServerStreamAsync(server);
		}
	}

	private static NamedPipeServerStream CreateNamedPipeServer()
	{
		return new NamedPipeServerStream(PIPE_NAME, PipeDirection.In, 3, PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
	}

	private static async Task WaitForConnectionAsync(NamedPipeServerStream server, CancellationToken token)
	{
		Logger.Info("管道服务器等待客户端连接...");
		await server.WaitForConnectionAsync(token);
		Logger.Info("管道服务器已接受客户端连接");
	}

	private static async Task HandleIoException(IOException ex, CancellationToken token)
	{
		if (ex.Message.Contains("所有的管道范例都在使用中"))
		{
			Logger.Warn("管道资源暂时不可用: " + ex.Message);
			await Task.Delay(500, token);
		}
		else
		{
			Logger.Warn("管道服务器IO异常: " + ex.Message);
			await Task.Delay(200, token);
		}
	}

	private static async Task CleanupServerStreamAsync(NamedPipeServerStream server)
	{
		if (server == null)
		{
			return;
		}
		try
		{
			if (server.IsConnected)
			{
				server.Disconnect();
			}
			await server.DisposeAsync();
		}
		catch (Exception ex)
		{
			Logger.Warn("清理管道服务器资源时发生异常: " + ex.Message);
		}
	}

	private static async Task ProcessConnectionAsync(NamedPipeServerStream pipeServer, Action<string> onUrlReceived, CancellationToken token)
	{
		_ = 2;
		try
		{
			string args;
			await using (pipeServer)
			{
				using CancellationTokenSource readCts = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[1] { token });
				readCts.CancelAfter(5000);
				try
				{
					args = await ReadMessageAsync(pipeServer, readCts.Token);
				}
				catch (OperationCanceledException innerException) when (!token.IsCancellationRequested)
				{
					throw new TimeoutException("读取管道消息超时，连接已关闭", innerException);
				}
			}
			token.ThrowIfCancellationRequested();
			if (!string.IsNullOrWhiteSpace(args))
			{
				await InvokeCallbackOnUIThreadAsync(args, onUrlReceived, token);
			}
			else
			{
				Logger.Warn("接收到空消息");
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			Logger.Info("连接处理已取消");
		}
		catch (Exception ex2)
		{
			Logger.Warn("处理管道连接时发生异常: " + ex2.Message);
		}
	}

	private static async Task<string> ReadMessageAsync(NamedPipeServerStream pipeServer, CancellationToken token)
	{
		using StreamReader reader = new StreamReader(pipeServer, null, detectEncodingFromByteOrderMarks: true, -1, leaveOpen: true);
		StringBuilder message = new StringBuilder();
		char[] character = new char[1];
		while (await reader.ReadAsync(character.AsMemory(), token) != 0)
		{
			if (character[0] == '\r')
			{
				await reader.ReadAsync(character.AsMemory(), token);
				return message.ToString();
			}
			if (character[0] == '\n')
			{
				return message.ToString();
			}
			message.Append(character[0]);
		}
		return (message.Length == 0) ? null : message.ToString();
	}

	private static async Task InvokeCallbackOnUIThreadAsync(string args, Action<string> onUrlReceived, CancellationToken token)
	{
		Application current = Application.Current;
		if (current == null)
		{
			Logger.Warn("Application.Current为null，无法在UI线程执行回调");
			return;
		}
		await current.Dispatcher.InvokeAsync(() =>
		{
			try
			{
				Logger.Info("接收到参数：" + args);
				onUrlReceived?.Invoke(args);
			}
			catch (Exception ex)
			{
				Logger.Error("执行回调时发生异常: " + ex.Message, ex);
			}
		}, DispatcherPriority.Normal, token);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		lock (_disposeLock)
		{
			if (!_disposed)
			{
				StopNamedPipeServer();
				_disposed = true;
			}
		}
	}
}
