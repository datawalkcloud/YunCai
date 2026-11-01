using System;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using TaxOfficeClient.Tools;

namespace TaxOfficeClient.Manager;

public static class HttpClientManager
{
	private static readonly Lazy<HttpClient> _lazyHttpClient = new Lazy<HttpClient>(() =>
	{
		HttpClient httpClient = new HttpClient(new SocketsHttpHandler
		{
			PooledConnectionLifetime = TimeSpan.FromMinutes(15.0),
			SslOptions = new SslClientAuthenticationOptions
			{
				RemoteCertificateValidationCallback = (object _, X509Certificate _, X509Chain _, SslPolicyErrors sslPolicyErrors) =>
				{
					if (sslPolicyErrors != SslPolicyErrors.None)
					{
						Logger.Warn($"SSL证书验证警告: {sslPolicyErrors}");
					}
					return true;
				}
			}
		});
		httpClient.Timeout = TimeSpan.FromSeconds(30.0);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"TaxClient/{Assembly.GetEntryAssembly()?.GetName().Version} (DeviceId={GuidManager.AppGuid}; RunId={GuidManager.RunId})");
		return httpClient;
	});

	public static HttpClient Instance => _lazyHttpClient.Value;

	public static void Dispose()
	{
		if (_lazyHttpClient.IsValueCreated)
		{
			_lazyHttpClient.Value?.Dispose();
		}
	}
}
