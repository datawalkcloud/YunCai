using YunCai.Modules.Auditing.Domain;

namespace YunCai.Modules.Auditing.Application
{
    /// <summary>
    /// 操作日志应用服务：查询与写入。写入失败不应影响业务主流程，
    /// 故 Record 内部吞掉异常仅记录（审计是旁路能力）。
    /// </summary>
    public sealed class OperationLogService
    {
        private readonly IOperationLogRepository _logRepository;

        public OperationLogService(IOperationLogRepository logRepository)
        {
            _logRepository = logRepository;
        }

        /// <summary>按条件查询操作日志（时间倒序）。</summary>
        public async Task<IReadOnlyList<OperationLogDto>> QueryAsync(Guid accountSetId, OperationLogQuery query, CancellationToken cancellationToken = default)
        {
            var logs = await _logRepository.QueryAsync(
                accountSetId,
                query.Start,
                query.End,
                string.IsNullOrWhiteSpace(query.UserId) ? null : query.UserId.Trim(),
                string.IsNullOrWhiteSpace(query.Keyword) ? null : query.Keyword.Trim(),
                cancellationToken);
            return logs.Select(OperationLogDto.FromDomain).ToList();
        }

        /// <summary>写入一条操作日志（旁路，失败不抛出）。</summary>
        public async Task RecordAsync(Guid accountSetId, string userId, string userName, string type, string module, string log, CancellationToken cancellationToken = default)
        {
            try
            {
                await _logRepository.AddAsync(OperationLog.Create(accountSetId, userId, userName, type, module, log), cancellationToken);
                await _logRepository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                // 审计写入失败不阻断业务。
            }
        }
    }
}
