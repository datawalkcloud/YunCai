namespace YunCai.Modules.Accounting.Domain
{
    /// <summary>
    /// 会计科目仓储。以聚合根 <see cref="Account"/> 为单位进行持久化访问。
    /// </summary>
    public interface IAccountRepository
    {
        /// <summary>按标识获取科目（限定账套）。</summary>
        Task<Account?> GetByIdAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 按标识获取科目，并一并载入其科目级设置（辅助核算维度 + 外币核算币别，限定账套）。
        /// 制单校验、维度与币别设置读写必须走本方法，否则导航集合是未载入的空集合，
        /// 覆盖写入时既读不到旧值也删不掉旧行。
        /// </summary>
        Task<Account?> GetByIdWithSettingsAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>按科目编码获取科目（限定账套）。</summary>
        Task<Account?> GetByCodeAsync(Guid accountSetId, string code, CancellationToken cancellationToken = default);

        /// <summary>获取指定账套的全部科目（用于在应用层组装科目树）。</summary>
        Task<IReadOnlyList<Account>> GetAllAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取指定账套的全部科目，并一并载入科目级设置（辅助核算维度 + 外币核算币别）。
        /// 凭证服务用它装载科目，以便按科目上已设置的维度与币别校验分录。
        /// </summary>
        Task<IReadOnlyList<Account>> GetAllWithSettingsAsync(Guid accountSetId, CancellationToken cancellationToken = default);

        /// <summary>获取指定上级下的直接下级科目（限定账套）；parentId 为 null 时返回顶级科目。</summary>
        Task<IReadOnlyList<Account>> GetChildrenAsync(Guid accountSetId, Guid? parentId, CancellationToken cancellationToken = default);

        /// <summary>判断科目编码在指定账套内是否已存在。</summary>
        Task<bool> ExistsByCodeAsync(Guid accountSetId, string code, CancellationToken cancellationToken = default);

        /// <summary>判断科目是否存在下级明细科目（限定账套）。</summary>
        Task<bool> HasChildrenAsync(Guid accountSetId, Guid id, CancellationToken cancellationToken = default);

        /// <summary>新增科目。</summary>
        Task AddAsync(Account account, CancellationToken cancellationToken = default);

        /// <summary>删除科目（应用层需先校验非预置且无下级）。</summary>
        void Remove(Account account);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
