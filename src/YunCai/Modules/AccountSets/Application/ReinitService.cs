using YunCai.Modules.AccountSets.Domain;
using YunCai.Modules.Auditing.Application;

namespace YunCai.Modules.AccountSets.Application
{
    /// <summary>重新初始化相关业务错误。</summary>
    public sealed class ReinitException : Exception
    {
        public ReinitException(string message) : base(message)
        {
        }
    }

    /// <summary>重新初始化结果。</summary>
    public sealed class ReinitResultDto
    {
        /// <summary>各表清除行数。</summary>
        public IReadOnlyDictionary<string, int> Wiped { get; init; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// 重新初始化应用服务：清空账套全部业务数据并退回启用期间。
    /// 高危操作：必须显式传入账套名称作为确认口令，且全程写入操作日志。
    /// </summary>
    public sealed class ReinitService
    {
        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IReinitRepository _reinitRepository;
        private readonly OperationLogService _operationLogService;

        public ReinitService(
            IAccountSetRepository accountSetRepository,
            IReinitRepository reinitRepository,
            OperationLogService operationLogService)
        {
            _accountSetRepository = accountSetRepository;
            _reinitRepository = reinitRepository;
            _operationLogService = operationLogService;
        }

        /// <summary>执行重新初始化。confirmName 必须与账套名称完全一致。</summary>
        public async Task<ReinitResultDto> WipeAsync(Guid accountSetId, string confirmName, string operatorUserId, string operatorUserName, CancellationToken cancellationToken = default)
        {
            var accountSet = await _accountSetRepository.GetByIdAsync(accountSetId, cancellationToken)
                             ?? throw new ReinitException("指定的账套不存在。");

            if (!string.Equals((confirmName ?? string.Empty).Trim(), accountSet.Name, StringComparison.Ordinal))
            {
                throw new ReinitException("确认文本与账套名称不一致，未执行重新初始化。");
            }

            var wiped = await _reinitRepository.WipeBusinessDataAsync(accountSetId, cancellationToken);
            await _reinitRepository.ResetPeriodsAsync(accountSetId, cancellationToken);
            await _operationLogService.RecordAsync(
                accountSetId,
                operatorUserId,
                operatorUserName,
                "删除",
                "重新初始化",
                "账套业务数据已全部清空并退回启用期间",
                cancellationToken);
            return new ReinitResultDto { Wiped = wiped };
        }
    }
}
