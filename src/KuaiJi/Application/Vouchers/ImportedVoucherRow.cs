namespace KuaiJi.Application.Vouchers
{
    /// <summary>
    /// 从导入文件解析出的一行原始凭证分录数据。字段与外部平台导出的「凭证列表」列一一对应，
    /// 尚未做科目映射与聚合，均为原始文本/数值。
    /// </summary>
    public sealed class ImportedVoucherRow
    {
        /// <summary>数据所在行号（从 1 开始，供错误提示定位；不含表头）。</summary>
        public int RowNumber { get; init; }

        /// <summary>凭证日期。</summary>
        public DateOnly Date { get; init; }

        /// <summary>凭证号原文，如“记-1”。</summary>
        public string VoucherNo { get; init; } = string.Empty;

        /// <summary>凭证内分录行号（源文件“行号”列，通常自 1 递增）。</summary>
        public int LineNumber { get; init; }

        /// <summary>摘要。</summary>
        public string Summary { get; init; } = string.Empty;

        /// <summary>科目编码。</summary>
        public string AccountCode { get; init; } = string.Empty;

        /// <summary>科目名称。</summary>
        public string AccountName { get; init; } = string.Empty;

        /// <summary>借方本币金额（原样，允许为负）。</summary>
        public decimal Debit { get; init; }

        /// <summary>贷方本币金额（原样，允许为负）。</summary>
        public decimal Credit { get; init; }

        /// <summary>制单人。</summary>
        public string? Preparer { get; init; }

        /// <summary>审核人（可能为空）。</summary>
        public string? Reviewer { get; init; }

        /// <summary>附件数。</summary>
        public int AttachmentCount { get; init; }
    }
}
