namespace KuaiJi.Modules.Funds.Application
{
    /// <summary>内部转账 DTO（含两端账户名称与关联凭证号）。</summary>
    public sealed class TransferDto
    {
        public Guid Id { get; init; }

        public DateOnly Date { get; init; }

        public string Summary { get; init; } = "";

        public Guid FromJournalId { get; init; }

        public string FromJournalName { get; init; } = "";

        public Guid ToJournalId { get; init; }

        public string ToJournalName { get; init; } = "";

        public decimal Amount { get; init; }

        public string? Remark { get; init; }

        public Guid? VoucherId { get; init; }

        /// <summary>关联凭证号，如「记-3」；未生成时为空。</summary>
        public string? VoucherNo { get; init; }
    }

    /// <summary>新建 / 修改内部转账请求。</summary>
    public sealed class SaveTransferRequest
    {
        public DateOnly Date { get; init; }

        /// <summary>摘要；省略时默认「资金内部调拨」。</summary>
        public string? Summary { get; init; }

        public Guid FromJournalId { get; init; }

        public Guid ToJournalId { get; init; }

        public decimal Amount { get; init; }

        public string? Remark { get; init; }
    }
}
