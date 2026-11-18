namespace YunCai.SharedKernel.Infrastructure.Runtime;

/// <summary>
/// 运行时开关，对应配置节 <c>YunCai</c>。默认值面向生产：不做自动迁移，
/// 开发环境通过 appsettings.Development.json 放宽。
/// </summary>
public sealed class RuntimeOptions
{
    public const string SectionName = "YunCai";

    public DatabaseOptions Database { get; init; } = new();

    public NetworkingOptions Networking { get; init; } = new();

    public LoggingOptions Logging { get; init; } = new();
}

public sealed class DatabaseOptions
{
    /// <summary>
    /// 是否在进程启动时执行 EF Core 迁移。多实例部署必须由一次性任务
    /// （<c>dotnet YunCai.dll --migrate-only</c>）完成，不能让每个实例并发迁移。
    /// </summary>
    public bool AutoMigrate { get; init; }
}

public sealed class NetworkingOptions
{
    /// <summary>部署在 nginx 等反向代理后时解析 X-Forwarded-*，用于限流分区与 HTTPS 跳转。</summary>
    public bool TrustForwardedHeaders { get; init; }

    /// <summary>是否强制 HTTPS 跳转。TLS 未就位（或代理不透传 X-Forwarded-Proto）时关闭。</summary>
    public bool HttpsRedirection { get; init; } = true;

    /// <summary>非开发环境是否下发 HSTS。</summary>
    public bool UseHsts { get; init; } = true;
}

public sealed class LoggingOptions
{
    /// <summary>
    /// 滚动日志文件路径（支持通配符，如 <c>Logs/log-.txt</c>）。为空时只输出到控制台，
    /// 容器环境应由日志驱动收集 stdout。相对路径依赖工作目录，容器与系统服务里务必给绝对路径。
    /// </summary>
    public string? File { get; init; }
}
