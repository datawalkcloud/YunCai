namespace YunCai.Modules.FixedAssets.Domain
{
    /// <summary>
    /// 资产变动记录：原值调整 / 累计折旧调整 / 清理等操作的留痕，
    /// 变动前后内容以文本呈现（与柠檬云「变更记录及生成凭证」页一致）。
    /// </summary>
    public class FixedAssetChange
    {
        /// <summary>变动前后内容文本最大长度。</summary>
        public const int MaxTextLength = 200;

        private FixedAssetChange()
        {
        }

        public FixedAssetChange(
            Guid id,
            Guid accountSetId,
            Guid assetId,
            FixedAssetChangeKind kind,
            string before,
            string after,
            int period,
            decimal amount = 0m)
        {
            Id = id;
            AccountSetId = accountSetId;
            AssetId = assetId;
            Kind = kind;
            Before = before;
            After = after;
            Period = period;
            Amount = amount;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>变动记录标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>资产卡片标识。</summary>
        public Guid AssetId { get; private set; }

        /// <summary>变动单类别。</summary>
        public FixedAssetChangeKind Kind { get; private set; }

        /// <summary>变动前内容。</summary>
        public string Before { get; private set; } = null!;

        /// <summary>变动后内容。</summary>
        public string After { get; private set; } = null!;

        /// <summary>变动期间（yyyyMM）。</summary>
        public int Period { get; private set; }

        /// <summary>变动金额（计提折旧 / 金额调整时使用，清理为 0）。</summary>
        public decimal Amount { get; private set; }

        /// <summary>记录时间。</summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>关联凭证标识（生成凭证后回填；凭证被删除后自愈清空）。</summary>
        public Guid? VoucherId { get; private set; }

        /// <summary>标记本条变动已生成凭证。</summary>
        public void MarkVoucher(Guid voucherId) => VoucherId = voucherId;

        /// <summary>凭证被删除后清空关联，使该条变动可以重新生成凭证。</summary>
        internal bool ClearStaleVoucher(IReadOnlySet<Guid> existingVoucherIds)
        {
            if (VoucherId is { } id && !existingVoucherIds.Contains(id))
            {
                VoucherId = null;
                return true;
            }

            return false;
        }
    }
}
