namespace KuaiJi.Modules.Funds.Domain
{
    /// <summary>
    /// 日记账仓储。以聚合根 <see cref="Journal"/> 为单位持久化访问（含其分录）。
    /// </summary>
    public interface IJournalRepository
    {
        /// <summary>按标识获取日记账（含分录，限定账套）。</summary>
        Task<Journal?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>按对应科目获取日记账（含分录，限定账套）。</summary>
        Task<Journal?> GetByAccountIdAsync(Guid accountSetId, Guid accountId, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部日记账（不含分录，用于列表）。</summary>
        Task<IReadOnlyList<Journal>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>生成指定账套、指定类型的下一个可用账户编码（纯数字序号，长度对齐 <see cref="Journal.MaxCodeLength"/>）。</summary>
        Task<string> NextCodeAsync(Guid accountSetId, JournalType type, CancellationToken cancellationToken = default);

        /// <summary>判断同账套同类型下指定编码是否已被占用（<paramref name="excludeId"/> 非空时排除该账户自身）。</summary>
        Task<bool> ExistsByCodeAsync(Guid accountSetId, JournalType type, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

        /// <summary>获取资金类（现金 + 银行存款）日记账在日期区间内的分录（含所属账簿名与科目），按日期升序。</summary>
        Task<IReadOnlyList<(Journal Journal, JournalEntry Entry)>> GetFundEntriesRangeAsync(
            Guid accountSetId,
            DateOnly? startDate = null,
            DateOnly? endDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>新增日记账。</summary>
        Task AddAsync(Journal journal, CancellationToken cancellationToken = default);

        /// <summary>删除日记账（含其全部分录）。</summary>
        void Remove(Journal journal);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
