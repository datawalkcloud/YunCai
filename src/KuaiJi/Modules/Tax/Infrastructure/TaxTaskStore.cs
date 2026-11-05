using KuaiJi.Modules.Tax.Application;
using KuaiJi.Modules.Tax.Domain;
using KuaiJi.SharedKernel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KuaiJi.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 任务中心的写入与查询。税局操作跑在无头浏览器上、一部分还发生在请求之外，
    /// 所以和 <see cref="TaxPortalSessionStore"/> 一样用 IServiceScopeFactory 自取 DbContext。
    /// 台账是旁路能力：写失败只记日志，不能把取数本身带崩。
    /// </summary>
    public sealed class TaxTaskStore
    {
        /// <summary>税局业务只在中国大陆，日期区间一律按东八区自然日切，与服务器的系统时区无关。</summary>
        private static readonly TimeSpan PortalOffset = TimeSpan.FromHours(8);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TaxTaskStore> _logger;

        public TaxTaskStore(IServiceScopeFactory scopeFactory, ILogger<TaxTaskStore> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>落一条任务记录；period 省略时取提交时所在的自然月。</summary>
        public async Task RecordAsync(
            TaxPortalSessionKey key,
            string userName,
            string title,
            bool success,
            string? suggestion,
            DateTimeOffset submittedAt,
            int? period = null,
            CancellationToken cancellationToken = default)
        {
            var finishedAt = DateTimeOffset.UtcNow;
            var local = submittedAt.ToOffset(PortalOffset);
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.TaxTasks.Add(new TaxTaskRecord(
                    key.AccountSetId,
                    key.UserId,
                    userName,
                    period ?? local.Year * 100 + local.Month,
                    title,
                    success ? TaxTaskStatus.Success : TaxTaskStatus.Failed,
                    suggestion,
                    submittedAt,
                    finishedAt));
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("[任务中心] 记录「{Title}」失败：{Message}", title, ex.Message);
            }
        }

        /// <summary>
        /// 挂一条「进行中」的任务：后台任务在提交时就先落一行，办完再回填。
        /// id 由调用方（任务执行器）生成，页面拿它轮询，所以台账与内存里的任务必须是同一个标识。
        /// period 省略时取提交时所在的自然月。
        /// </summary>
        public async Task StartAsync(
            Guid id,
            TaxPortalSessionKey key,
            string userName,
            string title,
            DateTimeOffset submittedAt,
            int? period = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var local = submittedAt.ToOffset(PortalOffset);
                db.TaxTasks.Add(new TaxTaskRecord(
                    key.AccountSetId,
                    key.UserId,
                    userName,
                    period ?? local.Year * 100 + local.Month,
                    title,
                    TaxTaskStatus.Running,
                    null,
                    submittedAt,
                    submittedAt,
                    id));
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("[任务中心] 登记「{Title}」失败：{Message}", title, ex.Message);
            }
        }

        /// <summary>回填任务结果：状态、结束时间与失败时的解决建议。</summary>
        public async Task CompleteAsync(
            Guid id,
            bool success,
            string? suggestion,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var task = await db.TaxTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
                if (task is null)
                {
                    return;
                }

                task.Complete(success, suggestion, DateTimeOffset.UtcNow);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("[任务中心] 回填任务 {TaskId} 失败：{Message}", id, ex.Message);
            }
        }

        /// <summary>
        /// 撤掉一条登记：任务提交时先落了「进行中」，但真正跑起来之前就被挡回来（税局没登录），
        /// 这种什么都没办的记录不该留在任务中心里。
        /// </summary>
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var task = await db.TaxTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
                if (task is null)
                {
                    return;
                }

                db.TaxTasks.Remove(task);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("[任务中心] 撤销任务 {TaskId} 失败：{Message}", id, ex.Message);
            }
        }

        /// <summary>按标识读本账套的一行；台账里查不到（进程重启丢掉的内存任务）时返回 null。</summary>
        public async Task<TaxTaskRowDto?> FindAsync(Guid id, Guid accountSetId, CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var task = await db.TaxTasks.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.AccountSetId == accountSetId, cancellationToken);
            return task is null ? null : ToRow(task);
        }

        /// <summary>
        /// 进程重启后，内存里的任务全没了，数据库里却可能还留着「进行中」。
        /// 启动时把这些行收口成失败，免得任务中心一直显示一个不会动的进行中。
        /// </summary>
        public async Task<int> AbandonRunningAsync(string suggestion, CancellationToken cancellationToken = default)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stale = await db.TaxTasks
                .Where(t => t.Status == TaxTaskStatus.Running)
                .ToListAsync(cancellationToken);
            foreach (var task in stale)
            {
                task.Complete(false, suggestion, DateTimeOffset.UtcNow);
            }

            if (stale.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            return stale.Count;
        }

        /// <summary>按提交时间区间（东八区自然日，含首尾）查询本账套的任务，时间倒序。</summary>
        public async Task<TaxTasksResultDto> QueryAsync(
            Guid accountSetId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var query = db.TaxTasks.AsNoTracking().Where(task => task.AccountSetId == accountSetId);

            if (from is { } start)
            {
                var startAt = DayStart(start);
                query = query.Where(task => task.SubmittedAt >= startAt);
            }

            if (to is { } end)
            {
                var afterEnd = DayStart(end.AddDays(1));
                query = query.Where(task => task.SubmittedAt < afterEnd);
            }

            var rows = await query
                .OrderByDescending(task => task.SubmittedAt)
                .ToListAsync(cancellationToken);

            return new TaxTasksResultDto
            {
                Total = rows.Count,
                Rows = rows.Select(ToRow).ToList(),
            };
        }

        /// <summary>台账行 → 任务中心的一行；进行中的任务还没有结束时间，回一个占位值让页面显示「—」。</summary>
        private static TaxTaskRowDto ToRow(TaxTaskRecord task) => new()
        {
            Key = task.Id.ToString("N"),
            Period = task.Period,
            Title = task.Title,
            Status = task.Status switch
            {
                TaxTaskStatus.Running => "running",
                TaxTaskStatus.Failed => "failed",
                _ => "success",
            },
            Suggestion = task.Suggestion,
            SubmittedAt = task.SubmittedAt,
            FinishedAt = task.Status == TaxTaskStatus.Running ? default : task.FinishedAt,
            Operator = task.UserName,
        };

        /// <summary>东八区自然日的零点。Npgsql 的 timestamptz 只接受 UTC 偏移的参数，故换算回 UTC 再比较。</summary>
        private static DateTimeOffset DayStart(DateOnly day)
            => new DateTimeOffset(day.Year, day.Month, day.Day, 0, 0, 0, PortalOffset).ToUniversalTime();
    }
}
