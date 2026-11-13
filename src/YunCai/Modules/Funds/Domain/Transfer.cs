namespace YunCai.Modules.Funds.Domain
{
    /// <summary>
    /// 内部转账聚合根：同一账套下两个资金账户之间的资金划转（提现、存现、账户调拨）。
    /// 创建时自动在两个日记账中各登记一笔转出/转入流水（本聚合持有两端分录标识，
    /// 删除转账时一并清理）；可另行生成一张转账凭证（借 转入科目 / 贷 转出科目）。
    /// </summary>
    public class Transfer
    {
        // EF Core 物化实体所需的无参构造函数。
        private Transfer()
        {
        }

        private Transfer(
            Guid id,
            Guid accountSetId,
            DateOnly date,
            string summary,
            Guid fromJournalId,
            Guid toJournalId,
            decimal amount,
            string? remark)
        {
            Id = id;
            AccountSetId = accountSetId;
            Date = date;
            Summary = summary;
            FromJournalId = fromJournalId;
            ToJournalId = toJournalId;
            Amount = amount;
            Remark = remark;
        }

        /// <summary>转账标识。</summary>
        public Guid Id { get; private set; }

        /// <summary>所属账套标识。</summary>
        public Guid AccountSetId { get; private set; }

        /// <summary>转账日期。</summary>
        public DateOnly Date { get; private set; }

        /// <summary>摘要（默认「资金内部调拨」）。</summary>
        public string Summary { get; private set; } = null!;

        /// <summary>转出账户（日记账）标识。</summary>
        public Guid FromJournalId { get; private set; }

        /// <summary>转入账户（日记账）标识。</summary>
        public Guid ToJournalId { get; private set; }

        /// <summary>转账金额（正数）。</summary>
        public decimal Amount { get; private set; }

        /// <summary>备注，可为空。</summary>
        public string? Remark { get; private set; }

        /// <summary>转出流水标识（在转出日记账中登记的支出分录）。</summary>
        public Guid FromEntryId { get; private set; }

        /// <summary>转入流水标识（在转入日记账中登记的收入分录）。</summary>
        public Guid ToEntryId { get; private set; }

        /// <summary>关联凭证标识；未生成凭证时为空。</summary>
        public Guid? VoucherId { get; private set; }

        /// <summary>创建转账记录（两端流水与标识由应用层登记后回填）。</summary>
        public static Transfer Create(
            Guid accountSetId,
            DateOnly date,
            string? summary,
            Guid fromJournalId,
            Guid toJournalId,
            decimal amount,
            string? remark)
        {
            if (accountSetId == Guid.Empty)
            {
                throw new ArgumentException("必须指定所属账套。");
            }

            if (fromJournalId == Guid.Empty || toJournalId == Guid.Empty)
            {
                throw new ArgumentException("转出与转入账户不能为空。");
            }

            if (fromJournalId == toJournalId)
            {
                throw new ArgumentException("转出与转入账户不能相同。");
            }

            if (amount <= 0m)
            {
                throw new ArgumentException("转账金额必须大于 0。");
            }

            var finalSummary = string.IsNullOrWhiteSpace(summary) ? DefaultSummary : summary.Trim();
            if (finalSummary.Length > MaxSummaryLength)
            {
                throw new ArgumentException($"摘要不能超过 {MaxSummaryLength} 个字符。", nameof(summary));
            }

            return new Transfer(
                Guid.NewGuid(),
                accountSetId,
                date,
                finalSummary,
                fromJournalId,
                toJournalId,
                amount,
                string.IsNullOrWhiteSpace(remark) ? null : remark.Trim());
        }

        /// <summary>默认摘要。</summary>
        public const string DefaultSummary = "资金内部调拨";

        /// <summary>摘要最大长度（与日记账分录保持一致）。</summary>
        public const int MaxSummaryLength = 200;

        /// <summary>回填两端流水标识（由应用层登记流水后调用）。</summary>
        public void AttachEntries(Guid fromEntryId, Guid toEntryId)
        {
            FromEntryId = fromEntryId;
            ToEntryId = toEntryId;
        }

        /// <summary>回填生成的凭证标识。</summary>
        public void AttachVoucher(Guid voucherId) => VoucherId = voucherId;

        /// <summary>
        /// 清除失效的关联凭证：凭证被删除后转账仍残留 VoucherId，会导致转账无法修改或删除。
        /// 传入仍存在的凭证标识集合，返回是否发生了变更。
        /// </summary>
        public bool ClearStaleVoucher(IReadOnlySet<Guid> existingVoucherIds)
        {
            if (VoucherId is not { } vid || existingVoucherIds.Contains(vid))
            {
                return false;
            }

            VoucherId = null;
            return true;
        }

        /// <summary>修改转账信息（仅在未生成凭证前允许，由应用层校验）。</summary>
        public void Update(DateOnly date, string? summary, decimal amount, string? remark)
        {
            if (amount <= 0m)
            {
                throw new ArgumentException("转账金额必须大于 0。");
            }

            var finalSummary = string.IsNullOrWhiteSpace(summary) ? DefaultSummary : summary.Trim();
            if (finalSummary.Length > MaxSummaryLength)
            {
                throw new ArgumentException($"摘要不能超过 {MaxSummaryLength} 个字符。", nameof(summary));
            }

            Date = date;
            Summary = finalSummary;
            Amount = amount;
            Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
        }
    }
}
