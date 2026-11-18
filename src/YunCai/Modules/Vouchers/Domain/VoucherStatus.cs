namespace YunCai.Modules.Vouchers.Domain
{
    /// <summary>
    /// 记账凭证状态。遵循「制单 → 过账」工作流：保存即登记入账，
    /// 只有反过账退回的凭证才是暂存，可自由修改后再次保存入账。
    /// 界面文案用「暂存 / 正常」，枚举名保留过账语义。
    /// </summary>
    public enum VoucherStatus
    {
        /// <summary>暂存（反过账退回或导入不平衡，可修改；不计入账簿）。</summary>
        Draft = 1,

        /// <summary>正常（登记入账并锁定，账簿与报表据此取数）。</summary>
        Posted = 3,
    }
}
