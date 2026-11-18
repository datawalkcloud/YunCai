namespace YunCai.Modules.Vouchers.Domain
{
    /// <summary>
    /// 凭证附件仓储。附件按凭证归属，读取时一律带凭证标识做越权隔离。
    /// </summary>
    public interface IVoucherAttachmentRepository
    {
        /// <summary>列出某张凭证的全部附件元数据（不含文件内容）。</summary>
        Task<IReadOnlyList<VoucherAttachmentInfo>> ListByVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default);

        /// <summary>获取单个附件（含文件内容）。</summary>
        Task<VoucherAttachment?> GetAsync(Guid voucherId, Guid attachmentId, CancellationToken cancellationToken = default);

        /// <summary>新增附件。</summary>
        Task AddAsync(VoucherAttachment attachment, CancellationToken cancellationToken = default);

        /// <summary>删除附件。</summary>
        void Remove(VoucherAttachment attachment);

        /// <summary>提交工作单元。</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
