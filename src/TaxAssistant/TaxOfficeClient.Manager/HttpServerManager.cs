using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TaxOfficeClient.Model.Manager;
using TaxOfficeClient.Tools;

namespace TaxOfficeClient.Manager;

public sealed class HttpServerManager : IDisposable
{
	private HttpListener _httpListener;

	private CancellationTokenSource _cancellationTokenSource;

	private Task _listenerTask;

	private bool _disposed;

	private readonly object _lockObject = new object();

	public Task StartAsync(int port)
	{
		lock (_lockObject)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException("HttpServerManager");
			}
			_httpListener = new HttpListener();
			_httpListener.Prefixes.Add($"http://localhost:{port}/");
			_cancellationTokenSource = new CancellationTokenSource();
			_httpListener.Start();
			_listenerTask = Task.Run(() => HandleRequestsAsync(_cancellationTokenSource.Token));
			Logger.Info($"HTTP服务器已启动，监听端口: {port}");
			return Task.CompletedTask;
		}
	}

	private async Task HandleRequestsAsync(CancellationToken cancellationToken)
	{
		_ = 1;
		try
		{
			while (!cancellationToken.IsCancellationRequested && (_httpListener?.IsListening ?? false))
			{
				try
				{
					HttpListenerContext context = await _httpListener.GetContextAsync();
					Task.Run(() => ProcessRequestAsync(context), cancellationToken);
				}
				catch (Exception ex) when (cancellationToken.IsCancellationRequested || ex is ObjectDisposedException)
				{
					break;
				}
				catch (Exception ex2)
				{
					Logger.Warn("HTTP监听器异常: " + ex2.Message);
					await Task.Delay(1000, cancellationToken);
				}
			}
		}
		catch (OperationCanceledException)
		{
			Logger.Info("HTTP监听任务被取消");
		}
		catch (Exception ex4)
		{
			Logger.Error("HTTP监听任务发生未预期异常: " + ex4.Message);
		}
		Logger.Info("HTTP监听任务已退出");
	}

	private static async Task ProcessRequestAsync(HttpListenerContext context)
	{
		try
		{
			HttpListenerRequest request = context.Request;
			HttpListenerResponse response = context.Response;
			ApplyCors(request, response);
			string path = request.Url?.AbsolutePath ?? string.Empty;
			string method = request.HttpMethod ?? string.Empty;

			// https 公网页访问 localhost 会先发 PNA 预检，必须应答 204 并带私有网络头。
			if (method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
			{
				response.StatusCode = 204;
				response.Close();
				return;
			}

			if (IsPath(path, "/tax-login/start") && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
			{
				TaxLoginRequest loginRequest = await ReadJsonBodyAsync<TaxLoginRequest>(request);
				TaxLoginStatus result = await TaxLoginService.Instance.StartAsync(loginRequest);
				await SendJsonResponse(response, result);
				return;
			}
			if (IsPath(path, "/tax-login/verify") && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
			{
				TaxLoginVerifyRequest verifyRequest = await ReadJsonBodyAsync<TaxLoginVerifyRequest>(request);
				TaxLoginStatus result = await TaxLoginService.Instance.VerifyAsync(verifyRequest);
				await SendJsonResponse(response, result);
				return;
			}
			if (IsPath(path, "/tax-login/status") && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
			{
				TaxLoginStatus result = TaxLoginService.Instance.GetStatus(request.QueryString["sessionId"]);
				await SendJsonResponse(response, result);
				return;
			}

			// 页面每次检测都会打 /ping，不写日志免得刷屏。
			bool isHealthCheck = IsPath(path, "/ping");
			if (!isHealthCheck)
			{
				Logger.Info($"收到HTTP请求: {method} {request.Url}");
			}
			if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
			{
				await SendSuccessResponse(response, !isHealthCheck);
			}
			else
			{
				SendErrorResponse(response, 405, "Method Not Allowed");
			}
		}
		catch (JsonException ex)
		{
			Logger.Warn("本机接口请求体不是合法JSON: " + ex.Message);
			try
			{
				await SendJsonResponse(context.Response, new TaxLoginStatus
				{
					State = TaxLoginStates.Failed,
					Message = "请求格式错误，请回到网页重试。"
				}, 400);
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			Logger.Error("处理HTTP请求时发生异常: " + ex.Message);
			try
			{
				SendErrorResponse(context.Response, 500, "Internal Server Error");
			}
			catch
			{
			}
		}
	}

	private static bool IsPath(string path, string expected)
	{
		return path.Equals(expected, StringComparison.OrdinalIgnoreCase);
	}

	private static void ApplyCors(HttpListenerRequest request, HttpListenerResponse response)
	{
		response.Headers["Access-Control-Allow-Origin"] = "*";
		response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
		response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
		response.Headers["Access-Control-Max-Age"] = "600";
		string privateNetwork = request.Headers["Access-Control-Request-Private-Network"];
		if (privateNetwork != null && privateNetwork.Equals("true", StringComparison.OrdinalIgnoreCase))
		{
			response.Headers["Access-Control-Allow-Private-Network"] = "true";
		}
	}

	private static async Task<T> ReadJsonBodyAsync<T>(HttpListenerRequest request) where T : new()
	{
		if (request.ContentLength64 > 65536)
		{
			throw new InvalidOperationException("请求体过大。");
		}
		using StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
		string body = await reader.ReadToEndAsync();
		if (string.IsNullOrWhiteSpace(body))
		{
			return new T();
		}
		return JsonConvert.DeserializeObject<T>(body) ?? new T();
	}

	private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
	{
		ContractResolver = new CamelCasePropertyNamesContractResolver()
	};

	private static async Task SendJsonResponse(HttpListenerResponse response, object payload, int statusCode = 200)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload, JsonSettings));
		response.StatusCode = statusCode;
		response.ContentType = "application/json; charset=utf-8";
		response.ContentLength64 = bytes.Length;
		response.Headers["Cache-Control"] = "no-store";
		await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
		response.OutputStream.Close();
	}

	private static async Task SendSuccessResponse(HttpListenerResponse response, bool logResponse)
	{
		byte[] bytes = Encoding.UTF8.GetBytes("C4D038B4BED09FDB1471EF51EC3A32CD");
		response.ContentLength64 = bytes.Length;
		response.ContentType = "text/plain; charset=utf-8";
		response.StatusCode = 200;
		await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
		response.OutputStream.Close();
		if (logResponse)
		{
			Logger.Info("已响应GET请求，返回: C4D038B4BED09FDB1471EF51EC3A32CD");
		}
	}

	private static void SendErrorResponse(HttpListenerResponse response, int statusCode, string statusDescription)
	{
		try
		{
			response.StatusCode = statusCode;
			response.StatusDescription = statusDescription;
			response.Close();
		}
		catch (Exception ex)
		{
			Logger.Warn("发送错误响应时发生异常: " + ex.Message);
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		lock (_lockObject)
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
		}
		Logger.Info("开始释放HTTP服务器资源");
		try
		{
			_cancellationTokenSource?.Cancel();
		}
		catch (Exception ex)
		{
			Logger.Warn("取消HTTP服务器令牌时发生异常: " + ex.Message);
		}
		try
		{
			_httpListener?.Stop();
		}
		catch (Exception ex2)
		{
			Logger.Warn("停止HttpListener时发生异常: " + ex2.Message);
		}
		try
		{
			_listenerTask?.Wait(TimeSpan.FromSeconds(3.0));
		}
		catch (Exception ex3)
		{
			Logger.Warn("等待HTTP监听任务完成时发生异常: " + ex3.Message);
		}
		try
		{
			_cancellationTokenSource?.Dispose();
			_httpListener?.Close();
			_listenerTask?.Dispose();
		}
		catch (Exception ex4)
		{
			Logger.Warn("释放HTTP服务器资源时发生异常: " + ex4.Message);
		}
		Logger.Info("HTTP服务器资源释放完成");
	}

	~HttpServerManager()
	{
		Dispose();
	}
}
