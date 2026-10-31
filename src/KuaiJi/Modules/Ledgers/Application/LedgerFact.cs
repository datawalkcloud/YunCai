using KuaiJi.Modules.Assist.Domain;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>账簿业务中的可预期错误（账套不存在、期间不合法、科目条件不合法等）。</summary>
    public sealed class LedgerException : Exception
    {
        public LedgerException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 账簿事实行：一条已过账凭证分录展平凭证头信息后的结果，是全部账页的唯一数据源。
    /// 展平（而非持有 Voucher 聚合根）是为了让各账页只做投影，不重复装载与遍历。
    /// </summary>
    public sealed record LedgerFact
    {
        public required Guid VoucherId { get; init; }

        /// <summary>凭证日期。</summary>
        public required DateOnly Date { get; init; }

        /// <summary>会计期间（yyyyMM），装载时预计算，避免各处重复取年月。</summary>
        public required int Period { get; init; }

        /// <summary>凭证字。</summary>
        public required string Word { get; init; }

        /// <summary>凭证号。</summary>
        public required int Number { get; init; }

        /// <summary>凭证号显示文本，如「记-1」。</summary>
        public string VoucherNo => $"{Word}-{Number}";

        /// <summary>分录行号。</summary>
        public required int LineNumber { get; init; }

        /// <summary>摘要。</summary>
        public required string Summary { get; init; }

        /// <summary>科目标识。</summary>
        public required Guid AccountId { get; init; }

        /// <summary>借方金额。</summary>
        public required decimal Debit { get; init; }

        /// <summary>贷方金额。</summary>
        public required decimal Credit { get; init; }

        /// <summary>净额（借 - 贷），账簿内部一律以此口径累计。</summary>
        public decimal NetDebit => Debit - Credit;

        /// <summary>制单人。</summary>
        public required string Preparer { get; init; }

        /// <summary>审核人。</summary>
        public string? Reviewer { get; init; }

        /// <summary>凭证所附单据张数（同一凭证各行重复携带，参与合计时须按凭证去重）。</summary>
        public required int AttachmentCount { get; init; }

        /// <summary>凭证备注（凭证头字段，同一凭证各行相同）。</summary>
        public string? Remark { get; init; }

        /// <summary>该分录登记的辅助核算取值。</summary>
        public IReadOnlyList<AssistRef> Assists { get; init; } = [];
    }
}
