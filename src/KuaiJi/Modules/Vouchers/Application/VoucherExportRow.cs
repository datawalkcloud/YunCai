namespace KuaiJi.Modules.Vouchers.Application
{
    /// <summary>
    /// 凭证导出扁平行：每条分录一行，凭证头字段在同一凭证的各分录行重复。
    /// 列与 <c>XlsVoucherImportReader</c> 的解析列同构，因此导出文件可回传导入。
    /// </summary>
    public sealed class VoucherExportRow
    {
        /// <summary>凭证日期。</summary>
        public DateOnly Date { get; init; }

        /// <summary>凭证号，格式「字-号」（如「记-1」）。</summary>
        public string VoucherNo { get; init; } = string.Empty;

        /// <summary>分录行号（自 1 递增）。</summary>
        public int LineNumber { get; init; }

        /// <summary>摘要。</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>科目编码。</summary>
        public string AccountCode { get; init; } = string.Empty;

        /// <summary>科目名称。</summary>
        public string AccountName { get; init; } = string.Empty;

        /// <summary>借方本币金额。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方本币金额。</summary>
        public decimal Credit { get; init; }

        /// <summary>制单人。</summary>
        public string Preparer { get; init; } = string.Empty;

        /// <summary>审核人（未审核为空）。</summary>
        public string Reviewer { get; init; } = string.Empty;

        /// <summary>附单据数。</summary>
        public int AttachmentCount { get; init; }
    }
}
