using System.Collections.Concurrent;
using YunCai.Modules.Invoices.Domain;
using YunCai.Modules.Tax.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace YunCai.Modules.Tax.Application
{
    /// <summary>
    /// 「一键取票」的后台执行器：先在税局页面里查发票，再把结果写进发票池。
    ///
    /// 查询要在无头浏览器里点菜单、翻页，耗时不可控，挂在 HTTP 请求上容易被掐断，
    /// 所以和个税取数一样改成提交制：接口立刻返回任务标识并落一行「进行中」台账，
    /// 页面轮询到完成后刷新发票列表。落库经 IServiceScopeFactory 取仓储，
    /// 不把请求作用域里的 DbContext 带进后台线程。
    /// </summary>
    public sealed class TaxInvoiceSyncJobRunner
    {
        /// <summary>一次取票在后台最多跑多久。翻页较多时比个税取数留得更宽。</summary>
        private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(5);

        /// <summary>已完成的任务在内存里保留多久，够页面把结果取走。</summary>
        private static readonly TimeSpan ResultRetention = TimeSpan.FromMinutes(10);

        private const int MaxJobs = 50;

        private readonly ConcurrentDictionary<Guid, Job> _jobs = new();
        private readonly TaxPortalService _portal;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TaxTaskStore _tasks;
        private readonly ILogger<TaxInvoiceSyncJobRunner> _logger;

        public TaxInvoiceSyncJobRunner(
            TaxPortalService portal,
            IServiceScopeFactory scopeFactory,
            TaxTaskStore tasks,
            ILogger<TaxInvoiceSyncJobRunner> logger)
        {
            _portal = portal;
            _scopeFactory = scopeFactory;
            _tasks = tasks;
            _logger = logger;
        }

        /// <summary>提交一次取票：先落「进行中」台账，再交给后台线程，立刻返回任务进展。</summary>
        public async Task<TaxInvoiceSyncJobDto> SubmitAsync(
            TaxPortalSessionKey key,
            string userName,
            string taxpayerNumber,
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken)
        {
            var submittedAt = DateTimeOffset.UtcNow;
            var id = Guid.NewGuid();
            var title = $"一键取票（{start:yyyy-MM-dd} 至 {end:yyyy-MM-dd}）";
            await _tasks.StartAsync(
                id, key, userName, title, submittedAt, start.Year * 100 + start.Month, cancellationToken);

            Trim();
            var job = new Job(id, key, title, submittedAt);
            _jobs[id] = job;
            _ = Task.Run(() => RunAsync(job, taxpayerNumber, start, end));
            return job.Snapshot();
        }

        /// <summary>页面轮询：内存里有这个任务就回进展和结果，没有就回落到台账那一行。</summary>
        public async Task<TaxInvoiceSyncJobDto> GetAsync(
            TaxPortalSessionKey key,
            Guid id,
            CancellationToken cancellationToken)
        {
            if (_jobs.TryGetValue(id, out var job) && job.Key == key)
            {
                return job.Snapshot();
            }

            var row = await _tasks.FindAsync(id, key.AccountSetId, cancellationToken);
            if (row is null)
            {
                return new TaxInvoiceSyncJobDto
                {
                    Key = id.ToString("N"),
                    Title = "一键取票",
                    Status = "failed",
                    Message = "这次任务已失效，请重新发起取票。",
                    SubmittedAt = DateTimeOffset.UtcNow,
                };
            }

            // 结果虽然在内存里丢了，但发票已经落库，页面刷新列表就能看到。
            var lost = row.Status == "success";
            return new TaxInvoiceSyncJobDto
            {
                Key = row.Key,
                Title = row.Title,
                Status = row.Status,
                Message = lost ? "任务已完成，结果明细已失效，请刷新发票列表查看。" : row.Suggestion,
                SubmittedAt = row.SubmittedAt,
                FinishedAt = row.FinishedAt == default ? null : row.FinishedAt,
            };
        }

        private async Task RunAsync(Job job, string taxpayerNumber, DateOnly start, DateOnly end)
        {
            using var timeout = new CancellationTokenSource(JobTimeout);
            TaxInvoiceSyncResultDto result;
            try
            {
                result = await ExecuteAsync(job, taxpayerNumber, start, end, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                result = Failed($"取票超过 {JobTimeout.TotalMinutes:0} 分钟仍未完成，已中止。");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[税局取票·后台] 任务「{Title}」执行出错：{Message}", job.Title, ex.Message);
                result = Failed("一键取票失败，请稍后重试。");
            }

            job.Complete(result);
            if (result.NeedLogin)
            {
                // 没进税局等于没办过事，不在任务中心留失败记录；内存快照留给页面弹登录。
                await _tasks.DeleteAsync(job.Id);
                _logger.LogInformation("[税局取票·后台] 任务「{Title}」未执行（缺税局登录态），撤销台账记录", job.Title);
                return;
            }

            var success = result.Message is null;
            await _tasks.CompleteAsync(job.Id, success, result.Message);
            _logger.LogInformation(
                "[税局取票·后台] 任务「{Title}」{State}，取回={Fetched} 新增={Imported} 跳过={Skipped}",
                job.Title,
                success ? "成功" : "失败",
                result.Fetched,
                result.Imported,
                result.Skipped);
        }

        private async Task<TaxInvoiceSyncResultDto> ExecuteAsync(
            Job job,
            string taxpayerNumber,
            DateOnly start,
            DateOnly end,
            CancellationToken cancellationToken)
        {
            var fetch = await _portal.FetchInvoicesAsync(job.Key, taxpayerNumber, start, end, cancellationToken);
            if (fetch.NeedLogin)
            {
                return new TaxInvoiceSyncResultDto
                {
                    NeedLogin = true,
                    Message = fetch.Message,
                    Diagnostics = fetch.Diagnostics,
                    FetchedAt = fetch.FetchedAt,
                };
            }

            if (fetch.Message is not null)
            {
                return new TaxInvoiceSyncResultDto
                {
                    Message = fetch.Message,
                    Diagnostics = fetch.Diagnostics,
                    FetchedAt = fetch.FetchedAt,
                };
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<IInvoiceTaxImporter>();
            var outcome = await importer.ImportAsync(job.Key.AccountSetId, fetch.Rows, cancellationToken);
            return new TaxInvoiceSyncResultDto
            {
                Fetched = fetch.Rows.Count,
                Imported = outcome.Imported,
                Skipped = outcome.Skipped,
                Warnings = outcome.Warnings,
                Summary = Summarize(fetch.Rows, start, end),
                FetchedAt = fetch.FetchedAt,
            };
        }

        /// <summary>
        /// 按销项/进项汇总税局取回的发票。两个方向都出一行，没取到也报 0 张，
        /// 页面据此说明「这个区间确实没有票」而不是「没查」。
        /// </summary>
        private static IReadOnlyList<TaxInvoiceSyncSummaryDto> Summarize(
            IReadOnlyList<TaxInvoiceRecord> rows,
            DateOnly start,
            DateOnly end)
        {
            InvoiceDirection[] directions = [InvoiceDirection.Sales, InvoiceDirection.Purchase];
            return directions
                .Select(direction =>
                {
                    var group = rows.Where(row => row.Direction == direction).ToList();
                    var amount = group.Sum(row => row.Amount);
                    var tax = group.Sum(row => row.Tax);
                    return new TaxInvoiceSyncSummaryDto
                    {
                        Direction = direction == InvoiceDirection.Sales ? "销项发票" : "进项发票",
                        PeriodStart = start,
                        PeriodEnd = end,
                        Count = group.Count,
                        Amount = amount,
                        Tax = tax,
                        Total = amount + tax,
                    };
                })
                .ToList();
        }

        private static TaxInvoiceSyncResultDto Failed(string message) => new()
        {
            Message = message,
            FetchedAt = DateTimeOffset.UtcNow,
        };

        /// <summary>清掉内存里已过期的结果，并给任务数量设上限。</summary>
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
                         .Where(item => item.FinishedAt is not null)
                         .OrderBy(item => item.FinishedAt)
                         .Take(_jobs.Count - MaxJobs))
            {
                _jobs.TryRemove(stale.Id, out _);
            }
        }

        private sealed class Job(Guid id, TaxPortalSessionKey key, string title, DateTimeOffset submittedAt)
        {
            public Guid Id { get; } = id;

            public TaxPortalSessionKey Key { get; } = key;

            public string Title { get; } = title;

            public DateTimeOffset SubmittedAt { get; } = submittedAt;

            public TaxInvoiceSyncResultDto? Result { get; private set; }

            public DateTimeOffset? FinishedAt { get; private set; }

            public void Complete(TaxInvoiceSyncResultDto result)
            {
                Result = result;
                FinishedAt = DateTimeOffset.UtcNow;
            }

            public TaxInvoiceSyncJobDto Snapshot()
            {
                var result = Result;
                return new TaxInvoiceSyncJobDto
                {
                    Key = Id.ToString("N"),
                    Title = Title,
                    Status = result is null ? "running" : (result.Message is null ? "success" : "failed"),
                    NeedLogin = result?.NeedLogin ?? false,
                    Message = result?.Message,
                    Data = result is null || result.NeedLogin ? null : result,
                    SubmittedAt = SubmittedAt,
                    FinishedAt = FinishedAt,
                };
            }
        }
    }
}
