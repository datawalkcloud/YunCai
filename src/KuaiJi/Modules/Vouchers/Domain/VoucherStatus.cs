namespace KuaiJi.Modules.Vouchers.Domain
{
    /// <summary>
    /// 记账凭证状态。遵循「制单 → 审核 → 过账」工作流：
    /// 草稿可自由修改，审核/过账后锁定不可改。
    /// </summary>
    public enum VoucherStatus
    {
        /// <summary>草稿（已制单，未审核，可修改）。</summary>
        Draft = 1,

        /// <summary>已审核（锁定，等待过账）。</summary>
        Reviewed = 2,

        /// <summary>已过账（登记入账，锁定）。</summary>
        Posted = 3,
    }
}
