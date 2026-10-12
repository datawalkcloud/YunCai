using KuaiJi.Modules.Auditing.Domain;

namespace KuaiJi.Modules.Auditing.Application
{
    /// <summary>操作日志 DTO。</summary>
    public sealed class OperationLogDto
    {
        public Guid Id { get; init; }

        public string UserId { get; init; } = null!;

        public string UserName { get; init; } = null!;

        public string Type { get; init; } = null!;

        public string Module { get; init; } = null!;

        public string Log { get; init; } = null!;

        public DateTime Time { get; init; }

        public static OperationLogDto FromDomain(OperationLog log) => new()
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = log.UserName,
            Type = log.Type,
            Module = log.Module,
            Log = log.Log,
            Time = log.Time,
        };
    }

    /// <summary>操作日志查询请求。</summary>
    public sealed class OperationLogQuery
    {
        public DateTime? Start { get; init; }

        public DateTime? End { get; init; }

        public string? UserId { get; init; }

        public string? Keyword { get; init; }
    }
}
