using System.Collections.Concurrent;
using KuaiJi.Modules.Tax.Infrastructure;

namespace KuaiJi.Modules.Tax.Application
{
    /// <summary>
    /// 自然人电子税务局取数的后台执行器。
    ///
    /// 税局的每一步都要在无头浏览器里点菜单、等页面渲染，快则几秒、慢则一分钟以上，
    /// 挂在 HTTP 请求上容易被浏览器或反向代理掐断，用户也只能对着转圈的表格干等。
    /// 所以取数改成提交制：接口立刻返回任务标识并落一行「进行中」台账，真正的取数
    /// 在请求之外跑，跑完回填台账的状态，页面按标识轮询结果。
    /// 结果只留在进程内存里（重启即失效），台账仍然查得到。
    /// </summary>
    public sealed class TaxIitFetchJobRunner
    {
        /// <summary>单次取数在后台最多跑多久。税局页面卡死时靠它兜底，不让任务一直挂在「进行中」。</summary>
        private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(3);

        /// <summary>已完成的任务在内存里保留多久，够页面把结果取走。</summary>
        private static readonly TimeSpan ResultRetention = TimeSpan.FromMinutes(10);

        /// <summary>内存里最多同时留着多少个任务，超出时挤掉最老的、已结束的。</summary>
        private const int MaxJobs = 100;

        private readonly ConcurrentDictionary<Guid, Job> _jobs = new();
        private readonly TaxTaskStore _tasks;
        private readonly ILogger<TaxIitFetchJobRunner> _logger;

        public TaxIitFetchJobRunner(TaxTaskStore tasks, ILogger<TaxIitFetchJobRunner> logger)
        {
            _tasks = tasks;
            _logger = logger;
        }

        /// <summary>
        /// 提交一次取数：先落「进行中」台账，再把取数交给后台线程，立刻返回任务进展给页面。
        /// fetch 拿到的是与请求无关的取消令牌（只受 <see cref="JobTimeout"/> 控制），
        /// 所以页面关掉、连接断开都不会中断已经在跑的税局操作。
        /// </summary>
        public async Task<TaxIitJobDto> SubmitAsync(
            TaxPortalSessionKey key,
            string userName,
            string title,
            Func<CancellationToken, Task<IIitFetchResult>> fetch,
            CancellationToken cancellationToken)
        {
            var submittedAt = DateTimeOffset.UtcNow;
            var id = Guid.NewGuid();
            await _tasks.StartAsync(id, key, userName, title, submittedAt, cancellationToken: cancellationToken);

            Trim();
            var job = new Job(id, key, title, submittedAt);
            _jobs[id] = job;
            _ = Task.Run(() => RunAsync(job, fetch));
            return job.Snapshot();
        }

        /// <summary>页面轮询：内存里有这个任务就回进展和结果，没有就回落到台账那一行。</summary>
        public async Task<TaxIitJobDto> GetAsync(TaxPortalSessionKey key, Guid id, CancellationToken cancellationToken)
        {
            if (_jobs.TryGetValue(id, out var job) && job.Key == key)
            {
                return job.Snapshot();
            }

            var row = await _tasks.FindAsync(id, key.AccountSetId, cancellationToken);
            if (row is null)
            {
                return new TaxIitJobDto
                {
                    Key = id.ToString("N"),
                    Title = "个税取数",
                    Status = "failed",
                    Message = "这次任务已失效，请重新发起查询。",
                    SubmittedAt = DateTimeOffset.UtcNow,
                };
            }

            // 台账说成功、内存里却没有结果，只可能是进程重启或服务多实例：数据取不回来了。
            var lost = row.Status == "success";
            return new TaxIitJobDto
            {
                Key = row.Key,
                Title = row.Title,
                Status = row.Status,
                Message = lost ? "任务已完成，但结果已失效，请重新发起查询。" : row.Suggestion,
                SubmittedAt = row.SubmittedAt,
                FinishedAt = row.FinishedAt == default ? null : row.FinishedAt,
            };
        }

        /// <summary>服务重启后内存任务全丢，把台账里残留的「进行中」收口成失败。</summary>
        public async Task AbandonInterruptedAsync(CancellationToken cancellationToken = default)
        {
            var count = await _tasks.AbandonRunningAsync("服务已重启，这次取数任务中断，请重新发起查询。", cancellationToken);
            if (count > 0)
            {
                _logger.LogInformation("[个税取数·后台] 收口 {Count} 条重启前未完成任务", count);
            }
        }

        private async Task RunAsync(Job job, Func<CancellationToken, Task<IIitFetchResult>> fetch)
        {
            using var timeout = new CancellationTokenSource(JobTimeout);
            IIitFetchResult result;
            try
            {
                result = await fetch(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                result = new FailedFetchResult($"个税取数超过 {JobTimeout.TotalMinutes:0} 分钟仍未完成，已中止。请到任务中心查看这次任务。");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[个税取数·后台] 任务「{Title}」执行出错：{Message}", job.Title, ex.Message);
                result = new FailedFetchResult("个税取数失败，请稍后重试。");
            }

            job.Complete(result);
            if (result.NeedLogin)
            {
                // 连税局都没进去（登录态缺失或已过期），这一趟等于没办过，不该在任务中心留一行失败。
                // 内存快照仍然留着：页面轮询要凭 needLogin 弹出「个税申报登录」。
                await _tasks.DeleteAsync(job.Id);
                _logger.LogInformation("[个税取数·后台] 任务「{Title}」未执行（缺个税登录态），撤销台账记录", job.Title);
                return;
            }

            var success = result.Message is null;
            await _tasks.CompleteAsync(job.Id, success, result.Message);
            _logger.LogInformation(
                "[个税取数·后台] 任务「{Title}」{State}，行数={Rows}",
                job.Title,
                success ? "成功" : "失败",
                result.RowCount);
        }

        /// <summary>清掉内存里已经过期的结果，顺便给任务数量设个上限。</summary>
        private void Trim()
        {
            var cutoff = DateTimeOffset.UtcNow - ResultRetention;
            foreach (var (id, job) in _jobs)
            {
                if (job.FinishedAt is { } finished && finished < cutoff)
                {
                    _jobs.TryRemove(id, out _);
                }
            }

            if (_jobs.Count <= MaxJobs)
            {
                return;
            }

            foreach (var stale in _jobs.Values
                         .Where(j => j.FinishedAt is not null)
                         .OrderBy(j => j.FinishedAt)
                         .Take(_jobs.Count - MaxJobs))
            {
                _jobs.TryRemove(stale.Id, out _);
            }
        }

        /// <summary>取数抛异常或超时时的结果：没有行，只有给用户的说明。</summary>
        private sealed class FailedFetchResult : IIitFetchResult
        {
            public FailedFetchResult(string message) => Message = message;

            public int RowCount => 0;

            public bool NeedLogin => false;

            public string? Message { get; }
        }

        /// <summary>内存中的一个后台任务：提交时的信息不变，结束时补上结果。</summary>
        private sealed class Job(Guid id, TaxPortalSessionKey key, string title, DateTimeOffset submittedAt)
        {
            public Guid Id { get; } = id;

            public TaxPortalSessionKey Key { get; } = key;

            public string Title { get; } = title;

            public DateTimeOffset SubmittedAt { get; } = submittedAt;

            public IIitFetchResult? Result { get; private set; }

            public DateTimeOffset? FinishedAt { get; private set; }

            public void Complete(IIitFetchResult result)
            {
                Result = result;
                FinishedAt = DateTimeOffset.UtcNow;
            }

            public TaxIitJobDto Snapshot()
            {
                var result = Result;
                return new TaxIitJobDto
                {
                    Key = Id.ToString("N"),
                    Title = Title,
                    Status = result is null ? "running" : (result.Message is null ? "success" : "failed"),
                    NeedLogin = result?.NeedLogin ?? false,
                    Message = result?.Message,
                    Data = result is null || result.Message is not null ? null : result,
                    SubmittedAt = SubmittedAt,
                    FinishedAt = FinishedAt,
                };
            }
        }
    }
}
